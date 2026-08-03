# Scoring

This document describes how YARG.Core scores notes, builds the multiplier, and derives star
thresholds.

## Ticks and time

Before the scoring math, the time units:

- **Tick** - the smallest time unit in a chart. A tick is `1 / Resolution` of a
  quarter note (one beat).
- **Resolution** - ticks per quarter note (`SyncTrack.Resolution`). YARG does not
  fix it; the value comes from the file: the .chart `[Song]` header (default 192 if
  absent, `ChartReader.DEFAULT_RESOLUTION`), or the MIDI header division (ticks per
  quarter note). Charts authored for YARG use 480, so one beat = 480 ticks. 480 is
  conventional, not required: at that resolution an eighth-note triplet is 160
  ticks, a 16th note is 120, and a 32nd note is 60. Older charts may use 192 or 384;
  precision charts may use 960+. Resolution bounds charting precision, but does not
  directly scale hit windows, which are measured in seconds.
- **Beat** - one quarter note. At resolution 480: 480 ticks. The quarter note is a
  fixed unit defined by `Resolution`, not by the time signature: in 3/4 a measure
  holds three quarter notes (1440 ticks at 480), and the beat is still 480 ticks.

### Conversions (time)

Tempo affects tick-to-time conversions (`SyncTrack.TickToTime` / `TimeToTick`) - the
same tick distance can have a different duration before and after a tempo change.
Chart positions and most scoring quantities use ticks. Hit windows, whammy buffers,
sustain-drop leniency, and coda recharge use *seconds* (where applicable, scaled by
song speed).

## Core constants

Defined in `YARG.Core/Engine/BaseEngine.cs`:

| Constant | Value | Meaning |
|---|---|---|
| `POINTS_PER_NOTE` | 50 | Points for one regular note (guitar, keys, 4-lane drums) |
| `POINTS_PER_PRO_NOTE` | 60 | Points for one pro four-lane drum pad |
| `POINTS_PER_PRO_KEYS_NOTE` | 120 | Points for one pro keys note (`ProKeysEngine.cs`) |
| `POINTS_PER_BEAT` | 25 | Points per quarter-note beat of sustain (see [Ticks and time](#ticks-and-time)) |

Derived values (`YARG.Core/Engine/BaseEngine.Generic.cs`, engine constructor):

```
TicksPerSustainPoint = SyncTrack.Resolution / POINTS_PER_BEAT
```

At the standard resolution of 480 ticks/beat this is 19.2 ticks per sustain point.

## Multiplier

The score multiplier steps with combo:

```
multiplier = min(combo / 10 + 1, MaxMultiplier)   // integer division
```

| Combo | Multiplier |
|---|---|
| 0–9 | 1x |
| 10–19 | 2x |
| 20–29 | 3x |
| 30+ | 4x (cap) |

Notes:

- `MaxMultiplier` is an engine parameter (default 4).
- For instruments, a hit increments combo, updates the multiplier, then scores. The
  note that carries combo from 9 → 10 is therefore scored at 2x.
- While star power is active the multiplier is doubled (`ScoreMultiplier *= 2`).
- Vocals are the exception: `multiplier = min(combo + 1, MaxMultiplier)`. A vocal
  phrase increments combo, scores with the existing multiplier, then updates the
  multiplier for the next phrase. Vocals have no 10-combo step.

## Runtime scoring (gameplay)

When a note is hit, `AddScore` is called and the engine stats are updated:

Chord data uses one parent note plus zero or more `ChildNotes`; `AllNotes` enumerates
the parent followed by its children. A **disjoint chord** has members with different
sustain lengths (`IsDisjoint`). Unless stated otherwise, scoring still treats it as
one chord event.

- **Note points** - `POINTS_PER_NOTE` (or `POINTS_PER_PRO_NOTE` for pro drums, or
  `POINTS_PER_PRO_KEYS_NOTE` for pro keys) per note. Guitar/keys/drums score every member
  of a chord: 3-note chord = 150 points (360 on pro keys).
- **Sustain points** - per sustained *note*, not per chord: `ceil(note.TickLength /
  TicksPerSustainPoint)` for each note carrying a sustain. While held, accrued value
  appears in `PendingScore`. At the burst point, natural end, or a normal drop, the
  accumulated value is committed through `AddScore` using the multiplier then in
  effect. Every sustained chord member contributes its own value at its own length,
  so a 2-note chord sustain is worth 2× a single note's, whether disjoint or not. One
  beat (480 ticks) = 25 points per note. A long sustain may therefore commit at a
  higher multiplier than its parent note. The `SustainScore` stats bucket excludes
  star power doubling; `CommittedScore` keeps it (see
  [Score accounting while active](#score-accounting-while-active)).
- **Committed score** - the permanent, definitive score: points that have finished
  scoring and can no longer be lost or changed. It never decreases and never contains
  unearned value. Points only *become* committed through one path, `AddScore(int)` →
  `CommittedScore += points × ScoreMultiplier` (a 50-point note at 2x commits 100).
  Note hits, partial vocal phrases, and sustain commits all use this same call with
  the multiplier then in effect. By contrast, a sustain that is still being held
  accrues value into `PendingScore` - provisional, shown live on the UI, not part of
  `CommittedScore`; it becomes permanent only when the sustain finishes (burst point
  or natural end, full value) or drops early (only the value accrued so far). For a
  note without a sustain, the stats buckets balance as
  `NoteScore + MultiplierScore + StarPowerScore = CommittedScore` (`StarPowerScore`
  is 0 unless points were committed while star power was active). Sustain scoring
  does not balance this way because their
  multiplier bonus appears in both `SustainScore` and `MultiplierScore`. The committed
  total remains correct - see
  [Score accounting while active](#score-accounting-while-active).

### Combo increments per engine

| Engine | Combo per | Source |
|---|---|---|
| Guitar (5-fret) | 1 per strum (whole chord, even disjoint chords) | `GuitarEngine.HitNote` |
| Five-lane keys | 1 per chord (first note hit in a chord) | `FiveLaneKeysEngine.HitNote` - "Only increase combo for the first note in a chord" |
| Pro keys | 1 per chord | `ProKeysEngine` - "Pro Keys combo increments per chord, not per note." |
| Drums | 1 per note, *including* every chord member (`1 + ChildNotes.Count`) | `DrumsEngine` |
| Vocals | 1 per phrase | `VocalsEngine` |

A 3-note chord therefore gives +3 combo on drums but +1 on guitar/keys. This is a
deliberate engine difference and is mirrored exactly by the chart base score
(see [Chart base score](#chart-base-score)).

## Vocals

Vocals score per **phrase**, not per note. A charted phrase is a parent `VocalNote`
whose child notes are the individual syllables; the engine resolves the whole phrase at
once when it ends (`YargVocalsEngine.Update`).

### Phrase hit resolution

When the current tick passes the phrase end:

```
hitPercent = PhraseTicksHit / PhraseTicksTotal   // tick-weighted, not note-counted
hit = hitPercent >= PhraseHitPercent             // parameter, e.g. 0.7
```

- **Full hit** - `PointsPerPhrase` (engine parameter, typically 100; see
  `VocalsEngineParameters`) and combo +1. It scores using the multiplier carried into
  the phrase, then updates the multiplier for the next phrase. A phrase with no
  singable ticks scores nothing and does not increment combo.
- **Miss / partial** - combo resets, but `round(PointsPerPhrase × hitPercent)` is
  passed to `AddScore` (`AddPartialScore`). The multiplier from the preceding phrase
  still applies because `UpdateMultiplier` runs after this score is applied.

### Percussion

Percussion phrases behave differently (`VocalsEngine.HitNote`):

- Each percussion note is hit individually - `POINTS_PER_PERCUSSION = 100` per note.
- No combo and no phrase status. A miss deducts no points and does not reset combo,
  but the missed note earns none of its possible hit score.
- Percussion phrases are removed from `TotalNotes` at construction and skipped in
  `CalculateChartScores` - intentionally excluded from base score so they don't
  affect star calculations.

### Multiplier, star power, harmony

- **Multiplier** - `min(combo + 1, MaxMultiplier)`: every full phrase raises the
  multiplier used by the following phrase, with no 10-combo step (see
  [Multiplier](#multiplier)).
- **Star power** - a star power phrase fully hit calls `AwardStarPower` (quarter bar,
  same as instruments; see [Earning star power](#earning-star-power)); missing one
  calls `StripStarPower`, revoking the unearned phrase. The
  `SingToActivateStarPower` parameter lets the game allow activation by singing
  rather than a button.
- **Harmony** - each harmony part is a separate engine instance (`EngineManager`
  `HarmonyIndex`) with fully independent scoring, combo, and star power. No shared
  state.

## Forgiveness mechanics

These mechanics forgive errors without a score penalty. They change *whether* a note
scores, never *how much* it scores.

### Lanes (tremolo / trills)

Lane notes are chord members flagged `IsLane` (with `IsLaneStart` / `IsLaneEnd`
markers; `IsTrill` vs `IsTremolo`). While a lane is active (`BaseEngine.Generic.cs`
`AutohitNoteFromLane` / `SubmitLaneNote`):

- The **first** lane note must be hit manually - `AutohitNoteFromLane` explicitly
  refuses `IsLaneStart` notes. The autohit window is a single timer, extended by
  every correct input, so at the end of a lane it can still be running into the
  next one; without the guard it would carry over and autohit the next lane's
  first note too.
- Subsequent lane notes that would be missed are **autohit** instead: `HitNote` is
  called, so the note scores fully (points, combo, sustain) as if played. The window is
  `HitWindow.LaneAutohitWindow`, extended by every correct input.
- Trills alternate the required fret (`NextTrillNote`); `WildcardMask` (open strum)
  satisfies any lane.
- **Proximity protection** (`IsInLaneLeniencyWindow`): inputs near a lane start/end
  within `HitWindow.LaneProximityProtectionWindow` are forgiven instead of punished as
  overstrums - guitar's parameterless variant forgives any input, drums/keys only
  inputs that would satisfy the nearby lane.

Stats: autohit lane notes still count toward `NotesHit`, but `IncrementNotesHit` routes
`IsLane` notes to `LanedNotesHit` and skips offset tracking - `GetAverageOffset()` and
`GetOffsetSamples()` (the score-screen timing distribution) exclude them, since their
hit time is synthetic.

### Sustain burst

Sustains get a fixed early-commit grace period of one quarter-beat
(`SUSTAIN_BURST_FRACTION = 4`, `SustainBurstThreshold = Resolution / 4`). Whether it
applies depends on the **charted** sustain length:

- **Charted longer than a quarter-beat:** finishes scoring at `TickEnd −
  SustainBurstThreshold` - you may release up to a quarter-beat early and still get
  full credit.
- **Charted a quarter-beat or less:** no grace period - it scores in full
  immediately when the note is hit (burst condition `CurrentTick >= note.Tick`),
  so no holding is required.

Drop leniency: after release, the sustain is kept in `IsLeniencyHeld` state for
`SustainDropLeniency` seconds (scaled by song speed) before it is dropped and
forfeits the remaining points.

### Overstrum / overhit (guitar)

Overstrumming has **no direct score penalty** - no points are taken. The punishment is
indirect (`GuitarEngine.Overstrum`):

- All active sustains are broken immediately (only points scored so far are banked).
- The current star power phrase is stripped if it was not the start of a phrase
  (`StripStarPower`), so a broken SP phrase no longer banks.
- Combo resets, `Overstrums++`, multiplier recomputed.

Overstrum is *prevented* entirely in several situations: before the first note, during
wait countdown, when the input satisfies an active lane, inside the lane proximity
window, and during active Big Rock Ending (BRE) free-play. After that free-play span
ends, `CodaHasStarted` remains true until the ending chord resolves. An overstrum in
this tail calls `Codas[CurrentCodaIndex].Overhit()`, which forfeits the coda bonus
(see [Coda](#coda-big-rock-endings)).

### Derived stats

- `AverageMultiplier` - `CommittedScore / BaseNoteScore` (float, recomputed on every
  `AddScore`). Includes star power doubling, so a song played with SP averages higher
  than its combo curve would suggest.
- `GetAverageOffset()` - mean hit timing in seconds (negative = early), excluding lane
  notes.

## Star power

### Two tick coordinates: chart ticks and normalized measure ticks

Star power drain is defined in chart measures: a phrase fills a quarter of the bar,
four phrases fill it, and a full bar drains over 8 measures. Raw chart ticks cannot
represent that duration uniformly because ticks per measure depend on both parts of
the time signature:

```
chart ticks per measure = Resolution * 4 * Numerator / Denominator
```

At resolution 480, a 3/4 or 6/8 measure is 1440 chart ticks while a 4/4 measure is
1920.

The meter therefore stores **normalized measure ticks**, not raw chart ticks. Every
charted measure maps to `MeasureResolution = Resolution * 4` normalized ticks
(`TimeSignatureEvent.MEASURE_RESOLUTION_SCALE`), regardless of that measure's time
signature. At resolution 480, every chart measure advances the normalized coordinate
by 1920:

| Tick count | Resolution | Used for |
|---|---|---|
| Chart ticks | `Resolution` per beat (480 at res. 480) | Note positions, sustains, sustain points |
| Normalized measure ticks | `Resolution * 4` per chart measure (1920 at res. 480) | Star power storage, drain, thresholds, activation timing |

A phrase awards a fixed quarter bar (`TicksPerQuarterSpBar = MeasureResolution * 2`)
regardless of its charted length.

### The 4/4 coincidence (and the trap)

In 4/4, one charted measure is `Resolution * 4` raw ticks, so chart ticks and
normalized measure ticks produce the same *numbers*: a note at 1440 chart ticks is
also at 1440 normalized measure ticks. In 3/4 they diverge: a charted measure is
only 1440 chart ticks but converts to 1920 normalized measure ticks (ratio
`MeasureResolution` / ticks-per-measure: 4:3).

The engine never relies on the coincidence: every update it converts `CurrentTick`
with `QuarterTickToMeasureTick` (`StarPowerTickPosition`), and drain is the
difference of two *converted* positions (`CalculateStarPowerDrain`). The trap is a
future edit that skips the conversion - say, draining `CurrentTick − lastTick`
directly. On 4/4 charts that is indistinguishable from correct code, because the
numbers are the same. On 3/4 it drains 1440 ticks per measure instead of 1920: a
full bar lasts 10⅔ measures instead of 8, and half-bar thresholds land at the
wrong positions. 4/4 charts can never catch the bug.

### Converting between chart and normalized measure ticks

Switching between chart ticks and normalized measure ticks requires conversion
(`QuarterTickToMeasureTick` /
`MeasureTickToQuarterTick`), which is *time-signature*-aware: it counts how many
measures were traveled on the chart side, then scales by `Resolution * 4`.

### The bar

The star power bar is measured in normalized measure ticks (`BaseEngine.cs`
constructor):

```
TicksPerQuarterSpBar = MeasureResolution * 2
TicksPerHalfSpBar     = TicksPerQuarterSpBar * 2   // 4 chart measures
TicksPerFullSpBar     = TicksPerQuarterSpBar * 4   // 8 chart measures
```

A full bar is always 8 chart measures (15360 normalized ticks at resolution 480).
That duration is 32 quarter-note beats in 4/4, 24 in 3/4 or 6/8.

### Earning star power

A star power **phrase** is a charted span: in .chart files, between the `[sp]` and
`[end]` markers. The notes inside - however many - carry the star power flag, and
every note in the span must be hit to earn it. Phrase length is author-chosen and
irrelevant to the award; the meter fills a fixed quarter bar either way.

| Source | Gain | Notes |
|---|---|---|
| Completing a star power phrase | Quarter bar (2 measures) | `AwardStarPower` → `GainStarPower(TicksPerQuarterSpBar)`; 4 phrases fill a bar |
| Whammying a star power sustain | Time-based | Only while the sustain is held *and* the whammy timer is active (`StarPowerWhammyBuffer` parameter adds a grace buffer) |
| Unison bonus (non-vocal instruments, band mode) | Quarter bar | `AwardUnisonBonus` |

Whammy gain is the exception to measure-based drain. It is computed from raw chart
ticks (`CalculateStarPowerGain`) and added directly to the meter because the bar
constants are also multiples of `Resolution`. With gain factor `32/30`
(`GAIN_FACTOR = MAX_BEATS / (MAX_BEATS - 2)`, `MAX_BEATS = 32`), 30 quarter-note
beats of whammy add the same numeric amount as a full bar (`32 * Resolution`). Thus
whammy earning is quarter-note-based while drain is chart-measure-based. Do not use
raw chart-tick durations for drain or threshold math. Fractional gain carries over
via `WhammyTicksRemainder`; the bar caps at `TicksPerFullSpBar`.

Gain while star power is already active extends the current activation
(`UpdateStarPowerEnds` recomputes the end position).

### Activation

- Requires at least a half bar (`CanStarPowerActivate`: `StarPowerTickAmount >=
  TicksPerHalfSpBar`) plus star power input.
- If a bandmate has failed (`PlayerNeedsRevive`), activating burns half a bar and
  revives them (`StarPowerRevives++`) instead of activating star power - even if the
  player is already in star power.
- Otherwise star power activates: `ScoreMultiplier` doubles (`UpdateMultiplier`
  multiplies by 2 while active), activation time/position/count are recorded, and the
  end time is derived from the current bar amount.

### Drain and duration

While active, the bar drains one normalized measure tick per normalized measure tick
of chart progress (`CalculateStarPowerDrain`). A full bar therefore lasts 8 chart
measures. Activation ends when the bar hits zero (`ReleaseStarPower`); whammy gain
during an activation pushes the end later.

### Score accounting while active

`AddScore` (called per commit - scored note, partial phrase, or finished sustain) while star power is active:

```
scoreMultiplier = score * ScoreMultiplier        // includes the ×2
spScore        = scoreMultiplier / 2             // half - the SP portion
CommittedScore += scoreMultiplier
StarPowerScore += spScore
MultiplierScore += spScore - score               // combo bonus only; SP portion stays in StarPowerScore
BandBonusScore  += BandBonusMultiplier * spScore // band layer (see band section)
```

`StarPowerScore` is the total points attributable to star power (the extra half of
every doubled note, `spScore`). `MultiplierScore` receives `spScore - score` - the
combo bonus only, the same increment a non-SP note at the same combo multiplier
would add. Star power therefore does *not* inflate `MultiplierScore`; the SP
portion lives in `StarPowerScore` alone.

The **total** has exactly one formula: every commit runs the same line
`CommittedScore += points × ScoreMultiplier`. The buckets split each commit
differently, though (combo multiplier `m`; star power on, so the total multiplier
is `2m`):

| Commit | `NoteScore` | `SustainScore` | `MultiplierScore` | `StarPowerScore` |
|---|---|---|---|---|
| Plain note | `+points` | `+0` | `+points × (m − 1)` | `+points × m` |
| Sustain finish | `+0` | `+points × m` | `+points × (m − 1)` | `+points × m` |

With star power off, drop the `StarPowerScore` column (`m` becomes the total
multiplier). For plain notes the buckets sum exactly to `CommittedScore` - a
50-point note at 2x combo with SP doubling commits 200 = 50 note + 0 + 50 combo
bonus + 100 SP.

Sustain commits are the exception: the combo bonus is counted twice. It appears in
`SustainScore` (inside `points × m`) *and* in `MultiplierScore` (as
`points × (m − 1)`), so the bucket sum overshoots `CommittedScore` - a 25-point
sustain finished at 2x combo while SP is active commits 100, yet the buckets read
50 (sustain) + 25 (combo) + 50 (SP) = 125. The total is
always correct; only the bucket sum lies, and only for sustains.

Star power does not change the star *cutoffs* (thresholds are derived from the chart
base score), but live star progress uses `TotalScore`, which includes star power
doubling - see [Star thresholds](#star-thresholds).

## Coda (Big Rock Endings)

Big Rock Endings are chart phrases (`PhraseType.BigRockEnding`) with a start/end time.
BRE-flagged notes in the span do not score as normal notes: the engine suppresses or
auto-resolves them, skips them in the chart base score, and excludes them from
`TotalNotes`. Instead the player free-plays and builds a bonus.

Two related states matter. `IsCodaActive` covers the timed free-play span. After that
span ends, `CodaHasStarted` can remain true until the charted ending chord is fully
resolved; this interval is the post-BRE tail mentioned above.

**Scoring zones** (`CodaSection.cs`):

| Instrument | Zones | Max score per zone hit |
|---|---|---|
| Guitar, keys (fret mode) | 5 (one per fret) | 150 |
| Drums | 1 (all pads) | 750 |

Zones are notional lanes for bonus bookkeeping, not visual lanes. During the coda,
every input hit calls `HitLane(time, zone)` and the zone awards a bonus based on
time since its last hit:

```
bonus = floor(min(time - lastCollectedTime[zone], 1.5) / 1.5 * MaxLaneScore)
```

`BONUS_RECHARGE_TIME = 1.5`s. Hitting the same zone again within 1.5s awards a partial
bonus scaled by the elapsed time; waiting a full 1.5s awards the maximum. Zones recharge
independently, so on guitar/keys cycling frets collects up to 5 × 150 per 1.5s window.
`TotalCodaBonus` accumulates over the whole section.

**Success condition** - the bonus is only banked if the coda succeeds:

- Starts `true`. BRE-flagged notes inside free-play cannot be missed normally, but a
  miss or overhit while `CodaHasStarted`-notably around the ending chord/tail-sets it
  to `false`.
- The coda ends when its final chord is fully resolved (hit or missed) - coda-end
  markers sit on a single sub-note, so ending early on one chord member is guarded
  against in both `HitNote` and `MissNote`.
- `AwardCodaBonus(success)` banks `CurrentCodaBonus` into `CodaBonuses` (and thus
  `TotalScore` → live stars) only on success.
- Band mode (`EngineManager.Band.cs`): `CodaSuccess` is the AND of every engine's
  success - if any player fails, nobody banks the bonus.

After ending, `InhibitCoda` prevents re-activation until the next coda
(`AwardCodaBonus` clears it).

## Chart base score

`CalculateChartScores()` (abstract in `BaseEngine.Generic.cs`, implemented per engine) is
run once at engine construction and produces two numbers:

- **BaseScore** - the star-cutoff reference score.
- **BaseNoteScore** - the raw point value of the chart (all notes + sustains at 1x, no
  multiplier, no combo).

### The pre-note multiplier convention

**BaseScore values every note at the multiplier the player *brings into* it** - the
multiplier computed from the combo *before* that note's combo increment. It is deliberately
*not* the maximum possible score.

Rationale (commit `df171b81`, "Improve Star Score requirements for sparse charts (#221)"):
players start every song at 1x and must build the multiplier. If the star reference assumed
a permanent 4x, sparse charts (long gaps, low note density) would have mathematically
unreachable star thresholds. Basing stars on the pre-note multiplier gives every chart a
reachable reference and makes star difficulty roughly comparable across songs.

Consequences:

- On instrument charts that cross a multiplier step, BaseScore is lower than an FC's
  committed score. Example: 10 singles → BaseScore 500 (all at 1x), FC committed 550
  (10th note at 2x). Charts that never cross a step can be equal; vocals also use the
  pre-phrase multiplier at runtime. The example's 50-point gap is `MultiplierScore`,
  the bonus earned by building combo.
- The convention must be applied *uniformly*: every note - chord member or not - is valued
  at the pre-increment multiplier of its chord. A chord never scores at two different
  multipliers inside the base calculation.
- Sustains are valued at the same per-chord multiplier as their parent note.

### Per-engine implementation

All engines share the same skeleton (`BaseEngine.cs` / engine-specific overrides);
Big Rock Endings are skipped because they can't be scored as notes - their value is a
runtime bonus, not chartable points (see [Coda](#coda-big-rock-endings)):

```
for each parent note (one chord event):
    if note is BRE: skip                              // BREs can't be scored as notes
    multiplier = min(combo / 10 + 1, MaxMultiplier)   // pre-increment
    note points  = POINTS_PER_NOTE * (1 + ChildNotes.Count)
    baseScore   += multiplier * notePoints
    noteScore   += notePoints
    sustain points = sum over chord of ceil(TickLength / TicksPerSustainPoint)
    baseScore   += multiplier * sustainPoints
    noteScore   += sustainPoints
    combo++     // once per chord, except drums
```

Engine-specific differences:

Sustain handling is **identical** across guitar, five-lane keys, and pro keys (see below);
the real per-engine differences are combo semantics and note point values:

| Engine | Combo | Note points | Sustain handling |
|---|---|---|---|
| Guitar | `combo++` once per chord | `POINTS_PER_NOTE` (50) per chord member | shared (below) |
| Five-lane keys | `combo++` once per chord | `POINTS_PER_NOTE` (50) per chord member | shared |
| Pro keys | `combo++` once per chord | `POINTS_PER_PRO_KEYS_NOTE` (120) per chord member | shared |
| Drums | `combo += 1 + ChildNotes.Count` - every chord member increments | `POINTS_PER_NOTE`, or `POINTS_PER_PRO_NOTE` (60) in pro 4-lane mode, per chord member | none - drums have no sustained notes in base calc |
| Vocals | `combo++` per phrase; multiplier = `min(combo + 1, MaxMultiplier)`, no 10-combo step | `PointsPerPhrase` (typically 100) per phrase | n/a - phrases score as a unit, no sustains; percussion phrases intentionally excluded from base score |

Sources: `GuitarEngine.cs:398`, `FiveLaneKeysEngine.cs:169`, `ProKeysEngine.cs:235`, `DrumsEngine.cs:363`, `VocalsEngine.cs:324`.

**Shared sustain handling (guitar, five-lane keys, pro keys):** sum over `note.AllNotes` -
`ceil(chordNote.TickLength / TicksPerSustainPoint)` per member, each valued at the chord's
pre-increment multiplier. The `AllNotes` enumerator's index 0 is the parent note itself, so
the parent sustain is counted exactly once through the loop - never add it separately.

### Pitfalls (bugs found here)

- The `AllNotes` enumerator starts at index 0 = the note itself. Summing sustains over
  `AllNotes` *and* adding the parent's sustain separately double-counts the parent sustain.
  The parent's sustain must only be counted via the `AllNotes` loop.
- Recomputing the multiplier or incrementing combo *inside* the per-note chord loop
  (instead of once per chord) breaks the pre-note convention: chord members after the first
  get scored at a stepped-up multiplier, inflating BaseScore and shifting star thresholds.
  This happened to five-lane keys (PR #439) because the per-note loop recomputed both per
  chord member.

## Total score and live stars

`TotalScore` (`BaseStats.cs`) composes every scoring surface:

```
TotalScore = CommittedScore + PendingScore + SoloBonuses + CodaBonuses
```

- `CommittedScore` - banked points (notes, sustains, multiplier, star power).
- `PendingScore` - points earned but not yet banked (sustain still being held).
- `SoloBonuses` - bonus from completed solo sections (see [Solo bonuses](#solo-bonuses)).
- `CodaBonuses` - bonus from Big Rock Endings (see [Coda](#coda-big-rock-endings)).

`UpdateStars` runs after scored notes and sustain updates. It advances
`CurrentStarIndex` only when `TotalScore` is strictly greater than a threshold. At
exact equality the index has not advanced, although interpolated `Stars` can equal
the next integer because progress is 1.

```
Stars = CurrentStarIndex + progress   // progress = inverse lerp between thresholds
```

Live star progress therefore moves with *everything*: star power doubling, solo
bonuses, coda bonuses, and pending sustain points. The thresholds themselves are
static (computed once at engine construction from the [chart base score](#chart-base-score)); only
the comparison value is live.

### Solo bonuses

Solo sections are chart markers (`IsSolo` notes between `solo` / `soloend` events).
`SoloSection` tracks the section's note count and how many were hit; when the section
ends (`EndSolo`, `BaseEngine.Generic.cs`):

```
soloPercentage = NotesHit / NoteCount

if soloPercentage < 0.6:
    SoloBonus = 0
else:
    multiplier = clamp((soloPercentage - 0.6) / 0.4, 0, 1)
    SoloBonus = floor_to_50(100 * NotesHit * multiplier)
```

- **60% threshold** - below 60% yields no bonus. Exactly 60% also computes to zero;
  the bonus becomes positive only above 60%.
- **Scales 60% → 100%** - bonus ramps from 0 to `100 × NotesHit` (2×
  `POINTS_PER_NOTE` per hit; the code comment flags this value as unverified against
  the old engine).
- **Rounded down to nearest 50.**

For star thresholds, the engine precomputes `MaxSoloBonusPoints`
(`CalculateTotalSoloBonus`): `Σ solo.NoteCount × 100` over all solo sections - the
bonus if every solo is 100% hit. It feeds the `soloScore` term of the [star thresholds](#star-thresholds) formula,
so solos raise the bar *and* provide the points to reach it.

## Star thresholds

`PopulateStarScoreThresholds` (`BaseEngine.Generic.cs`):

```
threshold[i] = floor(baseScore * starThresholds[i] + soloScore * soloThresholds[i])
```

- `starThresholds` / `soloThresholds` are engine parameters (fractions of BaseScore /
  MaxSoloBonusPoints).
- Instrument test fixtures use 0.06, 0.12, 0.2, 0.45, 0.75, and 1.09 for stars 1–6;
  these are test inputs, not constants owned by the engine. Vocal fixtures differ.
- `soloScore` = `MaxSoloBonusPoints`, the maximum solo bonus available in the chart.
- Star power is not part of base score or thresholds; it doubles committed score mid-song
  but does not change the star cutoffs.

Thresholds are computed once at engine construction and never move. Whether the player
actually reaches them depends on live `TotalScore` (see
[Total score and live stars](#total-score-and-live-stars)),
which includes star power doubling, solo bonuses, coda bonuses, and pending sustains.

## Band layer

In band mode (`EngineManager.Band.cs`) the per-player engines keep their own scores, and
the manager adds the band layer on top.

### Band multiplier and band bonus

```
BandMultiplier = max(_starpowerCount * 2, 1)     // 1x with none; then 2x, 4x, 6x, ...
BandBonusMultiplier = BandMultiplier - (own SP active ? 2 : 1)
```

`_starpowerCount` tracks how many players have star power active (updated from each
engine's `OnStarPowerStatus`). The player's own star power contribution is subtracted
from their band bonus multiplier because their individual `ScoreMultiplier` already
includes their own doubling.

Examples (2 players, A in star power):

| Player | BandMultiplier | BandBonusMultiplier | Effect |
|---|---|---|---|
| A (SP active) | 2 | 0 | own doubling already in individual score; no band bonus |
| B (no SP) | 2 | 1 | every scored note earns a +100% band bonus |

`AddScore` accumulates band bonus on every scored note (`BaseEngine.Generic.cs`):

```
// star power inactive:
BandBonusScore += BandBonusMultiplier * scoreMultiplier
// star power active (uses spScore = scoreMultiplier / 2):
BandBonusScore += BandBonusMultiplier * spScore
```

`BandBonusScore` is engine-side; the band's displayed `Score`/`Combo`/`Stars` are
assembled by the game layer from all players' committed scores plus their
`BandBonusScore`s (the manager's `Score` property is set externally).

### Band star cutoffs

`GetStarScoreCutoffs` combines each player's per-threshold cutoffs:

```
bandCutoff[i] = floor((sum over players of playerCutoff[i]) * (1 + 0.265 * (nPlayers - 1)))
```

Six thresholds (`NUMBER_OF_STAR_SCORE_THRESHOLDS = 6`). The `1 + 0.265 × (nPlayers − 1)`
factor compensates for band play: more players make the multiplier easier to keep high
and star power easier to chain, so the combined cutoff is scaled up rather than summed
plain.

### Unison bonus

Matching star power phrases across at least two non-vocal instrument tracks
(`UnisonEvent`). Vocals and harmonies are explicitly excluded. When every participant
completes their phrase, `AwardUnisonBonus` gives each participating engine a quarter
bar of star power (see [Earning star power](#earning-star-power)). The game layer
listens for `OnUnisonBonusAwarded` to show the bonus.

## Score stats glossary

| Stat | Meaning |
|---|---|
| `BaseScore` | Chart reference score: every note/sustain at pre-note multiplier. Star cutoffs derive from it. |
| `BaseNoteScore` | Raw chart value: every note/sustain at 1x. |
| `CommittedScore` | Actual banked score from note, phrase, partial-vocal, and committed sustain scoring; multiplier included. |
| `NoteScore` | Committed points from note hits (base value, no multiplier). |
| `SustainScore` | Sustain points credited at commit: `points × combo multiplier` at the multiplier in effect when the sustain finishes, star power doubling stripped. Includes the multiplier - not a base value. |
| `MultiplierScore` | Accumulated multiplier bonus, summed per `AddScore` call: `scored - base` (or `scored / 2 - base` while star power is active, the SP half landing in `StarPowerScore`). Includes sustain bonuses; not equal to `CommittedScore - NoteScore - SustainScore` (that residual only balances for notes without sustains - see [Score accounting while active](#score-accounting-while-active)). |
| `PendingScore` | Points earned but not yet banked (e.g. sustain still being held). |
| `MaxCombo` | Highest combo reached. |
| `Percent` | `NotesHit / TotalNotes`; forced to 1.0 when `TotalNotes` is 0. Vocals override this with tick-based percent. |
| `IsFullCombo` | Whether `MaxCombo` reached the chart target: `TotalNotes`, or `TotalChords` for keys. A combo-breaking overhit can make this false even when no charted note was missed. |
| `TotalScore` | `CommittedScore + PendingScore + SoloBonuses + CodaBonuses`. The value compared against star thresholds. |
| `SoloBonuses` | Sum of bonuses from completed solo sections. |
| `CodaBonuses` | Sum of bonuses from completed Big Rock Endings. |
| `MaxSoloBonusPoints` | Theoretical maximum solo bonus (`Σ solo.NoteCount × 100`); the `soloScore` term of the star thresholds. |
| `BandMultiplier` | `max(2 × players-in-SP, 1)`. |
| `BandBonusMultiplier` | Per-player band bonus factor = `BandMultiplier − (own SP active ? 2 : 1)`. |
| `BandBonusScore` | Accumulated band bonus. Uses `BandBonusMultiplier × scoreMultiplier` outside own SP, or `BandBonusMultiplier × spScore` during own SP. |
| `StarPowerScore` | Points attributable to star power: half of every score committed while active. |
| `StarPowerWhammyTicks` | Total whammy ticks earned from sustains. |
| `TotalStarPowerTicks` | Total star power ticks earned from all sources (phrases, whammy, unison). |
| `TotalStarPowerBarsFilled` | `TotalStarPowerTicks / TicksPerFullSpBar`. |
| `StarPowerActivationCount` | Number of star power activations. |
| `StarPowerRevives` | Number of half-bar revives performed. |
| `TimeInStarPower` | Total time spent with star power active. |
| `LanedNotesHit` | Lane notes autohit (or hit) while a lane was active; excluded from timing-offset stats. |
| `Overstrums` | Number of overstrums/overhits. |
| `AverageMultiplier` | `CommittedScore / BaseNoteScore`; float, recomputed on every `AddScore`. |

## History

- `df171b81` (PR #221, Jul 2025) - "Improve Star Score requirements for sparse charts":
  introduced weighted base score and the sparse-chart leniency rationale.
- `b3d37169` (Apr 2026) - star rework: replaced weighting with the current pre-note
  multiplier, split `noteScore` from `baseScore`, added disjoint-chord handling to
  `CalculateChartScores`. Accidentally double-counted parent sustains (five-lane keys) and
  double-incremented combo (guitar disjoint chords, keys).
- PR #439 - fixed the sustain double-count and disjoint-chord combo overcount; restored
  per-chord combo + per-chord multiplier in five-lane keys.
- (current) - guitar sustain crediting no longer gated on `IsDisjoint`; non-disjoint
  chords now credit every sustained member's sustain in base score and runtime.

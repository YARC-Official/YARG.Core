# 5-Fret to 6-Fret Chart Conversion

This document describes how YARG converts a 5-fret guitar chart into a 6-fret (GH Live style) chart when a player chooses a 6-fret game mode for a song that only has a 5-fret track.

The conversion lives in `Chart/Tracks/InstrumentDifficultyExtensions.cs` (`ConvertFiveFretToSixFret` and helpers) and is reached through `SongChart.GetSixFretPlayableDifficulty`.

---

## 1. Why conversion is needed

Fret values coincide numerically between formats, so a naive mapping is a plain copy:

| 5-fret  | value | 6-fret  | lane |
| :------ | :---- | :------ | :--- |
| Green   | 1     | Black 1 | 0    |
| Red     | 2     | Black 2 | 1    |
| Yellow  | 3     | Black 3 | 2    |
| Blue    | 4     | White 1 | 0    |
| Orange  | 5     | White 2 | 1    |
| —       | —     | White 3 | 2    |
| Open    | 7     | Open    | —    |

However, GH Live charting rules make several naive placements **illegal or unplayable**:

1. **Barre legality** — two chord notes in the same lane form a *barre*; no other chord note may sit in a lane to its **left**. The identity mapping creates the `Black2+White2` barre for every `Red+Orange` chord, which is illegal whenever Green or Blue is also in the chord.
2. **Sustain clarity** — a second sustain must not stack onto a lane that already has an overlapping sustain from a different note.
3. **Barre hopos** — no pull-offs from a barre chord into a single in the barre's own lane, and no hammer-ons from a single onto a barre in the single's own lane.
4. **Anchor phrases** — patterns like `YB,Y,Y,YB` are played by holding one note and tapping the other. The shared fret must keep its placed position across the phrase.
5. **Capacity** — a legal chord holds at most **5** fretted notes: two barres in the two leftmost lanes plus one single in the rightmost (e.g. `B1W1,B2W2,B3`). A triple barre is forbidden, and any single to the left of a barre is forbidden, so 6-note chords have no legal placement.

## 2. Where conversion runs

`SongChart.GetSixFretPlayableDifficulty(instrument, difficulty)` is the single entry point:

- Six-fret instruments read their own track directly (no conversion).
- Five-fret instruments get `ConvertFiveFretToSixFret`, which **clones** the difficulty (the shared chart data is never mutated) and rewrites the clone's frets.

Both gameplay (`SixFretGuitarPlayer.GetNotes`) and replay analysis (`ReplayAnalyzer`) must go through this helper. If analysis ever resimulates the raw 5-fret track, replays fail verification because gameplay ran on different notes.

Conversion happens once per song load; it is not a gameplay hot path.

## 3. The algorithm

The converter walks the notes in order and places each chord (parent + children) at a chosen position. State carried between chords:

- previous placed **lane centroid** and previous **identity fret centroid** (movement scoring)
- previous chord's placed frets + 5-fret identities (hopo rule, repeat penalty, anchor memory)
- active sustains as `(tickEnd, placedFret, identityFret)` (lane exclusion / absorption)

For each chord:

### 3.1 Member collection

Open and Wildcard members keep their shared value (7/8), are excluded from lane logic, and their mask bits survive untouched. Members are then sorted by fret.

### 3.2 Candidates

Candidates are all **order-preserving** assignments of distinct 6-fret values (1-6) to the sorted members — i.e. combinations, `C(6,k) ≤ 15` per chord. Preserving fret order keeps chords physically shaped like their 5-fret originals.

### 3.3 Hard filters (pass 0)

A candidate is rejected when any of the following fails. If *every* candidate is rejected, a second pass re-runs without the soft exclusions (sustain lanes and hopo rule) so a placement always exists:

- **Barre legality** — for each lane containing both members, no chord note in any lane to its left.
- **Sustain lanes** — no chord note may strike into a lane occupied by an active sustain, *except* when the chord **absorbs** that sustain: it contains the same 5-fret note and places it at the sustained fret. Absorption is what keeps anchor phrases with sustaining anchors playable (see §3.6).
- **Barre hopo rule** — checked per lane between consecutive chords: a candidate may not barre a lane where the previous chord held a single note (hammer-on onto a barre), and may not place a note alone in a lane where the previous chord had a barre (pull-off from a barre). This applies even when the chords have other notes in different lanes.

### 3.4 Movement scoring

Surviving candidates are scored primarily in **lane space**, not fret space: the 6-fret highway visually snakes (black row left-to-right, then white row), so a fret-monotonic sequence would zig-zag on screen. Lane-monotonic placements read as clean left-to-right sweeps.

```
idealLane = previousLane + (identityFretCentroid - previousIdentityCentroid) * LANES_PER_FRET
```

`LANES_PER_FRET` is 0.5; exact half-lane ideals are pushed 0.5 in the direction of the chart's step so runs sweep lane-by-lane instead of hovering.

```
score = 2.0 * |candidateLane - idealLane|        (lane distance dominates)
      + 0.125 * |candidateFretCentroid - identityFretCentroid|   (row tie-break)
      + 2.0  * (per member) moving a fret the previous chord also contains
      + lookahead overflow (below)

Additionally, two placement rules are enforced as pass-0 filters rather than score
terms: a chord with a *different* 5-fret shape than the previous chord may not map
to the identical 6-fret placement (distinct chords must stay distinct, otherwise a
passage collapses into repeated shapes), and the sustain/hopo exclusions of §3.3.
```

- The **anchor term** implements §1.4: whenever the current chord shares a 5-fret fret with the previous chord, that member is strongly encouraged to keep its previous placed fret. Because it is a score penalty rather than a hard filter, sustains and legality still win when they conflict.
- The **lookahead** term penalizes placements that would push the *next* chord's ideal lane off the highway, so rising runs shift over early instead of pinning at White 3 and snapping back.

The first chord of a track has no movement history; its ideal lane is simply the identity mapping's lane centroid.

### 3.5 Applying the placement

Each member's `Fret` is set and its `NoteMask` / `DisjointMask` bits are remapped; the parent's `NoteMask` is rebuilt from the chosen mask plus any open bits. The 5-fret identity is recorded **before** the fret is overwritten (it feeds the next chord's anchor and absorption checks).

Sustaining members are registered as `(tickEnd, placedFret, identityFret)` in the active-sustain list, pruned by tick each chord.

### 3.6 Anchor phrases and sustain absorption

A phrase like `YB,Y,Y,YB` (or a sustained Y under `YB` chords) is played in 5-fret by holding one note and tapping the other. Two rules cooperate to translate this:

- The **anchor penalty** (§3.4) keeps shared frets at their placed positions across chords — including across intervening chords that do not contain the fret, since the anchor state persists per fret.
- The **absorption exception** (§3.3) lets a chord strike the sustained fret itself, so a sustaining anchor note can continue straight through its chords instead of forcing them into far lanes.

### 3.7 Five-note chords

`G,R,Y,B,O` fits exactly one legal shape family: two barres in the leftmost lanes plus a rightmost single — `B1W1,B2W2,B3` or `B1W1,B2W2,W3`. The candidate enumeration finds these automatically; no notes are dropped. (A 6-note chord — a triple barre — would have no legal placement, but 5-fret charts cannot produce one.)

## 4. Known limits

- **Sustain walls** — charts with many long overlapping sustains (ambient/post-rock styles) can occupy all three lanes at once. The fallback then places nearest to the ideal and some sustain overlap is unavoidable; three lanes cannot hold what a 5-fret chart overlaps.
- **Edge clamps** — runs that exceed the highway (more than 2 lanes of continuous rise) clamp at Black 1 / White 3; the lookahead shifts them over early but the final step of an over-long run always flattens.
- **Context drift** — because placement is greedy and context-sensitive, the same 5-fret chord can legitimately map to different 6-fret shapes in different parts of a song. This mirrors how human GH Live charters work but means there is no fixed per-fret lookup table.

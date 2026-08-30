# Auto-Generated Lipsync for Charts Without Milo Data

When a chart lacks Milo lipsync data (typically from Rock Band `.lipsync` files), YARG automatically generates lipsync events from the chart's lyrics track. This document describes the implementation.

The phoneme→viseme mapping, weight model, and expression behavior documented here were validated against handmade Milo lipsync data from **12 Rock Band charts** (~400k viseme keyframes, 3,217 CMU-resolved lyric-word windows), using per-phoneme statistical lift analysis with carryover confounds split out conditionally.

---

## Overview

The lipsync system has two sources:

1. **Milo Lipsync** — Pre-authored lipsync data from Rock Band charts (`.lipsync` files), parsed via `MiloLipsync` and `MiloVenue`
2. **Auto-Generated** — Generated at chart load time from lyrics using `LipsyncGenerator`

The fallback logic is in `SongChart.AutoGeneration.GenerateMissingLipsync()`: Milo (then `.voc`) data is used when present; the generator only runs for parts that have no authored lipsync. Charts with authored lipsync are unaffected by the generator.

### Lo-only visemes

The generator emits only `_lo` visemes. Authored Milo data pairs most `_lo` keyframes with a matching `_hi` channel (11 of the 12 reference charts), but YARG avatars define no `_hi` expression clips and the runtime (`VRMCharacter.SetExpression`) falls back to a single generic mouth key for unknown viseme names — so emitting `_hi` today only doubles the event count with no visual effect. Revisit if avatars ship `_hi` clips (authored pairing is an exact mirror, or lo ≥ hi).

---

## LipsyncGenerator Pipeline

### 1. Dictionary Initialization

On game startup (`LoadingScreen.cs`), the CMU Pronouncing Dictionary is loaded from `Assets/Resources/cmudict.txt` and parsed:

```csharp
var cmudictAsset = Resources.Load<TextAsset>("cmudict");
var dictText = cmudictAsset.text;
await UniTask.RunOnThreadPool(() => LipsyncGenerator.Initialize(dictText));
```

`CMUDict.Initialize()` builds a `Dictionary<string, string[]>` mapping words → phoneme arrays (e.g. `"HELLO"` → `["HH", "AH0", "L", "OW1"]`), stripping `(N)` homograph suffixes (first pronunciation wins) and stress digits.

Dictionary hit rate on the reference charts is ~69% of lyric tokens (≈90% excluding one chant song). Misses are per-note syllable fragments (`GON`, `SKAT`, `NEV` — RB3 charts split words per note without join symbols) and punctuation-bearing keys; those words fall back to `SimpleSyllable`.

### 2. Per-Word Processing

Syllable fragments are re-joined into whole words before analysis. Fragments linked by
`JoinWithNext`/`HyphenateWithNext` flags (e.g. `Du-` + `vet`) are concatenated, and empty
slide-gap fragments (pitch slides) extend the previous syllable's audible length.

For each word in each `LyricsPhrase`:

1. **Clean text** — Concatenate fragment texts, strip vocal symbols (`-`, `=`, `+`, `#`, `^`, `*`, `$`, …)
2. **Lookup phonemes** — Try CMU dict on the whole word; fallback to simple vowel mapping
3. **Split phonemes → syllables** — One syllable per vowel, using maximum-onset (consonants between vowels start the next syllable)
4. **Align syllables → fragments** — 1:1 when counts match; proportional grouping when the word has more/fewer syllables than fragments
5. **Map phonemes → visemes** — Each phoneme maps to a `LipsyncEvent.LipsyncType` (see Viseme Mapping below)
6. **Generate timed events** — Create `LipsyncEvent` entries with smooth ramped transitions

### 3. Syllable Structure

Each lyric produces a `Syllable`:

```csharp
struct Syllable {
    List<LipsyncType> Initial;     // Consonants before vowel
    LipsyncType VowelMain;         // Primary vowel viseme
    LipsyncType? VowelEnd;         // Diphthong end viseme (e.g. "boy" = OX → EAT)
    List<LipsyncType> Final;       // Consonants after vowel
}
```

Phoneme classification:
- **Vowels**: AA, AE, AH, AO, AW, AY, EH, ER, EY, IH, IY, OW, OY, UH, UW
- **Diphthongs**: AY, EY, AW, OY (have a `VowelEnd` transition); OW holds its start shape
- **Consonants**: Everything else → Initial (pre-vowel) or Final (post-vowel)

### 4. Timing & Transitions

| Constant | Value | Purpose |
|----------|-------|---------|
| `ATTACK_MAX_TIME` | 0.20s | Consonant/vowel attack ramp length |
| `CODA_MAX_TIME` | 0.15s | Final-consonant ramp length |
| `RELEASE_TIME` | 0.20s | Mouth close ramp length |
| `RELEASE_THRESHOLD` | 0.30s | Minimum gap before releasing the mouth |
| `STEP_TIME` | 1/30s | Interpolation step interval (Milo-style keyframes) |
| `MIN_SLOT_DURATION` | 0.05s | Minimum syllable slot duration |
| `VOWEL_PEAK_BASE_WEIGHT` | 0.20 | Peak weight floor for short syllables |
| `VOWEL_PEAK_SCALE` | 0.35/s | Peak weight growth per second of note length |
| `VOWEL_PEAK_CAP` | 0.90 | Maximum vowel peak weight |
| `VOWEL_TAIL_WEIGHT` | 0.05 | Weight the vowel decays to by the slot end |
| `VOWEL_PEAK_HOLD_FRACTION` | 0.30 | Fraction of a short slot spent at/near the peak before decay |
| `VOWEL_PEAK_HOLD_TIME` | 0.30s | Absolute cap on the peak hold (long slots never hold wide open) |
| `VOWEL_DECAY_TIME` | 0.60s | Absolute decay time from peak to tail |
| `MAX_UNVOICED_SLOT` | 2.0s | Slot cap when no vocal note end is available (phrase-final words) |
| `CONSONANT_WEIGHT` | 0.32 | Weight for consonant shapes (authored consonant means measure ~0.28) |
| `CO_ARTICULATION_RESIDUAL` | 0.08 | Faint weight the outgoing viseme keeps mid-ramp |

**Vowel peak envelope.** The vowel rides a peak-shaped envelope instead of a flat hold: the attack ramps up to the syllable's peak weight (scaling with note length; longer/held notes open fuller), the peak is sustained briefly, then it decays to a low tail within ~0.9s and settles there — regardless of how long the note or slot lasts. This matches authored data, where keyframe weights are mostly low (median 0.19, p90 0.51, only ~1% ≥ 0.9) and the mouth settles between syllables and during long sustains.

**Co-articulation.** During any viseme ramp, the outgoing shape fades to a faint residual (0.08, clamped to the outgoing weight) over the first 60% of the ramp and then out by the end — two mouth shapes are briefly active together, like authored keyframes.

**Sustains.** On note-capped slots the vowel settles at a 0.5 sustain floor after the brief peak — authored long sustains keep the mouth open (~0.5 dominant weight throughout the note) and close only at the note's end. Slot ends use the note's `TotalTimeEnd`, so pitch-slide children extend the sustain through pitch changes, and the sustain weight is modulated ±0.12 by the pitch at that moment (higher pitch = slightly more open, lerped across slide segments via `PitchAtSongTime`). Slots without note evidence (lyrics-track path) decay to the 0.05 tail instead.

**Per-slot closure.** At the end of every syllable slot, all visemes that slot used are explicitly driven to 0 — viseme channels persist until rewritten, and authored data zeroes inactive channels constantly. Without this, envelope tails and transition residuals keep the mouth open through silence on avatars with per-viseme blendshapes.

**Timing sources:** when a vocals part is available, each syllable uses its vocal note's
actual end time; the mouth closes at the note end if a silent gap follows instead of holding
open until the next lyric. Otherwise slots run until the next fragment's start time.

**Timeline for a syllable slot `[t, t+d]`:**

```
t-attack                 t                       t+d
│                        │                       │
├─ Attack (anticipation): ramp through initial consonants into the vowel,
│   peaking AT the lyric time (in the gap before the slot when available)
├─ Vowel envelope (dense per-frame keys)
│   ├─ peak sustained ~45% of the hold
│   └─ smoothstep decay to the tail weight by t+d
│   └─ If diphthong: envelope to 60%, then ramp to VowelEnd at the
│       envelope's current weight over the remaining 40%
├─ Coda: ramp from the tail through each final consonant up to t+d
└─ Release (only if a gap > RELEASE_THRESHOLD follows): ramp weight to 0 over RELEASE_TIME
```

All ramps use smoothstep easing for S-curved motion. Mouth state is tracked while
generating, so transitions ramp from the *actual* previous viseme and weight — no instant
snaps. Ramps interpolate in ~30fps steps so the runtime never applies multiple collapsed
sub-frame steps in one update.

### 5. Brow & Expression Events

Authored data keeps brow/emotional channels active for most of the song (measured duty cycle 60–100%, stacking multiple channels, segments from ~1s to minutes). The generator therefore uses a **sustained brow state machine** instead of per-phrase blips:

- Lyric phrases are grouped into **sections** separated by silent gaps > 2s (`BROW_SECTION_GAP`)
- One brow state is chosen per section (weighted random) and held across the section and its internal instrumental gaps, with slow 0.8s fades in/out
- 40% chance of a faint secondary brow channel (0.1–0.25 weight) under the primary, mirroring authored stacking
- Primary intensity 0.3–0.7

Palette (weights roughly follow authored segment counts): `Brow_down` (5), `Brow_pouty` (5), `Brow_aggressive` (5), `Brow_up` (2), `Squint` (1.5), `Brow_dramatic` (1); 8% of sections use `exp_rocker_smile_intense_01` instead of a brow.

`exp_rocker_teethgrit_happy_01` and `exp_dramatic_happy_eyesopen_01` are never emitted — they appear in zero of the 12 reference charts.

### 6. Blinks

- **Blinks**: Every 2–6 seconds (randomized), ~0.15s duration (on → off), independent of viseme/expression changes

---

## Viseme Mapping (Phoneme → LipsyncType)

Validated against authored Milo data (per-phoneme lift analysis; "—" = unchanged from the pre-analysis mapping and not contradicted by evidence).

### Vowels

| Phoneme | Example | Viseme | Evidence |
|---------|---------|--------|----------|
| AA | f**a**ther | `Ox_lo` | — |
| AE | c**a**t | `Cage_lo` | ambiguous in authored data (no significant lift); kept, see follow-ups |
| AH | **u**p | `If_lo` | — |
| AO | th**ou**ght | `Ox_lo` | lift 1.9× (was Earth) |
| EH | b**e**d | `If_lo` | `If` in 84% of windows (was Cage) |
| ER | b**ir**d | `Earth_lo` | lift 2.3× (was Church) |
| IH | b**i**t | `If_lo` | — |
| IY | s**ee** | `Eat_lo` | — |
| UH | b**oo**k | `Though_lo` | sparse evidence; unchanged |
| UW | f**oo**d | `Wet_lo` | — |

### Diphthongs

| Phoneme | Example | Viseme Transition | Evidence |
|---------|---------|------------|---------------|
| AY | **eye** | `Eat_lo` → `If_lo` | `Eat` in 69% of windows, `If` tail (was Ox → If) |
| EY | d**ay** | `Ox_lo` → `If_lo` | Ox start lift 1.9× (was Cage → If) |
| OW | g**o** | `Ox_lo` (held, no tail) | Ox in 76% of windows; `Wet` absent from tails (was Oat → Wet) |
| AW | c**ow** | `Ox_lo` → `Wet_lo` | — |
| OY | b**oy** | `Ox_lo` → `Eat_lo` | `Eat` tail-dominant (was Oat → If) |

### Consonants

| Phonemes | Viseme | Evidence |
|----------|--------|----------|
| B, P, M | `Bump_lo` | — |
| F, V | `Fave_lo` | — |
| TH, DH | `Though_lo` | lift 4.3× / 5.2× (was Told) |
| S, Z | `Size_lo` | — |
| T, D | `Told_lo` | — |
| N | `New_lo` | — |
| L | `Told_lo` | 61% of clean-L windows (was New) |
| NG | `New_lo` | lift 4.1× (was Told) |
| K, G | `Cage_lo` | K lift 4.9×; G inferred from same velar class (was Told) |
| SH, ZH, CH, JH | `Church_lo` | lift 9.8× / 8.6× for SH/JH (was Told) |
| R | `Roar_lo` | — |
| W | `Wet_lo` | — |
| Y | `Eat_lo` | — |

### Fallback: Simple Syllable (No CMU Dict)

If CMU dict lookup fails (uninitialized or word not found), `SimpleSyllable()` uses first vowel character:

| Vowel | Viseme |
|-------|--------|
| a | `Ox_lo` |
| e | `Cage_lo` |
| i | `Eat_lo` |
| o | `Oat_lo` |
| u | `Wet_lo` |
| (none) | `If_lo` |

No initial/final consonants or diphthongs are generated in fallback mode.

---

## Data Structures

### LipsyncEvent

```csharp
public class LipsyncEvent : ChartEvent, ICloneable<LipsyncEvent>
{
    public enum LipsyncType { /* visemes (_lo/_hi) + brows + expressions */ }

    public LipsyncType Type { get; }
    public float Value { get; }  // 0.0–1.0 (weight)

    public LipsyncEvent(LipsyncType type, float value, double time, uint tick)
}
```

- `Value` is analog (0.0–1.0): vowel peaks scale with note length, consonants sit at ~0.32, `0f` for released visemes
- Multiple events at same timestamp can overlap (e.g. co-articulation ramps from→to)

### LyricsTrack → LyricsPhrase → LyricEvent

```
LyricsTrack
  └─ List<LyricsPhrase> Phrases
       └─ LyricsPhrase (time, timeLength, tick, tickLength)
            └─ List<LyricEvent> Lyrics
                 └─ LyricEvent (text, time, tick, flags)
```

---

## Logging

Enable trace logging to debug generation:

```csharp
YargLogger.LogFormatTrace("Lipsync word '{0}' at {1:F3}s -> {2} syllable(s)", ...);
```

---

## Files

| File | Purpose |
|------|---------|
| `YARG.Core/Chart/LipsyncGenerator.cs` | Main generation logic |
| `YARG.Core/Chart/CMUDict.cs` | CMU dictionary parser |
| `YARG.Core/Chart/Events/LipsyncEvent.cs` | Event data structure |
| `YARG.Core/Chart/Tracks/Lyrics/*.cs` | Lyrics data structures |
| `YARG.Core/IO/Milo/MiloLipsync.cs` | Milo `.lipsync` parser |
| `YARG.Core/Chart/Tracks/MiloVenue.cs` | Milo venue/lipsync integration |
| `YARG.Core/Chart/SongChart.AutoGeneration.cs` | Fallback logic |
| `Assets/Resources/cmudict.txt` | Phoneme dictionary (CMU format) |
| `Assets/Script/Persistent/LoadingScreen.cs` | Dictionary initialization |
| `YARG.Core.UnitTests/Chart/LipsyncGeneratorTests.cs` | Mapping unit tests |

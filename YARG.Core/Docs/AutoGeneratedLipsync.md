# Auto-Generated Lipsync for Charts Without Milo Data

When a chart lacks Milo lipsync data (typically from Rock Band `.lipsync` files), YARG automatically generates lipsync events from the chart's lyrics track. This document describes the implementation.

---

## Overview

The lipsync system has two sources:

1. **Milo Lipsync** — Pre-authored lipsync data from Rock Band charts (`.lipsync` files), parsed via `MiloLipsync` and `MiloVenue`
2. **Auto-Generated** — Generated at chart load time from lyrics using `LipsyncGenerator`

The fallback logic is in `SongChart.LoadLipsyncFromMilo()`:

```csharp
public static void LoadLipsyncFromMilo(SongChart songChart, SongEntry songEntry)
{
    var miloLipsync = new MiloVenue(songChart, songEntry);
    miloLipsync.Load();
    songChart.LipsyncEvents.AddRange(miloLipsync.LipsyncEvents);

    // Generate lipsync from vocals if no lipsync data was found
    if (songChart.LipsyncEvents.Count == 0)
    {
        GenerateLipsyncFromVocals(songChart);
    }
}

public static void GenerateLipsyncFromVocals(SongChart songChart)
{
    if (!songChart.Lyrics.IsEmpty)
    {
        songChart.LipsyncEvents.AddRange(LipsyncGenerator.GenerateFromLyrics(songChart.Lyrics));
    }
}
```

---

## LipsyncGenerator Pipeline

### 1. Dictionary Initialization

On game startup (`LoadingScreen.cs`), the CMU Pronouncing Dictionary is loaded from `Assets/Resources/cmudict.txt` and parsed:

```csharp
var cmudictAsset = Resources.Load<TextAsset>("cmudict");
var dictText = cmudictAsset.text;
await UniTask.RunOnThreadPool(() => LipsyncGenerator.Initialize(dictText));
```

`CMUDict.Initialize()` builds a `Dictionary<string, string[]>` mapping words → phoneme arrays (e.g., `"HELLO"` → `["HH", "AH0", "L", "OW1"]`).

### 2. Per-Word Processing

Syllable fragments are re-joined into whole words before analysis. Fragments linked by
`JoinWithNext`/`HyphenateWithNext` flags (e.g. `Du-` + `vet`) are concatenated, and empty
slide-gap fragments (pitch slides) extend the previous syllable's audible length.

For each word in each `LyricsPhrase`:

1. **Clean text** — Concatenate fragment texts, strip vocal symbols (`-`, `=`, `#`, `!`, `^`, `$`)
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
    LipsyncType? VowelEnd;         // Diphthong end viseme (e.g., "eye" = OX → IF)
    List<LipsyncType> Final;       // Consonants after vowel
}
```

Phoneme classification:
- **Vowels**: AA, AE, AH, AO, AW, AY, EH, ER, EY, IH, IY, OW, OY, UH, UW
- **Diphthongs**: AY, EY, OW, AW, OY (have a `VowelEnd` transition)
- **Consonants**: Everything else → Initial (pre-vowel) or Final (post-vowel)

### 4. Timing & Transitions

| Constant | Value | Purpose |
|----------|-------|---------|
| `ATTACK_MAX_TIME` | 0.20s | Consonant/vowel attack ramp length |
| `CODA_MAX_TIME` | 0.15s | Final-consonant ramp length |
| `RELEASE_TIME` | 0.20s | Mouth close ramp length |
| `RELEASE_THRESHOLD` | 0.30s | Minimum gap before releasing the mouth |
| `STEP_TIME` | 1/30s | Interpolation step interval (Milo-style keyframes) |
| `VOWEL_BASE_WEIGHT` | 0.55 | Minimum vowel weight |
| `VOWEL_WEIGHT_SCALE` | 1.0 | Vowel weight growth per second of note length |
| `VOWEL_WEIGHT_CAP` | 0.95 | Maximum vowel weight |
| `CONSONANT_WEIGHT` | 0.45 | Partial weight for consonant shapes |

Vowel openness scales with the note's duration (longer/held notes open fuller), and holds are
re-emitted as dense per-frame keyframes — matching how authored Milo lipsync data behaves.

**Timing sources:** when a vocals part is available, each syllable uses its vocal note's
actual end time; the mouth closes at the note end if a silent gap follows instead of holding
open until the next lyric. Otherwise slots run until the next fragment's start time.

**Timeline for a syllable slot `[t, t+d]`:**

```
t-attack                 t                       t+d
│                        │                       │
├─ Attack (anticipation): ramp through initial consonants into the vowel,
│   peaking AT the lyric time (in the gap before the slot when available)
├─ Vowel hold (dense per-frame keys; weights glide)
│   └─ If diphthong: hold VowelMain 60%, then 40% ramp to VowelEnd
├─ Coda: ramp from the vowel through each final consonant up to t+d
└─ Release (only if a gap > RELEASE_THRESHOLD follows): ramp weight to 0 over RELEASE_TIME
```

All ramps use smoothstep easing for S-curved motion.

Mouth state is tracked while generating, so transitions ramp from the *actual* previous
viseme and weight — there are no instant snaps and no bulk viseme resets. Ramps interpolate
in ~30fps steps so the runtime never applies multiple collapsed sub-frame steps in one update.

### 5. Blink & Expression Events

- **Blinks**: Every 2–6 seconds (randomized), 0.15s duration (on → off)
- **Expressions**: At phrase starts, 30–70% intensity, max 1.5s or 60% of phrase duration
- Expression types: `Brow_up`, `Brow_down`, `exp_rocker_smile_mellow_01`, `exp_rocker_teethgrit_happy_01`, `exp_dramatic_happy_eyesopen_01`

---

## Viseme Mapping (Phoneme → LipsyncType)

### Vowels

| Phoneme | Example | Viseme | Diphthong End |
|---------|---------|--------|---------------|
| AA | f**a**ther | `Ox_lo` | — |
| AE | c**a**t | `Cage_lo` | — |
| AH | **u**p | `If_lo` | — |
| AO | th**ou**ght | `Earth_lo` | — |
| EH | b**e**d | `Cage_lo` | — |
| ER | b**ir**d | `Church_lo` | — |
| IH | b**i**t | `If_lo` | — |
| IY | s**ee** | `Eat_lo` | — |
| UH | b**oo**k | `Though_lo` | — |
| UW | f**oo**d | `Wet_lo` | — |

### Diphthongs

| Phoneme | Example | Main → End | Viseme Transition |
|---------|---------|------------|-------------------|
| AY | **eye** | `Ox_lo` → `If_lo` | OX → IF |
| EY | d**ay** | `Cage_lo` → `If_lo` | CAGE → IF |
| OW | g**o** | `Oat_lo` → `Wet_lo` | OAT → WET |
| AW | c**ow** | `Ox_lo` → `Wet_lo` | OX → WET |
| OY | b**oy** | `Oat_lo` → `If_lo` | OAT → IF |

### Consonants

| Phonemes | Viseme | Mouth Shape |
|----------|--------|-------------|
| B, P, M | `Bump_lo` | Lips closed |
| F, V | `Fave_lo` | Teeth on lip |
| TH, DH | `Told_lo` | Tongue between teeth |
| S, Z | `Size_lo` | Teeth together |
| T, D, NG, K, G | `Told_lo` | Tongue behind teeth / velar closure |
| N, L | `New_lo` | Tongue up, mouth nearly closed |
| SH, ZH, CH, JH | `Told_lo` | Tongue back |
| R | `Roar_lo` | Rounded |
| W | `Wet_lo` | Rounded |
| Y | `Eat_lo` | Spread |

---

## Data Structures

### LipsyncEvent

```csharp
public class LipsyncEvent : ChartEvent, ICloneable<LipsyncEvent>
{
    public enum LipsyncType { /* 59 values: visemes + expressions */ }
    
    public LipsyncType Type { get; }
    public float Value { get; }  // 0.0–1.0 (weight)
    
    public LipsyncEvent(LipsyncType type, float value, double time, uint tick)
}
```

- `Value` is analog (0.0–1.0): vowel weights scale with note length, consonants sit at ~0.45, `0f` for released visemes
- Multiple events at same timestamp can overlap (e.g., ramp from→to)

### LyricsTrack → LyricsPhrase → LyricEvent

```
LyricsTrack
  └─ List<LyricsPhrase> Phrases
       └─ LyricsPhrase (time, timeLength, tick, tickLength)
            └─ List<LyricEvent> Lyrics
                 └─ LyricEvent (text, time, tick, flags)
```

---

## Fallback: Simple Syllable (No CMU Dict)

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

## Integration Points

| Component | Role |
|-----------|------|
| `LoadingScreen` | Loads `cmudict.txt` from Resources, calls `LipsyncGenerator.Initialize()` |
| `SongChart.LoadLipsyncFromMilo()` | Tries Milo first, falls back to `GenerateLipsyncFromVocals()` |
| `LipsyncGenerator.GenerateFromLyrics()` | Main generation entry point |
| `MiloVenue.HandleLipsync()` | Parses Milo `.lipsync` → `LipsyncEvent` (30fps frame data) |
| `VisemeLookup` (in `MiloVenue`) | Maps Milo `Visemes` enum → `LipsyncEvent.LipsyncType` |

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

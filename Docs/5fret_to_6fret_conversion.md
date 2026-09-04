# 5-Fret to 6-Fret Chart Conversion

This document describes how YARG converts a 5-fret guitar chart into a 6-fret (GH Live style) chart when a player chooses a 6-fret game mode for a song that only has a 5-fret track.

The conversion lives in `Chart/Tracks/InstrumentDifficultyExtensions.cs` (`ConvertFiveFretToSixFret`) and is reached through `SongChart.GetSixFretPlayableDifficulty`.

## 1. The conversion

The conversion is a **direct per-chord lookup**. Fret values coincide numerically between formats, so chords map 1:1:

| 5-fret  | value | 6-fret  | value |
| :------ | :---- | :------ | :---- |
| Green   | 1     | Black 1 | 1     |
| Red     | 2     | Black 2 | 2     |
| Yellow  | 3     | Black 3 | 3     |
| Blue    | 4     | White 1 | 4     |
| Orange  | 5     | White 2 | 5     |
| —       | —     | White 3 | 6     |
| Open    | 7     | Open    | 7     |

### 1.1 Chord substitutions

A small, fixed set of chord shapes maps to a different 6-fret chord instead of the direct mapping:

| 5-fret chord | 6-fret chord    | Notes |
| :----------- | :-------------- | :---- |
| `GRO`        | `B1 W1 W3`      | |
| `RBO`        | `B2 W2 W3`      | |
| `YBO`        | `W1 W2 B3`      | Deliberately mirrors the (unsubstituted) `GRYB` conversion |
| `GRYO`       | `B1 W1 B2 W3`   | Deliberately mirrors the (unsubstituted) `GYBO` conversion |
| `GRBO`       | `B1 W1 B3 W3`   | Feels more in the spirit of GRBO chords; the chord is illegal either way (and `GRBO` is illegal in 5-fret charting in the first place) |
| `RYBO`       | `B1 W1 W2 W3`   | |
| `GRYBO`      | `B1 W1 B2 W2 B3 W3` | The full five-note chord maps to holding **all six** frets |

Every other chord shape (and every single note) uses the direct 1:1 mapping. Open and wildcard notes keep their shared fret value and never take part in substitutions.

## 2. Sustain truncation

The converter tracks active sustains while walking the track. When a chord strikes into a column (fret position 1–3) that already carries an active sustain from an earlier note, the earlier sustain is **truncated** at the chord's tick — a new sustain in a column replaces the older one rather than stacking onto it. For example, if a `W2` sustain starts while a `B2` sustain is still active, the `B2` sustain is cut short.

Truncation applies to any strike into the sustained column, sustained or not. Sustains that have already ended before the chord's tick are simply dropped from tracking.

## 3. Lefty flip

Lefty flip mirrors the 6-fret highway, which physically swaps the black and white pad rows. To keep notes playable on the mirrored highway, every note's pads are color-flipped as well: `B1↔W1`, `B2↔W2`, `B3↔W3` (open and wildcard unchanged).

The flip is applied in `SongChart.GetSixFretPlayableDifficulty` via `FlipSixFretColors` **after** the 5-fret-to-6-fret conversion, so it affects both native 6-fret charts and converted 5-fret charts. It returns a clone; the shared chart data is never mutated.

Both gameplay (`SixFretGuitarPlayer.GetNotes`) and replay analysis (`ReplayAnalyzer`) pass the profile's lefty-flip flag into `GetSixFretPlayableDifficulty` — replays must resimulate the same notes gameplay used or verification fails.

## 4. Where conversion runs

`SongChart.GetSixFretPlayableDifficulty(instrument, difficulty)` is the single entry point:

- Six-fret instruments read their own track directly (no conversion).
- Five-fret instruments get `ConvertFiveFretToSixFret`, which **clones** the difficulty (the shared chart data is never mutated) and rewrites the clone's frets.

Both gameplay (`SixFretGuitarPlayer.GetNotes`) and replay analysis (`ReplayAnalyzer`) must go through this helper. If analysis ever resimulates the raw 5-fret track, replays fail verification because gameplay ran on different notes.

Conversion happens once per song load; it is not a gameplay hot path.

## 5. Notes

- The conversion is **context-free per chord**: the same 5-fret chord always maps to the same 6-fret chord, so there is a fixed lookup table.
- The `GRYBO` substitution maps to holding **all six** frets: the five note members map in pad order to `B1 W1 B2 W2 B3` and a sixth note member is added on `W3`, so the chord is complete both visually and mechanically — the engine requires every fret to be held and scores all six notes.
- The substitution chords are not necessarily *legal* 6-fret chord shapes under strict GH Live charting rules — the mapping intentionally favors simplicity and recognizable shapes over legality.

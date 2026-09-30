using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace YARG.Core.Chart.Hashing
{
    /// <summary>
    /// Writes a CHNF/20260801 BTrack and its TrackHash.
    /// Sections 1–9 follow https://rhinestone-guide-ff8.notion.site/BTrack-Specification-38d2fadb443280a9b0b2ce54efee14af
    /// Vocal sections are a YARG extension in the unknown-section range.
    /// </summary>
    public static class BTrackHasher
    {
        // Constant (identifies “CHNF” file type)
        private const uint FileFormatHeader = 0x43484E46;
        private const uint FileFormatVersion = 20260801;

        private enum BTrackSectionId : ulong
        {
            Resolution = 1,
            TempoMarker = 2,
            TimeSignature = 3,
            StarPower = 4,
            SoloSection = 5,
            FlexLane = 6,
            DrumFreestyle = 7,
            RangeShift = 8,
            Note = 9,

            // YARG vocal extension. CHNF/20260801 unknown-section range: CH
            // readers ignore these payloads and exclude them from TrackHash.
            // YARG hashes every section it writes. Do not reuse ids 4-9 for
            // vocals (closed note enum, empty-SP pruning against section 9).
            YargVocalStarPower = 0x59410001,
            YargVocalPhrase = 0x59410002,
            YargVocalNote = 0x59410003,
        }

        private enum BTrackVocalNoteKind : uint
        {
            Pitched = 1,
            Unpitched = 2,
            Percussion = 3,
        }

        private enum BTrackNoteType : uint
        {
            Open = 1,
            Green = 2,
            Red = 3,
            Yellow = 4,
            Blue = 5,
            Orange = 6,

            Black1 = 7,
            Black2 = 8,
            Black3 = 9,
            White1 = 10,
            White2 = 11,
            White3 = 12,

            Kick = 13,
            RedDrum = 14,
            YellowDrum = 15,
            BlueDrum = 16,
            GreenDrum = 17,
        }

        [Flags]
        private enum BTrackNoteFlags : uint
        {
            None = 0,
            Strum = 1,
            Hopo = 2,
            Tap = 4,
            DoubleKick = 8,
            Tom = 16,
            Cymbal = 32,
            DiscoNoFlip = 64,
            Disco = 128,
            Flam = 256,
            Ghost = 512,
            Accent = 1024,
        }

        public static bool TryCalculateTrackHash(SongChart chart, Instrument instrument, Difficulty difficulty, out BTrackHashResult result)
        {
            result = default;

            List<Phrase> phrases;
            List<RangeShift> rangeShiftEvents;
            List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> notes;
            switch (instrument)
            {
                case Instrument.FiveFretGuitar:
                case Instrument.FiveFretBass:
                case Instrument.FiveFretRhythm:
                case Instrument.FiveFretCoopGuitar:
                case Instrument.Keys:
                    if (!chart.GetFiveFretTrack(instrument).TryGetDifficulty(difficulty, out var guitarDifficulty))
                    {
                        return false;
                    }
                    phrases = guitarDifficulty.Phrases;
                    rangeShiftEvents = guitarDifficulty.RangeShiftEvents;
                    notes = NormalizeGuitarNotes(guitarDifficulty.Notes, TryMapFiveFretNote);
                    break;

                case Instrument.SixFretGuitar:
                case Instrument.SixFretBass:
                case Instrument.SixFretRhythm:
                case Instrument.SixFretCoopGuitar:
                    if (!chart.GetSixFretTrack(instrument).TryGetDifficulty(difficulty, out var sixFretDifficulty))
                    {
                        return false;
                    }
                    phrases = sixFretDifficulty.Phrases;
                    rangeShiftEvents = sixFretDifficulty.RangeShiftEvents;
                    notes = NormalizeGuitarNotes(sixFretDifficulty.Notes, TryMapSixFretNote);
                    break;

                case Instrument.FourLaneDrums:
                case Instrument.ProDrums:
                case Instrument.FiveLaneDrums:
                    if (!chart.GetDrumsTrack(instrument).TryGetDifficulty(difficulty, out var drumDifficulty))
                    {
                        return false;
                    }
                    phrases = drumDifficulty.Phrases;
                    rangeShiftEvents = drumDifficulty.RangeShiftEvents;
                    notes = NormalizeDrumNotes(instrument, drumDifficulty.Notes);
                    break;

                case Instrument.Vocals:
                case Instrument.Harmony:
                    return TryCalculateVocalTrackHash(chart, instrument, difficulty, out result);

                default:
                    return false;
            }

            result = WriteBTrack(
                chart.SyncTrack,
                ResolveOverlaps(PruneEmptyPhrases(GetPhrases(phrases, PhraseType.StarPower), notes),
                    phrase => phrase.Tick, phrase => phrase.Length, (_, tick, length) => (tick, length)),
                ResolveOverlaps(PruneEmptyPhrases(GetPhrases(phrases, PhraseType.Solo, chart.SoloSectionLengthIncludesTerminalTick), notes),
                    phrase => phrase.Tick, phrase => phrase.Length, (_, tick, length) => (tick, length)),
                PruneEmptyFlexLanes(GetFlexLanes(phrases), notes),
                GetDrumFreestyles(phrases, chart.GlobalEvents),
                GetRangeShifts(rangeShiftEvents),
                notes);
            return true;
        }

        private static bool TryCalculateVocalTrackHash(SongChart chart, Instrument instrument, Difficulty difficulty,
            out BTrackHashResult result)
        {
            result = default;
            if (difficulty != Difficulty.Expert)
            {
                return false;
            }

            var track = chart.GetVocalsTrack(instrument);
            var notes = new List<(long Tick, long Length, BTrackVocalNoteKind Kind, uint Pitch, uint Part)>();
            var phrases = new List<(long Tick, long Length, bool IsPercussion)>();
            var starPower = new List<(long Tick, long Length)>();

            foreach (var part in track.Parts)
            {
                foreach (var phrase in part.NotePhrases)
                {
                    if (phrase.IsStarPower)
                    {
                        starPower.Add(((long) phrase.Tick, (long) phrase.TickLength));
                    }

                    phrases.Add(((long) phrase.Tick, (long) phrase.TickLength, phrase.IsPercussion));
                    foreach (var child in phrase.PhraseParentNote.ChildNotes)
                    {
                        foreach (var sung in child.AllNotes)
                        {
                            if (TryMapVocalNote(sung, out var kind, out var pitch))
                            {
                                notes.Add((sung.Tick, sung.TickLength, kind, pitch, (uint) sung.HarmonyPart));
                            }
                        }
                    }
                }
            }

            notes = NormalizeVocalNotes(notes);
            if (notes.Count == 0)
            {
                return false;
            }

            result = WriteVocalBTrack(
                chart.SyncTrack,
                ResolveOverlaps(
                    PruneEmptyVocalRanges(DedupRanges(starPower), notes),
                    phrase => phrase.Tick, phrase => phrase.Length, (_, tick, length) => (tick, length)),
                ResolveOverlaps(
                    PruneEmptyVocalPhrases(DedupVocalPhrases(phrases), notes),
                    phrase => phrase.Tick, phrase => phrase.Length,
                    (phrase, tick, length) => (tick, length, phrase.IsPercussion)),
                notes);
            return true;
        }

        private static bool TryMapVocalNote(VocalNote note, out BTrackVocalNoteKind kind, out uint pitch)
        {
            pitch = 0;
            if (note.IsPhrase)
            {
                kind = default;
                return false;
            }

            if (note.IsPercussion)
            {
                kind = BTrackVocalNoteKind.Percussion;
                return true;
            }

            if (note.IsNonPitched)
            {
                kind = BTrackVocalNoteKind.Unpitched;
                return true;
            }

            var midi = (int) Math.Round(note.Pitch);
            if (midi < 0 || midi > 127)
            {
                kind = default;
                return false;
            }

            kind = BTrackVocalNoteKind.Pitched;
            pitch = (uint) midi;
            return true;
        }

        private static List<(long Tick, long Length, BTrackVocalNoteKind Kind, uint Pitch, uint Part)> NormalizeVocalNotes(
            List<(long Tick, long Length, BTrackVocalNoteKind Kind, uint Pitch, uint Part)> notes)
        {
            var deduped = notes
                .GroupBy(note => new { note.Tick, note.Kind, note.Pitch, note.Part })
                .Select(group => (
                    Tick: group.Key.Tick,
                    Length: group.Max(note => note.Length),
                    Kind: group.Key.Kind,
                    Pitch: group.Key.Pitch,
                    Part: group.Key.Part))
                .OrderBy(note => note.Tick)
                .ThenBy(note => note.Part)
                .ThenBy(note => note.Kind)
                .ThenBy(note => note.Pitch)
                .ToList();

            return deduped
                .GroupBy(note => new { note.Part, note.Kind, note.Pitch })
                .SelectMany(group => ResolveOverlaps(group.OrderBy(note => note.Tick).ToList(),
                    note => note.Tick, note => note.Length,
                    (note, tick, length) => (tick, length, note.Kind, note.Pitch, note.Part)))
                .OrderBy(note => note.Tick)
                .ThenBy(note => note.Part)
                .ThenBy(note => note.Kind)
                .ThenBy(note => note.Pitch)
                .ToList();
        }

        private static List<(long Tick, long Length, bool IsPercussion)> DedupVocalPhrases(
            List<(long Tick, long Length, bool IsPercussion)> phrases)
        {
            return phrases
                .GroupBy(phrase => phrase.Tick)
                .Select(group => (
                    Tick: group.Key,
                    Length: group.Max(phrase => phrase.Length),
                    IsPercussion: group.Last().IsPercussion))
                .OrderBy(phrase => phrase.Tick)
                .ToList();
        }

        private static List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> NormalizeGuitarNotes(
            List<GuitarNote> notes, TryMapFret tryMap)
        {
            var normalized = new List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)>();
            foreach (var note in notes)
            {
                foreach (var child in note.AllNotes)
                {
                    if (tryMap(child, out var type))
                    {
                        normalized.Add((child.Tick, child.TickLength, type, MapGuitarFlags(child)));
                    }
                }
            }
            return NormalizeNotes(normalized);
        }

        private delegate bool TryMapFret(GuitarNote note, out BTrackNoteType type);

        private static bool TryMapFiveFretNote(GuitarNote note, out BTrackNoteType type)
        {
            type = note.Fret switch
            {
                (int) FiveFretGuitarFret.Open => BTrackNoteType.Open,
                (int) FiveFretGuitarFret.Green => BTrackNoteType.Green,
                (int) FiveFretGuitarFret.Red => BTrackNoteType.Red,
                (int) FiveFretGuitarFret.Yellow => BTrackNoteType.Yellow,
                (int) FiveFretGuitarFret.Blue => BTrackNoteType.Blue,
                (int) FiveFretGuitarFret.Orange => BTrackNoteType.Orange,
                _ => default,
            };
            return type != default;
        }

        private static bool TryMapSixFretNote(GuitarNote note, out BTrackNoteType type)
        {
            type = note.Fret switch
            {
                (int) SixFretGuitarFret.Open => BTrackNoteType.Open,
                (int) SixFretGuitarFret.Black1 => BTrackNoteType.Black1,
                (int) SixFretGuitarFret.Black2 => BTrackNoteType.Black2,
                (int) SixFretGuitarFret.Black3 => BTrackNoteType.Black3,
                (int) SixFretGuitarFret.White1 => BTrackNoteType.White1,
                (int) SixFretGuitarFret.White2 => BTrackNoteType.White2,
                (int) SixFretGuitarFret.White3 => BTrackNoteType.White3,
                _ => default,
            };
            return type != default;
        }

        private static BTrackNoteFlags MapGuitarFlags(GuitarNote note)
        {
            return note.Type switch
            {
                GuitarNoteType.Hopo => BTrackNoteFlags.Hopo,
                GuitarNoteType.Tap => BTrackNoteFlags.Tap,
                _ => BTrackNoteFlags.Strum,
            };
        }

        private static List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> NormalizeDrumNotes(
            Instrument instrument, List<DrumNote> notes)
        {
            var normalized = new List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)>();
            foreach (var note in notes)
            {
                foreach (var child in note.AllNotes)
                {
                    if (TryMapDrumNote(instrument, child, out var type, out var flags))
                    {
                        normalized.Add((child.Tick, child.TickLength, type, flags));
                    }
                }
            }
            return NormalizeNotes(normalized);
        }

        private static bool TryMapDrumNote(Instrument instrument, DrumNote note, out BTrackNoteType type, out BTrackNoteFlags flags)
        {
            flags = BTrackNoteFlags.None;
            if (note.IsDoubleKick)
            {
                flags |= BTrackNoteFlags.DoubleKick;
            }

            if (note.IsGhost)
            {
                flags |= BTrackNoteFlags.Ghost;
            }
            else if (note.IsAccent)
            {
                flags |= BTrackNoteFlags.Accent;
            }

            if (instrument == Instrument.FiveLaneDrums)
            {
                return TryMapFiveLaneDrumNote(note, ref flags, out type);
            }

            return TryMapFourLaneDrumNote(note, ref flags, out type);
        }

        private static bool TryMapFourLaneDrumNote(DrumNote note, ref BTrackNoteFlags flags, out BTrackNoteType type)
        {
            switch ((FourLaneDrumPad) note.Pad)
            {
                case FourLaneDrumPad.Kick:
                    type = BTrackNoteType.Kick;
                    return true;
                case FourLaneDrumPad.RedDrum:
                    flags |= BTrackNoteFlags.Tom;
                    type = BTrackNoteType.RedDrum;
                    return true;
                case FourLaneDrumPad.YellowDrum:
                    flags |= BTrackNoteFlags.Tom;
                    type = BTrackNoteType.YellowDrum;
                    return true;
                case FourLaneDrumPad.BlueDrum:
                    flags |= BTrackNoteFlags.Tom;
                    type = BTrackNoteType.BlueDrum;
                    return true;
                case FourLaneDrumPad.GreenDrum:
                    flags |= BTrackNoteFlags.Tom;
                    type = BTrackNoteType.GreenDrum;
                    return true;
                case FourLaneDrumPad.YellowCymbal:
                    flags |= BTrackNoteFlags.Cymbal;
                    type = BTrackNoteType.YellowDrum;
                    return true;
                case FourLaneDrumPad.BlueCymbal:
                    flags |= BTrackNoteFlags.Cymbal;
                    type = BTrackNoteType.BlueDrum;
                    return true;
                case FourLaneDrumPad.GreenCymbal:
                    flags |= BTrackNoteFlags.Cymbal;
                    type = BTrackNoteType.GreenDrum;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }

        private static bool TryMapFiveLaneDrumNote(DrumNote note, ref BTrackNoteFlags flags, out BTrackNoteType type)
        {
            switch ((FiveLaneDrumPad) note.Pad)
            {
                case FiveLaneDrumPad.Kick:
                    type = BTrackNoteType.Kick;
                    return true;
                case FiveLaneDrumPad.Red:
                    flags |= BTrackNoteFlags.Tom;
                    type = BTrackNoteType.RedDrum;
                    return true;
                case FiveLaneDrumPad.Yellow:
                    flags |= BTrackNoteFlags.Cymbal;
                    type = BTrackNoteType.YellowDrum;
                    return true;
                case FiveLaneDrumPad.Blue:
                    flags |= BTrackNoteFlags.Tom;
                    type = BTrackNoteType.BlueDrum;
                    return true;
                case FiveLaneDrumPad.Orange:
                    flags |= BTrackNoteFlags.Cymbal;
                    type = BTrackNoteType.GreenDrum;
                    return true;
                case FiveLaneDrumPad.Green:
                    flags |= BTrackNoteFlags.Tom;
                    type = BTrackNoteType.GreenDrum;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }

        private static List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> NormalizeNotes(
            List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> notes)
        {
            var deduped = notes
                .GroupBy(note => new { note.Tick, note.Type })
                .Select(group => (
                    Tick: group.Key.Tick,
                    Length: group.Max(note => note.Length),
                    Type: group.Key.Type,
                    Flags: NormalizeFlags(CombineFlags(group.Select(note => note.Flags)))))
                .OrderBy(note => note.Tick)
                .ThenBy(note => note.Type)
                .ToList();

            return deduped
                .GroupBy(note => note.Type)
                .SelectMany(group => ResolveOverlaps(group.OrderBy(note => note.Tick).ToList(),
                    note => note.Tick, note => note.Length,
                    (note, tick, length) => (tick, length, note.Type, note.Flags)))
                .OrderBy(note => note.Tick)
                .ThenBy(note => note.Type)
                .ToList();
        }

        private static BTrackNoteFlags CombineFlags(IEnumerable<BTrackNoteFlags> flags)
        {
            return flags.Aggregate(BTrackNoteFlags.None, (current, flag) => current | flag);
        }

        private static BTrackNoteFlags NormalizeFlags(BTrackNoteFlags flags)
        {
            flags = NormalizeFlagGroup(flags, BTrackNoteFlags.Strum, BTrackNoteFlags.Hopo, BTrackNoteFlags.Tap);
            flags = NormalizeFlagGroup(flags, BTrackNoteFlags.DoubleKick, BTrackNoteFlags.Tom, BTrackNoteFlags.Cymbal);
            flags = NormalizeFlagGroup(flags, BTrackNoteFlags.DiscoNoFlip, BTrackNoteFlags.Disco);
            flags = NormalizeFlagGroup(flags, BTrackNoteFlags.Ghost, BTrackNoteFlags.Accent);
            return flags;
        }

        private static BTrackNoteFlags NormalizeFlagGroup(BTrackNoteFlags flags, params BTrackNoteFlags[] group)
        {
            var selected = BTrackNoteFlags.None;
            foreach (var flag in group)
            {
                if ((flags & flag) != 0)
                {
                    flags &= ~flag;
                    selected = flag;
                }
            }
            return flags | selected;
        }

        private static List<(long Tick, long Length)> GetPhrases(List<Phrase> phrases, PhraseType type, bool includeTerminalTick = false)
        {
            var extraTick = type == PhraseType.Solo && includeTerminalTick ? 1 : 0;
            return phrases
                .Where(phrase => phrase.Type == type)
                .GroupBy(phrase => phrase.Tick)
                .Select(group => (Tick: (long) group.Key, Length: (long) group.Max(phrase => phrase.TickLength) + extraTick))
                .OrderBy(phrase => phrase.Tick)
                .ToList();
        }

        private static List<(long Tick, long Length, bool IsDouble)> GetFlexLanes(List<Phrase> phrases)
        {
            return phrases
                .Where(phrase => phrase.Type is PhraseType.TremoloLane or PhraseType.TrillLane)
                .GroupBy(phrase => new { phrase.Tick, IsDouble = phrase.Type == PhraseType.TrillLane })
                .Select(group => (Tick: (long) group.Key.Tick, Length: (long) group.Max(phrase => phrase.TickLength), IsDouble: group.Key.IsDouble))
                .OrderBy(lane => lane.Tick)
                .ThenBy(lane => lane.IsDouble)
                .ToList();
        }

        private static List<(long Tick, long Length, bool IsCoda)> GetDrumFreestyles(List<Phrase> phrases, List<TextEvent> globalEvents)
        {
            var codaTick = FirstCodaTick(globalEvents);
            return phrases
                .Where(phrase => phrase.IsDrumFreestyle)
                .Select(phrase => (
                    Tick: (long) phrase.Tick,
                    Length: (long) phrase.TickLength,
                    IsCoda: codaTick is uint start && phrase.Tick >= start))
                .OrderBy(phrase => phrase.Tick)
                .ToList();
        }

        private static uint? FirstCodaTick(List<TextEvent> globalEvents)
        {
            uint? codaTick = null;
            foreach (var textEvent in globalEvents)
            {
                if (!IsCodaEvent(textEvent.Text))
                {
                    continue;
                }

                if (codaTick is null || textEvent.Tick < codaTick)
                {
                    codaTick = textEvent.Tick;
                }
            }

            return codaTick;
        }

        private static List<(long Tick, long Length)> DedupRanges(List<(long Tick, long Length)> ranges)
        {
            return ranges
                .GroupBy(range => range.Tick)
                .Select(group => (Tick: group.Key, Length: group.Max(range => range.Length)))
                .OrderBy(range => range.Tick)
                .ToList();
        }

        private static bool IsCodaEvent(string text)
        {
            var trimmed = text.Trim();
            return trimmed.Equals("coda", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("[coda]", StringComparison.OrdinalIgnoreCase);
        }

        private static List<(long Tick, long Position, long Size)> GetRangeShifts(List<RangeShift> rangeShifts)
        {
            return GetLastPerTick(rangeShifts, rangeShift => rangeShift.Tick)
                .Select(rangeShift => (Tick: (long) rangeShift.Tick, Position: (long) rangeShift.Range, Size: (long) rangeShift.Size))
                .ToList();
        }

        private static List<(long Tick, long Length)> PruneEmptyPhrases(
            List<(long Tick, long Length)> phrases,
            List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> notes)
        {
            return phrases
                .Where(phrase => notes.Any(note => note.Tick >= phrase.Tick && note.Tick < phrase.Tick + Math.Max(phrase.Length, 1)))
                .ToList();
        }

        private static List<(long Tick, long Length, bool IsDouble)> PruneEmptyFlexLanes(
            List<(long Tick, long Length, bool IsDouble)> lanes,
            List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> notes)
        {
            return lanes
                .Where(lane => notes.Any(note => note.Tick >= lane.Tick && note.Tick <= lane.Tick + lane.Length))
                .ToList();
        }

        private static List<(long Tick, long Length)> PruneEmptyVocalRanges(
            List<(long Tick, long Length)> ranges,
            List<(long Tick, long Length, BTrackVocalNoteKind Kind, uint Pitch, uint Part)> notes)
        {
            return ranges
                .Where(range => notes.Any(note => NoteInExclusiveRange(note.Tick, range.Tick, range.Length)))
                .ToList();
        }

        private static List<(long Tick, long Length, bool IsPercussion)> PruneEmptyVocalPhrases(
            List<(long Tick, long Length, bool IsPercussion)> phrases,
            List<(long Tick, long Length, BTrackVocalNoteKind Kind, uint Pitch, uint Part)> notes)
        {
            return phrases
                .Where(phrase => notes.Any(note => NoteInExclusiveRange(note.Tick, phrase.Tick, phrase.Length)))
                .ToList();
        }

        private static bool NoteInExclusiveRange(long noteTick, long rangeTick, long rangeLength)
        {
            return noteTick >= rangeTick && noteTick < rangeTick + Math.Max(rangeLength, 1);
        }

        private static List<T> ResolveOverlaps<T>(
            List<T> items,
            Func<T, long> tick,
            Func<T, long> length,
            Func<T, long, long, T> withRange)
        {
            var resolved = items.ToList();
            for (var i = 0; i < resolved.Count - 1; i++)
            {
                var currentTick = tick(resolved[i]);
                var nextTick = tick(resolved[i + 1]);
                if (currentTick >= nextTick)
                {
                    continue;
                }

                var currentEnd = currentTick + length(resolved[i]);
                if (currentEnd <= nextTick)
                {
                    continue;
                }

                var nextEnd = Math.Max(currentEnd, nextTick + length(resolved[i + 1]));
                resolved[i] = withRange(resolved[i], currentTick, nextTick - currentTick);
                resolved[i + 1] = withRange(resolved[i + 1], nextTick, nextEnd - nextTick);
            }
            return resolved;
        }

        private static BTrackHashResult WriteBTrack(
            SyncTrack syncTrack,
            List<(long Tick, long Length)> starPower,
            List<(long Tick, long Length)> soloSections,
            List<(long Tick, long Length, bool IsDouble)> flexLanes,
            List<(long Tick, long Length, bool IsCoda)> drumFreestyles,
            List<(long Tick, long Position, long Size)> rangeShifts,
            List<(long Tick, long Length, BTrackNoteType Type, BTrackNoteFlags Flags)> notes)
        {
            var sections = CreateTimingSections(syncTrack);
            AddListSection(sections, BTrackSectionId.StarPower, starPower,
                (writer, phrase) =>
                {
                    writer.Write(phrase.Tick);
                    writer.Write(phrase.Length);
                });
            AddListSection(sections, BTrackSectionId.SoloSection, soloSections,
                (writer, phrase) =>
                {
                    writer.Write(phrase.Tick);
                    writer.Write(phrase.Length);
                });
            AddListSection(sections, BTrackSectionId.FlexLane, flexLanes,
                (writer, lane) =>
                {
                    writer.Write(lane.Tick);
                    writer.Write(lane.Length);
                    writer.Write((byte) (lane.IsDouble ? 1 : 0));
                });
            AddListSection(sections, BTrackSectionId.DrumFreestyle, drumFreestyles,
                (writer, phrase) =>
                {
                    writer.Write(phrase.Tick);
                    writer.Write(phrase.Length);
                    writer.Write((byte) (phrase.IsCoda ? 1 : 0));
                });
            AddListSection(sections, BTrackSectionId.RangeShift, rangeShifts,
                (writer, rangeShift) =>
                {
                    writer.Write(rangeShift.Tick);
                    writer.Write(rangeShift.Position);
                    writer.Write(rangeShift.Size);
                });
            AddListSection(sections, BTrackSectionId.Note, notes,
                (writer, note) =>
                {
                    writer.Write(note.Tick);
                    writer.Write(note.Length);
                    writer.Write((uint) note.Type);
                    writer.Write((uint) note.Flags);
                });

            return new BTrackHashResult(WriteFileBytes(sections), WriteHashInputBytes(sections));
        }

        private static BTrackHashResult WriteVocalBTrack(
            SyncTrack syncTrack,
            List<(long Tick, long Length)> starPower,
            List<(long Tick, long Length, bool IsPercussion)> phrases,
            List<(long Tick, long Length, BTrackVocalNoteKind Kind, uint Pitch, uint Part)> notes)
        {
            var sections = CreateTimingSections(syncTrack);
            AddListSection(sections, BTrackSectionId.YargVocalStarPower, starPower,
                (writer, phrase) =>
                {
                    writer.Write(phrase.Tick);
                    writer.Write(phrase.Length);
                });
            AddListSection(sections, BTrackSectionId.YargVocalPhrase, phrases,
                (writer, phrase) =>
                {
                    writer.Write(phrase.Tick);
                    writer.Write(phrase.Length);
                    writer.Write((byte) (phrase.IsPercussion ? 1 : 0));
                });
            AddListSection(sections, BTrackSectionId.YargVocalNote, notes,
                (writer, note) =>
                {
                    writer.Write(note.Tick);
                    writer.Write(note.Length);
                    writer.Write((uint) note.Kind);
                    writer.Write(note.Pitch);
                    writer.Write(note.Part);
                });

            return new BTrackHashResult(WriteFileBytes(sections), WriteHashInputBytes(sections));
        }

        private static List<(ulong Id, byte[] Payload)> CreateTimingSections(SyncTrack syncTrack)
        {
            var resolution = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(resolution, syncTrack.Resolution);
            var sections = new List<(ulong Id, byte[] Payload)>
            {
                ((ulong) BTrackSectionId.Resolution, resolution),
            };

            AddListSection(sections, BTrackSectionId.TempoMarker, GetLastPerTick(syncTrack.Tempos, tempo => tempo.Tick),
                (writer, tempo) =>
                {
                    writer.Write((long) tempo.Tick);
                    writer.Write(tempo.BeatsPerMinute);
                });
            AddListSection(sections, BTrackSectionId.TimeSignature,
                GetLastPerTick(syncTrack.TimeSignatures, timeSignature => timeSignature.Tick),
                (writer, timeSignature) =>
                {
                    writer.Write((long) timeSignature.Tick);
                    writer.Write(timeSignature.Numerator);
                    writer.Write(timeSignature.Denominator);
                });
            return sections;
        }

        private static void AddListSection<T>(List<(ulong Id, byte[] Payload)> sections, BTrackSectionId id, List<T> items,
            Action<BinaryWriter, T> writeItem)
        {
            if (items.Count == 0)
            {
                return;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((uint) items.Count);
            foreach (var item in items)
            {
                writeItem(writer, item);
            }

            sections.Add(((ulong) id, stream.ToArray()));
        }

        private static byte[] WriteFileBytes(List<(ulong Id, byte[] Payload)> sections)
        {
            const int headerSize = 8;
            var mapSize = 4 + sections.Count * 20;
            var offset = (ulong) (headerSize + mapSize);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var formatHeader = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(formatHeader, FileFormatHeader);
            writer.Write(formatHeader);
            writer.Write(FileFormatVersion);
            writer.Write((uint) sections.Count);
            foreach (var section in sections)
            {
                writer.Write(section.Id);
                writer.Write(offset);
                writer.Write((uint) section.Payload.Length);
                offset += (ulong) section.Payload.Length;
            }

            foreach (var section in sections)
            {
                writer.Write(section.Payload);
            }

            return stream.ToArray();
        }

        private static byte[] WriteHashInputBytes(List<(ulong Id, byte[] Payload)> sections)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((uint) sections.Count);
            foreach (var section in sections)
            {
                writer.Write(section.Id);
            }

            foreach (var section in sections)
            {
                writer.Write(section.Payload);
            }

            return stream.ToArray();
        }

        private static List<T> GetLastPerTick<T>(List<T> events, Func<T, uint> getTick)
        {
            return events
                .GroupBy(getTick)
                .Select(group => group.Last())
                .OrderBy(getTick)
                .ToList();
        }
    }
}
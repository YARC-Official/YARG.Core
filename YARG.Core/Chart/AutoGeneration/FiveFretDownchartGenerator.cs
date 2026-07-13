using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Core.Chart.AutoGeneration
{
    public static class FiveFretDownchartGenerator
    {
        private const double MIN_CAPABILITY = 0.8;
        private const double CAPABILITY_PRECISION = 0.005;
        private const double HAND_INDEPENDENCE = 2;
        private const double EPSILON = 0.0000001;
        private const double ROCK_METER_SIZE = 42;
        private static readonly Instrument[] FiveFretInstruments = GameMode.FiveFretGuitar.PossibleInstruments();

        public static InstrumentDifficulty<GuitarNote> Generate(
            InstrumentDifficulty<GuitarNote> source,
            Difficulty targetDifficulty,
            SyncTrack syncTrack,
            double intensity = 1.0)
        {
            ValidateArguments(source, targetDifficulty, intensity);

            var sourceChords = CreateSourceChords(source, syncTrack.Resolution);
            return GenerateDifficulty(source, targetDifficulty, syncTrack, intensity, sourceChords,
                FindMinimumPassingIntensity(sourceChords));
        }

        public static IReadOnlyDictionary<Difficulty, InstrumentDifficulty<GuitarNote>> GenerateAll(
            InstrumentDifficulty<GuitarNote> source,
            SyncTrack syncTrack,
            double intensity = 1.0)
        {
            ValidateArguments(source, Difficulty.Hard, intensity);

            var sourceChords = CreateSourceChords(source, syncTrack.Resolution);
            double minimumPassingIntensity = FindMinimumPassingIntensity(sourceChords);
            var generated = new Dictionary<Difficulty, InstrumentDifficulty<GuitarNote>>();
            foreach (var target in new[] { Difficulty.Hard, Difficulty.Medium, Difficulty.Easy })
            {
                generated.Add(target, GenerateDifficulty(source, target, syncTrack, intensity, sourceChords,
                    minimumPassingIntensity));
            }

            return generated;
        }

        private static InstrumentDifficulty<GuitarNote> GenerateDifficulty(
            InstrumentDifficulty<GuitarNote> source,
            Difficulty targetDifficulty,
            SyncTrack syncTrack,
            double intensity,
            List<DownchartChord> sourceChords,
            double minimumPassingIntensity)
        {
            var chords = sourceChords.Select(chord => chord.Clone()).ToList();

            switch (targetDifficulty)
            {
                case Difficulty.Hard:
                    ReduceChords(chords, Difficulty.Expert);
                    ReduceChart(chords, Math.Max(1, minimumPassingIntensity * intensity));
                    break;
                case Difficulty.Medium:
                    ReduceChords(chords, Difficulty.Expert);
                    ReduceChords(chords, Difficulty.Hard);
                    ReduceRange(chords, Difficulty.Medium, false);
                    ReduceChart(chords, Math.Max(1, minimumPassingIntensity * intensity * 0.7));
                    break;
                case Difficulty.Easy:
                    ReduceChords(chords, Difficulty.Expert);
                    ReduceChords(chords, Difficulty.Hard);
                    ReduceRange(chords, Difficulty.Medium, true);
                    ReduceChords(chords, Difficulty.Medium);
                    ReduceRange(chords, Difficulty.Easy, false);
                    ReduceChart(chords, Math.Max(1, minimumPassingIntensity * intensity * 0.4));
                    break;
            }

            var phrases = source.Phrases
                .Where(phrase => phrase.Type is not PhraseType.TremoloLane and not PhraseType.TrillLane)
                .Select(phrase => phrase.Clone())
                .ToList();
            var textEvents = source.TextEvents.Select(text => text.Clone()).ToList();
            var notes = BuildNotes(chords, syncTrack);

            ApplyPhraseFlags(notes, phrases);

            return new InstrumentDifficulty<GuitarNote>(
                source.Instrument, targetDifficulty, notes, phrases, textEvents, true);
        }

        public static int GenerateMissing(
            InstrumentTrack<GuitarNote> track,
            SyncTrack syncTrack,
            double intensity = 1.0)
        {
            ValidateInstrument(track.Instrument);
            ValidateIntensity(intensity);

            if (!track.TryGetDifficulty(Difficulty.Expert, out var expert) || expert.Notes.Count == 0)
            {
                return 0;
            }

            Difficulty[] targets = { Difficulty.Hard, Difficulty.Medium, Difficulty.Easy };
            var missingTargets = targets.Where(target =>
                !track.TryGetDifficulty(target, out var existing) || existing.Notes.Count == 0).ToList();
            if (missingTargets.Count == 0)
            {
                return 0;
            }

            var generatedDifficulties = GenerateAll(expert, syncTrack, intensity);
            foreach (var target in missingTargets)
            {
                track.TryGetDifficulty(target, out var existing);
                if (existing != null)
                {
                    track.RemoveDifficulty(target);
                }

                var generatedDifficulty = generatedDifficulties[target];
                PreserveAuthoredMetadata(existing, generatedDifficulty);
                track.AddDifficulty(target, generatedDifficulty);
            }

            return missingTargets.Count;
        }

        private static List<DownchartChord> CreateSourceChords(
            InstrumentDifficulty<GuitarNote> source,
            uint resolution)
        {
            return source.Notes.Select(note => DownchartChord.FromNote(note, resolution)).ToList();
        }

        private static void PreserveAuthoredMetadata(
            InstrumentDifficulty<GuitarNote>? existing,
            InstrumentDifficulty<GuitarNote> generated)
        {
            if (existing == null || existing.IsGenerated)
            {
                return;
            }

            if (existing.Phrases.Count > 0)
            {
                generated.Phrases.Clear();
                generated.Phrases.AddRange(existing.Phrases.Select(phrase => phrase.Clone()));
                ApplyPhraseFlags(generated.Notes, generated.Phrases);
            }

            if (existing.TextEvents.Count > 0)
            {
                generated.TextEvents.Clear();
                generated.TextEvents.AddRange(existing.TextEvents.Select(text => text.Clone()));
            }
        }

        private static void ValidateArguments(
            InstrumentDifficulty<GuitarNote> source,
            Difficulty targetDifficulty,
            double intensity)
        {
            ValidateInstrument(source.Instrument);
            ValidateIntensity(intensity);

            if (source.Difficulty != Difficulty.Expert)
            {
                throw new ArgumentException("The source difficulty must be Expert.", nameof(source));
            }

            if (targetDifficulty is not Difficulty.Easy and not Difficulty.Medium and not Difficulty.Hard)
            {
                throw new ArgumentOutOfRangeException(nameof(targetDifficulty),
                    "The target difficulty must be Easy, Medium, or Hard.");
            }
        }

        private static void ValidateInstrument(Instrument instrument)
        {
            if (!FiveFretInstruments.Contains(instrument))
            {
                throw new ArgumentException($"Instrument {instrument} is not a five-fret instrument.", nameof(instrument));
            }
        }

        private static void ValidateIntensity(double intensity)
        {
            if (double.IsNaN(intensity) || double.IsInfinity(intensity) || intensity < 0 || intensity > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be between 0 and 1.");
            }
        }

        private static double FindMinimumPassingIntensity(List<DownchartChord> chords)
        {
            if (chords.Count < 3)
            {
                return MIN_CAPABILITY;
            }

            var intensities = new List<double>(chords.Count - 2);
            for (int i = 1; i < chords.Count - 1; i++)
            {
                intensities.Add(GetGapIntensity(chords[i], chords[i - 1]));
            }

            double left = MIN_CAPABILITY;
            double right = intensities.Max();
            while (right - left > CAPABILITY_PRECISION)
            {
                double capability = (left + right) / 2;
                if (SimulateRun(intensities, capability))
                {
                    right = capability;
                }
                else
                {
                    left = capability;
                }
            }

            return right;
        }

        private static bool SimulateRun(List<double> intensities, double capability)
        {
            double meter = ROCK_METER_SIZE * 5 / 6;
            foreach (double intensity in intensities)
            {
                double hitProbability = 1 / Math.Max(intensity / capability, 1);
                double expectedChange = hitProbability * 0.25 + (1 - hitProbability) * -2;
                meter = Math.Min(ROCK_METER_SIZE, meter + expectedChange);
                if (meter < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static double GetGapIntensity(DownchartChord chord, DownchartChord previous)
        {
            double elapsed = chord.Time - previous.Time;
            if (elapsed <= 0)
            {
                return 1;
            }

            double leftHandVelocity = 1 / elapsed;
            double rightHandVelocity = leftHandVelocity;
            int previousFrets = previous.Shape >> 1;
            int currentFrets = chord.Shape >> 1;
            int lifts = CountFrets(previousFrets & ~currentFrets);
            int presses = CountFrets(currentFrets & ~previousFrets);
            double leftHandActions = 0.5 * (Math.Max(1, lifts) + presses);
            int rightHandActions = IsStrum(chord) || chord.Shape == previous.Shape ? 1 : 0;

            double leftIntensity = Math.Max(leftHandVelocity * leftHandActions, EPSILON);
            double rightIntensity = Math.Max(rightHandVelocity * rightHandActions, EPSILON);
            return Math.Max(1, Math.Pow(
                Math.Pow(leftIntensity, HAND_INDEPENDENCE) +
                Math.Pow(rightIntensity, HAND_INDEPENDENCE),
                1 / HAND_INDEPENDENCE));
        }

        private static bool IsStrum(DownchartChord chord)
        {
            return chord.Type == GuitarNoteType.Strum;
        }

        private static void ReduceChart(List<DownchartChord> chords, double intensityThreshold)
        {
            int index = 1;
            while (index < chords.Count - 1)
            {
                var current = chords[index];
                var previous = chords[index - 1];
                if (GetGapIntensity(current, previous) <= intensityThreshold)
                {
                    index++;
                    continue;
                }

                bool joinChords = false;
                if (!IsStrum(current) && current.Type != GuitarNoteType.Tap)
                {
                    joinChords = index <= 1 || IsStrum(previous);
                    if (current.TimeLength <= 0.2)
                    {
                        joinChords = false;
                    }
                }

                if (joinChords)
                {
                    previous.TickLength = current.TickEnd - previous.Tick;
                    previous.TimeLength = current.TimeEnd - previous.Time;
                    chords.RemoveAt(index);
                }
                else if (current.GetScore() > previous.GetScore())
                {
                    chords.RemoveAt(index - 1);
                    if (index > 1 && index - 1 < chords.Count &&
                        chords[index - 1].Type == GuitarNoteType.Hopo &&
                        chords[index - 1].Tick - chords[index - 2].Tick >
                        17 * chords[index - 1].Resolution / 24)
                    {
                        chords[index - 1].Type = GuitarNoteType.Strum;
                    }
                }
                else
                {
                    chords.RemoveAt(index);
                }
            }
        }

        private static void ReduceChords(List<DownchartChord> chords, Difficulty difficulty)
        {
            foreach (var chord in chords)
            {
                chord.Shape = GetReducedShape(chord.Shape, difficulty);
            }
        }

        private static int GetReducedShape(int shape, Difficulty difficulty)
        {
            return (difficulty, shape) switch
            {
                (Difficulty.Expert, 0b_00111_0) => 0b_00011_0,
                (Difficulty.Expert, 0b_01011_0) => 0b_00101_0,
                (Difficulty.Expert, 0b_01101_0) => 0b_00110_0,
                (Difficulty.Expert, 0b_01110_0) => 0b_01010_0,
                (Difficulty.Expert, 0b_10011_0) => 0b_01001_0,
                (Difficulty.Expert, 0b_10101_0) => 0b_01010_0,
                (Difficulty.Expert, 0b_10110_0) => 0b_01100_0,
                (Difficulty.Expert, 0b_11001_0) => 0b_10010_0,
                (Difficulty.Expert, 0b_11010_0) => 0b_10100_0,
                (Difficulty.Expert, 0b_11100_0) => 0b_11000_0,
                (Difficulty.Hard, 0b_01001_0) => 0b_00110_0,
                (Difficulty.Hard, 0b_10010_0) => 0b_01100_0,
                (Difficulty.Medium, 0b_00011_0) => 0b_00001_0,
                (Difficulty.Medium, 0b_00101_0) => 0b_00010_0,
                (Difficulty.Medium, 0b_00110_0) => 0b_00100_0,
                (Difficulty.Medium, 0b_01010_0) => 0b_01000_0,
                (Difficulty.Medium, 0b_01100_0) => 0b_10000_0,
                _ => shape,
            };
        }

        private static void ReduceRange(List<DownchartChord> chords, Difficulty difficulty, bool chordsOnly)
        {
            int leftMask;
            int rightMask;
            int shift;
            if (difficulty == Difficulty.Medium)
            {
                leftMask = 0b_00001_0;
                rightMask = 0b_10000_0;
                shift = 1;
            }
            else
            {
                leftMask = 0b_00011_0;
                rightMask = 0b_11000_0;
                shift = 2;
            }

            bool shifted = false;
            foreach (var chord in chords)
            {
                if ((leftMask & chord.Shape) != 0)
                {
                    shifted = false;
                }
                else if ((rightMask & chord.Shape) != 0)
                {
                    shifted = true;
                }

                if (shifted && chord.Shape != 0b_00000_1 && (!chordsOnly || !IsSingleNote(chord.Shape)))
                {
                    chord.Shape >>= shift;
                }
            }
        }

        private static List<GuitarNote> BuildNotes(List<DownchartChord> chords, SyncTrack syncTrack)
        {
            var notes = new List<GuitarNote>(chords.Count);
            GuitarNote? previous = null;
            foreach (var chord in chords)
            {
                var frets = GetFrets(chord.Shape);
                if (frets.Count == 0)
                {
                    continue;
                }

                var time = syncTrack.TickToTime(chord.Tick);
                var timeEnd = syncTrack.TickToTime(chord.TickEnd);
                var flags = chord.Flags & ~(NoteFlags.StarPower | NoteFlags.StarPowerStart |
                    NoteFlags.StarPowerEnd | NoteFlags.Solo | NoteFlags.SoloStart | NoteFlags.SoloEnd |
                    NoteFlags.Tremolo | NoteFlags.Trill | NoteFlags.LaneStart | NoteFlags.LaneEnd |
                    NoteFlags.BigRockEnding | NoteFlags.CodaStart | NoteFlags.CodaEnd);

                var parent = new GuitarNote(frets[0], chord.Type, chord.GuitarFlags, flags,
                    time, timeEnd - time, chord.Tick, chord.TickLength);
                for (int i = 1; i < frets.Count; i++)
                {
                    parent.AddChildNote(new GuitarNote(frets[i], chord.Type, chord.GuitarFlags, flags,
                        time, timeEnd - time, chord.Tick, chord.TickLength));
                }

                parent.PreviousNote = previous;
                foreach (var child in parent.ChildNotes)
                {
                    child.PreviousNote = previous;
                }

                if (previous != null)
                {
                    previous.NextNote = parent;
                    foreach (var child in previous.ChildNotes)
                    {
                        child.NextNote = parent;
                    }
                }

                notes.Add(parent);
                previous = parent;
            }

            return notes;
        }

        private static void ApplyPhraseFlags(List<GuitarNote> notes, List<Phrase> phrases)
        {
            const NoteFlags phraseFlags = NoteFlags.StarPower | NoteFlags.StarPowerStart |
                NoteFlags.StarPowerEnd | NoteFlags.Solo | NoteFlags.SoloStart | NoteFlags.SoloEnd |
                NoteFlags.BigRockEnding | NoteFlags.CodaStart | NoteFlags.CodaEnd;
            foreach (var note in notes)
            {
                note.Flags &= ~phraseFlags;
                foreach (var child in note.ChildNotes)
                {
                    child.Flags &= ~phraseFlags;
                }
            }

            foreach (var phrase in phrases)
            {
                NoteFlags activeFlag;
                NoteFlags startFlag;
                NoteFlags endFlag;
                bool inclusiveEnd;
                switch (phrase.Type)
                {
                    case PhraseType.StarPower:
                        activeFlag = NoteFlags.StarPower;
                        startFlag = NoteFlags.StarPowerStart;
                        endFlag = NoteFlags.StarPowerEnd;
                        inclusiveEnd = false;
                        break;
                    case PhraseType.Solo:
                        activeFlag = NoteFlags.Solo;
                        startFlag = NoteFlags.SoloStart;
                        endFlag = NoteFlags.SoloEnd;
                        inclusiveEnd = true;
                        break;
                    case PhraseType.BigRockEnding:
                        activeFlag = NoteFlags.BigRockEnding;
                        startFlag = NoteFlags.None;
                        endFlag = NoteFlags.None;
                        inclusiveEnd = true;
                        break;
                    case PhraseType.Coda:
                        activeFlag = NoteFlags.None;
                        startFlag = NoteFlags.CodaStart;
                        endFlag = NoteFlags.CodaEnd;
                        inclusiveEnd = true;
                        break;
                    default:
                        continue;
                }

                var phraseNotes = notes.Where(note =>
                    note.Tick >= phrase.Tick &&
                    (inclusiveEnd ? note.Tick <= phrase.TickEnd : note.Tick < phrase.TickEnd)).ToList();
                if (phraseNotes.Count == 0)
                {
                    continue;
                }

                foreach (var note in phraseNotes)
                {
                    ActivateFlagOnChord(note, activeFlag);
                }

                ActivateFlagOnChord(phraseNotes[0], startFlag);
                ActivateFlagOnChord(phraseNotes[^1], endFlag);
            }
        }

        private static void ActivateFlagOnChord(GuitarNote note, NoteFlags flag)
        {
            note.Flags |= flag;
            foreach (var child in note.ChildNotes)
            {
                child.Flags |= flag;
            }
        }

        private static List<FiveFretGuitarFret> GetFrets(int shape)
        {
            var frets = new List<FiveFretGuitarFret>(5);
            if ((shape & 1) != 0)
            {
                frets.Add(FiveFretGuitarFret.Open);
            }

            for (int fret = 1; fret <= 5; fret++)
            {
                if ((shape & (1 << fret)) != 0)
                {
                    frets.Add((FiveFretGuitarFret) fret);
                }
            }

            return frets;
        }

        private static bool IsSingleNote(int shape)
        {
            return shape > 0 && (shape & (shape - 1)) == 0;
        }

        private static int CountFrets(int shape)
        {
            int count = 0;
            while (shape != 0)
            {
                shape &= shape - 1;
                count++;
            }

            return count;
        }

        private sealed class DownchartChord
        {
            public double Time;
            public double TimeLength;
            public uint Tick;
            public uint TickLength;
            public uint Resolution;
            public int Shape;
            public GuitarNoteType Type;
            public GuitarNoteFlags GuitarFlags;
            public NoteFlags Flags;

            public double TimeEnd => Time + TimeLength;
            public uint TickEnd => Tick + TickLength;

            public static DownchartChord FromNote(GuitarNote note, uint resolution)
            {
                int shape = 0;
                uint tickEnd = note.TickEnd;
                double timeEnd = note.TimeEnd;
                var guitarFlags = GuitarNoteFlags.None;
                foreach (var gem in note.AllNotes)
                {
                    shape |= gem.Fret == (int) FiveFretGuitarFret.Open ? 1 : 1 << gem.Fret;
                    tickEnd = Math.Max(tickEnd, gem.TickEnd);
                    timeEnd = Math.Max(timeEnd, gem.TimeEnd);
                    guitarFlags |= gem.GuitarFlags;
                }

                return new DownchartChord
                {
                    Time = note.Time,
                    TimeLength = timeEnd - note.Time,
                    Tick = note.Tick,
                    TickLength = tickEnd - note.Tick,
                    Resolution = resolution,
                    Shape = shape,
                    Type = note.Type,
                    GuitarFlags = guitarFlags,
                    Flags = note.Flags,
                };
            }

            public double GetScore()
            {
                double score = 0;
                if (TimeLength >= 0.2)
                {
                    score += 100 * Math.Pow((double) TickLength / Resolution, 2);
                }

                if (Shape != 1)
                {
                    score += 100 + CountFrets(Shape) * 100;
                }

                score += 100.0 * GreatestCommonDivisor(Tick, Resolution) / Resolution;
                return score;
            }

            public DownchartChord Clone()
            {
                return new DownchartChord
                {
                    Time = Time,
                    TimeLength = TimeLength,
                    Tick = Tick,
                    TickLength = TickLength,
                    Resolution = Resolution,
                    Shape = Shape,
                    Type = Type,
                    GuitarFlags = GuitarFlags,
                    Flags = Flags,
                };
            }

            private static uint GreatestCommonDivisor(uint left, uint right)
            {
                while (right != 0)
                {
                    (left, right) = (right, left % right);
                }

                return left;
            }
        }
    }
}
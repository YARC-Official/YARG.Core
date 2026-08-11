using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Chart.AutoGeneration
{
    public sealed class MidiDownchartExportOptions
    {
        public double Intensity { get; set; } = 1.2;
        public bool ReplaceExisting { get; set; }
        public IReadOnlyCollection<Instrument>? Instruments { get; set; }
    }

    public sealed class MidiDownchartExportResult
    {
        public MidiFile Midi { get; }
        public int GeneratedDifficultyCount { get; }
        public int SkippedDifficultyCount { get; }

        internal MidiDownchartExportResult(MidiFile midi, int generatedDifficultyCount, int skippedDifficultyCount)
        {
            Midi = midi;
            GeneratedDifficultyCount = generatedDifficultyCount;
            SkippedDifficultyCount = skippedDifficultyCount;
        }
    }

    public static class MidiDownchartExporter
    {
        private static readonly Instrument[] AllInstruments = GameMode.FiveFretGuitar.PossibleInstruments();

        private static readonly Dictionary<string, Instrument> TrackInstruments = new()
        {
            { MidIOHelper.GUITAR_TRACK, Instrument.FiveFretGuitar },
            { MidIOHelper.GH1_GUITAR_TRACK, Instrument.FiveFretGuitar },
            { MidIOHelper.GUITAR_COOP_TRACK, Instrument.FiveFretCoopGuitar },
            { MidIOHelper.BASS_TRACK, Instrument.FiveFretBass },
            { MidIOHelper.RHYTHM_TRACK, Instrument.FiveFretRhythm },
            { MidIOHelper.KEYS_TRACK, Instrument.Keys },
        };

        public static MidiDownchartExportResult Generate(
            MidiFile source,
            MidiDownchartExportOptions? options = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            options ??= new MidiDownchartExportOptions();
            ValidateOptions(options);

            var selectedInstruments = new HashSet<Instrument>(options.Instruments ?? AllInstruments);
            var settings = ParseSettings.Default_Midi;
            var chart = SongChart.FromMidi(settings, source);
            var output = source.Clone();

            int generatedCount = 0;
            int skippedCount = 0;
            for (int chunkIndex = 0; chunkIndex < output.Chunks.Count; chunkIndex++)
            {
                if (output.Chunks[chunkIndex] is not TrackChunk track ||
                    !TrackInstruments.TryGetValue(track.GetTrackName(), out var instrument) ||
                    !selectedInstruments.Contains(instrument))
                {
                    continue;
                }

                var instrumentTrack = chart.GetFiveFretTrack(instrument);
                if (!instrumentTrack.TryGetDifficulty(Difficulty.Expert, out var expert) ||
                    expert.Notes.Count == 0)
                {
                    skippedCount += 3;
                    continue;
                }

                var targets = new List<Difficulty>();
                foreach (var target in new[] { Difficulty.Hard, Difficulty.Medium, Difficulty.Easy })
                {
                    if (!options.ReplaceExisting &&
                        instrumentTrack.TryGetDifficulty(target, out var existing) &&
                        existing.Notes.Count > 0 &&
                        !existing.IsGenerated)
                    {
                        skippedCount++;
                        continue;
                    }

                    targets.Add(target);
                }

                if (targets.Count > 0)
                {
                    var allGenerated = FiveFretDownchartGenerator.GenerateAll(
                        expert, chart.SyncTrack, options.Intensity);
                    var generated = targets.ToDictionary(target => target, target => allGenerated[target]);
                    generatedCount += generated.Count;
                    output.Chunks[chunkIndex] = RewriteTrack(track, generated);
                }
            }

            return new MidiDownchartExportResult(output, generatedCount, skippedCount);
        }

        private static void ValidateOptions(MidiDownchartExportOptions options)
        {
            if (double.IsNaN(options.Intensity) ||
                double.IsInfinity(options.Intensity) ||
                options.Intensity < 0 || options.Intensity > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Intensity), "Intensity must be between 0 and 2.");
            }

            if (options.Instruments == null)
            {
                return;
            }

            foreach (var instrument in options.Instruments)
            {
                if (!AllInstruments.Contains(instrument))
                {
                    throw new ArgumentException($"Instrument {instrument} is not a supported five-fret instrument.",
                        nameof(options.Instruments));
                }
            }
        }

        private static TrackChunk RewriteTrack(
            TrackChunk source,
            Dictionary<Difficulty, InstrumentDifficulty<GuitarNote>> generated)
        {
            var events = new List<TimedEvent>();
            long absoluteTick = 0;
            int sequence = 0;
            foreach (var midiEvent in source.Events)
            {
                absoluteTick += midiEvent.DeltaTime;
                if (ShouldRemove(midiEvent, generated.Keys))
                {
                    continue;
                }

                var clone = midiEvent.Clone();
                clone.DeltaTime = 0;
                events.Add(new TimedEvent(absoluteTick, clone, sequence++));
            }

            foreach (var (difficulty, chart) in generated)
            {
                AddDifficultyEvents(events, chart, difficulty, ref sequence);
            }

            events.Sort(CompareEvents);
            long previousTick = 0;
            foreach (var timedEvent in events)
            {
                timedEvent.Event.DeltaTime = timedEvent.Tick - previousTick;
                previousTick = timedEvent.Tick;
            }

            var result = new TrackChunk();
            result.Events.AddRange(events.Select(timed => timed.Event));
            return result;
        }

        private static bool ShouldRemove(MidiEvent midiEvent, IEnumerable<Difficulty> generated)
        {
            int noteNumber = midiEvent switch
            {
                NoteOnEvent noteOn => noteOn.NoteNumber,
                NoteOffEvent noteOff => noteOff.NoteNumber,
                _ => -1,
            };

            foreach (var difficulty in generated)
            {
                int baseNote = GetBaseNote(difficulty);
                if (noteNumber >= baseNote - 1 && noteNumber <= baseNote + 6)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddDifficultyEvents(
            List<TimedEvent> events,
            InstrumentDifficulty<GuitarNote> chart,
            Difficulty difficulty,
            ref int sequence)
        {
            int baseNote = GetBaseNote(difficulty);
            int hopoMarker = baseNote + 5;
            int strumMarker = baseNote + 6;

            foreach (var guitarNote in chart.Notes)
            {
                uint markerLength = Math.Max(guitarNote.TickLength, 1);
                foreach (var note in guitarNote.AllNotes)
                {
                    int noteNumber = note.Fret == (int) FiveFretGuitarFret.Open
                        ? baseNote - 1
                        : baseNote + note.Fret - 1;
                    AddNote(events, note.Tick, Math.Max(note.TickLength, 1), noteNumber, ref sequence);
                }

                if (guitarNote.Type == GuitarNoteType.Hopo)
                {
                    AddNote(events, guitarNote.Tick, markerLength, hopoMarker, ref sequence);
                }
                else if (guitarNote.Type == GuitarNoteType.Strum)
                {
                    AddNote(events, guitarNote.Tick, markerLength, strumMarker, ref sequence);
                }
            }
        }

        private static void AddNote(
            List<TimedEvent> events,
            uint tick,
            uint length,
            int noteNumber,
            ref int sequence)
        {
            events.Add(new TimedEvent(tick, new NoteOnEvent
            {
                NoteNumber = (SevenBitNumber) noteNumber,
                Velocity = (SevenBitNumber) MidIOHelper.VELOCITY,
            }, sequence++));
            events.Add(new TimedEvent(tick + length, new NoteOffEvent
            {
                NoteNumber = (SevenBitNumber) noteNumber,
                Velocity = (SevenBitNumber) 0,
            }, sequence++));
        }

        private static int GetBaseNote(Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy => 60,
                Difficulty.Medium => 72,
                Difficulty.Hard => 84,
                _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
            };
        }

        private static int CompareEvents(TimedEvent left, TimedEvent right)
        {
            int comparison = left.Tick.CompareTo(right.Tick);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = EventPriority(left.Event).CompareTo(EventPriority(right.Event));
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = GetNoteNumber(left.Event).CompareTo(GetNoteNumber(right.Event));
            return comparison != 0 ? comparison : left.Sequence.CompareTo(right.Sequence);
        }

        private static int EventPriority(MidiEvent midiEvent)
        {
            return midiEvent switch
            {
                NoteOffEvent => 0,
                NoteOnEvent noteOn when noteOn.Velocity == (SevenBitNumber) 0 => 0,
                NoteOnEvent => 2,
                _ => 1,
            };
        }

        private static int GetNoteNumber(MidiEvent midiEvent)
        {
            return midiEvent switch
            {
                NoteOnEvent noteOn => noteOn.NoteNumber,
                NoteOffEvent noteOff => noteOff.NoteNumber,
                _ => -1,
            };
        }

        private sealed class TimedEvent
        {
            public long Tick { get; }
            public MidiEvent Event { get; }
            public int Sequence { get; }

            public TimedEvent(long tick, MidiEvent midiEvent, int sequence)
            {
                Tick = tick;
                Event = midiEvent;
                Sequence = sequence;
            }
        }
    }
}

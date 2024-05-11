using System;
using System.Collections.Generic;
using YARG.Core.Logging;

namespace YARG.Core.Chart.Parsing
{
    internal static class GuitarHandler
    {
        public static void FinishTrack(SongChart chart, in ParseSettings settings, GameMode gameMode,
            InstrumentDifficulty<GuitarNote> track, List<IntermediateGuitarNote> intermediateNotes)
        {
            switch (gameMode)
            {
                case GameMode.FiveFretGuitar:
                    FinishTrack(chart, settings, track, intermediateNotes, GetFiveFretNoteFret);
                    break;
                case GameMode.SixFretGuitar:
                    FinishTrack(chart, settings, track, intermediateNotes, GetSixFretNoteFret);
                    break;
                default:
                    throw new ArgumentException($"Game mode {gameMode} is not a guitar mode!");
            }
            
        }

        private static void FinishTrack(SongChart chart, in ParseSettings settings,
            InstrumentDifficulty<GuitarNote> track, List<IntermediateGuitarNote> intermediateNotes,
            Func<IntermediateGuitarNote, int> getFret)
        {
            YargLogger.Assert(track.Notes.Count == 0);

            float hopoThreshold = settings.GetHopoThreshold(chart.Resolution);

            int phraseIndex = 0;
            var currentPhrases = new Dictionary<PhraseType, Phrase>();
            var tempoTracker = new ChartEventTickTracker<TempoChange>(chart.SyncTrack.Tempos);

            for (int index = 0; index < intermediateNotes.Count; index++)
            {
                var intermediate = intermediateNotes[index];

                tempoTracker.Update(intermediate.Tick);
                TrackHandler.UpdatePhrases(currentPhrases, track.Phrases, ref phraseIndex, intermediate.Tick);

                double startTime = chart.SyncTrack.TickToTime(intermediate.Tick, tempoTracker.Current);
                double endTime = chart.SyncTrack.TickToTime(intermediate.Tick + intermediate.TickLength, tempoTracker.Current);

                int fret = getFret(intermediate);
                var noteType = GetNoteType(track, intermediate, hopoThreshold);
                var guitarFlags = GetNoteFlags(intermediateNotes, index, settings);
                var generalFlags = TrackHandler.GetGeneralFlags(intermediateNotes, index, currentPhrases);

                TrackHandler.AddNote(track.Notes, new(fret, noteType, guitarFlags, generalFlags,
                    startTime, endTime - startTime, intermediate.Tick, intermediate.TickLength), settings);
            }
        }

        private static int GetFiveFretNoteFret(IntermediateGuitarNote note)
        {
            var fret = note.Fret switch
            {
                IntermediateGuitarFret.Open => FiveFretGuitarFret.Open,
                IntermediateGuitarFret.Fret1 => FiveFretGuitarFret.Green,
                IntermediateGuitarFret.Fret2 => FiveFretGuitarFret.Red,
                IntermediateGuitarFret.Fret3 => FiveFretGuitarFret.Yellow,
                IntermediateGuitarFret.Fret4 => FiveFretGuitarFret.Blue,
                IntermediateGuitarFret.Fret5 => FiveFretGuitarFret.Orange,
                IntermediateGuitarFret.Fret6 => throw new InvalidOperationException("6th fret is not valid for 5-fret!"),

                _ => throw new InvalidOperationException($"Invalid intermediate guitar fret {note.Fret}!")
            };
            return (int) fret;
        }

        private static int GetSixFretNoteFret(IntermediateGuitarNote note)
        {
            var fret = note.Fret switch
            {
                IntermediateGuitarFret.Open => SixFretGuitarFret.Open,
                IntermediateGuitarFret.Fret1 => SixFretGuitarFret.Black1,
                IntermediateGuitarFret.Fret2 => SixFretGuitarFret.Black2,
                IntermediateGuitarFret.Fret3 => SixFretGuitarFret.Black3,
                IntermediateGuitarFret.Fret4 => SixFretGuitarFret.White1,
                IntermediateGuitarFret.Fret5 => SixFretGuitarFret.White2,
                IntermediateGuitarFret.Fret6 => SixFretGuitarFret.White3,

                _ => throw new InvalidOperationException($"Invalid intermediate guitar fret {note.Fret}!")
            };
            return (int) fret;
        }

        private static GuitarNoteType GetNoteType(InstrumentDifficulty<GuitarNote> track,
            IntermediateGuitarNote note, float hopoThreshold)
        {
            var noteType = GuitarNoteType.Strum;

            // Tap notes take priority
            if ((note.Flags & IntermediateGuitarFlags.Tap) != 0)
            {
                noteType = GuitarNoteType.Tap;
            }
            // HOPOs take priority after that, to match Rock Band
            else if ((note.Flags & IntermediateGuitarFlags.ForceHopo) != 0)
            {
                noteType = GuitarNoteType.Tap;
            }
            // Then forced strum
            else if ((note.Flags & IntermediateGuitarFlags.ForceStrum) != 0)
            {
                noteType = GuitarNoteType.Strum;
            }
            // Then force flip/natural HOPOs
            else if (track.Notes.Count > 0)
            {
                bool isHopo = (note.Flags & IntermediateGuitarFlags.ForceFlip) != 0;
                var previousNote = track.Notes[^1];
                if (!previousNote.IsChord && (previousNote.Tick - note.Tick) <= hopoThreshold)
                    isHopo = !isHopo;

                if (isHopo)
                    noteType = GuitarNoteType.Hopo;
            }

            return noteType;
        }

        private static GuitarNoteFlags GetNoteFlags(List<IntermediateGuitarNote> intermediateNotes, int index,
            in ParseSettings settings)
        {
            var flags = GuitarNoteFlags.None;

            var current = intermediateNotes[index];

            // Extended sustains
            var nextNote = TrackHandler.GetNextSeparateEvent(intermediateNotes, index);
            if (nextNote is not null && (current.Tick + current.TickLength) > nextNote.Tick &&
                (nextNote.Tick - current.Tick) > settings.NoteSnapThreshold)
            {
                flags |= GuitarNoteFlags.ExtendedSustain;
            }

            // Disjoint chords
            var (start, end) = TrackHandler.GetEventChord(intermediateNotes, index);
            for (int i = start; i < end; i++)
            {
                var note = intermediateNotes[i];
                if (note.TickLength != current.TickLength)
                {
                    flags |= GuitarNoteFlags.Disjoint;
                    break;
                }
            }

            return flags;
        }
    }
}

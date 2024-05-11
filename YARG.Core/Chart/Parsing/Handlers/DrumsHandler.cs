using System.Collections.Generic;
using YARG.Core.Logging;

namespace YARG.Core.Chart.Parsing
{
    internal static partial class DrumsHandler
    {
        private delegate int GetDrumsPad(List<IntermediateDrumsNote> intermediateNotes, int index, in ParseSettings settings);

        public static void FinishTrack(SongChart chart, ref ParseSettings settings, Difficulty difficulty,
            InstrumentDifficulty<DrumNote> fourLane,
            InstrumentDifficulty<DrumNote> fourPro,
            InstrumentDifficulty<DrumNote> fiveLane,
            List<IntermediateDrumsNote> intermediateNotes)
        {
            YargLogger.Assert(fourLane.Notes.Count == 0);
            YargLogger.Assert(fourPro.Notes.Count == 0);
            YargLogger.Assert(fiveLane.Notes.Count == 0);

            // Fallback path if drums type hasn't been determined yet
            // This path should never be hit from the game itself, this is primarily
            // to make loading charts in tests and such not error out
            if (settings.DrumsType == DrumsType.Unknown)
            {
                YargLogger.LogDebug("Chart drums type unknown, doing manual calculation");
                settings.DrumsType = CalculateDrumsType(intermediateNotes);
            }

            FinalizeTrack(chart, settings, difficulty, fourLane, intermediateNotes, GetFourLaneDrumPad);
            FinalizeTrack(chart, settings, difficulty, fourPro, intermediateNotes, GetFourLaneProDrumPad);
            FinalizeTrack(chart, settings, difficulty, fiveLane, intermediateNotes, GetFiveLaneDrumPad);
        }

        private static DrumsType CalculateDrumsType(List<IntermediateDrumsNote> intermediateNotes)
        {
            foreach (var intermediate in intermediateNotes)
            {
                if (intermediate.Pad == IntermediateDrumPad.Lane5)
                    return DrumsType.FiveLane;

                if ((intermediate.Flags & IntermediateDrumsNoteFlags.Cymbal) != 0)
                    return DrumsType.FourLane;
            }

            return DrumsType.FourLane;
        }

        private static void FinalizeTrack(SongChart chart, in ParseSettings settings, Difficulty difficulty,
            InstrumentDifficulty<DrumNote> track, List<IntermediateDrumsNote> intermediateNotes,
            GetDrumsPad getNotePad)
        {
            int phraseIndex = 0;
            var currentPhrases = new Dictionary<PhraseType, Phrase>();
            var tempoTracker = new ChartEventTickTracker<TempoChange>(chart.SyncTrack.Tempos);

            for (int index = 0; index < intermediateNotes.Count; index++)
            {
                var intermediate = intermediateNotes[index];

                if ((intermediate.Flags & IntermediateDrumsNoteFlags.ExpertPlus) != 0 &&
                    difficulty != Difficulty.ExpertPlus)
                    continue;

                tempoTracker.Update(intermediate.Tick);
                TrackHandler.UpdatePhrases(currentPhrases, track.Phrases, ref phraseIndex, intermediate.Tick);

                double startTime = chart.SyncTrack.TickToTime(intermediate.Tick, tempoTracker.Current);

                int pad = getNotePad(intermediateNotes, index, settings);
                var noteType = GetNoteType(intermediate);
                var drumFlags = GetNoteFlags(chart, intermediateNotes, index, currentPhrases);
                var generalFlags = TrackHandler.GetGeneralFlags(intermediateNotes, index, currentPhrases);

                TrackHandler.AddNote(track.Notes, new(pad, noteType, drumFlags, generalFlags,
                    startTime, intermediate.Tick), settings);
            }
        }

        private static DrumNoteType GetNoteType(IntermediateDrumsNote note)
        {
            var noteType = DrumNoteType.Neutral;

            // Accents/ghosts
            if ((note.Flags & IntermediateDrumsNoteFlags.Accent) != 0)
                noteType = DrumNoteType.Accent;
            else if ((note.Flags & IntermediateDrumsNoteFlags.Ghost) != 0)
                noteType = DrumNoteType.Ghost;

            return noteType;
        }

        private static DrumNoteFlags GetNoteFlags(SongChart chart,
            List<IntermediateDrumsNote> intermediateNotes, int index,
            Dictionary<PhraseType, Phrase> currentPhrases)
        {
            var flags = DrumNoteFlags.None;

            // SP activation
            if (currentPhrases.TryGetValue(PhraseType.DrumFill, out var activationPhrase) &&
                TrackHandler.IsClosestToEnd(chart, intermediateNotes, index, activationPhrase))
            {
                flags |= DrumNoteFlags.StarPowerActivator;
            }

            return flags;
        }
    }
}
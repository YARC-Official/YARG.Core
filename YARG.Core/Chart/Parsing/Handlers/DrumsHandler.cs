using System;
using System.Collections.Generic;
using YARG.Core.Logging;

namespace YARG.Core.Chart.Parsing
{
    internal static partial class DrumsHandler
    {
        private delegate int GetDrumsPad(List<IntermediateDrumsNote> intermediateNotes, int index, in ParseSettings settings);

        public static void FinishTrack(SongChart chart, in ParseSettings settings, Difficulty difficulty,
            ChartEventTickTracker<TempoChange> tempoTracker,
            InstrumentDifficulty<DrumNote> fourLane,
            InstrumentDifficulty<DrumNote> fourPro,
            InstrumentDifficulty<DrumNote> fiveLane,
            List<IntermediateDrumsNote> intermediateNotes)
        {
            YargLogger.Assert(fourLane.Notes.Count == 0);
            YargLogger.Assert(fourPro.Notes.Count == 0);
            YargLogger.Assert(fiveLane.Notes.Count == 0);
            YargLogger.Assert(tempoTracker.CurrentIndex == 0);

            FinalizeTrack(chart, settings, difficulty, tempoTracker, fourLane, intermediateNotes, GetFourLaneDrumPad);
            FinalizeTrack(chart, settings, difficulty, tempoTracker, fourPro, intermediateNotes, GetFourLaneProDrumPad);
            FinalizeTrack(chart, settings, difficulty, tempoTracker, fiveLane, intermediateNotes, GetFiveLaneDrumPad);
        }

        private static void FinalizeTrack(SongChart chart, in ParseSettings settings, Difficulty difficulty,
            ChartEventTickTracker<TempoChange> tempoTracker,
            InstrumentDifficulty<DrumNote> track, List<IntermediateDrumsNote> intermediateNotes,
            GetDrumsPad getNotePad)
        {
            int phraseIndex = 0;
            var currentPhrases = new Dictionary<PhraseType, Phrase>();

            for (int index = 0; index < intermediateNotes.Count; index++)
            {
                var intermediate = intermediateNotes[index];

                if (intermediate.Pad == IntermediateDrumPad.KickPlus && difficulty != Difficulty.ExpertPlus)
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
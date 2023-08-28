using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Drums.Engines
{
    public class YargDrumsEngine : DrumsEngine
    {
        public YargDrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack, DrumsEngineParameters engineParameters) : base(chart, syncTrack, engineParameters)
        {
        }

        protected override bool UpdateEngineState(double time)
        {
            UpdateTimeVariables(time);

            DepleteStarPower(GetUsedStarPower());

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count) return false;

            return CheckForNoteMiss();
        }

        protected override bool CheckForNoteMiss()
        {
            var note = Notes[State.NoteIndex];

            if (State.CurrentTime > note.Time + EngineParameters.BackEnd)
            {
                foreach (var chordNote in note.ChordEnumerator())
                {
                    if (chordNote.WasHit || chordNote.WasMissed)
                    {
                        continue;
                    }

                    MissNote(chordNote);
                }
            }

            return false;
        }

        protected override bool UpdateEngineInput(double time)
        {
            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count) return false;

            bool isNoteHit = CheckForNoteHit();

            // Check for over hits
            if (IsNoteInput(CurrentInput) && CurrentInput.Button)
            {
                if (!isNoteHit)
                {
                    Overhit();
                }

                OnPadHit?.Invoke(CurrentInput.GetAction<DrumsAction>(), isNoteHit);
            }

            return isNoteHit;
        }

        protected override bool CheckForNoteHit()
        {
            var note = Notes[State.NoteIndex];

            if (State.CurrentTime < note.Time + EngineParameters.FrontEnd)
            {
                return false;
            }

            // Remember that while playing drums, all notes of a chord don't have to be hit.
            // Treat all notes separately.
            foreach (var chordNote in note.ChordEnumerator())
            {
                if (chordNote.WasHit || chordNote.WasMissed)
                {
                    continue;
                }

                if (CanNoteBeHit(chordNote))
                {
                    HitNote(chordNote);
                    return true;
                }
            }

            return false;
        }

        protected override bool CanNoteBeHit(DrumNote note)
        {
            if (!CurrentInput.Button)
            {
                return false;
            }

            int padFromAction = CurrentInput.GetAction<DrumsAction>() switch
            {
                DrumsAction.Kick => (int) FourLaneDrumPad.Kick,

                DrumsAction.Drum1 => (int) FourLaneDrumPad.RedDrum,
                DrumsAction.Drum2 => (int) FourLaneDrumPad.YellowDrum,
                DrumsAction.Drum3 => (int) FourLaneDrumPad.BlueDrum,
                DrumsAction.Drum4 => (int) FourLaneDrumPad.GreenDrum,

                DrumsAction.Cymbal1 => (int) FourLaneDrumPad.YellowCymbal,
                DrumsAction.Cymbal2 => (int) FourLaneDrumPad.BlueCymbal,
                DrumsAction.Cymbal3 => (int) FourLaneDrumPad.GreenCymbal,

                _ => -1
            };

            if (note.Pad == padFromAction)
            {
                return true;
            }

            return false;
        }
    }
}
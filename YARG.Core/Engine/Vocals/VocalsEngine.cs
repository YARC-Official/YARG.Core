using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Vocals
{
    public abstract class VocalsEngine :
        BaseEngine<VocalNote, VocalsAction, VocalsEngineParameters, VocalsStats, VocalsEngineState>
    {
        public delegate void HittingStateAction(bool isHitting);

        public HittingStateAction? OnHittingStateChanged;

        public override bool TreatChordAsSeparate => false;

        public VocalsEngine(InstrumentDifficulty<VocalNote> chart, SyncTrack syncTrack, VocalsEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
        }

        protected override bool HitNote(VocalNote note)
        {
            note.SetHitState(true, false);

            if (note.IsStarPower && note.IsStarPowerEnd)
            {
                AwardStarPower(note);
                EngineStats.PhrasesHit++;
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (State.IsSoloActive)
            {
                Solos[State.CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd)
            {
                EndSolo();
            }

            EngineStats.Combo++;

            if (EngineStats.Combo > EngineStats.MaxCombo)
            {
                EngineStats.MaxCombo = EngineStats.Combo;
            }

            EngineStats.NotesHit++;

            UpdateMultiplier();

            AddScore(note);

            State.NoteIndex++;

            return true;
        }

        protected override void MissNote(VocalNote note)
        {
            throw new System.NotImplementedException();
        }

        protected override void AddScore(VocalNote note)
        {
            throw new System.NotImplementedException();
        }

        protected override void UpdateMultiplier()
        {
            throw new System.NotImplementedException();
        }

        protected override int CalculateBaseScore() => throw new System.NotImplementedException();
    }
}
using YARG.Core.Chart;

namespace YARG.Core.Engine.Vocals
{
    public abstract class VocalsEngine :
        BaseEngine<VocalNote, VocalsEngineParameters, VocalsStats, VocalsEngineState>
    {
        public delegate void TargetNoteChangeEvent(VocalNote targetNote);

        public delegate void PhraseHitEvent(double hitPercentAfterParams, bool fullPoints);

        public TargetNoteChangeEvent? OnTargetNoteChanged;

        public PhraseHitEvent? OnPhraseHit;

        protected VocalsEngine(InstrumentDifficulty<VocalNote> chart, SyncTrack syncTrack, VocalsEngineParameters engineParameters, bool isChordSeparate) : base(chart, syncTrack, engineParameters, isChordSeparate)
        {
        }
    }
}
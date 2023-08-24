using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Drums
{
    public abstract class DrumsEngine : BaseEngine<DrumNote, DrumsAction, DrumsEngineParameters,
        DrumsStats, DrumsEngineState>
    {
        protected DrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack,
            DrumsEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
        }
    }
}
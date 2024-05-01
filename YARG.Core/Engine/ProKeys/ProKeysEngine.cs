using YARG.Core.Chart;

namespace YARG.Core.Engine.ProKeys
{
    public abstract class ProKeysEngine : BaseEngine<GuitarNote, ProKeysEngineParameters,
        ProKeysStats, ProKeysEngineState>
    {
        protected ProKeysEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, false, isBot)
        {
        }
    }
}
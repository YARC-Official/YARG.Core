using YARG.Core.Chart;

namespace YARG.Core.Engine.ProGuitar.Engines
{
    public class YargProGuitarEngine : ProGuitarEngine
    {
        public YargProGuitarEngine(InstrumentDifficulty<ProGuitarNote> chart, SyncTrack syncTrack,
            ProGuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
        }
    }
}
using YARG.Core.Chart;

namespace YARG.Core.Engine
{
    public class SyncTrackChange : SyncEvent
    {
        public TempoChange Tempo;

        public TimeSignatureChange TimeSignature;

        public SyncTrackChange(TempoChange tempo, TimeSignatureChange timeSig, double time, uint tick) : base(time, tick)
        {
            Tempo = tempo;
            TimeSignature = timeSig;
        }
    }
}
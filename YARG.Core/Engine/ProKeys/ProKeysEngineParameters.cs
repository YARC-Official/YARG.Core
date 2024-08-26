using YARG.Core.Replays.Serialization;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysEngineParameters : BaseEngineParameters
    {
        public readonly double ChordStaggerWindow;

        public readonly double FatFingerWindow;

        internal ProKeysEngineParameters(SerializedProKeysEngineParameters proKeysParams,
            SerializedBaseEngineParameters baseParams) : base(baseParams)
        {
            ChordStaggerWindow = proKeysParams.ChordStaggerWindow;
            FatFingerWindow = proKeysParams.FatFingerWindow;
        }

        public ProKeysEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double chordStaggerWindow,
            double fatFingerWindow)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
            ChordStaggerWindow = chordStaggerWindow;
            FatFingerWindow = fatFingerWindow;
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Chord stagger window: {ChordStaggerWindow}\n" +
                $"Fat finger window: {FatFingerWindow}";
        }
    }
}
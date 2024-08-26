namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysEngineParameters : BaseEngineParameters
    {
        public double ChordStaggerWindow;

        public double FatFingerWindow;

        public ProKeysEngineParameters()
        {
        }

        public ProKeysEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double chordStaggerWindow, double fatFingerWindow)
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
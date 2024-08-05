using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysEngineState : BaseEngineState
    {
        // Used for hit logic. May not be the same value as KeyHeldMask
        public int KeyMask;

        public double[] KeyPressTimes = new double[(int)ProKeysAction.Key25 + 1];

        /// <summary>
        /// The integer value for the key that was hit this update. <c>null</c> is none.
        /// </summary>
        public int? KeyHit;
        /// <summary>
        /// The integer value for the key that was released this update. <c>null</c> is none.
        /// </summary>
        public int? KeyReleased;

        public int? FatFingerKey;

        public EngineTimer ChordStaggerTimer;
        public EngineTimer FatFingerTimer;

        public ProKeysNote? FatFingerNote;

        public void Initialize(ProKeysEngineParameters parameters)
        {
            ChordStaggerTimer = new(parameters.ChordStaggerWindow);
            FatFingerTimer = new(parameters.FatFingerWindow);

            KeyPressTimes = new double[(int)ProKeysAction.Key25 + 1];
            for(int i = 0; i < KeyPressTimes.Length; i++)
            {
                KeyPressTimes[i] = -9999;
            }
        }

        public override void Reset()
        {
            base.Reset();

            KeyMask = 0;

            for(int i = 0; i < KeyPressTimes.Length; i++)
            {
                KeyPressTimes[i] = -9999;
            }

            KeyHit = null;
            KeyReleased = null;

            FatFingerKey = null;

            ChordStaggerTimer.Disable();
            FatFingerTimer.Disable();

            FatFingerNote = null;
        }
    }
}
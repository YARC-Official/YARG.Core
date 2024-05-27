using YARG.Core.Chart;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysEngineState : BaseEngineState
    {
        public int KeyMask;

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
        }

        public override void Reset()
        {
            base.Reset();

            KeyMask = 0;

            KeyHit = null;
            KeyReleased = null;

            FatFingerKey = null;

            ChordStaggerTimer.Disable();
            FatFingerTimer.Disable();

            FatFingerNote = null;
        }
    }
}
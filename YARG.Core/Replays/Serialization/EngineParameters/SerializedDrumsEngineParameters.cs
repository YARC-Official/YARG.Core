using YARG.Core.Engine.Drums;

namespace YARG.Core.Replays.Serialization
{
    internal class SerializedDrumsEngineParameters
    {
        public DrumsEngineParameters.DrumMode Mode;

        public float VelocityThreshold;

        public float SituationalVelocityWindow;
    }
}
using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters : IBinarySerializable
    {
        // Don't use this value in logic, use the FrontEnd and BackEnd (-1/2 and +1/2 values respectively)
        public double HitWindow        { get; }
        public double FrontToBackRatio { get; }

        /// <summary>
        /// How much time ahead of the strikeline can a note be hit. This value is always negative.
        /// </summary>
        public double FrontEnd { get; private set; }

        /// <summary>
        /// How much time behind the strikeline can a note be hit. This value is always positive.
        /// </summary>
        public double BackEnd { get; private set; }

        public float[] StarMultiplierThresholds { get; private set; }

        protected BaseEngineParameters()
        {
            StarMultiplierThresholds = Array.Empty<float>();
        }

        protected BaseEngineParameters(double hitWindow, double frontBackRatio, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            FrontToBackRatio = frontBackRatio;
            StarMultiplierThresholds = starMultiplierThresholds;

            FrontEnd = -(Math.Abs(HitWindow / 2) * FrontToBackRatio);
            BackEnd = Math.Abs(HitWindow / 2) * (2 - FrontToBackRatio);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(FrontEnd);
            writer.Write(BackEnd);
        }

        public virtual void Deserialize(BinaryReader reader, int version = 0)
        {
            FrontEnd = reader.ReadDouble();
            BackEnd = reader.ReadDouble();
        }
    }
}
using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters : IBinarySerializable
    {
        public HitWindowSettings HitWindow { get; private set;}

        public float[] StarMultiplierThresholds { get; private set; }

        protected BaseEngineParameters()
        {
            StarMultiplierThresholds = Array.Empty<float>();
        }

        protected BaseEngineParameters(HitWindowSettings hitWindow, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            StarMultiplierThresholds = starMultiplierThresholds;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            HitWindow.Serialize(writer);

            // Write star multiplier thresholds
            writer.Write(StarMultiplierThresholds.Length);
            foreach (var f in StarMultiplierThresholds)
            {
                writer.Write(f);
            }
        }

        public virtual void Deserialize(BinaryReader reader, int version = 0)
        {
            // Since "HitWindow" is a property and returns
            // a "temporary value," we gotta do this.
            var hitWindow = new HitWindowSettings();
            hitWindow.Deserialize(reader, version);
            HitWindow = hitWindow;

            // Read star multiplier thresholds
            StarMultiplierThresholds = new float[reader.ReadInt32()];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarMultiplierThresholds[i] = reader.ReadSingle();
            }
        }
    }
}
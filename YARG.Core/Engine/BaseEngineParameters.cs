using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters : IBinarySerializable
    {
        public HitWindowSettings HitWindow { get; private set; }

        public int MaxMultiplier { get; private set; }

        public float[] StarMultiplierThresholds { get; private set; }

        protected BaseEngineParameters()
        {
            StarMultiplierThresholds = Array.Empty<float>();
        }

        protected BaseEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            MaxMultiplier = maxMultiplier;
            StarMultiplierThresholds = starMultiplierThresholds;
        }

        public void SetHitWindowScale(double scale)
        {
            // Since "HitWindow" is a property and returns
            // a "temporary value," we gotta do this.
            var hitWindow = HitWindow;
            hitWindow.Scale = scale;
            HitWindow = hitWindow;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            HitWindow.Serialize(writer);

            writer.Write(MaxMultiplier);

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

            MaxMultiplier = reader.ReadInt32();

            // Read star multiplier thresholds
            StarMultiplierThresholds = new float[reader.ReadInt32()];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarMultiplierThresholds[i] = reader.ReadSingle();
            }
        }
    }
}
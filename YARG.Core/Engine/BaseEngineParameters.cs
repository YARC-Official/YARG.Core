using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters : IBinarySerializable
    {
        public readonly HitWindowSettings HitWindow;

        public int MaxMultiplier { get; private set; }

        public float[] StarMultiplierThresholds { get; private set; }

        public double SongSpeed;

        protected BaseEngineParameters()
        {
            HitWindow = new HitWindowSettings();
            StarMultiplierThresholds = Array.Empty<float>();
        }

        protected BaseEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            MaxMultiplier = maxMultiplier;
            StarMultiplierThresholds = starMultiplierThresholds;
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

            writer.Write(SongSpeed);
        }

        public virtual void Deserialize(BinaryReader reader, int version = 0)
        {
            var hitWindow = new HitWindowSettings();
            hitWindow.Deserialize(reader, version);

            MaxMultiplier = reader.ReadInt32();

            // Read star multiplier thresholds
            StarMultiplierThresholds = new float[reader.ReadInt32()];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarMultiplierThresholds[i] = reader.ReadSingle();
            }

            SongSpeed = reader.ReadDouble();
        }
    }
}
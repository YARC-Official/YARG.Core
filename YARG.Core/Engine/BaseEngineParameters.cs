using System;
using System.Globalization;
using System.IO;
using System.Linq;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseEngineParameters : IBinarySerializable
    {
        public HitWindowSettings HitWindow;

        public int MaxMultiplier;

        public float[] StarMultiplierThresholds;

        public double StarPowerWhammyBuffer;
        public double SongSpeed;

        protected BaseEngineParameters()
        {
            HitWindow = new HitWindowSettings();
            StarMultiplierThresholds = Array.Empty<float>();
        }

        protected BaseEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer, float[] starMultiplierThresholds)
        {
            HitWindow = hitWindow;
            StarPowerWhammyBuffer = spWhammyBuffer;
            MaxMultiplier = maxMultiplier;
            StarMultiplierThresholds = starMultiplierThresholds;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            HitWindow.Serialize(writer);

            writer.Write(MaxMultiplier);

            writer.Write(StarPowerWhammyBuffer);

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
            HitWindow.Deserialize(reader, version);

            MaxMultiplier = reader.ReadInt32();

            StarPowerWhammyBuffer = reader.ReadDouble();

            // Read star multiplier thresholds
            StarMultiplierThresholds = new float[reader.ReadInt32()];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarMultiplierThresholds[i] = reader.ReadSingle();
            }

            if (version >= 5)
            {
                SongSpeed = reader.ReadDouble();
            }
        }

        public override string ToString()
        {
            var thresholds = string.Join(", ",
                StarMultiplierThresholds.Select(i => i.ToString(CultureInfo.InvariantCulture)));

            return
                $"Hit window: ({HitWindow.MinWindow}, {HitWindow.MaxWindow})\n" +
                $"Hit window dynamic: {HitWindow.IsDynamic}\n" +
                $"Max multiplier: {MaxMultiplier}\n" +
                $"Star thresholds: {thresholds}";
        }
    }
}
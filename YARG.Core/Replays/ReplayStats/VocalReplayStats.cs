using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Engine.Vocals;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Replays
{
    public sealed class VocalsReplayStats : ReplayStats
    {
        public readonly int  NumPhrases;
        public readonly int  NumPerfectPhrases;
        public readonly bool CensorshipEnabled;

        public VocalsReplayStats(string name, bool isReplayPlayer, bool censorshipEnabled, VocalsStats stats)
            : base(name, stats, isReplayPlayer)
        {
            NumPhrases = 0;
            NumPerfectPhrases = 0;
            CensorshipEnabled = censorshipEnabled;
        }

        public VocalsReplayStats(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            NumPhrases = stream.Read<int>(Endianness.Little);
            NumPerfectPhrases = stream.Read<int>(Endianness.Little);
            if (version >= 16)
            {
                CensorshipEnabled = stream.ReadBoolean();
            }
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write((byte) GameMode.Vocals);
            base.Serialize(writer);
            writer.Write(NumPhrases);
            writer.Write(NumPerfectPhrases);
            writer.Write(CensorshipEnabled);
        }
    }
}

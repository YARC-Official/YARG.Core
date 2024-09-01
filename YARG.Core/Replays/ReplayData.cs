using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Replays
{
    public class ReplayData
    {
        public readonly ReplayPresetContainer ReplayPresetContainer;
        public readonly ReplayFrame[] Frames;

        public int PlayerCount => Frames.Length;

        public ReplayData(ReplayPresetContainer presets, ReplayFrame[] frames)
        {
            ReplayPresetContainer = presets;
            Frames = frames;
        }

        public ReplayData(UnmanagedMemoryStream stream, int version)
        {
            ReplayPresetContainer = new ReplayPresetContainer(stream, version);
            int count = stream.Read<int>(Endianness.Little);
            Frames = new ReplayFrame[count];
            for (int i = 0; i != count; i++)
            {
                Frames[i] = new ReplayFrame(stream, version);
            }
        }
    }
}

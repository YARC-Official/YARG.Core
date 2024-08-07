using System;

namespace YARG.Core.Replays
{
    public class Replay
    {
        public ReplayHeader          Header;
        public ReplayMetadata        Metadata;
        public ReplayPresetContainer PresetContainer;

        public int PlayerCount;

        public string[] PlayerNames;

        public ReplayFrame[] Frames;

        public Replay()
        {
            Header = new ReplayHeader();
            Metadata = new ReplayMetadata();
            PresetContainer = new ReplayPresetContainer();
            PlayerNames = Array.Empty<string>();
            PlayerCount = 0;
            Frames = Array.Empty<ReplayFrame>();
        }
    }
}
using System;

namespace YARG.Core.Replays
{
    public class ReplayNew
    {
        public ReplayHeader          Header;
        public ReplayMetadata        Metadata;
        public ReplayPresetContainer PresetContainer;

        public int PlayerCount;

        public string[] PlayerNames;

        public ReplayFrame[] Frames;

        public ReplayNew()
        {
            Metadata = new ReplayMetadata();
            PresetContainer = new ReplayPresetContainer();
            PlayerNames = Array.Empty<string>();
            Frames = Array.Empty<ReplayFrame>();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public class SerializedMic
    {
        public readonly string BaseName;
        public readonly int Channel;

        public SerializedMic(string baseName, int channel)
        {
            BaseName = baseName;
            Channel = channel;
        }

        public SerializedMic(string displayName)
        {
            if (InputDeviceInfo.TryParseDisplayName(displayName, out var parsedBaseName, out var parsedChannel))
            {
                BaseName = parsedBaseName;
                Channel = parsedChannel;
            }
            else
            {
                BaseName = displayName ?? string.Empty;
                Channel = 0;
            }
        }

        public string Name => Channel > 0 ? $"{BaseName} - Channel {Channel + 1}" : BaseName;
    }
}

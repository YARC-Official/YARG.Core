using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public class SerializedMic
    {
        public readonly string BaseName;
        public readonly int Channel;

        public readonly string? EndpointId;

        public MicId Id => new(BaseName, Channel, EndpointId);

        public SerializedMic(MicId id) : this(id.BaseName, id.Channel, id.EndpointId)
        {
        }

        public SerializedMic(string baseName, int channel) : this(baseName, channel, null)
        {
        }

        public SerializedMic(string baseName, int channel, string? endpointId)
        {
            BaseName = baseName;
            Channel = channel;
            EndpointId = endpointId;
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

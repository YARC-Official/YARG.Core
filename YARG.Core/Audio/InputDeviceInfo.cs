using System;

namespace YARG.Core.Audio
{
    /// <summary>
    /// An input device as it appears in the input devices list
    /// </summary>
    public readonly struct InputDeviceInfo
    {
        public const string CHANNEL_SUFFIX = " - Channel ";

        /// <summary>
        /// BASS recording device index to open.
        /// </summary>
        public readonly int DeviceId;

        /// <summary>
        /// Base BASS device name, without the channel suffix.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Channel to record from the interleaved stream.
        /// </summary>
        public readonly int Channel;

        /// <summary>
        /// Total channels the device exposes (1 for mono)
        /// </summary>
        public readonly int ChannelCount;

        /// <summary>
        /// Name shown in the mic list and used when restoring a saved mic.  e.g. "Focusrite 2i2 - Channel 1"
        /// </summary>
        public string DisplayName => ChannelCount > 1 ? $"{Name}{CHANNEL_SUFFIX}{Channel + 1}" : Name;

        public InputDeviceInfo(int deviceId, string name, int channel, int channelCount)
        {
            DeviceId = deviceId;
            Name = name;
            Channel = channel;
            ChannelCount = channelCount;
        }

        public static bool TryParseDisplayName(string displayName, out string baseName, out int channel)
        {
            if (displayName == null)
            {
                baseName = null!;
                channel = 0;
                return false;
            }

            int separator = displayName.LastIndexOf(CHANNEL_SUFFIX, StringComparison.Ordinal);
            if (separator >= 0)
            {
                string parsedBaseName = displayName.Substring(0, separator);
                string channelText = displayName.Substring(separator + CHANNEL_SUFFIX.Length);
                if (int.TryParse(channelText, out int parsedChannel) && parsedChannel >= 1)
                {
                    baseName = parsedBaseName;
                    channel = parsedChannel - 1;
                    return true;
                }
            }

            baseName = displayName;
            channel = 0;
            return true;
        }
    }
}
using System;

namespace YARG.Core.Audio
{
    /// <summary>
    /// An input device as it appears in the mic list and in saved mic settings.
    /// </summary>
    /// <remarks>
    /// A device with multiple channels (like a USB interface with two XLR inputs)
    /// shows up as one entry per channel. Each entry opens the same BASS device,
    /// but only records <see cref="Channel"/> of the interleaved stream. Mono
    /// devices have <see cref="ChannelCount"/> == 1 and no suffix in
    /// <see cref="DisplayName"/>.
    /// </remarks>
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
        /// Channel to record from the interleaved stream. Only used when
        /// <see cref="ChannelCount"/> is greater than 1.
        /// </summary>
        public readonly int Channel;

        /// <summary>
        /// Total channels the device exposes (1 for mono). Only affects
        /// <see cref="DisplayName"/>; parsed names carry a best-effort value.
        /// </summary>
        public readonly int ChannelCount;

        /// <summary>
        /// Name shown in the mic list and used when restoring a saved mic.
        /// Split devices get a 1-based " - Channel N" suffix.
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
                string b = displayName.Substring(0, separator);
                string channelText = displayName.Substring(separator + CHANNEL_SUFFIX.Length);
                if (int.TryParse(channelText, out int c) && c >= 1)
                {
                    baseName = b;
                    channel = c - 1;
                    return true;
                }
            }

            baseName = displayName;
            channel = 0;
            return true;
        }
    }
}
using System;

namespace YARG.Core.Audio
{
    public readonly struct MicId : IEquatable<MicId>
    {
        public readonly string BaseName;
        public readonly int    Channel;

        public MicId(string baseName, int channel)
        {
            BaseName = baseName ?? string.Empty;
            Channel = channel;
        }

        public bool Equals(MicId other)
        {
            return string.Equals(BaseName, other.BaseName, StringComparison.Ordinal)
                && Channel == other.Channel;
        }

        public override bool Equals(object? obj) => obj is MicId other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(BaseName), Channel);

        public static bool operator ==(MicId left, MicId right) => left.Equals(right);
        public static bool operator !=(MicId left, MicId right) => !left.Equals(right);

        public override string ToString() => $"{BaseName}:{Channel}";
    }
}

using System;

namespace YARG.Core.Audio
{
    public readonly struct MicId : IEquatable<MicId>
    {
        public readonly string BaseName;
        public readonly int    Channel;

        public readonly string? EndpointId;

        public MicId(string baseName, int channel) : this(baseName, channel, null)
        {
        }

        public MicId(string baseName, int channel, string? endpointId)
        {
            BaseName = baseName ?? string.Empty;
            Channel = channel;
            EndpointId = string.IsNullOrEmpty(endpointId) ? null : endpointId;
        }

        public bool Equals(MicId other)
        {
            return Channel == other.Channel && EndpointId == other.EndpointId &&
                (EndpointId != null || BaseName == other.BaseName);
        }

        // Old bindings have only a device name and channel. Ownership always uses exact equality.
        public bool MatchesInput(MicId input) => MatchesInput(input, StringComparison.Ordinal);

        public bool MatchesInput(MicId input, StringComparison nameComparison) => EndpointId == null
            ? string.Equals(BaseName, input.BaseName, nameComparison) && Channel == input.Channel
            : Equals(input);

        public override bool Equals(object? obj) => obj is MicId other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(EndpointId, EndpointId ?? BaseName, Channel);

        public static bool operator ==(MicId left, MicId right) => left.Equals(right);
        public static bool operator !=(MicId left, MicId right) => !left.Equals(right);

        public override string ToString() => $"{BaseName}:{Channel}";
    }
}

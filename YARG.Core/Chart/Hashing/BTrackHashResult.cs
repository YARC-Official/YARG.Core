using System;

namespace YARG.Core.Chart.Hashing
{
    public readonly struct BTrackHashResult
    {
        public byte[] BTrack { get; }

        public string Hash { get; }

        public BTrackHashResult(byte[] bTrack, byte[] hashInput)
        {
            BTrack = bTrack;
            Hash = Encode(Blake3.Hash(hashInput));
        }

        internal static string Encode(byte[] hash)
        {
            return Convert.ToBase64String(hash)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
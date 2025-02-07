using System;

namespace YARG.Core.Song
{
    public struct RBAudio<TType>
        where TType : unmanaged
    {
        public static readonly RBAudio<TType> Empty = new()
        {
            Track = Array.Empty<TType>(),
            Drums = Array.Empty<TType>(),
            Bass = Array.Empty<TType>(),
            Guitar = Array.Empty<TType>(),
            Keys = Array.Empty<TType>(),
            Vocals = Array.Empty<TType>(),
            Crowd = Array.Empty<TType>(),
        };

        public TType[] Track;
        public TType[] Drums;
        public TType[] Bass;
        public TType[] Guitar;
        public TType[] Keys;
        public TType[] Vocals;
        public TType[] Crowd;
    }
}

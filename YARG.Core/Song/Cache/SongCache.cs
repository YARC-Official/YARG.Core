using System;
using System.Collections.Generic;

namespace YARG.Core.Song
{
    public sealed class SongCache
    {
        public readonly Dictionary<HashWrapper, List<SongEntry>> Entries = new();
    }
}

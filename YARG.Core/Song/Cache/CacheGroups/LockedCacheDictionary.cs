using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public class LockedCacheDictionary<T>
    {
        public readonly object Lock = new();
        public readonly Dictionary<string, T> Values = new();

        public void Add(string key, T value)
        {
            lock (Lock)
                Values.Add(key, value);
        }
    }
}

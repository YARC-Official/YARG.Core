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

    public class LockedConGroupList<TGroup>
        where TGroup : CONGroup
    {
        public readonly object Lock = new();
        public readonly List<(string Location, TGroup Group)> Values = new();

        public void Add(string key, TGroup group)
        {
            lock (Lock)
                Values.Add((key, group));
        }
    }
}

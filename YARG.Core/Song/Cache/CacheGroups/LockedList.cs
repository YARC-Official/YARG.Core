using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Song.Cache
{
    public class LockedList<T>
    {
        public readonly object Lock = new();
        public readonly List<T> Values = new();

        public void Add(T value)
        {
            lock (Lock)
                Values.Add(value);
        }
    }
}

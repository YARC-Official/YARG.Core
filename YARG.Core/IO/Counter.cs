using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.IO
{
    public class Counter
    {
        private int _count = 1;
        private object _lock = new();
        public int Count => _count;

        public void Increment()
        {
            lock (_lock)
                ++_count;
        }

        public void Decrement()
        {
            lock (_lock)
                --_count;
        }
    }
}

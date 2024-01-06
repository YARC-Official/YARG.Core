using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.IO
{
    public abstract class RefCounter<T> : IDisposable
        where T : RefCounter<T>
    {
        private int _refCount;
        private object _lock;

        protected RefCounter()
        {
            _refCount = 1;
            _lock = new();
        }

        public T AddRef()
        {
            lock (_lock)
            {
                if (_refCount == 0)
                {
                    throw new InvalidOperationException();
                }
                ++_refCount;
            }
            return (T)this;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                --_refCount;
                if (_refCount == 0)
                {
                    Dispose(disposing: true);
                    GC.SuppressFinalize(this);
                }
            }
        }

        protected abstract void Dispose(bool disposing);

        ~RefCounter()
        {
            // Forcibly dispose, regardless of RefCount
            // because if we're here, there are no references
            Dispose(disposing: false);
        }
    }
}

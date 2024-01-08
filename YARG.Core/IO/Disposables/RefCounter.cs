using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.IO
{
    /// <summary>
    /// An extension on disposable classes that manually disposes instances
    /// of said classes based on a explicitly kept count.
    /// </summary>
    /// <typeparam name="T">The disposable class to attach to</typeparam>
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

        /// <summary>
        /// Increments the internal count of references by one
        /// </summary>
        /// <returns>The object the counter is attached to</returns>
        /// <exception cref="InvalidOperationException">If the object was already disposed from reaching a count of 0</exception>
        public T AddRef()
        {
            lock (_lock)
            {
                if (_refCount <= 0)
                {
                    throw new InvalidOperationException();
                }
                ++_refCount;
            }
            return (T)this;
        }

        /// <summary>
        /// Decrements the internal count of references by one.<br></br>
        /// If the count becomes 0, the counter will call the object's disposal method.
        /// </summary>
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

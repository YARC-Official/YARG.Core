﻿using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.IO
{
    /// <summary>
    /// An extension on disposable classes that manually disposes instances
    /// of said classes based on a explicitly kept count.
    /// </summary>
    /// <typeparam name="T">The disposable class to attach to</typeparam>
    public sealed class DisposableCounter<T> : IDisposable
        where T : IDisposable
    {
        private int _refCount;
        private object _lock;

        private T _value;

        public ref T Value
        {
            get
            {
                lock (_lock)
                {
                    if (_refCount <= 0)
                    {
                        throw new InvalidOperationException();
                    }
                    return ref _value;
                }
            }
        }

        public DisposableCounter(T value)
        {
            _refCount = 1;
            _lock = new();
            _value = value;
        }

        /// <summary>
        /// Increments the internal count of references by one
        /// </summary>
        /// <returns>The object the counter is attached to</returns>
        /// <exception cref="InvalidOperationException">If the object was already disposed from reaching a count of 0</exception>
        public DisposableCounter<T> AddRef()
        {
            lock (_lock)
            {
                if (_refCount <= 0)
                {
                    throw new InvalidOperationException();
                }
                ++_refCount;
            }
            return this;
        }

        public T Release()
        {
            lock (_lock)
            {
                _refCount = 0;
                GC.SuppressFinalize(this);
                return _value;
            }
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
                    _value.Dispose();
                    GC.SuppressFinalize(this);
                }
            }
        }

        ~DisposableCounter()
        {
            // Forcibly dispose, regardless of RefCount
            // because if we're here, there are no references
            _value.Dispose();
        }
    }

    public static class DisposableCounter
    {
        public static DisposableCounter<T> Wrap<T>(T value)
             where T : IDisposable
        {
            return new DisposableCounter<T>(value);
        }
    }
}

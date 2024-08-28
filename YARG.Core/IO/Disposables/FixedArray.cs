using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using YARG.Core.Logging;

namespace YARG.Core.IO.Disposables
{
    /// <summary>
    /// A wrapper interface over a fixed area of unmanaged memory.
    /// Provides functions to create spans and span slices alongside
    /// basic indexing and enumeration.<br></br><br></br>
    /// 
    /// For serious performance-critical code, the raw pointer to
    /// the start of the memory block is also provided.<br></br>
    /// However, code that uses the value directly should first check
    /// for valid boundaries.
    /// </summary>
    /// <remarks>DO NOT USE THIS AS AN ALTERNATIVE TO STACK-BASED ARRAYS</remarks>
    /// <typeparam name="T">The unmanaged type contained in the block of memory</typeparam>
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(FixedArray<>.FixedArrayDebugView))]
    public unsafe class FixedArray<T> : IDisposable
        where T : unmanaged
    {
        public static FixedArray<T> Alias(T* ptr, long length)
        {
            return new FixedArray<T>(ptr, length);
        }

        /// <summary>
        /// Pointer to the beginning of the memory block.<br></br>
        /// DO NOT TOUCH UNLESS YOU ENSURE YOU'RE WITHIN BOUNDS
        /// </summary>
        public readonly T* Ptr;

        /// <summary>
        /// Number of elements within the block
        /// </summary>
        public readonly long Length;

        protected FixedArray(T* ptr, long length)
        {
            Ptr = ptr;
            Length = length;
        }

        /// <summary>
        /// Provides the pointer to the block of memory in IntPtr form
        /// </summary>
        public IntPtr IntPtr => (IntPtr) Ptr;

        /// <summary>
        /// Provides a ReadOnlySpan over the block of memory
        /// </summary>
        public ReadOnlySpan<T> ReadOnlySpan => new(Ptr, (int)Length);

        public UnmanagedMemoryStream ToStream() => new((byte*)Ptr, (int)(Length * sizeof(T)));

        /// <summary>
        /// Indexer into the fixed block of memory
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public ref T this[long index]
        {
            get
            {
                if (index < 0 || Length <= index)
                {
                    throw new IndexOutOfRangeException();
                }
                return ref Ptr[index];
            }
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        ~FixedArray()
        {
            YargLogger.LogWarning($"{GetType()} was not disposed correctly!");
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
                DisposeManaged();
            DisposeUnmanaged();
        }

        protected virtual void DisposeManaged() { }
        protected virtual void DisposeUnmanaged() { }

        private sealed class FixedArrayDebugView
        {
            private readonly FixedArray<T> array;
            public FixedArrayDebugView(FixedArray<T> array)
            {
                this.array = array ?? throw new ArgumentNullException(nameof(array));
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public ReadOnlySpan<T> Items => array.ReadOnlySpan;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
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
    public sealed unsafe class FixedArray<T> : DisposableCounter<FixedArray<T>>, IEnumerable<T>
        where T : unmanaged
    {
        /// <summary>
        /// Pointer to the beginning of the memory block.<br></br>
        /// DO NOT TOUCH UNLESS YOU ENSURE YOU'RE WITHIN BOUNDS
        /// </summary>
        public readonly T* Ptr;

        /// <summary>
        /// Number of elements within the block
        /// </summary>
        public readonly int Length;

        private bool _disposedValue;
        private readonly Action _disposal;

        /// <summary>
        /// Fully loads the data of a file into a fixed location in memory
        /// </summary>
        /// <remarks>Be wary of any file I/O related exceptions. Those will NOT be handled</remarks>
        /// <param name="path">The file to grab</param>
        /// <returns>A new FixedArray filled with the file's data</returns>
        public static FixedArray<T> Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return Load(fs);
        }

        /// <summary>
        /// Fully loads the data of a stream (or whatever is leftover in the stream)
        /// into a fixed location in memory
        /// </summary>
        /// <remarks>Any exceptions thrown from attempting to read the stream will NOT be handled</remarks>
        /// <param name="stream">The stream to read from</param>
        /// <returns>A new FixedArray filled with the stream's leftover data</returns>
        public static FixedArray<T> Load(Stream stream)
        {
            int length = (int)(stream.Length - stream.Position);
            return Load(stream, length);
        }

        /// <summary>
        /// Loads the given amount of data from a stream at its current position
        /// into a fixed location in memory
        /// </summary>
        /// <remarks>Any exceptions thrown from attempting to read the stream will NOT be handled</remarks>
        /// <param name="stream">The stream to read from</param>
        /// <param name="length">The amount of data (in bytes) to read</param>
        /// <returns>A new FixedArray filled with the requested amount of data</returns>
        public static FixedArray<T> Load(Stream stream, int length)
        {
            if (stream.Position + length > stream.Length)
                throw new EndOfStreamException();

            byte* buffer = (byte*)Marshal.AllocHGlobal(length);
            stream.Read(new Span<byte>(buffer, length));
            return new FixedArray<T>((T*)buffer, length / sizeof(T), () => Marshal.FreeHGlobal((IntPtr)buffer));
        }

        /// <summary>
        /// Allocates an uninitialized block of memory
        /// </summary>
        /// <param name="length">The number of elements (of `T`) to allocate for</param>
        /// <returns>A new FixedArray with the requested amount of allocated memory</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        public static FixedArray<T> Alloc(int length)
        {
            int bufferLength = length * sizeof(T);
            var ptr = (T*)Marshal.AllocHGlobal(bufferLength);
            return new FixedArray<T>(ptr, length, () => Marshal.FreeHGlobal((IntPtr)ptr));
        }

        /// <summary>
        /// Pins a managed array to a fixed location
        /// </summary>
        /// <remarks>PREFER STACK-ALLOCATED ARRAYS OVER THIS WHEN POSSIBLE</remarks>
        /// <param name="array">The managed array to pin</param>
        /// <returns>A FixedArray that points to the location of the now-pinned array</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        public static FixedArray<T> Pin(T[] array)
        {
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            return new FixedArray<T>((T*)handle.AddrOfPinnedObject(), array.Length, () => handle.Free());
        }

        /// <param name="disposal">The function to use when the object needs to be disposed</param>
        public FixedArray(T* ptr, int length, Action disposal)
        {
            Ptr = ptr;
            Length = length;
            _disposal = disposal;
        }

        /// <summary>
        /// Indexer into the fixed block of memory
        /// </summary>
        /// <param name="index"></param>
        /// <returns>A reference to the object at the provided index</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public ref T this[int index]
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                if (index < 0 || Length <= index )
                {
                    throw new IndexOutOfRangeException();
                }
                return ref Ptr[index];
            }
        }

        /// <summary>
        /// Creates a span over the requested slice of memory
        /// </summary>
        /// <param name="offset">Starting position of the slice</param>
        /// <param name="count">Number of elements in the slice</param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public Span<T> Slice(int offset, int count)
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (0 <= offset && offset + count <= Length)
                return new Span<T>(Ptr + offset, count);
            throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// Provides the pointer to the block of memory in IntPtr form
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public IntPtr IntPtr
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                return (IntPtr)Ptr;
            }
        }

        /// <summary>
        /// Provides a Span over the block of memory
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public Span<T> Span
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                return new Span<T>(Ptr, Length);
            }
        }

        /// <summary>
        /// Provides a ReadOnlySpan over the block of memory
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public ReadOnlySpan<T> ReadOnlySpan
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                return new ReadOnlySpan<T>(Ptr, Length);
            }
        }

        /// <summary>
        /// Provides a copy of the block of memory in a managed array
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public T[] ToArray()
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            var array = new T[Length];
            Span.CopyTo(array);
            return array;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _disposal();
                _disposedValue = true;
            }
        }

        public IEnumerator GetEnumerator() { return new Enumerator(this); }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly FixedArray<T> _arr;
            private int _index;

            internal Enumerator(FixedArray<T> arr)
            {
                _arr = arr.AddRef();
                _index = -1;
            }

            public void Dispose()
            {
                _arr.Dispose();
            }

            public bool MoveNext()
            {
                ++_index;
                return _index < _arr.Length;
            }

            public readonly T Current => _arr[_index];

            readonly object IEnumerator.Current => Current;

            void IEnumerator.Reset()
            {
                _index = -1;
            }
        }
    }
}

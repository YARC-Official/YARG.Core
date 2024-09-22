using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using YARG.Core.Logging;

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
    /// <remarks>
    /// 1. DO NOT USE THIS AS AN ALTERNATIVE TO STACK-BASED ARRAYS!<br></br>
    /// 2. YOU MUST MANUALLY DISPOSE OF ANY INSTANCE YOU CREATE! IT WILL NOT DO IT FOR YOU!
    /// </remarks>
    /// <typeparam name="T">The unmanaged type contained in the block of memory</typeparam>
    [DebuggerDisplay("Length = {Length}")]
    public unsafe struct FixedArray<T> : IDisposable
        where T : unmanaged
    {
        /// <summary>
        /// A indisposable default instance with a null pointer
        /// </summary>
        public static readonly FixedArray<T> Null = new()
        {
            _disposed = true
        };

        /// <summary>
        /// Loads all of the given file's data into a FixedArray buffer
        /// </summary>
        /// <param name="filename">The path to the file</param>
        /// <returns>The instance carrying the loaded data</returns>
        public static FixedArray<T> Load(string filename)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return Read(stream, stream.Length);
        }

        /// <summary>
        /// Loads all the data remaining in the stream into a FixedArray buffer
        /// </summary>
        /// <param name="stream">Stream with leftover data</param>
        /// <returns>The instance carrying the loaded data</returns>
        public static FixedArray<T> ReadRemainder(Stream stream)
        {
            return Read(stream, stream.Length - stream.Position);
        }

        /// <summary>
        /// Loads the given amount of data from the stream into a FixedArray buffer
        /// </summary>
        /// <param name="stream">Stream with leftover data</param>
        /// <param name="byteCount">Number of bytes to read from the stream</param>
        /// <returns>The instance carrying the loaded data</returns>
        public static FixedArray<T> Read(Stream stream, long byteCount)
        {
            if (stream.Position > stream.Length - byteCount)
            {
                throw new ArgumentException("Length extends past end of stream");
            }

            var buffer = Alloc(byteCount);
            stream.Read(new Span<byte>(buffer.Ptr, (int) byteCount));
            return buffer;
        }

        /// <summary>
        /// Allocates a uninitialized buffer of data
        /// </summary>
        /// <param name="byteCount">Length of the buffer in bytes</param>
        /// <returns>The instance carrying the empty buffer</returns>
        public static FixedArray<T> Alloc(long byteCount)
        {
            var ptr = (T*) Marshal.AllocHGlobal((int) byteCount);
            return new FixedArray<T>(ptr, byteCount / sizeof(T));
        }

        private bool _disposed;

        /// <summary>
        /// Pointer to the beginning of the memory block.<br></br>
        /// DO NOT TOUCH UNLESS YOU ENSURE YOU'RE WITHIN BOUNDS
        /// </summary>
        public readonly T* Ptr;

        /// <summary>
        /// Number of elements within the block
        /// </summary>
        public readonly long Length;

        /// <summary>
        /// Returns whether the instance points to actual data
        /// </summary>
        public readonly bool IsAllocated => Ptr != null;

        /// <summary>
        /// Provides the pointer to the block of memory in IntPtr form
        /// </summary>
        public readonly IntPtr IntPtr => (IntPtr) Ptr;

        /// <summary>
        /// Provides a ReadOnlySpan over the block of memory
        /// </summary>
        public readonly ReadOnlySpan<T> ReadOnlySpan => new(Ptr, (int) Length);

        public readonly Span<T> Span => new(Ptr, (int) Length);

        private FixedArray(T* ptr, long length)
        {
            Ptr = ptr;
            Length = length;
            _disposed = false;
        }

        public readonly Span<T> Slice(long offset, long count)
        {
            if (offset < 0 || Length < offset + count)
            {
                throw new IndexOutOfRangeException();
            }
            return new Span<T>(Ptr + offset, (int) count);
        }

        /// <summary>
        /// Provides a unmanaged stream over the buffer of data. By design, the stream runs over bytes.
        /// </summary>
        /// <returns>The stream, duh</returns>
        public readonly UnmanagedMemoryStream ToStream() => new((byte*) Ptr, Length * sizeof(T));

        /// <summary>
        /// Creates an instance of FixedArray that solely functions as an cast over the current buffer
        /// </summary>
        /// <remarks>The casted copy will not dispose of the original data, so any callers must maintain the original buffer instance.</remarks>
        /// <param name="offset">Index to start the cast from</param>
        /// <param name="count">Number of original elements to be casted</param>
        /// <returns>The buffer casted to the new type</returns>
        public readonly FixedArray<U> Cast<U>(long offset, long count)
            where U : unmanaged
        {
            if (offset < 0 || Length < offset + count)
            {
                throw new IndexOutOfRangeException();
            }

            long length = (count * sizeof(T)) / sizeof(U);
            return new FixedArray<U>((U*) (Ptr + offset), length)
            {
                _disposed = true
            };
        }

        /// <summary>
        /// Copies the pointer and length to a new instance of FixedArray, leaving the current one
        /// in a limbo state - no longer responsible for disposing of the data.
        /// </summary>
        /// <remarks>Useful for cleanly handling exception safety</remarks>
        /// <returns>The instance that takes responsibilty over disposing of the buffer</returns>
        public FixedArray<T> TransferOwnership()
        {
            _disposed = true;
            return new FixedArray<T>(Ptr, Length);
        }

        /// <summary>
        /// Indexer into the fixed block of memory
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public readonly ref T this[long index]
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

        public void Dispose()
        {
            if (!_disposed)
            {
                Marshal.FreeHGlobal(IntPtr);
                _disposed = true;
            }
        }
    }
}

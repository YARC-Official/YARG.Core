using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    public static class FixedArray
    {
        /// <summary>
        /// Loads all of the given file's data into a FixedArray buffer
        /// </summary>
        /// <param name="filename">The path to the file</param>
        /// <returns>The instance carrying the loaded data</returns>
        public static FixedArray<byte> LoadFile(string filename)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return Read(stream, stream.Length);
        }

        /// <summary>
        /// Loads all the data remaining in the stream into a FixedArray buffer
        /// </summary>
        /// <param name="stream">Stream with leftover data</param>
        /// <returns>The instance carrying the loaded data</returns>
        public static FixedArray<byte> ReadRemainder(Stream stream)
        {
            return Read(stream, stream.Length - stream.Position);
        }

        /// <summary>
        /// Loads the given amount of data from the stream into a FixedArray buffer
        /// </summary>
        /// <param name="stream">Stream with leftover data</param>
        /// <param name="numElements">Number of <see cref="T"/> elements to read from the stream</param>
        /// <returns>The instance carrying the loaded data</returns>
        public static FixedArray<byte> Read(Stream stream, long numElements)
        {
            long byteCount = numElements;
            if (stream.Position > stream.Length - byteCount)
            {
                throw new ArgumentException("Length extends past end of stream");
            }

            var buffer = FixedArray<byte>.Alloc(numElements);
            unsafe
            {
                if (stream.Read(new Span<byte>(buffer.Ptr, (int) byteCount)) != byteCount)
                {
                    buffer.Dispose();
                    throw new IOException("Could not read data from file");
                }
            }
            return buffer;
        }

        public static FixedArrayStream ToValueStream(in this FixedArray<byte> arr)
        {
            return new FixedArrayStream(in arr);
        }

        public static UnmanagedMemoryStream ToReferenceStream(in this FixedArray<byte> arr)
        {
            unsafe
            {
                return new UnmanagedMemoryStream(arr.Ptr, arr.Length);
            }
        }
    }

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
        public static readonly FixedArray<T> Null = new(null, 0);

        /// <summary>
        /// Allocates a uninitialized buffer of data
        /// </summary>
        /// <param name="numElements">Number of the elements to hold in the buffer</param>
        /// <returns>The instance carrying the empty buffer</returns>
        public static FixedArray<T> Alloc(long numElements)
        {
            var ptr = (T*) Marshal.AllocHGlobal((IntPtr) (numElements * sizeof(T)));
            return new FixedArray<T>(ptr, numElements);
        }

        /// <summary>
        /// Creates an instance of FixedArray that solely functions as an cast over the current buffer
        /// </summary>
        /// <remarks>The casted copy will not dispose of the original data, so any callers must maintain the original buffer instance.</remarks>
        /// <param name="source">The source buffer to cast</param>
        /// <param name="offset">The index in the source buffer to start the cast from</param>
        /// <param name="numElements">The number of elements to cast to</param>
        /// <returns>The buffer casted to the new type</returns>
        public static FixedArray<T> Cast<U>(in FixedArray<U> source, long offset, long numElements)
            where U : unmanaged
        {
            if (offset < 0)
            {
                throw new IndexOutOfRangeException();
            }

            if ((source.Length - offset) * sizeof(U) < numElements * sizeof(T))
            {
                throw new ArgumentOutOfRangeException(nameof(numElements));
            }

            return new FixedArray<T>((T*) (source.Ptr + offset), numElements)
            {
                _owned = false
            };
        }

        private bool _owned;
        private T* _ptr;
        private long _length;

        /// <summary>
        /// Pointer to the beginning of the memory block.<br></br>
        /// DO NOT TOUCH UNLESS YOU ENSURE YOU'RE WITHIN BOUNDS
        /// </summary>
        public readonly T* Ptr => _ptr;

        /// <summary>
        /// Number of elements within the block
        /// </summary>
        public readonly long Length => _length;

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
            _ptr = ptr;
            _length = length;
            _owned = ptr != null;
        }

        public readonly Span<T> Slice(long offset, long count)
        {
            if (offset < 0 || offset + count > _length)
            {
                throw new IndexOutOfRangeException();
            }
            return new Span<T>(_ptr + offset, (int) count);
        }

        public readonly ReadOnlySpan<T> ReadonlySlice(long offset, long count)
        {
            if (offset < 0 || offset + count > _length)
            {
                throw new IndexOutOfRangeException();
            }
            return new ReadOnlySpan<T>(_ptr + offset, (int) count);
        }

        /// <summary>
        /// Copies the pointer and length to a new instance of FixedArray, leaving the current one
        /// in a limbo state - no longer responsible for disposing of the data.
        /// </summary>
        /// <remarks>Useful for cleanly handling exception safety</remarks>
        /// <returns>The instance that takes responsibilty over disposing of the buffer</returns>
        public FixedArray<T> TransferOwnership()
        {
            _owned = false;
            return new FixedArray<T>(_ptr, _length);
        }

        /// <summary>
        /// Indexer into the fixed block of memory
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public readonly ref T this[long index] => ref _ptr[index];

        /// <summary>
        /// Returns a reference to the value at the provided index, so long as the index lies within bounds.
        /// Indices out of bounds will throw an excpetion.
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public readonly ref T At(long index)
        {
            if (index < 0 || _length <= index)
            {
                throw new IndexOutOfRangeException();
            }
            return ref _ptr[index];
        }

        public void Resize(int numElements)
        {
            if (!_owned)
            {
                throw new InvalidOperationException("Can not resize an unowned array");
            }

            _ptr = (T*) Marshal.ReAllocHGlobal(IntPtr, (IntPtr) (numElements * sizeof(T)));
            _length = numElements;
        }

        public void Dispose()
        {
            if (_owned)
            {
                Marshal.FreeHGlobal(IntPtr);
                _owned = false;
            }
        }
    }
}

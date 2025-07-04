using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public static class FixedArray
    {
        /// <summary>
        /// Loads all the given file's data into a FixedArray buffer
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
        /// <param name="numElements">Number of bytes to read from the stream</param>
        /// <param name="vectorize">Flags whether to allocate the buffer with vector alignment</param>
        /// <returns>The instance carrying the loaded data</returns>
        public static FixedArray<byte> Read(Stream stream, long numElements, bool vectorize = false)
        {
            if (stream.Position > stream.Length - numElements)
            {
                throw new ArgumentException("Length extends past end of stream");
            }

            if (numElements > FixedArray<byte>.MAX_ELEMENTS)
            {
                throw new Exception($"Stream read count exceeds max of {int.MaxValue}");
            }

            var buffer = !vectorize
                ? FixedArray<byte>.Alloc((int)numElements)
                : FixedArray<byte>.AllocVectorAligned((int)numElements);

            unsafe
            {
                if (stream.Read(new Span<byte>(buffer.Ptr, (int) numElements)) != numElements)
                {
                    buffer.Dispose();
                    throw new IOException("Could not read data from file");
                }
            }
            return buffer;
        }

        public static FixedArrayStream ToValueStream(this FixedArray<byte> arr)
        {
            return new FixedArrayStream(arr);
        }

        public static UnmanagedMemoryStream ToReferenceStream(this FixedArray<byte> arr)
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
    public unsafe class FixedArray<T> : IDisposable
        where T : unmanaged
    {
        public static readonly int MAX_ELEMENTS = int.MaxValue / sizeof(T);

        /// <summary>
        /// Allocates a uninitialized buffer of data
        /// </summary>
        /// <param name="numElements">Number of the elements to hold in the buffer</param>
        /// <returns>The instance carrying the empty buffer</returns>
        public static FixedArray<T> Alloc(int numElements)
        {
            if (numElements > MAX_ELEMENTS)
            {
                throw new ArgumentOutOfRangeException(nameof(numElements));
            }

            var handle = Marshal.AllocHGlobal((IntPtr) (numElements * sizeof(T)));
            return new FixedArray<T>
            (
                handle,
                (T*) handle,
                numElements,
                false
            );
        }

        private static readonly int OVER_ALLOCATION = sizeof(Vector<byte>) - 1;
        public static FixedArray<T> AllocVectorAligned(int numElements)
        {
            if (numElements > MAX_ELEMENTS)
            {
                throw new ArgumentOutOfRangeException(nameof(numElements));
            }

            var handle = Marshal.AllocHGlobal((IntPtr) (numElements * sizeof(T) + OVER_ALLOCATION));

            long adjustment = handle.ToInt64() & OVER_ALLOCATION;
            if (adjustment > 0)
            {
                adjustment = sizeof(Vector<byte>) - adjustment;
            }

            return new FixedArray<T>
            (
                handle,
                (T*) ((byte*)handle + adjustment),
                numElements,
                true
            );
        }

        /// <summary>
        /// Creates an instance of FixedArray that solely functions as an cast over the current buffer
        /// </summary>
        /// <remarks>The casted copy will not dispose of the original data, so any callers must maintain the original buffer instance.</remarks>
        /// <param name="source">The source buffer to cast</param>
        /// <param name="offset">The index in the source buffer to start the cast from</param>
        /// <param name="numElements">The number of elements to cast to</param>
        /// <returns>The buffer casted to the new type</returns>
        public static FixedArray<T> Cast<U>(FixedArray<U> source, int offset, int numElements)
            where U : unmanaged
        {
            if (source._vectorized)
            {
                throw new InvalidOperationException("Do not cast from a vectorized source");
            }

            if (offset < 0
            || numElements < 0
            || (source.Length - offset) * sizeof(U) < numElements * sizeof(T))
            {
                throw new ArgumentOutOfRangeException();
            }

            return new FixedArray<T>
            (
                IntPtr.Zero,
                (T*) (source.Ptr + offset),
                numElements,
                false
            );
        }

        private readonly bool   _vectorized;
        private          IntPtr _handle;
        private          bool   _disposed;

        /// <summary>
        /// Pointer to the beginning of the memory block.<br></br>
        /// DO NOT TOUCH UNLESS YOU ENSURE YOU'RE WITHIN BOUNDS
        /// </summary>
        public T* Ptr { get; private set; }

        /// <summary>
        /// Number of elements within the block
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Provides a ReadOnlySpan over the block of memory
        /// </summary>
        public ReadOnlySpan<T> ReadOnlySpan => new(Ptr, Length);

        public Span<T> Span => new(Ptr, Length);

        private FixedArray(IntPtr handle, T* ptr, int length, bool vectorized)
        {
            _handle = handle;
            Ptr = ptr;
            Length = length;
            _vectorized = vectorized;
        }

        public Span<T> Slice(int offset, int count)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FixedArray<T>));
            }

            if (offset < 0 || offset + count > Length)
            {
                throw new IndexOutOfRangeException();
            }
            return new Span<T>(Ptr + offset, count);
        }

        public ReadOnlySpan<T> ReadonlySlice(int offset, int count)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FixedArray<T>));
            }

            if (offset < 0 || offset + count > Length)
            {
                throw new IndexOutOfRangeException();
            }
            return new ReadOnlySpan<T>(Ptr + offset, count);
        }

        /// <summary>
        /// Copies the pointer and length to a new instance of FixedArray, leaving the current one
        /// in a limbo state - no longer responsible for disposing of the data.
        /// </summary>
        /// <remarks>Useful for cleanly handling exception safety</remarks>
        /// <returns>The instance that takes responsibility over disposing of the buffer</returns>
        public FixedArray<T> TransferOwnership()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("This object owns no memory");
            }

            var handle = _handle;
            _handle = IntPtr.Zero;
            return new FixedArray<T>
            (
                handle,
                Ptr,
                Length,
                _vectorized
            );
        }

        /// <summary>
        /// Indexer into the fixed block of memory
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public ref T this[int index] => ref Ptr[index];

        /// <summary>
        /// Returns a reference to the value at the provided index, so long as the index lies within bounds.
        /// Indices out of bounds will throw an exception.
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public ref T At(int index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FixedArray<T>));
            }

            if (index < 0 || Length <= index)
            {
                throw new IndexOutOfRangeException();
            }
            return ref Ptr[index];
        }

        public void Resize(int numElements)
        {
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Can not resize an unowned array");
            }

            if (_vectorized)
            {
                // Reasoning: if the array is used for simd operations, that entails that
                // the data is either a file read buffer OR the whole file itself.
                // Resizing would therefore be illogical.
                throw new InvalidOperationException("Do not resize a vectorized array");
            }

            if (numElements == Length)
            {
                return;
            }

            _handle = Marshal.ReAllocHGlobal(_handle, (IntPtr) (numElements * sizeof(T)));
            Ptr = (T*)_handle;
            Length = numElements;
        }

        private void _Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_handle);
                _handle = IntPtr.Zero;
            }

            _disposed = true;
        }

        public void Dispose()
        {
            _Dispose();
            GC.SuppressFinalize(this);
        }

        ~FixedArray()
        {
            try
            {
                _Dispose();
                YargLogger.LogDebug("Finalizer called on FixedArray! Missing manual Dispose!");
            }
            catch
            {
                // ignored
            }
        }
    }
}

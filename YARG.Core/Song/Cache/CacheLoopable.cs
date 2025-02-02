using System;
using System.Collections;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    internal unsafe struct CacheLoopable : IEnumerable<(FixedArrayStream Slice, int Index)>
    {
        public FixedArrayStream* Stream;
        public int Count;

        public CacheLoopable(FixedArrayStream* stream)
        {
            Stream = stream;
            Count = stream->Read<int>(Endianness.Little);
        }

        public IEnumerator<(FixedArrayStream Slice, int Index)> GetEnumerator()
        {
            return new Enumerator(Stream, Count);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<(FixedArrayStream Slice, int Index)>
        {
            private readonly FixedArrayStream* _stream;
            private readonly int _count;
            private (FixedArrayStream Slice, int Index) _current;

            public Enumerator(FixedArrayStream* stream, int count)
            {
                _stream = stream;
                _count = count;
                _current = (default(FixedArrayStream), -1);
            }

            public readonly (FixedArrayStream Slice, int Index) Current => _current;

            readonly object IEnumerator.Current => _current;

            public readonly void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (++_current.Index == _count)
                {
                    return false;
                }

                int length = _stream->Read<int>(Endianness.Little);
                _current.Slice = _stream->Slice(length);
                return true;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}

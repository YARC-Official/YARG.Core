using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.IO
{
    public struct YARGMidiFile : IEnumerable<YARGMidiTrack>
    {
        private static readonly FourCC HEADER_TAG = new('M', 'T', 'h', 'd');
        private static readonly FourCC TRACK_TAG  = new('M', 'T', 'r', 'k');
        private const int TAG_SIZE = sizeof(uint);
        private const int DATA_OFFSET = TAG_SIZE + sizeof(int);

        private readonly ushort _format;
        private readonly ushort _num_tracks;
        private readonly ushort _resolution;

        private readonly FixedArray<byte> _data;
        private long _position;
        private ushort _trackNumber;

        public readonly ushort Format => _format;
        public readonly ushort NumTracks => _num_tracks;
        public readonly ushort Resolution => _resolution;
        public readonly ushort TrackNumber => _trackNumber;

        private const int SIZEOF_HEADER = 6;
        public YARGMidiFile(in FixedArray<byte> data)
        {
            if (TAG_SIZE > data.Length || !HEADER_TAG.Matches(data.ReadonlySlice(0, TAG_SIZE)))
            {
                throw new Exception("Midi header Tag 'MThd' mismatch");
            }

            if (DATA_OFFSET > data.Length)
            {
                throw new EndOfStreamException("End of stream found within midi header");
            }
            
            int headerSize = (data[TAG_SIZE] << 24) | (data[TAG_SIZE + 1] << 16) | (data[TAG_SIZE + 2] << 8) | data[TAG_SIZE + 3];
            if (headerSize < SIZEOF_HEADER)
            {
                throw new Exception("Midi header length less than minimum");
            }

            _position = DATA_OFFSET + headerSize;
            if (_position > data.Length)
            {
                throw new EndOfStreamException("End of stream found within midi header");
            }

            _format     = (ushort)((data[DATA_OFFSET] << 8)     | data[DATA_OFFSET + 1]);
            _num_tracks = (ushort)((data[DATA_OFFSET + 2] << 8) | data[DATA_OFFSET + 3]);
            _resolution = (ushort)((data[DATA_OFFSET + 4] << 8) | data[DATA_OFFSET + 5]);

            _data = data;
            _trackNumber = 0;
        }

        public bool LoadNextTrack(out YARGMidiTrack track)
        {
            track = default;
            if (_trackNumber == NumTracks || _position == _data.Length)
            {
                return false;
            }

            ++_trackNumber;
            if (_position + TAG_SIZE > _data.Length || !TRACK_TAG.Matches(_data.ReadonlySlice( _position, TAG_SIZE)))
            {
                throw new Exception("Midi Track Tag 'MTrk' mismatch");
            }
            _position += TAG_SIZE;

            if (_position + sizeof(int) > _data.Length)
            {
                throw new EndOfStreamException("End of stream found within midi track");
            }

            int length = (_data[_position] << 24) | (_data[_position + 1] << 16) | (_data[_position + 2] << 8) | _data[_position + 3];
            _position += sizeof(int);
            unsafe
            {
                track = new YARGMidiTrack(_data.Ptr + _position, length);
            }
            _position += length;
            return true;
        }

        public readonly Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        readonly IEnumerator<YARGMidiTrack> IEnumerable<YARGMidiTrack>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<YARGMidiTrack>
        {
            private YARGMidiFile _file;
            private YARGMidiTrack _current;
            public Enumerator(YARGMidiFile file)
            {
                _file = file;
                _current = default;
            }

            public readonly YARGMidiTrack Current => _current;

            readonly object IEnumerator.Current => _current;

            public bool MoveNext()
            {
                return _file.LoadNextTrack(out _current);
            }

            public readonly void Reset()
            {
                throw new NotImplementedException();
            }

            public readonly void Dispose()
            {
            }
        }
    }
}

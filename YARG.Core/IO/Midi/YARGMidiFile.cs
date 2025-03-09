using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.IO
{
    public ref struct YARGMidiFile
    {
        private const           int    TAG_SIZE             = sizeof(uint);
        private const           int    SIZEOF_HEADER        = 6;
        private const           int    DATA_OFFSET          = TAG_SIZE + sizeof(int);
        private const           int    FIRST_TRACK_POSITION = DATA_OFFSET + SIZEOF_HEADER;
        private static readonly FourCC HEADER_TAG           = new('M', 'T', 'h', 'd');
        private static readonly FourCC TRACK_TAG            = new('M', 'T', 'r', 'k');

        private FixedArray<byte> _data;
        private ushort _format;
        private ushort _numTracks;
        private ushort _resolution;

        private long _position;
        private ushort _trackNumber;

        public readonly ushort Format => _format;
        public readonly ushort NumTracks => _numTracks;
        public readonly ushort Resolution => _resolution;

        public static YARGMidiFile Load(in FixedArray<byte> data)
        {
            if (TAG_SIZE > data.Length
            || !HEADER_TAG.Matches(data.ReadonlySlice(0, TAG_SIZE)))
            {
                throw new Exception("Midi header Tag 'MThd' mismatch");
            }

            if (FIRST_TRACK_POSITION > data.Length)
            {
                throw new EndOfStreamException("Data ends within midi header");
            }

            // Track lengths are in big endian
            int headerSize =
                (data[TAG_SIZE] << 24) |
                (data[TAG_SIZE + 1] << 16) |
                (data[TAG_SIZE + 2] << 8) |
                 data[TAG_SIZE + 3];
            if (headerSize != SIZEOF_HEADER)
            {
                throw new Exception("Midi header of an unsupported length");
            }

            // These values reside at pre-defined offsets, so we can just use those offsets directly
            return new YARGMidiFile
            {
                _format = (ushort) ((data[DATA_OFFSET] << 8) | data[DATA_OFFSET + 1]),
                _numTracks = (ushort) ((data[DATA_OFFSET + 2] << 8) | data[DATA_OFFSET + 3]),
                _resolution = (ushort) ((data[DATA_OFFSET + 4] << 8) | data[DATA_OFFSET + 5]),
                _data = data,
                _position = FIRST_TRACK_POSITION,
                _trackNumber = 0,
            };
        }

        public bool GetNextTrack(out ushort trackNumber, out YARGMidiTrack track)
        {
            if (_trackNumber == _numTracks || _position == _data.Length)
            {
                trackNumber = _trackNumber;
                track = default;
                return false;
            }

            ++_trackNumber;
            if (_position + TAG_SIZE > _data.Length
                || !TRACK_TAG.Matches(_data.ReadonlySlice(_position, TAG_SIZE)))
            {
                throw new Exception("Midi Track Tag 'MTrk' mismatch");
            }
            _position += TAG_SIZE;

            if (_position + sizeof(int) > _data.Length)
            {
                throw new EndOfStreamException("End of stream found within midi track");
            }

            // Track lengths are in big endian
            int length =
                (_data[_position] << 24) |
                (_data[_position + 1] << 16) |
                (_data[_position + 2] << 8) |
                 _data[_position + 3];
            _position += sizeof(int);
            unsafe
            {
                track = new YARGMidiTrack(_data.Ptr + _position, length);
            }
            _position += length;
            trackNumber = _trackNumber;
            return true;
        }

        public void Reset()
        {
            _trackNumber = 0;
            _position = FIRST_TRACK_POSITION;
        }
    }
}

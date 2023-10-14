using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGMidiFile : IEnumerable<YARGMidiTrack>
    {
        internal static readonly byte[][] TRACKTAGS = { Encoding.ASCII.GetBytes("MThd"), Encoding.ASCII.GetBytes("MTrk") };

        private readonly Stream _stream;
        private readonly ushort _format;
        private readonly ushort _numTracks;
        private readonly ushort _tickRate;

        private ushort _trackNumber = 0;
        public ushort TrackNumber => _trackNumber;

        private const int SIZEOF_HEADER = 6;
        public YARGMidiFile(Stream stream)
        {
            _stream = stream;
            if (!TestTag(TRACKTAGS[0]))
                throw new Exception("Midi Header Chunk Tag 'MThd' not found");

            int length = stream.ReadInt32BE();
            if (length < SIZEOF_HEADER)
                throw new Exception("Midi Header not of sufficient length");

            YARGBinaryReader reader = new(stream, length);
            _format = reader.ReadUInt16(Endianness.BigEndian);
            _numTracks = reader.ReadUInt16(Endianness.BigEndian);
            _tickRate = reader.ReadUInt16(Endianness.BigEndian);
        }

        public YARGMidiFile(byte[] data) : this(new MemoryStream(data, 0, data.Length, false, true)) { }

        public YARGMidiFile(string path) : this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        public YARGMidiTrack? LoadNextTrack()
        {
            if (_trackNumber == _numTracks || _stream.Position == _stream.Length)
                return null;

            _trackNumber++;
            if (!TestTag(TRACKTAGS[1]))
                throw new Exception($"Midi Track Tag 'MTrk' not found for Track '{_trackNumber}'");

            return new YARGMidiTrack(_stream);
        }

        private bool TestTag(byte[] tag)
        {
            Span<byte> tagBuffer = stackalloc byte[4];
            _stream.Read(tagBuffer);
            return tagBuffer.SequenceEqual(tag);
        }

        public IEnumerator<YARGMidiTrack> GetEnumerator()
        {
            return new MidiFileEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public class MidiFileEnumerator : IEnumerator<YARGMidiTrack>
        {
            private readonly YARGMidiFile file;
            private YARGMidiTrack? _current;
            public MidiFileEnumerator(YARGMidiFile file)
            {
                this.file = file;
            }

            public YARGMidiTrack Current => _current!;

            object IEnumerator.Current => _current!;

            public bool MoveNext()
            {
                _current = file.LoadNextTrack();
                return _current != null;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                //throw new NotImplementedException();
            }
        }
    }
}

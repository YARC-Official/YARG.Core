using System;
using System.Buffers.Binary;
using System.IO;

namespace YARG.Core.Audio
{
    /// <summary>
    /// Reads an audio file's length straight from its container header, for formats where
    /// the header records it exactly. Opening the file through the audio backend instead
    /// (a full mixer, prescan and normalization) costs far more per song during a scan.
    /// </summary>
    /// <remarks>
    /// Returns false whenever the header can't give an exact answer -- callers then fall back
    /// to the backend. That covers MP3s without a Xing/Info/VBRI length header (a VBR file's
    /// length would only be an estimate), chained Ogg streams (the tail page only knows its
    /// own chain), and anything unrecognized, such as .mogg.
    /// </remarks>
    internal static class AudioLengthReader
    {
        // An Ogg page is at most 27 + 255 + 255 * 255 bytes, so the last page always starts
        // within this many bytes of the end.
        private const int OGG_TAIL_BYTES = 65307 + 1024;
        // How far past an ID3v2 tag to look for the first MPEG frame (some encoders pad).
        private const int MP3_SYNC_SEARCH_BYTES = 64 * 1024;

        public static bool TryGetExactLength(string path, out double seconds)
        {
            seconds = 0;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
                return TryGetExactLength(stream, out seconds);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Leaves the stream's position where it found it.</summary>
        public static bool TryGetExactLength(Stream stream, out double seconds)
        {
            seconds = 0;
            long origin = stream.Position;
            try
            {
                long start = SkipId3v2(stream, origin);
                Span<byte> magic = stackalloc byte[12];
                if (!ReadAt(stream, start, magic))
                {
                    return false;
                }

                bool found;
                if (Matches(magic.Slice(4, 4), "ftyp"))
                {
                    found = TryMp4(stream, start, out seconds);
                }
                else if (Matches(magic.Slice(0, 4), "RIFF") && Matches(magic.Slice(8, 4), "WAVE"))
                {
                    found = TryWav(stream, start, out seconds);
                }
                else if (Matches(magic.Slice(0, 4), "FORM") && (Matches(magic.Slice(8, 4), "AIFF") || Matches(magic.Slice(8, 4), "AIFC")))
                {
                    found = TryAiff(stream, start, out seconds);
                }
                else if (Matches(magic.Slice(0, 4), "fLaC"))
                {
                    found = TryFlac(stream, start, out seconds);
                }
                else if (Matches(magic.Slice(0, 4), "OggS"))
                {
                    found = TryOgg(stream, start, out seconds);
                }
                else
                {
                    found = TryMp3(stream, start, out seconds);
                }

                if (!found || !(seconds > 0) || double.IsInfinity(seconds))
                {
                    seconds = 0;
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                seconds = 0;
                return false;
            }
            finally
            {
                stream.Position = origin;
            }
        }

        #region MP4 / M4A

        // Duration of the first sound track: moov > trak > mdia > (hdlr "soun", mdhd).
        private static bool TryMp4(Stream stream, long start, out double seconds)
        {
            seconds = 0;
            return FindBox(stream, start, stream.Length, "moov", out long moovStart, out long moovEnd)
                && TryMp4Tracks(stream, moovStart, moovEnd, out seconds);
        }

        private static bool TryMp4Tracks(Stream stream, long moovStart, long moovEnd, out double seconds)
        {
            seconds = 0;
            long position = moovStart;
            while (NextBox(stream, ref position, moovEnd, out string type, out long bodyStart, out long bodyEnd))
            {
                if (type != "trak"
                    || !FindBox(stream, bodyStart, bodyEnd, "mdia", out long mdiaStart, out long mdiaEnd)
                    || !FindBox(stream, mdiaStart, mdiaEnd, "hdlr", out long hdlrStart, out _))
                {
                    continue;
                }

                // hdlr: version/flags (4), pre_defined (4), handler_type (4)
                Span<byte> handler = stackalloc byte[4];
                if (!ReadAt(stream, hdlrStart + 8, handler) || !Matches(handler, "soun"))
                {
                    continue;
                }

                return FindBox(stream, mdiaStart, mdiaEnd, "mdhd", out long mdhdStart, out _)
                    && TryMdhd(stream, mdhdStart, out seconds);
            }
            return false;
        }

        private static bool TryMdhd(Stream stream, long mdhdStart, out double seconds)
        {
            seconds = 0;
            Span<byte> header = stackalloc byte[32];
            if (!ReadAt(stream, mdhdStart, header.Slice(0, 1)))
            {
                return false;
            }

            uint timescale;
            ulong duration;
            if (header[0] == 1)
            {
                // version 1: flags (3), creation (8), modification (8), timescale (4), duration (8)
                if (!ReadAt(stream, mdhdStart, header))
                {
                    return false;
                }
                timescale = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(20));
                duration = BinaryPrimitives.ReadUInt64BigEndian(header.Slice(24));
                if (duration == ulong.MaxValue)
                {
                    return false;
                }
            }
            else
            {
                // version 0: flags (3), creation (4), modification (4), timescale (4), duration (4)
                if (!ReadAt(stream, mdhdStart, header.Slice(0, 20)))
                {
                    return false;
                }
                timescale = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(12));
                duration = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(16));
                if (duration == uint.MaxValue)
                {
                    return false;
                }
            }

            // A fragmented file stores 0 here and its real length in the fragments.
            if (timescale == 0 || duration == 0)
            {
                return false;
            }
            seconds = (double) duration / timescale;
            return true;
        }

        private static bool FindBox(Stream stream, long start, long end, string wanted, out long bodyStart, out long bodyEnd)
        {
            long position = start;
            while (NextBox(stream, ref position, end, out string type, out bodyStart, out bodyEnd))
            {
                if (type == wanted)
                {
                    return true;
                }
            }
            bodyStart = bodyEnd = 0;
            return false;
        }

        private static bool NextBox(Stream stream, ref long position, long end, out string type, out long bodyStart, out long bodyEnd)
        {
            type = string.Empty;
            bodyStart = bodyEnd = 0;
            Span<byte> header = stackalloc byte[16];
            if (position + 8 > end || !ReadAt(stream, position, header.Slice(0, 8)))
            {
                return false;
            }

            ulong size = BinaryPrimitives.ReadUInt32BigEndian(header);
            long headerSize = 8;
            if (size == 1)
            {
                if (!ReadAt(stream, position + 8, header.Slice(8, 8)))
                {
                    return false;
                }
                size = BinaryPrimitives.ReadUInt64BigEndian(header.Slice(8));
                headerSize = 16;
            }
            else if (size == 0)
            {
                size = (ulong) (end - position);
            }

            if (size < (ulong) headerSize || size > (ulong) (end - position))
            {
                return false;
            }

            type = AsAscii(header.Slice(4, 4));
            bodyStart = position + headerSize;
            bodyEnd = position + (long) size;
            position = bodyEnd;
            return true;
        }

        #endregion

        #region WAV / AIFF / FLAC

        private static bool TryWav(Stream stream, long start, out double seconds)
        {
            seconds = 0;
            Span<byte> chunk = stackalloc byte[16];
            long end = stream.Length;
            long position = start + 12;
            uint byteRate = 0;
            ushort format = 0;
            while (position + 8 <= end && ReadAt(stream, position, chunk.Slice(0, 8)))
            {
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(4));
                long body = position + 8;
                if (Matches(chunk.Slice(0, 4), "fmt "))
                {
                    if (size < 16 || !ReadAt(stream, body, chunk))
                    {
                        return false;
                    }
                    format = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                    byteRate = BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(8));
                }
                else if (Matches(chunk.Slice(0, 4), "data"))
                {
                    // Only uncompressed PCM/float has a fixed byte rate; a streamed file (size
                    // unknown) can claim more data than it holds.
                    const ushort PCM = 1, FLOAT = 3, EXTENSIBLE = 0xFFFE;
                    if (byteRate == 0 || (format != PCM && format != FLOAT && format != EXTENSIBLE) || size > end - body)
                    {
                        return false;
                    }
                    seconds = (double) size / byteRate;
                    return true;
                }
                position = body + size + (size & 1);
            }
            return false;
        }

        private static bool TryAiff(Stream stream, long start, out double seconds)
        {
            seconds = 0;
            Span<byte> chunk = stackalloc byte[18];
            long end = stream.Length;
            long position = start + 12;
            while (position + 8 <= end && ReadAt(stream, position, chunk.Slice(0, 8)))
            {
                uint size = BinaryPrimitives.ReadUInt32BigEndian(chunk.Slice(4));
                long body = position + 8;
                if (Matches(chunk.Slice(0, 4), "COMM"))
                {
                    // channels (2), sample frames (4), sample size (2), sample rate (80-bit float)
                    if (size < 18 || !ReadAt(stream, body, chunk))
                    {
                        return false;
                    }
                    uint frames = BinaryPrimitives.ReadUInt32BigEndian(chunk.Slice(2));
                    double rate = ReadExtended(chunk.Slice(8, 10));
                    if (!(rate > 0))
                    {
                        return false;
                    }
                    seconds = frames / rate;
                    return true;
                }
                position = body + size + (size & 1);
            }
            return false;
        }

        // IEEE 754 80-bit extended: sign + 15-bit exponent, then a 64-bit mantissa with an
        // explicit integer bit.
        private static double ReadExtended(ReadOnlySpan<byte> bytes)
        {
            int exponent = ((bytes[0] & 0x7F) << 8) | bytes[1];
            ulong mantissa = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(2));
            if (exponent == 0 && mantissa == 0)
            {
                return 0;
            }
            double value = mantissa * Math.Pow(2, exponent - 16383 - 63);
            return (bytes[0] & 0x80) != 0 ? -value : value;
        }

        private static bool TryFlac(Stream stream, long start, out double seconds)
        {
            seconds = 0;
            // "fLaC", then the mandatory STREAMINFO block: header (4), min/max block size (4),
            // min/max frame size (6), then 8 packed bytes -- 20 bits sample rate, 3 channels,
            // 5 bits-per-sample, 36 bits total samples.
            Span<byte> info = stackalloc byte[22];
            if (!ReadAt(stream, start + 4, info) || (info[0] & 0x7F) != 0)
            {
                return false;
            }

            var packed = info.Slice(14, 8);
            int rate = (packed[0] << 12) | (packed[1] << 4) | (packed[2] >> 4);
            ulong totalSamples = ((ulong) (packed[3] & 0x0F) << 32) | BinaryPrimitives.ReadUInt32BigEndian(packed.Slice(4));

            // A total of 0 means the encoder didn't know the length.
            if (rate == 0 || totalSamples == 0)
            {
                return false;
            }
            seconds = (double) totalSamples / rate;
            return true;
        }

        #endregion

        #region MP3

        private static bool TryMp3(Stream stream, long start, out double seconds)
        {
            seconds = 0;
            long limit = Math.Min(stream.Length, start + MP3_SYNC_SEARCH_BYTES);
            var window = new byte[(int) Math.Max(0, limit - start)];
            if (window.Length < 4 || !ReadAt(stream, start, window))
            {
                return false;
            }

            for (int i = 0; i + 4 <= window.Length; i++)
            {
                if (TryParseFrameHeader(window.AsSpan(i), out var frame))
                {
                    // Only the first frame can carry the length header; if it doesn't, the
                    // length is unknown without scanning every frame.
                    return TryXingOrVbri(stream, start + i, frame, out seconds);
                }
            }
            return false;
        }

        private struct Mp3Frame
        {
            public int SampleRate;
            public int SamplesPerFrame;
            public int SideInfoSize;
        }

        private static bool TryParseFrameHeader(ReadOnlySpan<byte> b, out Mp3Frame frame)
        {
            frame = default;
            if (b[0] != 0xFF || (b[1] & 0xE0) != 0xE0)
            {
                return false;
            }

            int version = (b[1] >> 3) & 3;      // 3 = MPEG1, 2 = MPEG2, 0 = MPEG2.5, 1 = reserved
            int layer = (b[1] >> 1) & 3;        // 1 = Layer III
            int bitrateIndex = b[2] >> 4;
            int rateIndex = (b[2] >> 2) & 3;
            if (version == 1 || layer != 1 || bitrateIndex == 0 || bitrateIndex == 15 || rateIndex == 3)
            {
                return false;
            }

            int baseRate = rateIndex switch { 0 => 44100, 1 => 48000, _ => 32000 };
            bool mpeg1 = version == 3;
            bool mono = (b[3] >> 6) == 3;
            frame.SampleRate = version switch { 3 => baseRate, 2 => baseRate / 2, _ => baseRate / 4 };
            frame.SamplesPerFrame = mpeg1 ? 1152 : 576;
            frame.SideInfoSize = mpeg1 ? (mono ? 17 : 32) : (mono ? 9 : 17);
            return true;
        }

        private static bool TryXingOrVbri(Stream stream, long frameStart, Mp3Frame frame, out double seconds)
        {
            seconds = 0;
            Span<byte> buffer = stackalloc byte[18];
            long xingStart = frameStart + 4 + frame.SideInfoSize;
            if (ReadAt(stream, xingStart, buffer.Slice(0, 8))
                && (Matches(buffer.Slice(0, 4), "Xing") || Matches(buffer.Slice(0, 4), "Info")))
            {
                uint flags = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4));
                const uint FRAMES = 1, BYTES = 2;
                if ((flags & FRAMES) == 0 || (flags & BYTES) == 0 || !ReadAt(stream, xingStart + 8, buffer.Slice(0, 8)))
                {
                    return false;
                }
                uint frames = BinaryPrimitives.ReadUInt32BigEndian(buffer);
                uint headerBytes = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4));
                if (!HeaderMatchesFile(stream, frameStart, frames, headerBytes, frame))
                {
                    return false;
                }
                // Every frame, as a prescan would count them -- encoder delay/padding (LAME tag)
                // is deliberately not trimmed, since that depends on the decoder.
                seconds = (double) frames * frame.SamplesPerFrame / frame.SampleRate;
                return true;
            }

            // VBRI: "VBRI", version (2), delay (2), quality (2), bytes (4), frames (4)
            long vbriStart = frameStart + 4 + 32;
            if (ReadAt(stream, vbriStart, buffer.Slice(0, 18)) && Matches(buffer.Slice(0, 4), "VBRI"))
            {
                uint headerBytes = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(10));
                uint frames = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(14));
                if (!HeaderMatchesFile(stream, frameStart, frames, headerBytes, frame))
                {
                    return false;
                }
                seconds = (double) frames * frame.SamplesPerFrame / frame.SampleRate;
                return true;
            }
            return false;
        }

        // The longest the header's byte count may disagree with the file before its frame
        // count is no longer trusted.
        private const double MP3_MAX_BYTE_MISMATCH_SECONDS = 0.1;

        /// <summary>
        /// A length header is written once, at encode time. If the file was cut or edited
        /// afterwards its frame count is stale, while counting the frames themselves (what the
        /// audio backend's prescan does) gives the real length. The header also records the
        /// stream's size in bytes, so a size that no longer matches the file -- by more than
        /// <see cref="MP3_MAX_BYTE_MISMATCH_SECONDS"/> of audio -- means the header can't be
        /// trusted.
        /// </summary>
        private static bool HeaderMatchesFile(Stream stream, long frameStart, uint frames, uint headerBytes, Mp3Frame frame)
        {
            if (frames == 0 || headerBytes == 0)
            {
                return false;
            }

            long audioBytes = TrailingTagStart(stream) - frameStart;
            double bytesPerSecond = headerBytes / ((double) frames * frame.SamplesPerFrame / frame.SampleRate);
            return Math.Abs(headerBytes - audioBytes) <= bytesPerSecond * MP3_MAX_BYTE_MISMATCH_SECONDS;
        }

        // Where the audio ends: before an ID3v1 tag and/or an APEv2 tag at the end of the file.
        private static long TrailingTagStart(Stream stream)
        {
            long end = stream.Length;
            Span<byte> footer = stackalloc byte[32];
            if (ReadAt(stream, end - 128, footer.Slice(0, 3)) && Matches(footer.Slice(0, 3), "TAG"))
            {
                end -= 128;
            }

            // APEv2 footer: "APETAGEX", version (4), size (4, excludes the header), items (4),
            // flags (4, bit 31 = a header is present), reserved (8)
            if (ReadAt(stream, end - 32, footer) && Matches(footer.Slice(0, 8), "APETAGEX"))
            {
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(footer.Slice(12));
                uint flags = BinaryPrimitives.ReadUInt32LittleEndian(footer.Slice(20));
                end -= size + ((flags & 0x80000000) != 0 ? 32 : 0);
            }
            return end;
        }

        #endregion

        #region Ogg

        private static bool TryOgg(Stream stream, long start, out double seconds)
        {
            seconds = 0;
            Span<byte> first = stackalloc byte[27 + 255];
            if (!ReadAt(stream, start, first.Slice(0, 27)))
            {
                return false;
            }
            uint serial = BinaryPrimitives.ReadUInt32LittleEndian(first.Slice(14));
            int segments = first[26];
            if (!ReadAt(stream, start + 27, first.Slice(27, segments)))
            {
                return false;
            }

            // The first packet identifies the codec.
            Span<byte> packet = stackalloc byte[19];
            if (!ReadAt(stream, start + 27 + segments, packet))
            {
                return false;
            }

            double rate;
            ulong preSkip = 0;
            if (packet[0] == 1 && Matches(packet.Slice(1, 6), "vorbis"))
            {
                rate = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(12));
            }
            else if (Matches(packet.Slice(0, 8), "OpusHead"))
            {
                rate = 48000;
                preSkip = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(10));
            }
            else
            {
                return false;
            }

            if (!TryLastPage(stream, start, out uint lastSerial, out ulong granule)
                || lastSerial != serial || granule == ulong.MaxValue || granule <= preSkip || !(rate > 0))
            {
                // A different serial at the end means a chained file.
                return false;
            }

            seconds = (granule - preSkip) / rate;
            return true;
        }

        private static bool TryLastPage(Stream stream, long start, out uint serial, out ulong granule)
        {
            serial = 0;
            granule = 0;
            long end = stream.Length;
            long tailStart = Math.Max(start, end - OGG_TAIL_BYTES);
            var tail = new byte[(int) (end - tailStart)];
            if (!ReadAt(stream, tailStart, tail))
            {
                return false;
            }

            // The last capture pattern whose page fits before the end of the file.
            for (int i = tail.Length - 27; i >= 0; i--)
            {
                if (tail[i] != (byte) 'O' || tail[i + 1] != (byte) 'g' || tail[i + 2] != (byte) 'g' || tail[i + 3] != (byte) 'S' || tail[i + 4] != 0)
                {
                    continue;
                }

                int segments = tail[i + 26];
                if (i + 27 + segments > tail.Length)
                {
                    continue;
                }
                int bodySize = 0;
                for (int s = 0; s < segments; s++)
                {
                    bodySize += tail[i + 27 + s];
                }
                if (i + 27 + segments + bodySize > tail.Length)
                {
                    continue;
                }

                granule = BinaryPrimitives.ReadUInt64LittleEndian(tail.AsSpan(i + 6));
                serial = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i + 14));
                return true;
            }
            return false;
        }

        #endregion

        #region Helpers

        // An ID3v2 tag can precede MP3 (and occasionally other) audio.
        private static long SkipId3v2(Stream stream, long start)
        {
            Span<byte> header = stackalloc byte[10];
            if (!ReadAt(stream, start, header) || !Matches(header.Slice(0, 3), "ID3"))
            {
                return start;
            }
            int size = (header[6] & 0x7F) << 21 | (header[7] & 0x7F) << 14 | (header[8] & 0x7F) << 7 | (header[9] & 0x7F);
            bool footer = (header[5] & 0x10) != 0;
            return start + 10 + size + (footer ? 10 : 0);
        }

        private static bool ReadAt(Stream stream, long position, Span<byte> buffer)
        {
            if (position < 0 || position + buffer.Length > stream.Length)
            {
                return false;
            }

            stream.Position = position;
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer.Slice(total));
                if (read <= 0)
                {
                    return false;
                }
                total += read;
            }
            return true;
        }

        private static bool Matches(ReadOnlySpan<byte> bytes, string ascii)
        {
            if (bytes.Length != ascii.Length)
            {
                return false;
            }
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != ascii[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static string AsAscii(ReadOnlySpan<byte> bytes)
        {
            Span<char> chars = stackalloc char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i] = (char) bytes[i];
            }
            return chars.ToString();
        }

        #endregion
    }
}

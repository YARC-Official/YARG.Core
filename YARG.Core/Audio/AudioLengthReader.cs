using System;
using System.Buffers;
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
    /// to the backend. That covers MP3s without a Xing/Info length header (a VBR file's
    /// length would only be an estimate), chained Ogg streams (the tail page only knows its
    /// own chain), and anything unrecognized, such as AIFF or .mogg.
    /// </remarks>
    internal static class AudioLengthReader
    {
        // An Ogg page is at most 27 + 255 + 255 * 255 bytes, so the last page always starts
        // within this many bytes of the end.
        private const int OGG_TAIL_BYTES = 65307 + 1024;
        // How far past an ID3v2 tag to look for the first MPEG frame (some encoders pad).
        private const int MP3_SYNC_SEARCH_BYTES = 64 * 1024;
        // The first read of a search window. The MP3 sync and the final Ogg page are almost
        // always within it, so the full window is read only when they aren't.
        private const int FIRST_READ_BYTES = 4096;

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

        #region WAV / FLAC

        private static bool TryWav(Stream stream, long start, out double seconds)
        {
            seconds = 0;
            Span<byte> fmt = stackalloc byte[16];
            long position = start + 12;
            if (!FindChunk(stream, ref position, "fmt ", out long fmtBody, out uint fmtSize)
                || fmtSize < 16 || !ReadAt(stream, fmtBody, fmt)
                || !FindChunk(stream, ref position, "data", out long dataBody, out uint dataSize))
            {
                return false;
            }

            ushort format = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
            uint byteRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt.Slice(8));

            // Only uncompressed PCM/float has a fixed byte rate; a streamed file (size
            // unknown) can claim more data than it holds.
            const ushort PCM = 1, FLOAT = 3, EXTENSIBLE = 0xFFFE;
            if (byteRate == 0 || (format != PCM && format != FLOAT && format != EXTENSIBLE) || dataSize > stream.Length - dataBody)
            {
                return false;
            }
            seconds = (double) dataSize / byteRate;
            return true;
        }

        // Walks RIFF chunks from `position` to the first one named `id`, leaving `position` just past it.
        private static bool FindChunk(Stream stream, ref long position, string id, out long body, out uint size)
        {
            Span<byte> header = stackalloc byte[8];
            while (position + 8 <= stream.Length && ReadAt(stream, position, header))
            {
                size = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4));
                body = position + 8;
                position = body + size + (size & 1);
                if (Matches(header.Slice(0, 4), id))
                {
                    return true;
                }
            }
            body = 0;
            size = 0;
            return false;
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
            int window = (int) Math.Min(Math.Max(0, stream.Length - start), MP3_SYNC_SEARCH_BYTES);
            if (window < 4)
            {
                return false;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(window);
            try
            {
                int read = Math.Min(window, FIRST_READ_BYTES);
                if (!ReadAt(stream, start, buffer.AsSpan(0, read)))
                {
                    return false;
                }

                int searched = 0;
                while (true)
                {
                    for (int i = searched; i + 4 <= read; i++)
                    {
                        if (TryParseFrameHeader(buffer.AsSpan(i, 4), out var frame))
                        {
                            // Only the first frame can carry the length header; if it doesn't,
                            // the length is unknown without scanning every frame.
                            return TryXingHeader(stream, start + i, frame, out seconds);
                        }
                    }

                    if (read == window || !ReadAt(stream, start + read, buffer.AsSpan(read, window - read)))
                    {
                        return false;
                    }
                    // Resume where the last pass stopped: a header may straddle the old end.
                    searched = read - 3;
                    read = window;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
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

        // Xing/Info: tag (4), flags (4), then frames (4) and bytes (4) when their flags are set.
        private static bool TryXingHeader(Stream stream, long frameStart, Mp3Frame frame, out double seconds)
        {
            seconds = 0;
            Span<byte> buffer = stackalloc byte[16];
            const uint FRAMES = 1, BYTES = 2;
            if (!ReadAt(stream, frameStart + 4 + frame.SideInfoSize, buffer)
                || !(Matches(buffer.Slice(0, 4), "Xing") || Matches(buffer.Slice(0, 4), "Info"))
                || (BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4)) & (FRAMES | BYTES)) != (FRAMES | BYTES))
            {
                return false;
            }

            uint frames = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(8));
            uint headerBytes = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(12));

            if (!HeaderMatchesFile(stream, frameStart, frames, headerBytes, frame))
            {
                return false;
            }
            // Every frame, as a prescan would count them. Encoder delay/padding (LAME tag) is
            // deliberately not trimmed, since that depends on the decoder.
            seconds = (double) frames * frame.SamplesPerFrame / frame.SampleRate;
            return true;
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

        // Where the audio ends: before an ID3v1 tag, if any. Any other trailing tag (e.g. APEv2)
        // makes the byte count disagree, so the caller falls back.
        private static long TrailingTagStart(Stream stream)
        {
            Span<byte> tag = stackalloc byte[3];
            long end = stream.Length;
            return ReadAt(stream, end - 128, tag) && Matches(tag, "TAG") ? end - 128 : end;
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
            long available = stream.Length - start;
            return TryLastPage(stream, (int) Math.Min(available, FIRST_READ_BYTES), out serial, out granule)
                || (available > FIRST_READ_BYTES
                    && TryLastPage(stream, (int) Math.Min(available, OGG_TAIL_BYTES), out serial, out granule));
        }

        // Searches the file's last `tailLength` bytes for the final page: the last capture
        // pattern whose page ends exactly at the end of the file.
        private static bool TryLastPage(Stream stream, int tailLength, out uint serial, out ulong granule)
        {
            serial = 0;
            granule = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(tailLength);
            try
            {
                var tail = buffer.AsSpan(0, tailLength);
                return ReadAt(stream, stream.Length - tailLength, tail) && TryFindLastPage(tail, out serial, out granule);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static bool TryFindLastPage(ReadOnlySpan<byte> tail, out uint serial, out ulong granule)
        {
            serial = 0;
            granule = 0;
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
                if (i + 27 + segments + bodySize != tail.Length)
                {
                    continue;
                }

                granule = BinaryPrimitives.ReadUInt64LittleEndian(tail.Slice(i + 6));
                serial = BinaryPrimitives.ReadUInt32LittleEndian(tail.Slice(i + 14));
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

using System.Buffers.Binary;
using System.Text;
using NUnit.Framework;
using YARG.Core.Audio;

namespace YARG.Core.UnitTests.Audio;

public class AudioLengthReaderTests
{
    #region Fixture builders

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    private static byte[] U32Be(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); return b; }
    private static byte[] U64Be(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(b, v); return b; }
    private static byte[] U16Be(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, v); return b; }
    private static byte[] U32Le(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }
    private static byte[] U64Le(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, v); return b; }
    private static byte[] U16Le(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); return b; }

    private static double Length(byte[] file)
    {
        using var stream = new MemoryStream(file);
        Assert.That(AudioLengthReader.TryGetExactLength(stream, out double seconds), Is.True, "expected an exact length");
        return seconds;
    }

    private static bool HasLength(byte[] file)
    {
        using var stream = new MemoryStream(file);
        return AudioLengthReader.TryGetExactLength(stream, out _);
    }

    // ---- MP4 ----

    private static byte[] Box(string type, params byte[][] body)
    {
        var content = Concat(body);
        return Concat(U32Be((uint) (8 + content.Length)), Ascii(type), content);
    }

    private static byte[] LargeBox(string type, byte[] content)
        => Concat(U32Be(1), Ascii(type), U64Be((ulong) (16 + content.Length)), content);

    private static byte[] Track(string handler, uint timescale, ulong duration, bool version1 = false)
    {
        var mdhd = version1
            ? Box("mdhd", new byte[] { 1, 0, 0, 0 }, U64Be(0), U64Be(0), U32Be(timescale), U64Be(duration), new byte[4])
            : Box("mdhd", new byte[] { 0, 0, 0, 0 }, U32Be(0), U32Be(0), U32Be(timescale), U32Be((uint) duration), new byte[4]);
        var hdlr = Box("hdlr", new byte[4], new byte[4], Ascii(handler), new byte[12], new byte[1]);
        return Box("trak", Box("tkhd", new byte[84]), Box("mdia", mdhd, hdlr));
    }

    private static byte[] Ftyp() => Box("ftyp", Ascii("M4A "), U32Be(0), Ascii("isomM4A "));

    // ---- MP3 ----

    // MPEG-1 Layer III, 128 kbps, 44.1 kHz, stereo: 32 bytes of side info.
    private static readonly byte[] MPEG1_FRAME_HEADER = { 0xFF, 0xFB, 0x90, 0x00 };
    // MPEG-2 Layer III, 64 kbps, 22.05 kHz, stereo: 17 bytes of side info.
    private static readonly byte[] MPEG2_FRAME_HEADER = { 0xFF, 0xF3, 0x80, 0x00 };

    /// <summary>A first frame carrying <paramref name="lengthHeader"/>, padded to <paramref name="streamBytes"/>.</summary>
    private static byte[] Mp3(byte[] frameHeader, int sideInfo, byte[] lengthHeader, int streamBytes, byte[]? prefix = null, byte[]? suffix = null)
    {
        var stream = new byte[streamBytes];
        frameHeader.CopyTo(stream, 0);
        lengthHeader.CopyTo(stream, 4 + sideInfo);
        return Concat(prefix ?? Array.Empty<byte>(), stream, suffix ?? Array.Empty<byte>());
    }

    private static byte[] Xing(string tag, uint frames, uint bytes)
        => Concat(Ascii(tag), U32Be(0x3), U32Be(frames), U32Be(bytes));

    private static byte[] Id3v2(int size)
        => Concat(Ascii("ID3"), new byte[] { 3, 0, 0 },
            new[] { (byte) ((size >> 21) & 0x7F), (byte) ((size >> 14) & 0x7F), (byte) ((size >> 7) & 0x7F), (byte) (size & 0x7F) },
            new byte[size]);

    // ---- Ogg ----

    private static byte[] OggPage(uint serial, ulong granule, byte[] body)
        => Concat(Ascii("OggS"), new byte[] { 0, 0 }, U64Le(granule), U32Le(serial), U32Le(0), U32Le(0),
            new[] { (byte) 1, (byte) body.Length }, body);

    private static byte[] VorbisId(uint rate)
        => Concat(new byte[] { 1 }, Ascii("vorbis"), U32Le(0), new byte[] { 2 }, U32Le(rate), new byte[16]);

    private static byte[] OpusHead(ushort preSkip)
        => Concat(Ascii("OpusHead"), new byte[] { 1, 2 }, U16Le(preSkip), U32Le(48000), new byte[3]);

    #endregion

    [Test]
    public void Mp4WithMoovBeforeMdat()
    {
        var file = Concat(Ftyp(), Box("moov", Track("vide", 600, 1), Track("soun", 44100, 44100 * 180 + 2112)), Box("mdat", new byte[64]));
        Assert.That(Length(file), Is.EqualTo((44100.0 * 180 + 2112) / 44100).Within(1e-9));
    }

    [Test]
    public void Mp4WithMoovAfterALargeMdat()
    {
        var file = Concat(Ftyp(), LargeBox("mdat", new byte[64]), Box("moov", Track("soun", 48000, 48000 * 3)));
        Assert.That(Length(file), Is.EqualTo(3.0).Within(1e-9));
    }

    [Test]
    public void Mp4WithVersion1MediaHeader()
    {
        var file = Concat(Ftyp(), Box("moov", Track("soun", 1000, 7500, version1: true)));
        Assert.That(Length(file), Is.EqualTo(7.5).Within(1e-9));
    }

    [Test]
    public void Mp4WithoutASoundTrackFallsBack()
    {
        var file = Concat(Ftyp(), Box("moov", Track("vide", 600, 6000)));
        Assert.That(HasLength(file), Is.False);
    }

    [Test]
    public void FragmentedMp4FallsBack()
    {
        // Fragmented files record 0 here and the real length in their fragments.
        var file = Concat(Ftyp(), Box("moov", Track("soun", 44100, 0)));
        Assert.That(HasLength(file), Is.False);
    }

    [Test]
    public void Wav()
    {
        var fmt = Concat(U16Le(1), U16Le(1), U32Le(8000), U32Le(8000), U16Le(1), U16Le(8));
        var data = new byte[12000];
        var body = Concat(Ascii("WAVE"), Ascii("fmt "), U32Le(16), fmt, Ascii("data"), U32Le((uint) data.Length), data);
        var file = Concat(Ascii("RIFF"), U32Le((uint) body.Length), body);
        Assert.That(Length(file), Is.EqualTo(1.5).Within(1e-9));
    }

    [Test]
    public void CompressedWavFallsBack()
    {
        // ADPCM has no fixed byte rate per sample frame worth trusting here.
        var fmt = Concat(U16Le(2), U16Le(1), U32Le(8000), U32Le(4000), U16Le(256), U16Le(4));
        var body = Concat(Ascii("WAVE"), Ascii("fmt "), U32Le(16), fmt, Ascii("data"), U32Le(4), new byte[4]);
        Assert.That(HasLength(Concat(Ascii("RIFF"), U32Le((uint) body.Length), body)), Is.False);
    }

    [Test]
    public void Aiff()
    {
        // 8000 Hz as an 80-bit extended float: exponent 16383 + 12, mantissa 8000 << 51.
        var rate = Concat(U16Be(16383 + 12), U64Be(8000UL << 51));
        var comm = Concat(U16Be(1), U32Be(12000), U16Be(8), rate);
        var body = Concat(Ascii("AIFF"), Ascii("COMM"), U32Be((uint) comm.Length), comm);
        Assert.That(Length(Concat(Ascii("FORM"), U32Be((uint) body.Length), body)), Is.EqualTo(1.5).Within(1e-9));
    }

    [Test]
    public void Flac()
    {
        // 20 bits rate | 3 bits channels-1 | 5 bits bps-1 | 36 bits total samples
        ulong packed = (44100UL << 44) | (1UL << 41) | (15UL << 36) | 441000UL;
        var streamInfo = Concat(new byte[10], U64Be(packed), new byte[16]);
        var file = Concat(Ascii("fLaC"), new byte[] { 0x80, 0, 0, 34 }, streamInfo);
        Assert.That(Length(file), Is.EqualTo(10.0).Within(1e-9));
    }

    [Test]
    public void FlacWithUnknownTotalFallsBack()
    {
        ulong packed = (44100UL << 44) | (1UL << 41) | (15UL << 36);
        var file = Concat(Ascii("fLaC"), new byte[] { 0x80, 0, 0, 34 }, new byte[10], U64Be(packed), new byte[16]);
        Assert.That(HasLength(file), Is.False);
    }

    [TestCase("Xing", TestName = "MP3 with a Xing header")]
    [TestCase("Info", TestName = "MP3 with an Info header (CBR)")]
    public void Mp3WithLengthHeader(string tag)
    {
        var file = Mp3(MPEG1_FRAME_HEADER, 32, Xing(tag, 1000, 4096), 4096);
        Assert.That(Length(file), Is.EqualTo(1000.0 * 1152 / 44100).Within(1e-9));
    }

    [Test]
    public void Mp3AfterAnId3v2TagAndBeforeAnId3v1Tag()
    {
        var id3v1 = Concat(Ascii("TAG"), new byte[125]);
        var file = Mp3(MPEG1_FRAME_HEADER, 32, Xing("Xing", 1000, 4096), 4096, Id3v2(300), id3v1);
        Assert.That(Length(file), Is.EqualTo(1000.0 * 1152 / 44100).Within(1e-9));
    }

    [Test]
    public void Mpeg2Mp3()
    {
        var file = Mp3(MPEG2_FRAME_HEADER, 17, Xing("Xing", 2000, 4096), 4096);
        Assert.That(Length(file), Is.EqualTo(2000.0 * 576 / 22050).Within(1e-9));
    }

    [Test]
    public void Mp3WithVbriHeader()
    {
        var vbri = Concat(Ascii("VBRI"), U16Be(1), U16Be(0), U16Be(75), U32Be(4096), U32Be(1000));
        var file = Mp3(MPEG1_FRAME_HEADER, 32, vbri, 4096);
        Assert.That(Length(file), Is.EqualTo(1000.0 * 1152 / 44100).Within(1e-9));
    }

    [Test]
    public void Mp3WithoutALengthHeaderFallsBack()
    {
        // Without a header a VBR file's length is only an estimate.
        Assert.That(HasLength(Mp3(MPEG1_FRAME_HEADER, 32, Array.Empty<byte>(), 4096)), Is.False);
    }

    [Test]
    public void Mp3WhoseHeaderNoLongerMatchesTheFileFallsBack()
    {
        // The header claims far more audio than the file holds -- it was cut after encoding,
        // so its frame count is stale.
        Assert.That(HasLength(Mp3(MPEG1_FRAME_HEADER, 32, Xing("Info", 10030, 9629760), 4096)), Is.False);
    }

    [Test]
    public void OggVorbis()
    {
        var file = Concat(OggPage(7, 0, VorbisId(44100)), OggPage(7, 44100UL * 12, new byte[20]));
        Assert.That(Length(file), Is.EqualTo(12.0).Within(1e-9));
    }

    [Test]
    public void OggOpusSubtractsPreSkip()
    {
        var file = Concat(OggPage(9, 0, OpusHead(312)), OggPage(9, 48000UL * 5 + 312, new byte[20]));
        Assert.That(Length(file), Is.EqualTo(5.0).Within(1e-9));
    }

    [Test]
    public void ChainedOggFallsBack()
    {
        // The last page belongs to a different chained stream, so its granule only covers
        // that chain.
        var file = Concat(OggPage(7, 0, VorbisId(44100)), OggPage(7, 44100UL * 12, new byte[20]),
            OggPage(8, 0, VorbisId(44100)), OggPage(8, 44100UL * 3, new byte[20]));
        Assert.That(HasLength(file), Is.False);
    }

    [Test]
    public void MoggFallsBack()
    {
        // A .mogg starts with its own header, with the Ogg data further in.
        var file = Concat(new byte[] { 0x0A, 0, 0, 0 }, new byte[60], OggPage(7, 0, VorbisId(44100)), OggPage(7, 44100, new byte[20]));
        Assert.That(HasLength(file), Is.False);
    }

    [TestCase(0, TestName = "An empty file falls back")]
    [TestCase(10, TestName = "A truncated header falls back")]
    public void TruncatedFilesFallBack(int length)
    {
        var whole = Concat(Ftyp(), Box("moov", Track("soun", 44100, 44100)));
        Assert.That(HasLength(whole.Take(length).ToArray()), Is.False);
    }

    [Test]
    public void LeavesTheStreamPositionUnchanged()
    {
        var file = Concat(Ftyp(), Box("moov", Track("soun", 44100, 44100)));
        using var stream = new MemoryStream(file) { Position = 5 };

        AudioLengthReader.TryGetExactLength(stream, out _);

        Assert.That(stream.Position, Is.EqualTo(5));
    }
}

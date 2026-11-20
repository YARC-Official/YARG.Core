using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using ChartFormat = YARG.Core.Song.ChartFormat;

namespace YARG.Core.UnitTests.Song;

/// <summary>
/// Ini songs without a song_length get their length from their audio stems at scan time.
/// The test audio backend can't open anything, so a non-zero length can only have come
/// from the stems' own headers.
/// </summary>
public class IniSongLengthTests
{
    private string _songDir = null!;

    [SetUp]
    public void SetUp()
    {
        GlobalAudioHandler.Initialize<NullAudioManager>();
        _songDir = Path.Combine(Path.GetTempPath(), $"yarg-ini-length-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_songDir);
        File.Copy(TestMidiPath(), Path.Combine(_songDir, "notes.mid"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_songDir))
        {
            Directory.Delete(_songDir, true);
        }
    }

    [Test]
    public void LengthIsTheLongestStemWhenEveryStemHasAnExactHeader()
    {
        WriteWav("song.wav", seconds: 1.5);
        WriteWav("guitar.wav", seconds: 2.0);

        Assert.That(ScanLength("[song]\nname = Test\n"), Is.EqualTo(2000));
    }

    [Test]
    public void ExplicitStemsCountTowardTheLength()
    {
        // The scan loads its mixer with censoring off, which includes the explicit stems.
        WriteWav("song.wav", seconds: 1.5);
        WriteWav("vocals_explicit.wav", seconds: 3.0);

        Assert.That(ScanLength("[song]\nname = Test\n"), Is.EqualTo(3000));
    }

    [Test]
    public void FallsBackToTheMixerWhenAStemHasNoExactHeader()
    {
        WriteWav("song.wav", seconds: 1.5);
        File.WriteAllBytes(Path.Combine(_songDir, "guitar.ogg"), new byte[] { 0x00 });

        Assert.That(ScanLength("[song]\nname = Test\n"), Is.EqualTo(0));
    }

    [Test]
    public void IniSongLengthStillWins()
    {
        WriteWav("song.wav", seconds: 1.5);

        Assert.That(ScanLength("[song]\nname = Test\nsong_length = 128000\n"), Is.EqualTo(128000));
    }

    private long ScanLength(string ini)
    {
        string iniPath = Path.Combine(_songDir, "song.ini");
        File.WriteAllText(iniPath, ini);

        var result = UnpackedIniEntry.ProcessNewEntry(_songDir, new FileInfo(Path.Combine(_songDir, "notes.mid")), ChartFormat.Mid,
            new FileInfo(iniPath), "", new FileCollection(new DirectoryInfo(_songDir)));
        Assert.That(result.HasValue, Is.True, $"Expected the scan to succeed, but got {result.Error}.");
        return result.Value.SongLengthMilliseconds;
    }

    // 8 kHz mono 8-bit PCM: 8000 bytes per second.
    private void WriteWav(string fileName, double seconds)
    {
        var data = new byte[(int) (8000 * seconds)];
        using var stream = File.Create(Path.Combine(_songDir, fileName));
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF".ToCharArray());
        writer.Write(4 + 8 + 16 + 8 + data.Length);
        writer.Write("WAVEfmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short) 1);    // PCM
        writer.Write((short) 1);    // mono
        writer.Write(8000);         // sample rate
        writer.Write(8000);         // byte rate
        writer.Write((short) 1);    // block align
        writer.Write((short) 8);    // bits per sample
        writer.Write("data".ToCharArray());
        writer.Write(data.Length);
        writer.Write(data);
    }

    private static string TestMidiPath()
    {
        string path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../Parsing/Test Charts/test.mid"));
        Assert.That(File.Exists(path), Is.True, $"Expected test MIDI fixture at {path}.");
        return path;
    }
}

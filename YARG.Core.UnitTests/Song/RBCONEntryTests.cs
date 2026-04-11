using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class RBCONEntryTests
{
    [Test]
    public void Create_AppliesFourLaneLeadVocalAndBandIntensities()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUnpackedEntry(
                root,
                "testsong",
                """
                (testsong
                  (name "Test Song")
                  (song
                    (name "songs/testsong/testsong")
                    (pans (0.0))
                    (vols (0.0))
                    (cores (0.0))
                  )
                  (rank
                    (drum 178)
                    (vocals 221)
                    (band 243)
                  )
                )
                """
            );

            using (Assert.EnterMultipleScope())
            {
                Assert.That(entry[Instrument.FourLaneDrums].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.ProDrums].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.Vocals].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.Harmony].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.Band].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.Band].IsActive(), Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Test]
    public void Create_AppliesProInstrumentIntensitiesAndBackfillsFallbackParts()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUnpackedEntry(
                root,
                "testsong",
                """
                (testsong
                  (name "Test Song")
                  (song
                    (name "songs/testsong/testsong")
                    (pans (0.0))
                    (vols (0.0))
                    (cores (0.0))
                  )
                  (rank
                    (real_guitar 264)
                    (real_bass 323)
                    (real_keys 269)
                    (real_drums 242)
                    (harmVocals 178)
                  )
                )
                """
            );

            using (Assert.EnterMultipleScope())
            {
                Assert.That(entry[Instrument.ProGuitar_17Fret].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.ProGuitar_22Fret].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.FiveFretGuitar].Intensity, Is.EqualTo(3));

                Assert.That(entry[Instrument.ProBass_17Fret].Intensity, Is.EqualTo(4));
                Assert.That(entry[Instrument.ProBass_22Fret].Intensity, Is.EqualTo(4));
                Assert.That(entry[Instrument.FiveFretBass].Intensity, Is.EqualTo(4));

                Assert.That(entry[Instrument.ProKeys].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.Keys].Intensity, Is.EqualTo(3));

                Assert.That(entry[Instrument.ProDrums].Intensity, Is.EqualTo(4));
                Assert.That(entry[Instrument.FourLaneDrums].Intensity, Is.EqualTo(4));

                Assert.That(entry[Instrument.Harmony].Intensity, Is.EqualTo(3));
                Assert.That(entry[Instrument.Vocals].Intensity, Is.EqualTo(3));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Test]
    public void GetLastWriteTime_ReturnsMostRecentValueAcrossBaseUpdateAndUpgrade()
    {
        var baseMidi = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var updateMidi = new DateTime(2024, 01, 03, 0, 0, 0, DateTimeKind.Utc);
        var upgradeMidi = new DateTime(2024, 01, 05, 0, 0, 0, DateTimeKind.Utc);

        var entry = new TestRBCONEntry();
        entry.SetMidiLastWriteTime(baseMidi);
        entry.UpdateInfo(null, updateMidi, new TestRBProUpgrade(upgradeMidi));

        Assert.That(entry.GetLastWriteTime(), Is.EqualTo(upgradeMidi));
    }

    [Test]
    public void GetLastWriteTime_ReturnsBaseMidiTimeWhenItIsLatest()
    {
        var baseMidi = new DateTime(2024, 01, 05, 0, 0, 0, DateTimeKind.Utc);
        var updateMidi = new DateTime(2024, 01, 03, 0, 0, 0, DateTimeKind.Utc);
        var upgradeMidi = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        var entry = new TestRBCONEntry();
        entry.SetMidiLastWriteTime(baseMidi);
        entry.UpdateInfo(null, updateMidi, new TestRBProUpgrade(upgradeMidi));

        Assert.That(entry.GetLastWriteTime(), Is.EqualTo(baseMidi));
    }

    private static RBCONEntry CreateUnpackedEntry(string root, string nodeName, string dtaText)
    {
        string songDirectory = Path.Combine(root, nodeName);
        Directory.CreateDirectory(songDirectory);

        string midiPath = Path.Combine(songDirectory, $"{nodeName}.mid");
        File.Copy(GetTestMidiPath(), midiPath);

        string moggPath = Path.Combine(songDirectory, $"{nodeName}.mogg");
        using (var mogg = new FileStream(moggPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            mogg.Write(RBCONEntry.UNENCRYPTED_MOGG, Endianness.Little);
        }

        byte[] bytes = YARGTextReader.UTF8Strict.GetBytes(dtaText);
        using var buffer = FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(buffer.Span);

        var container = YARGDTAReader.Create(buffer);
        Assert.That(YARGDTAReader.StartNode(ref container), Is.True);
        string parsedNodeName = YARGDTAReader.GetNameOfNode(ref container, false);
        var dta = DTAEntry.Create(parsedNodeName, container);

        var parameters = new RBScanParameters
        {
            Root = new AbridgedFileInfo(root, DateTime.UnixEpoch),
            NodeName = nodeName,
            DefaultPlaylist = "Default Playlist",
            BaseDta = dta,
            UpdateDta = DTAEntry.Empty,
            UpgradeDta = DTAEntry.Empty,
            UpdateDirectory = null,
            UpdateMidi = null,
            Upgrade = null,
        };

        var result = UnpackedRBCONEntry.Create(in parameters);
        Assert.That(result.HasValue, Is.True, $"Expected RBCON creation to succeed, but got {result.Error}.");
        return result.Value;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"yarg-rbcon-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetTestMidiPath()
    {
        string path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../Parsing/Test Charts/test.mid"));
        Assert.That(File.Exists(path), Is.True, $"Expected test MIDI fixture at {path}.");
        return path;
    }

    private sealed class TestRBCONEntry : RBCONEntry
    {
        private DateTime _midiLastWriteTime = DateTime.UnixEpoch;

        public TestRBCONEntry()
            : base(new AbridgedFileInfo("test-root", DateTime.UnixEpoch), "test-node")
        {
        }

        public override EntryType SubType => EntryType.CON;

        public override string SortBasedLocation => "test-node";

        public override string ActualLocation => "test-root";

        protected override DateTime MidiLastWriteTime => _midiLastWriteTime;

        public void SetMidiLastWriteTime(DateTime value)
        {
            _midiLastWriteTime = value;
        }

        protected override FixedArray<byte>? GetMainMidiData() => null;

        protected override Stream? GetMoggStream() => null;

        public override YARGImage? LoadAlbumData() => null;

        public override BackgroundResult? LoadBackground() => null;

        public override FixedArray<byte>? LoadMiloData() => null;
    }

    private sealed class TestRBProUpgrade : RBProUpgrade
    {
        private readonly DateTime _lastWriteTime;

        public TestRBProUpgrade(DateTime lastWriteTime)
            : base(new AbridgedFileInfo("test-upgrade-root", DateTime.UnixEpoch))
        {
            _lastWriteTime = lastWriteTime;
        }

        public override DateTime LastWriteTime => _lastWriteTime;

        public override FixedArray<byte>? LoadUpgradeMidi() => null;
    }
}

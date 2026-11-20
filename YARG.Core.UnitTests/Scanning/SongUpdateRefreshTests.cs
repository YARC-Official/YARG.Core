using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.Song;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Scanning;

[TestFixture]
[NonParallelizable]
public sealed class SongUpdateRefreshTests
{
    private string _root = null!;
    private string _library = null!;
    private string _updateDta = null!;
    private string CachePath => Path.Combine(_root, "songcache.bin");

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "yarg-update-refresh-" + Guid.NewGuid().ToString("N"));
        _library = Path.Combine(_root, "library");
        string songs = Path.Combine(_library, "songs");
        Directory.CreateDirectory(songs);
        File.WriteAllText(Path.Combine(songs, "songs.dta"), Dta("first", "Base First") + Dta("second", "Base Second"));
        WriteSongFiles(songs, "first", 96);
        WriteSongFiles(songs, "second", 97);

        string updates = Path.Combine(_library, "package", "songs_updates");
        Directory.CreateDirectory(updates);
        _updateDta = Path.Combine(updates, RBCONEntry.SONGUPDATES_DTA);
        File.WriteAllText(_updateDta, Dta("first", "Before"));
        File.SetLastWriteTimeUtc(_updateDta, DateTime.UtcNow.AddMinutes(1));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    // Cover both editing an existing update and replacing it with an update for another song.
    // The first full refresh must apply the current DTA; subsequent scans must retain it.
    [TestCase("first")]
    [TestCase("second")]
    public void ChangedUpdateDtaIsAppliedOnFirstFullScan(string updatedSong)
    {
        Assert.That(ScanNames(false), Is.EquivalentTo(new[] { "Before", "Base Second" }));
        var changedTime = File.GetLastWriteTimeUtc(_updateDta).AddMinutes(1);
        File.WriteAllText(_updateDta, Dta(updatedSong, "After"));
        File.SetLastWriteTimeUtc(_updateDta, changedTime);

        var firstRefresh = ScanNames(false);
        var secondRefresh = ScanNames(false);
        var fromCache = ScanNames(true);
        var expected = updatedSong == "first"
            ? new[] { "After", "Base Second" }
            : new[] { "Base First", "After" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstRefresh, Is.EquivalentTo(expected), "The changed update must not require a second refresh.");
            Assert.That(secondRefresh, Is.EquivalentTo(expected));
            Assert.That(fromCache, Is.EquivalentTo(expected));
        }
    }

    private string[] ScanNames(bool quick)
    {
        var cache = CacheHandler.RunScan(quick, CachePath, Path.Combine(_root, "badsongs.txt"),
            false, new() { _library });
        return cache.Entries.Values.SelectMany(entries => entries).Select(entry => entry.Name.ToString()).ToArray();
    }

    private static string Dta(string node, string title) => $$"""
        ({{node}}
          (name "{{title}}")
          (song_length 60000)
          (song
            (name "songs/{{node}}/{{node}}")
            (pans (0.0))
            (vols (0.0))
            (cores (0.0))
          )
        )

        """;

    private static void WriteSongFiles(string songs, string node, byte pitch)
    {
        string directory = Path.Combine(songs, node);
        Directory.CreateDirectory(directory);
        var midi = new MidiFile(new TrackChunk(), new TrackChunk(
            new SequenceTrackNameEvent("PART BASS"),
            new NoteOnEvent((SevenBitNumber) pitch, (SevenBitNumber) 100) { DeltaTime = 1 },
            new NoteOffEvent((SevenBitNumber) pitch, (SevenBitNumber) 0) { DeltaTime = 120 }))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480)
        };
        string path = Path.Combine(directory, node + ".mid");
        midi.Write(path);
        // Keep the MIDI's normalized timestamp equal to its last-write time for cache reuse.
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        using var mogg = File.Create(Path.Combine(directory, node + ".mogg"));
        mogg.Write(RBCONEntry.UNENCRYPTED_MOGG, Endianness.Little);
    }
}

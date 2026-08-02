using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Game;
using YARG.Core.IO;
using YARG.Core.Replays;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Replays;

public class ReplayInfoTests
{
    [Test]
    public void Serialize_ThenReadBack_RoundTripsAllFields()
    {
        var original = BuildCanonicalReplayInfo();
        byte[] bytes = SerializeToBytes(original);

        using var array = FixedArray.Read(new MemoryStream(bytes), bytes.Length);
        var stream = array.ToValueStream();
        var roundTripped = new ReplayInfo("C:\\fake\\test.replay", ref stream);

        AssertAllFieldsEqual(original, roundTripped);

        // The reader must consume exactly the bytes the writer produced.
        // Any layout mismatch between Serialize and the read constructor
        // (wrong field order, missing field, off-by-one version guard)
        // shifts the stream and breaks this invariant.
        Assert.That(stream.Position, Is.EqualTo(array.Length),
            "Reader did not consume exactly the bytes written by Serialize");
    }

    public static IEnumerable<ReplayStats> AllStatsModes
    {
        get
        {
            yield return new DrumsReplayStats("DrumsPlayer", true, new DrumsStats
            {
                TotalNotes = 50,
                NotesHit = 42,
                Overhits = 2,
                SoloBonuses = 1000,
            });
            yield return new ProKeysReplayStats("KeysPlayer", true, new KeysStats
            {
                TotalNotes = 40,
                NotesHit = 35,
                Overhits = 1,
                SoloBonuses = 800,
            });
            yield return new VocalsReplayStats("Vocalist", true, new VocalsStats
            {
                TotalNotes = 30,
                NotesHit = 28,
            });
        }
    }

    [Test]
    public void Serialize_ThenReadBack_RoundTripsEachStatsMode(
        [ValueSource(nameof(AllStatsModes))] ReplayStats stat)
    {
        var info = BuildCanonicalReplayInfo();
        typeof(ReplayInfo).GetField("Stats")!.SetValue(info, new[] { stat });

        byte[] bytes = SerializeToBytes(info);
        using var array = FixedArray.Read(new MemoryStream(bytes), bytes.Length);
        var stream = array.ToValueStream();
        var roundTripped = new ReplayInfo("C:\\fake\\test.replay", ref stream);

        // Is.InstanceOf in AssertStatsEqual verifies the mode byte written by
        // Serialize dispatches back to the same stats class on read.
        AssertAllFieldsEqual(info, roundTripped);
        Assert.That(stream.Position, Is.EqualTo(array.Length),
            "Reader did not consume exactly the bytes written by Serialize");
    }

    /// <summary>
    /// Builds a ReplayInfo whose every field holds a distinct, recognizable value,
    /// without knowing the constructor signature. The type is allocated uninitialized
    /// and all public fields are assigned via reflection, so newly added fields are
    /// picked up automatically.
    /// </summary>
    private static ReplayInfo BuildCanonicalReplayInfo()
    {
        var info = (ReplayInfo) RuntimeHelpers.GetUninitializedObject(typeof(ReplayInfo));

        foreach (var field in typeof(ReplayInfo).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object? value = field.Name switch
            {
                // Serialized as a constant by Serialize(), not round-tripped from the field.
                "ReplayVersion" => ReplayIO.REPLAY_VERSIONS.CURRENT,
                // Recomputed by the read constructor from the song/artist/charter/date fields.
                "ReplayName" => null,
                "FilePath" => "C:\\fake\\test.replay",
                "ReplayChecksum" => HashWrapper.FromString("00112233445566778899AABBCCDDEEFF00112233"),
                "SongChecksum" => HashWrapper.FromString("FFEEDDCCBBAA99887766554433221100FEDCBA98"),
                "Pauses" => new PauseInfo[]
                {
                    new() { PauseTime = 30.0, PauseLength = 5.0 },
                    new() { PauseTime = 60.0, PauseLength = 2.5 },
                },
                "Stats" => new ReplayStats[]
                {
                    new GuitarReplayStats("PlayerOne", true, BuildGuitarStats()),
                },
                "Date" => new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc),
                "BandStars" => StarAmount.Star5,
                "SongSpeed" => 1.25f,
                "ReplayLength" => 210.5,
                "BandScore" => 123456,
                "EngineVersion" => 5,
                _ => DistinctValueFor(field.FieldType, field.Name),
            };

            if (value != null)
            {
                field.SetValue(info, value);
            }
        }

        typeof(ReplayInfo).GetField(nameof(ReplayInfo.ReplayName))!.SetValue(info,
            ReplayInfo.ConstructReplayName(info.SongName, info.ArtistName, info.CharterName, info.Date));
        return info;
    }

    private static object DistinctValueFor(Type type, string fieldName)
    {
        if (type == typeof(string))
        {
            return "pǝɹʇ-юникод-名前";
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(int))
        {
            return 0x12345678;
        }

        if (type == typeof(long))
        {
            return 0x123456789ABCDEF0L;
        }

        if (type == typeof(float))
        {
            return 3.14159f;
        }

        if (type == typeof(double))
        {
            return 2.718281828;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().Last();
        }

        throw new InvalidOperationException($"No distinct test value defined for field {fieldName} of type {type}");
    }

    private static GuitarStats BuildGuitarStats()
    {
        return new GuitarStats
        {
            CommittedScore = 100000,
            TotalNotes = 100,
            NotesHit = 87,
            Overstrums = 3,
            GhostInputs = 2,
            SoloBonuses = 5000,
            TotalStarPowerPhrases = 10,
            StarPowerActivationCount = 2,
            Stars = 4.5f,
        };
    }

    private static byte[] SerializeToBytes(ReplayInfo info)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            info.Serialize(writer);
        }
        return ms.ToArray();
    }

    private static void AssertAllFieldsEqual(ReplayInfo expected, ReplayInfo actual)
    {
        var fields = typeof(ReplayInfo).GetFields(BindingFlags.Public | BindingFlags.Instance);
        using (Assert.EnterMultipleScope())
        {
            foreach (var field in fields)
            {
                var expectedValue = field.GetValue(expected);
                var actualValue = field.GetValue(actual);
                if (field.FieldType == typeof(PauseInfo[]))
                {
                    AssertPausesEqual((PauseInfo[]) expectedValue!, (PauseInfo[]) actualValue!, field.Name);
                }
                else if (field.FieldType == typeof(ReplayStats[]))
                {
                    AssertStatsEqual((ReplayStats[]) expectedValue!, (ReplayStats[]) actualValue!, field.Name);
                }
                else
                {
                    Assert.That(actualValue, Is.EqualTo(expectedValue), $"Field {field.Name} did not round-trip");
                }
            }
        }
    }

    private static void AssertPausesEqual(PauseInfo[] expected, PauseInfo[] actual, string context)
    {
        Assert.That(actual, Has.Length.EqualTo(expected.Length), $"{context}: pause count");
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(actual[i].PauseTime, Is.EqualTo(expected[i].PauseTime), $"{context}[{i}].PauseTime");
            Assert.That(actual[i].PauseLength, Is.EqualTo(expected[i].PauseLength), $"{context}[{i}].PauseLength");
        }
    }

    private static void AssertStatsEqual(ReplayStats[] expected, ReplayStats[] actual, string context)
    {
        Assert.That(actual, Has.Length.EqualTo(expected.Length), $"{context}: stats count");
        for (int i = 0; i < expected.Length; i++)
        {
            AssertStatsEqual(expected[i], actual[i], $"{context}[{i}]");
        }
    }

    private static void AssertStatsEqual(ReplayStats expected, ReplayStats actual, string context)
    {
        Assert.That(actual, Is.InstanceOf(expected.GetType()), context);

        var type = expected.GetType();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        {
            var expectedValue = field.GetValue(expected);
            var actualValue = field.GetValue(actual);
            Assert.That(actualValue, Is.EqualTo(expectedValue), $"{context}.{field.Name} did not round-trip");
        }
    }
}

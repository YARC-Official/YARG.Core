using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Extensions;

namespace YARG.Core.UnitTests.Chart;

public class SyncTrackTests
{
    private const uint RESOLUTION = 480;
    private const uint MEASURE_RESOLUTION = RESOLUTION * TimeSignatureChange.MEASURE_RESOLUTION_SCALE;

    // The individual components of the sync track are created in separate lists here,
    // so that they can be passed into the constructor that initializes the `StrongBeatlines` list

    private static List<TempoChange> _tempos =
    [
        new TempoChange(60, 0.0, RESOLUTION * 0),
        new TempoChange(80, 4.0, RESOLUTION * 4),
        new TempoChange(120, 8.5, RESOLUTION * 10),
        new TempoChange(160, 12.5, RESOLUTION * 18),
    ];

    private static List<TimeSignatureChange> _timesigs =
    [
        new TimeSignatureChange(4, 4, 0.0, RESOLUTION * 0, MEASURE_RESOLUTION * 0, 0, 0, 0),
        new TimeSignatureChange(6, 4, 4.0, RESOLUTION * 4, MEASURE_RESOLUTION * 1, 1, 4, 4),
        new TimeSignatureChange(4, 4, 8.5, RESOLUTION * 10, MEASURE_RESOLUTION * 2, 2, 10, 10),
        new TimeSignatureChange(7, 8, 10.5, RESOLUTION * 14, MEASURE_RESOLUTION * 3, 3, 14, 14),
        new TimeSignatureChange(7, 8, 12.25, (uint) (RESOLUTION * 17.5), MEASURE_RESOLUTION * 4, 4, 21, 17.5, interrupted: true),
        new TimeSignatureChange(4, 2, 12.5, RESOLUTION * 18, MEASURE_RESOLUTION * 5, 5, 22, 18),
    ];

    private static List<Beatline> _beatlines =
    [
        // 4/4
        new(BeatlineType.Measure, 0.0, RESOLUTION * 0),
        new(BeatlineType.Strong,  1.0, RESOLUTION * 1),
        new(BeatlineType.Strong,  2.0, RESOLUTION * 2),
        new(BeatlineType.Strong,  3.0, RESOLUTION * 3),

        // 6/4
        new(BeatlineType.Measure, 4.00, RESOLUTION * 4),
        new(BeatlineType.Strong,  4.75, RESOLUTION * 5),
        new(BeatlineType.Strong,  5.50, RESOLUTION * 6),
        new(BeatlineType.Strong,  6.25, RESOLUTION * 7),
        new(BeatlineType.Strong,  7.00, RESOLUTION * 8),
        new(BeatlineType.Strong,  7.75, RESOLUTION * 9),

        // 4/4
        new(BeatlineType.Measure,  8.5, RESOLUTION * 10),
        new(BeatlineType.Strong,   9.0, RESOLUTION * 11),
        new(BeatlineType.Strong,   9.5, RESOLUTION * 12),
        new(BeatlineType.Strong,  10.0, RESOLUTION * 13),

        // 7/8
        new(BeatlineType.Measure, 10.50, (uint) (RESOLUTION * 14.0)),
        new(BeatlineType.Weak,    10.75, (uint) (RESOLUTION * 14.5)),
        new(BeatlineType.Strong,  11.00, (uint) (RESOLUTION * 15.0)),
        new(BeatlineType.Weak,    11.25, (uint) (RESOLUTION * 15.5)),
        new(BeatlineType.Strong,  11.50, (uint) (RESOLUTION * 16.0)),
        new(BeatlineType.Weak,    11.75, (uint) (RESOLUTION * 16.5)),
        new(BeatlineType.Weak,    12.00, (uint) (RESOLUTION * 17.0)),
        new(BeatlineType.Measure, 12.25, (uint) (RESOLUTION * 17.5)),

        // 4/2
        new(BeatlineType.Measure, 12.50, RESOLUTION * 18),
        new(BeatlineType.Strong,  13.25, RESOLUTION * 20),
        new(BeatlineType.Strong,  14.00, RESOLUTION * 22),
        new(BeatlineType.Strong,  14.75, RESOLUTION * 24),
        new(BeatlineType.Measure, 15.50, RESOLUTION * 26),
        new(BeatlineType.Strong,  16.25, RESOLUTION * 28),
        new(BeatlineType.Strong,  17.00, RESOLUTION * 30),
        new(BeatlineType.Strong,  17.75, RESOLUTION * 32),
        new(BeatlineType.Measure, 18.50, RESOLUTION * 34),

        // Freeform
        new(BeatlineType.Measure, 19.2500, RESOLUTION * 36),
        new(BeatlineType.Weak,    19.4375, (uint) (RESOLUTION * 36.5)),
        new(BeatlineType.Strong,  19.6250, RESOLUTION * 37),
        new(BeatlineType.Strong,  20.0000, RESOLUTION * 38),
        new(BeatlineType.Weak,    20.1875, (uint) (RESOLUTION * 38.5)),
        new(BeatlineType.Strong,  20.3750, RESOLUTION * 39),
        new(BeatlineType.Measure, 20.7500, RESOLUTION * 40),
    ];

    private static SyncTrack _syncTrack = new(RESOLUTION, _tempos, _timesigs, _beatlines);

    [Test]
    public void ValidateSyncTrack()
    {
        for (int i = 1; i < _syncTrack.Tempos.Count; i++)
        {
            var previousTempo = _syncTrack.Tempos[i - 1];
            var tempo = _syncTrack.Tempos[i];

            double time = previousTempo.TickToTime(tempo.Tick, _syncTrack.Resolution);
            Assert.That(tempo.Time, Is.EqualTo(time), $"Time specified for tempo at tick {tempo.Tick} is incorrect");
        }

        foreach (var timesig in _syncTrack.TimeSignatures)
        {
            double time = _syncTrack.TickToTime(timesig.Tick);
            Assert.That(timesig.Time, Is.EqualTo(time), $"Time specified for time signature at tick {timesig.Tick} is incorrect");
        }

        foreach (var beatline in _syncTrack.Beatlines)
        {
            double time = _syncTrack.TickToTime(beatline.Tick);
            Assert.That(beatline.Time, Is.EqualTo(time), $"Time specified for beatline at tick {beatline.Tick} is incorrect");
        }
    }

    [Test]
    public void GenerateBeatlines()
    {
        var syncTrack = _syncTrack.Clone();

        // Exclude freeform beatlines from expected result
        var expected = syncTrack.Beatlines[..31];

        syncTrack.Beatlines.Clear();
        syncTrack.GenerateBeatlines(18.5);
        CollectionAssert.AreEqual(expected, syncTrack.Beatlines);

        syncTrack.Beatlines.Clear();
        syncTrack.GenerateBeatlines(16320);
        CollectionAssert.AreEqual(expected, syncTrack.Beatlines);
    }

    [Test]
    public void TimeToTick()
    {
        var positions = new (double time, uint tick, double roundTrip)[]
        {
            // Exact positions
            (0.0,  0,    0.0),
            (4.0,  1920, 4.0),
            (8.5,  4800, 8.5),
            (12.5, 8640, 12.5),

            // In-between positions
            (0.5, 240,  0.5),
            (1.0, 480,  1.0),
            (2.0, 960,  2.0),
            (6.0, 3200, 6.0),
            (8.0, 4480, 8.0),

            // Not all times will reliably round-trip due to
            // being quantized to the nearest tick
            (0.0001,  0,    0.0),
            (3.9999,  1920, 4.0),
            (4.0001,  1920, 4.0),
            (8.4999,  4800, 8.5),
            (8.5001,  4800, 8.5),
            (12.4999, 8640, 12.5),
            (12.5001, 8640, 12.5),

            (0.4999, 240,  0.5),
            (0.5001, 240,  0.5),
            (0.9999, 480,  1.0),
            (1.0001, 480,  1.0),
            (1.9999, 960,  2.0),
            (2.0001, 960,  2.0),
            (5.9999, 3200, 6.0),
            (6.0001, 3200, 6.0),
            (7.9999, 4480, 8.0),
            (8.0001, 4480, 8.0),
        };

        Assert.Multiple(() =>
        {
            foreach (var (expectedTime, expectedTick, expectedRoundTrip) in positions)
            {
                uint resultTick = _syncTrack.TimeToTick(expectedTime);
                Assert.That(resultTick, Is.EqualTo(expectedTick), "Incorrect time -> tick result");

                // Ensure that times round-trip
                // Some quantization will occur in the process
                double roundTripTime = _syncTrack.TickToTime(resultTick);
                Assert.That(roundTripTime, Is.EqualTo(expectedRoundTrip), "Incorrect time -> tick -> time result");
            }
        });
    }

    [Test]
    public void TickToTime()
    {
        var positions = new (uint tick, double time)[]
        {
            // Exact positions
            (0,    0.0),
            (1920, 4.0),
            (4800, 8.5),
            (8640, 12.5),

            // In-between positions
            (240,  0.5),
            (480,  1.0),
            (960,  2.0),
            (3200, 6.0),
            (4480, 8.0),
        };

        Assert.Multiple(() =>
        {
            foreach (var (expectedTick, expectedTime) in positions)
            {
                double convertedTime = _syncTrack.TickToTime(expectedTick);
                Assert.That(convertedTime, Is.EqualTo(expectedTime), "Tick does not convert to time correctly");

                // Ensure that ticks round-trip
                uint roundTripTick = _syncTrack.TimeToTick(convertedTime);
                Assert.That(roundTripTick, Is.EqualTo(expectedTick), "Tick value does not round-trip");
            }
        });

        // Thorough round-tripping test
        Assert.Multiple(() =>
        {
            var tempoTracker = new ChartEventTickTracker<TempoChange>(_syncTrack.Tempos);
            for (uint expectedTick = 0; expectedTick < 10_000; expectedTick++)
            {
                tempoTracker.Update(expectedTick);

                double convertedTime = _syncTrack.TickToTime(expectedTick, tempoTracker.Current!);
                uint roundTripTick = _syncTrack.TimeToTick(convertedTime, tempoTracker.Current!);
                Assert.That(roundTripTick, Is.EqualTo(expectedTick), "Tick value does not round-trip");
            }
        });
    }

    [Test]
    public void QuarterTickToMeasureTick()
    {
        var positions = new (uint quarterTick, uint measureTick)[]
        {
            // Exact positions
            (0,    0),
            (1920, 1920),
            (4800, 3840),
            (6720, 5760),
            (8400, 7680),
            (8640, 9600),

            // In-between positions
            (240,  240),
            (480,  480),
            (960,  960),
            (3360, 2880),
            (4800, 3840),
        };

        TestMeasureAndQuarterTicks(
            positions,
            _syncTrack.QuarterTickToMeasureTick,
            _syncTrack.MeasureTickToQuarterTick,
            "Quarter tick",
            "Measure tick",
            (timesig, tick) => timesig.Tick.CompareTo(tick)
        );
    }

    [Test]
    public void MeasureTickToQuarterTick()
    {
        var positions = new (uint measureTick, uint quarterTick)[]
        {
            // Exact positions
            (0,    0),
            (1920, 1920),
            (3840, 4800),
            (5760, 6720),
            (7680, 8400),
            (9600, 8640),

            // In-between positions
            (240,  240),
            (480,  480),
            (960,  960),
            (2880, 3360),
            (3840, 4800),
        };

        TestMeasureAndQuarterTicks(
            positions,
            _syncTrack.MeasureTickToQuarterTick,
            _syncTrack.QuarterTickToMeasureTick,
            "Measure tick",
            "Quarter tick",
            (timesig, measureTick) => timesig.MeasureTick.CompareTo(measureTick)
        );
    }

    private void TestMeasureAndQuarterTicks(
        (uint leftUnit, uint rightUnit)[] positions,
        Func<uint, uint> convertLeftToRight,
        Func<uint, uint> convertRightToLeft,
        string leftName,
        string rightName,
        Func<TimeSignatureChange, uint, int> compareTimesigAndLeft
    )
    {
        Assert.Multiple(() =>
        {
            foreach (var (expectedLeft, expectedRight) in positions)
            {
                uint convertedRight = convertLeftToRight(expectedLeft);
                Assert.That(convertedRight, Is.EqualTo(expectedRight), $"{rightName} does not round trip");

                // Ensure that ticks round-trip
                uint roundTripLeft = convertRightToLeft(convertedRight);
                Assert.That(roundTripLeft, Is.EqualTo(expectedLeft), $"{leftName} does not round trip");
            }
        });

        // Thorough round-tripping test
        Assert.Multiple(() =>
        {
            // Some quantization will happen when converting between tick systems
            // during non-4/4 time signatures
            uint tickError = 0;

            int timesigIndex = -1;
            for (uint expectedLeft = 0; expectedLeft < 10_000; expectedLeft++)
            {
                while (timesigIndex + 1 < _syncTrack.TimeSignatures.Count &&
                    compareTimesigAndLeft(_syncTrack.TimeSignatures[timesigIndex + 1], expectedLeft) <= 0)
                {
                    timesigIndex++;

                    var timeSig = _syncTrack.TimeSignatures[timesigIndex];

                    uint ticksPerMeasure = timeSig.IsInterrupted
                        ? _syncTrack.TimeSignatures[timesigIndex + 1].Tick - timeSig.Tick
                        : timeSig.GetTicksPerMeasure(_syncTrack.Resolution);

                    tickError = (uint) Math.Ceiling(_syncTrack.MeasureResolution / (double) ticksPerMeasure);
                }

                uint convertedRight = convertLeftToRight(expectedLeft);
                uint roundTripLeft = convertRightToLeft(convertedRight);

                uint lowerBound = expectedLeft - Math.Min(tickError, expectedLeft); // prevent underflow
                uint upperBound = expectedLeft + tickError;
                Assert.That(roundTripLeft, Is.InRange(lowerBound, upperBound), $"{leftName} does not round trip");
            }
        });
    }

    [Test]
    public void GetStrongBeatPosition()
    {
        var positions = new (uint tick, double time)[]
        {
            // Exact positions

            // 4/4
            (0,    0),
            (480,  1),
            (960,  2),
            (1440, 3),

            // 6/4
            (1920, 4),
            (2400, 5),
            (2880, 6),
            (3360, 7),
            (3840, 8),
            (4320, 9),

            // 4/4
            (4800, 10),
            (5280, 11),
            (5760, 12),
            (6240, 13),

            // 7/8
            (6720, 14.0),
            (6960, 14.5),
            (7200, 15.0),
            (7440, 15.5),
            (7680, 16.0),
            (7920, 16.0 + (1.0 / 3.0)),
            (8160, 16.0 + (2.0 / 3.0)),
            (8400, 17.0),

            // 4/2
            (8640,  18),
            (9600,  19),
            (10560, 20),
            (11520, 21),
            (12480, 22),
            (13440, 23),
            (14400, 24),
            (15360, 25),
            (16320, 26),

            // Freeform
            (17280, 27.0),
            (17520, 27.5),
            (17760, 28.0),
            (18240, 29.0),
            (18480, 29.5),
            (18720, 30.0),
            (19200, 31.0),

            // In-between positions
            (240,   0.5),
            (6000,  12.5),
            (12840, 22.375),
        };

        Assert.Multiple(() =>
        {
            foreach (var (tick, expectedPosition) in positions)
            {
                double position = _syncTrack.GetStrongBeatPosition(tick);
                Assert.That(position, Is.EqualTo(expectedPosition), "Tick does not convert to strong beat position properly");
            }
        });
    }

    [Test]
    public void GetWeakBeatPosition()
    {
        var positions = new (uint tick, double time)[]
        {
            // Exact positions

            // 4/4
            (0,    0),
            (480,  1),
            (960,  2),
            (1440, 3),

            // 6/4
            (1920, 4),
            (2400, 5),
            (2880, 6),
            (3360, 7),
            (3840, 8),
            (4320, 9),

            // 4/4
            (4800, 10),
            (5280, 11),
            (5760, 12),
            (6240, 13),

            // 7/8
            (6720, 14),
            (6960, 15),
            (7200, 16),
            (7440, 17),
            (7680, 18),
            (7920, 19),
            (8160, 20),
            (8400, 21),

            // 4/2
            (8640,  22),
            (9600,  23),
            (10560, 24),
            (11520, 25),
            (12480, 26),
            (13440, 27),
            (14400, 28),
            (15360, 29),
            (16320, 30),

            // Freeform
            (17280, 31),
            (17520, 32),
            (17760, 33),
            (18240, 34),
            (18480, 35),
            (18720, 36),
            (19200, 37),

            // In-between positions
            (240,   0.5),
            (6000,  12.5),
            (12840, 26.375),
        };

        Assert.Multiple(() =>
        {
            foreach (var (tick, expectedPosition) in positions)
            {
                double position = _syncTrack.GetWeakBeatPosition(tick);
                Assert.That(position, Is.EqualTo(expectedPosition), "Tick does not convert to weak beat position properly");
            }
        });
    }

    [Test]
    public void GetDenominatorBeatPosition()
    {
        var positions = new (uint tick, double time)[]
        {
            // Exact positions

            // 4/4
            (0,    0),
            (480,  1),
            (960,  2),
            (1440, 3),

            // 6/4
            (1920, 4),
            (2400, 5),
            (2880, 6),
            (3360, 7),
            (3840, 8),
            (4320, 9),

            // 4/4
            (4800, 10),
            (5280, 11),
            (5760, 12),
            (6240, 13),

            // 7/8
            (6720, 14),
            (6960, 15),
            (7200, 16),
            (7440, 17),
            (7680, 18),
            (7920, 19),
            (8160, 20),
            (8400, 21),

            // 4/2
            (8640,  22),
            (9600,  23),
            (10560, 24),
            (11520, 25),
            (12480, 26),
            (13440, 27),
            (14400, 28),
            (15360, 29),
            (16320, 30),

            // Freeform
            (17280, 31.00),
            (17520, 31.25),
            (17760, 31.50),
            (18240, 32.00),
            (18480, 32.25),
            (18720, 32.50),
            (19200, 33.00),

            // In-between positions
            (240,   0.5),
            (6000,  12.5),
            (12840, 26.375),
        };

        Assert.Multiple(() =>
        {
            foreach (var (tick, expectedPosition) in positions)
            {
                double position = _syncTrack.GetDenominatorBeatPosition(tick);
                Assert.That(position, Is.EqualTo(expectedPosition), "Tick does not convert to denominator beat position properly");
            }
        });
    }

    [Test]
    public void GetQuarterNotePosition()
    {
        var positions = new (uint tick, double time)[]
        {
            // Exact positions

            // 4/4
            (0,    0),
            (480,  1),
            (960,  2),
            (1440, 3),

            // 6/4
            (1920, 4),
            (2400, 5),
            (2880, 6),
            (3360, 7),
            (3840, 8),
            (4320, 9),

            // 4/4
            (4800, 10),
            (5280, 11),
            (5760, 12),
            (6240, 13),

            // 7/8
            (6720, 14.0),
            (6960, 14.5),
            (7200, 15.0),
            (7440, 15.5),
            (7680, 16.0),
            (7920, 16.5),
            (8160, 17.0),
            (8400, 17.5),

            // 4/2
            (8640,  18),
            (9600,  20),
            (10560, 22),
            (11520, 24),
            (12480, 26),
            (13440, 28),
            (14400, 30),
            (15360, 32),
            (16320, 34),

            // Freeform
            (17280, 36.0),
            (17520, 36.5),
            (17760, 37.0),
            (18240, 38.0),
            (18480, 38.5),
            (18720, 39.0),
            (19200, 40.0),

            // In-between positions
            (240,   0.5),
            (6000,  12.5),
            (12840, 26.75),
        };

        Assert.Multiple(() =>
        {
            foreach (var (tick, expectedPosition) in positions)
            {
                double position = _syncTrack.GetQuarterNotePosition(tick);
                Assert.That(position, Is.EqualTo(expectedPosition), "Tick does not convert to quarter note position properly");
            }
        });
    }

    [Test]
    public void GetMeasurePosition()
    {
        var positions = new (uint tick, double time)[]
        {
            // Exact positions

            // 4/4
            (0,    0.00),
            (480,  0.25),
            (960,  0.50),
            (1440, 0.75),

            // 6/4
            (1920, 1.0 + (0.0 / 6.0)),
            (2400, 1.0 + (1.0 / 6.0)),
            (2880, 1.0 + (2.0 / 6.0)),
            (3360, 1.0 + (3.0 / 6.0)),
            (3840, 1.0 + (4.0 / 6.0)),
            (4320, 1.0 + (5.0 / 6.0)),

            // 4/4
            (4800, 2.00),
            (5280, 2.25),
            (5760, 2.50),
            (6240, 2.75),

            // 7/8
            (6720, 3.0 + (0.0 / 7.0)),
            (6960, 3.0 + (1.0 / 7.0)),
            (7200, 3.0 + (2.0 / 7.0)),
            (7440, 3.0 + (3.0 / 7.0)),
            (7680, 3.0 + (4.0 / 7.0)),
            (7920, 3.0 + (5.0 / 7.0)),
            (8160, 3.0 + (6.0 / 7.0)),
            (8400, 4.0),

            // 4/2
            (8640,  5.00),
            (9600,  5.25),
            (10560, 5.50),
            (11520, 5.75),
            (12480, 6.00),
            (13440, 6.25),
            (14400, 6.50),
            (15360, 6.75),
            (16320, 7.00),

            // Freeform
            (17280, 7.2500),
            (17520, 7.3125),
            (17760, 7.3750),
            (18240, 7.5000),
            (18480, 7.5625),
            (18720, 7.6250),
            (19200, 7.7500),

            // In-between positions
            (240,   0.125),
            (6000,  2.625),
            (12840, 6.09375),
        };

        Assert.Multiple(() =>
        {
            foreach (var (tick, expectedPosition) in positions)
            {
                double position = _syncTrack.GetMeasurePosition(tick);
                Assert.That(position, Is.EqualTo(expectedPosition), "Tick does not convert to measure position properly");
            }
        });
    }
}

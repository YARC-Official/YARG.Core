using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Extensions;

namespace YARG.Core.UnitTests.Chart;

public class SyncTrackTests
{
    private const uint RESOLUTION = 480;
    private const uint MEASURE_RESOLUTION = RESOLUTION * TimeSignatureChange.MEASURE_RESOLUTION_SCALE;

    private SyncTrack _syncTrack = new(RESOLUTION)
    {
        Tempos =
        {
            new TempoChange(60, 0.0, RESOLUTION * 0),
            new TempoChange(80, 4.0, RESOLUTION * 4),
            new TempoChange(120, 8.5, RESOLUTION * 10),
            new TempoChange(160, 12.5, RESOLUTION * 18),
        },
        TimeSignatures =
        {
            new TimeSignatureChange(4, 4, 0.0, RESOLUTION * 0, MEASURE_RESOLUTION * 0),
            new TimeSignatureChange(6, 4, 4.0, RESOLUTION * 4, MEASURE_RESOLUTION * 1),
            new TimeSignatureChange(4, 4, 8.5, RESOLUTION * 10, MEASURE_RESOLUTION * 2),
            new TimeSignatureChange(7, 8, 10.5, RESOLUTION * 14, MEASURE_RESOLUTION * 3),
            new TimeSignatureChange(7, 8, 12.25, (uint) (RESOLUTION * 17.5), MEASURE_RESOLUTION * 4, interrupted: true),
            new TimeSignatureChange(4, 2, 12.5, RESOLUTION * 18, MEASURE_RESOLUTION * 5),
        },
        Beatlines =
        {
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
            new(BeatlineType.Measure, 12.500, RESOLUTION * 18),
            new(BeatlineType.Strong,  13.250, RESOLUTION * 20),
            new(BeatlineType.Strong,  14.000, RESOLUTION * 22),
            new(BeatlineType.Strong,  14.750, RESOLUTION * 24),
            new(BeatlineType.Measure, 15.500, RESOLUTION * 26),
            new(BeatlineType.Strong,  16.250, RESOLUTION * 28),
            new(BeatlineType.Strong,  17.000, RESOLUTION * 30),
            new(BeatlineType.Strong,  17.750, RESOLUTION * 32),
            new(BeatlineType.Measure, 18.500, RESOLUTION * 34),
        },
    };

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
                Assert.That(convertedTime, Is.EqualTo(expectedTime));

                // Ensure that ticks round-trip
                uint roundTripTick = _syncTrack.TimeToTick(convertedTime);
                Assert.That(roundTripTick, Is.EqualTo(expectedTick));
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
                Assert.That(roundTripTick, Is.EqualTo(expectedTick));
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
            (timesig, measureTick) => timesig.MeasureTick.CompareTo(measureTick)
        );
    }

    private void TestMeasureAndQuarterTicks(
        (uint leftUnit, uint rightUnit)[] positions,
        Func<uint, uint> convertLeftToRight,
        Func<uint, uint> convertRightToLeft,
        Func<TimeSignatureChange, uint, int> compareTimesigAndLeft
    )
    {
        Assert.Multiple(() =>
        {
            foreach (var (expectedLeft, expectedRight) in positions)
            {
                uint convertedRight = convertLeftToRight(expectedLeft);
                Assert.That(convertedRight, Is.EqualTo(expectedRight));

                // Ensure that ticks round-trip
                uint roundTripLeft = convertRightToLeft(convertedRight);
                Assert.That(roundTripLeft, Is.EqualTo(expectedLeft));
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
                Assert.That(roundTripLeft, Is.InRange(lowerBound, upperBound));
            }
        });
    }

    [Test]
    public void GenerateBeatlines()
    {
        var expected = _syncTrack.Beatlines.Duplicate();

        _syncTrack.Beatlines.Clear();
        _syncTrack.GenerateBeatlines(18.5);
        CollectionAssert.AreEqual(expected, _syncTrack.Beatlines);

        _syncTrack.Beatlines.Clear();
        _syncTrack.GenerateBeatlines(16320);
        CollectionAssert.AreEqual(expected, _syncTrack.Beatlines);
    }
}

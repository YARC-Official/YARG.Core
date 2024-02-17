using System;
using YARG.Core.Logging;
using YARG.Core.Utility;

namespace YARG.Core.Chart.Parsing
{
    using AsciiTrimSplitter = SpanSplitter<char, AsciiTrimSplitProcessor>;

    internal static partial class DotChartParser
    {
        private class SyncTrackHandler
        {
            private readonly SyncTrack _syncTrack;

            private bool _hasTempo;
            private bool _hasTimesig;

            private float _tempo;

            private uint _numerator;
            private uint _denominator;

            public SyncTrackHandler(uint resolution)
            {
                _syncTrack = new(resolution);
            }

            public void FinishTrack(SongChart chart)
            {
                chart.SyncTrack = _syncTrack;
            }

            public void FinishTick(uint tick)
            {
                double time = _syncTrack.Tempos.Count > 0
                    ? _syncTrack.TickToTime(tick, _syncTrack.Tempos[^1])
                    : 0;

                if (_hasTempo)
                    _syncTrack.Tempos.Add(new(_tempo, time, tick));
                else if (_syncTrack.Tempos.Count < 1)
                    _syncTrack.Tempos.Add(new(120, 0, 0));

                if (_hasTimesig)
                    _syncTrack.TimeSignatures.Add(new(_numerator, _denominator, time, tick));
                else if (_syncTrack.TimeSignatures.Count < 1)
                    _syncTrack.TimeSignatures.Add(new(4, 4, 0, 0));

                _hasTempo = false;
                _hasTimesig = false;
            }

            public void OnTempoChange(float tempo)
            {
                _hasTempo = true;
                _tempo = tempo;
            }

            public void OnTimeSignature(uint numerator, uint denominator)
            {
                _hasTimesig = true;
                _numerator = numerator;
                _denominator = denominator;
            }
        }

        private static void ParseSyncTrack(AsciiTrimSplitter section, SongChart chart, uint resolution)
        {
            var eventHandler = new SyncTrackHandler(resolution);

            uint currentTick = 0;
            foreach (var line in section)
            {
                uint tick = ParseEventLine(line, out var typeText, out var eventText);
                if (tick != currentTick)
                {
                    if (tick < currentTick)
                        throw new Exception($"Tick cannot go backwards! Went from {currentTick} to {tick}");

                    eventHandler.FinishTick(currentTick);
                    currentTick = tick;
                }

                if (typeText.Equals("TS", StringComparison.OrdinalIgnoreCase))
                {
                    ReadEventInt32Pair(eventText, out uint numerator, out uint? denominatorPower);
                    uint denominator = Pow(2, denominatorPower ?? 2);
                    eventHandler.OnTimeSignature(numerator, denominator);
                }
                else if (typeText.Equals("B", StringComparison.OrdinalIgnoreCase))
                {
                    float tempo = ReadEventInt32(eventText) / 1000f;
                    eventHandler.OnTempoChange(tempo);
                }
                else if (typeText.Equals("A", StringComparison.OrdinalIgnoreCase))
                {
                    // Not necessary currently, but we'll want these for YACE
                    // ulong microseconds = ReadEventInt64(eventText);
                    // eventHandler.OnAnchor(microseconds);
                }
                else
                {
                    YargLogger.LogFormatWarning("Unknown .chart sync event: {0}", line.ToString());
                    continue;
                }
            }

            eventHandler.FinishTick(currentTick);
            eventHandler.FinishTrack(chart);
        }
    }
}
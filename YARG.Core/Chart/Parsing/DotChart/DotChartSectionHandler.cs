using System;
using YARG.Core.Logging;
using YARG.Core.Utility;

namespace YARG.Core.Chart.Parsing
{
    using AsciiTrimSplitter = SpanSplitter<char, AsciiTrimSplitProcessor>;

    internal abstract class DotChartSectionHandler
    {
        protected readonly SongChart _chart;

        private readonly ChartEventTickTracker<TempoChange> _tempoTracker;

        public DotChartSectionHandler(SongChart chart)
        {
            _chart = chart;
            _tempoTracker = new(_chart.SyncTrack.Tempos);
        }

        protected abstract void FinishTick(uint tick);

        protected abstract bool ProcessEvent(ReadOnlySpan<char> typeText, ReadOnlySpan<char> eventText);

        protected virtual void FinishSection(ChartEventTickTracker<TempoChange> tempoTracker) {}

        public void ParseSection(AsciiTrimSplitter section)
        {
            uint currentTick = 0;
            foreach (var line in section)
            {
                // 1234 = TS 4 3

                // Parse tick
                var tickText = line.SplitOnceTrimmedAscii('=', out var eventText);
                if (!uint.TryParse(tickText, out uint tick))
                {
                    YargLogger.LogFormatError("Failed to parse tick text: {0}", tickText.ToString());
                    continue;
                }

                // Split event type and data
                var typeText = eventText.SplitOnceTrimmedAscii(' ', out eventText);
                if (typeText.IsEmpty || eventText.IsEmpty)
                {
                    YargLogger.LogFormatError("Malformed .chart event: {0}", line.ToString());
                    continue;
                }

                if (tick != currentTick)
                {
                    if (tick < currentTick)
                        throw new Exception($"Tick cannot go backwards! Went from {currentTick} to {tick}");

                    FinishTick(currentTick);
                    currentTick = tick;
                    _tempoTracker.Update(tick);
                }

                if (!ProcessEvent(typeText, eventText))
                {
                    // Only warn on unknown data in debug mode
                    YargLogger.LogFormatDebug("Unknown .chart event: {0}", line.ToString());
                }
            }

            FinishTick(currentTick);

            _tempoTracker.ResetToTick(0);
            FinishSection(_tempoTracker);
        }

        protected double TickToTime(uint tick)
            => _chart.SyncTrack.TickToTime(tick, _tempoTracker.Current);

        protected static uint ReadEventInt32(ReadOnlySpan<char> text)
        {
            return uint.TryParse(text, out uint value)
                ? value
                : throw new Exception($"Failed to parse uint text: {text.ToString()}");
        }

        protected static ulong ReadEventInt64(ReadOnlySpan<char> text)
        {
            return ulong.TryParse(text, out ulong value)
                ? value
                : throw new Exception($"Failed to parse ulong text: {text.ToString()}");
        }

        protected static void ReadEventInt32Pair(ReadOnlySpan<char> text, out uint param1, out uint param2)
        {
            var param1Text = text.SplitOnceTrimmedAscii(' ', out var param2Text);
            param1 = ReadEventInt32(param1Text);
            param2 = ReadEventInt32(param2Text);
        }

        protected static void ReadEventInt32Pair(ReadOnlySpan<char> text, out uint param1, out uint? param2)
        {
            var param1Text = text.SplitOnceTrimmedAscii(' ', out var param2Text);
            param1 = ReadEventInt32(param1Text);
            param2 = param2Text.IsEmpty ? null : ReadEventInt32(param2Text);
        }
    }
}
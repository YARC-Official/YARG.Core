using System;

namespace YARG.Core.Chart.Parsing
{
    internal sealed class DotChartSyncTrackHandler : DotChartSectionHandler
    {
        private bool _hasTempo;
        private bool _hasTimesig;

        private float _tempo;

        private uint _numerator;
        private uint _denominator;

        public DotChartSyncTrackHandler(SongChart chart) : base(chart) {}

        protected override void FinishTick(uint tick)
        {
            var syncTrack = _chart.SyncTrack;

            double time = syncTrack.Tempos.Count > 0
                ? syncTrack.TickToTime(tick, syncTrack.Tempos[^1])
                : 0;

            if (_hasTempo)
                syncTrack.Tempos.Add(new(_tempo, time, tick));
            else if (syncTrack.Tempos.Count < 1)
                syncTrack.Tempos.Add(new(120, 0, 0));

            if (_hasTimesig)
                syncTrack.TimeSignatures.Add(new(_numerator, _denominator, time, tick));
            else if (syncTrack.TimeSignatures.Count < 1)
                syncTrack.TimeSignatures.Add(new(4, 4, 0, 0));

            _hasTempo = false;
            _hasTimesig = false;
        }

        protected override bool ProcessEvent(ReadOnlySpan<char> typeText, ReadOnlySpan<char> eventText)
        {
            if (typeText.Equals("TS", StringComparison.OrdinalIgnoreCase))
            {
                ReadEventInt32Pair(eventText, out uint numerator, out uint? denominatorPower);
                uint denominator = Pow(2, denominatorPower ?? 2);

                _hasTimesig = true;
                _numerator = numerator;
                _denominator = denominator;

                return true;
            }
            else if (typeText.Equals("B", StringComparison.OrdinalIgnoreCase))
            {
                float tempo = ReadEventInt32(eventText) / 1000f;

                _hasTempo = true;
                _tempo = tempo;

                return true;
            }
            else if (typeText.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                // Not necessary currently, but we'll want these for YACE
                // ulong microseconds = ReadEventInt64(eventText);
                return true;
            }

            return false;
        }

        private static uint Pow(uint x, uint y)
        {
            if (y == 0)
                return 1;

            uint result = x;
            while (y > 1)
            {
                checked { result *= x; }
                y--;
            }

            return result;
        }
    }
}
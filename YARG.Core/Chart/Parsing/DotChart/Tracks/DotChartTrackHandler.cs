using System;

namespace YARG.Core.Chart.Parsing
{
    internal abstract class DotChartTrackHandler : DotChartSectionHandler
    {
        // A note:
        // This class deliberately does not do any direct access on chart data lists,
        // all data is handled by inheritors.
        // This is done to better accomodate drums, which has to be parsed into 3 tracks at once
        // due to there only being one shared drums track in the chart format itself.

        protected ParseSettings _settings;

        private uint? _versus1Phrase;
        private uint? _versus2Phrase;
        private uint? _starPowerPhrase;

        private TextPhraseHandler _soloPhraser = new(DotChartTextEvents.SOLO_START, DotChartTextEvents.SOLO_END, false);

        public DotChartTrackHandler(SongChart chart, in ParseSettings settings)
            : base(chart)
        {
            _settings = settings;
        }

        protected override void FinishTick(uint tick)
        {
            FinishPhrases(tick);
            FinishSolo(tick);
        }

        private void FinishPhrases(uint tick)
        {
            if (_versus1Phrase is {} versus1Length)
                AddPhrase(tick, versus1Length, PhraseType.VersusPlayer1);
            if (_versus2Phrase is {} versus2Length)
                AddPhrase(tick, versus2Length, PhraseType.VersusPlayer2);
            if (_starPowerPhrase is {} starPowerLength)
                AddPhrase(tick, starPowerLength, PhraseType.StarPower);
        }

        private void FinishSolo(uint tick)
        {
            if (_soloPhraser.FinishTick(tick, out uint startTick, out _))
                AddPhrase(startTick, tick - startTick, PhraseType.Solo);
        }

        protected abstract bool OnNoteEvent(uint note, uint length);

        protected void AddPhrase(uint startTick, uint length, PhraseType type)
        {
            double startTime = TickToTime(startTick);
            double endTime = TickToTime(startTick + length);
            AddPhrase(new(type, startTime, endTime - startTime, startTick, length));
        }

        protected abstract void AddPhrase(Phrase phrase);

        protected virtual bool OnPhraseEvent(uint type, uint length)
        {
            switch (type)
            {
                case 0: _versus1Phrase = length; break;
                case 1: _versus2Phrase = length; break;
                case 2: _starPowerPhrase = length; break;
                default: return false;
            }

            return true;
        }

        protected virtual bool OnTextEvent(ReadOnlySpan<char> text)
        {
            return _soloPhraser.ProcessEvent(text);
        }

        protected override bool ProcessEvent(ReadOnlySpan<char> typeText, ReadOnlySpan<char> eventText)
        {
            if (typeText.Equals("N", StringComparison.OrdinalIgnoreCase))
            {
                ReadEventInt32Pair(eventText, out uint note, out uint length);
                return OnNoteEvent(note, length);
            }
            else if (typeText.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                ReadEventInt32Pair(eventText, out uint type, out uint length);
                return OnPhraseEvent(type, length);
            }
            else if (typeText.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                return OnTextEvent(eventText);
            }

            return false;
        }
    }
}
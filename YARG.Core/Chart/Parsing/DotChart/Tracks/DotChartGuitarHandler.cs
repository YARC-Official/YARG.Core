using System;
using System.Collections.Generic;

namespace YARG.Core.Chart.Parsing
{
    internal class DotChartGuitarHandler : DotChartTrackHandler
    {
        private InstrumentDifficulty<GuitarNote> _track;
        private List<IntermediateGuitarNote> _intermediateNotes = new();

        private uint? _fret1Length;
        private uint? _fret2Length;
        private uint? _fret3Length;
        private uint? _fret4Length;
        private uint? _fret5Length;
        private uint? _fret6Length;
        private uint? _openLength;

        private bool _forceMarker;
        private bool _tapMarker;

        public DotChartGuitarHandler(Instrument instrument, Difficulty difficulty,
            SongChart chart, in ParseSettings settings)
            : base(chart, settings)
        {
            var gameMode = instrument.ToGameMode();
            var diffs = gameMode switch
            {
                GameMode.FiveFretGuitar => chart.GetFiveFretTrack(instrument).Difficulties,
                GameMode.SixFretGuitar => chart.GetSixFretTrack(instrument).Difficulties,
                _ => throw new ArgumentException($"Instrument {instrument} is not a guitar instrument!")
            };

            if (!diffs.TryGetValue(difficulty, out _track))
            {
                _track = new(instrument, difficulty);
                diffs[difficulty] = _track;
            }
        }

        protected override void FinishTick(uint tick)
        {
            FinishNotes(tick);
        }

        private void FinishNotes(uint tick)
        {
            if (_fret1Length is {} fret1Length)
                FinishNote(tick, fret1Length, GuitarFret.Fret1);
            if (_fret2Length is {} fret2Length)
                FinishNote(tick, fret2Length, GuitarFret.Fret2);
            if (_fret3Length is {} fret3Length)
                FinishNote(tick, fret3Length, GuitarFret.Fret3);
            if (_fret4Length is {} fret4Length)
                FinishNote(tick, fret4Length, GuitarFret.Fret4);
            if (_fret5Length is {} fret5Length)
                FinishNote(tick, fret5Length, GuitarFret.Fret5);
            if (_fret6Length is {} fret6Length)
                FinishNote(tick, fret6Length, GuitarFret.Fret6);
            if (_openLength is {} openLength)
                FinishNote(tick, openLength, GuitarFret.Open);
        }

        private void FinishNote(uint tick, uint length, GuitarFret fret)
        {
            var flags = IntermediateGuitarFlags.None;

            if (_tapMarker)
                flags |= IntermediateGuitarFlags.Tap;
            if (_forceMarker)
                flags |= IntermediateGuitarFlags.ForceFlip;

            _intermediateNotes.Add(new(tick, length, fret, flags));
        }

        protected override bool OnNoteEvent(uint note, uint length)
        {
            switch (note)
            {
                case 0: _fret1Length = length; break;
                case 1: _fret2Length = length; break;
                case 2: _fret3Length = length; break;
                case 3: _fret4Length = length; break;
                case 4: _fret5Length = length; break;
                case 8: _fret6Length = length; break;
                case 7: _openLength = length; break;

                case 5: _forceMarker = true; break;
                case 6: _tapMarker = true; break;

                default: return false;
            }

            return true;
        }

        protected override void AddPhrase(Phrase phrase)
        {
            _track.Phrases.Add(phrase);
        }
    }
}
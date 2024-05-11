using System;
using System.Collections.Generic;

namespace YARG.Core.Chart.Parsing
{
    internal class DotChartDrumsHandler : DotChartTrackHandler
    {
        private Difficulty _difficulty;

        // Parses to all 3 drums tracks at once,
        // since there's only a single drums track for all 3 in the chart
        private InstrumentDifficulty<DrumNote> _fourLane;
        private InstrumentDifficulty<DrumNote> _fourPro;
        private InstrumentDifficulty<DrumNote> _fiveLane;

        private List<IntermediateDrumsNote> _intermediateNotes = new();

        private bool _hasExpertPlus;

        private uint? _kickLength;
        private uint? _kickPlusLength;

        private uint? _note1Length;
        private uint? _note2Length;
        private uint? _note3Length;
        private uint? _note4Length;
        private uint? _note5Length;

        private IntermediateDrumsNoteFlags _note1Flags;
        private IntermediateDrumsNoteFlags _note2Flags;
        private IntermediateDrumsNoteFlags _note3Flags;
        private IntermediateDrumsNoteFlags _note4Flags;
        private IntermediateDrumsNoteFlags _note5Flags;

        private uint? _activationPhrase;
        private uint? _tremoloPhrase;
        private uint? _trillPhrase;

        private bool _discoFlip;

        public DotChartDrumsHandler(Difficulty difficulty,
            SongChart chart, in ParseSettings settings)
            : base(chart, settings)
        {
            _difficulty = difficulty;

            _fourLane = EnsureExists(chart, Instrument.FourLaneDrums, difficulty);
            _fourPro = EnsureExists(chart, Instrument.ProDrums, difficulty);
            _fiveLane = EnsureExists(chart, Instrument.FiveLaneDrums, difficulty);
        }

        private static InstrumentDifficulty<DrumNote> EnsureExists(SongChart chart, Instrument instrument, Difficulty difficulty)
        {
            var diffs = chart.GetDrumsTrack(instrument).Difficulties;
            if (!diffs.TryGetValue(difficulty, out var track))
            {
                track = new(instrument, difficulty);
                diffs[difficulty] = track;
            }

            return track;
        }

        protected override void FinishTrack(ChartEventTickTracker<TempoChange> tempoTracker)
        {
            DrumsHandler.FinishTrack(_chart, _settings, _difficulty, tempoTracker,
                _fourLane, _fourPro, _fiveLane, _intermediateNotes);

            if (_difficulty == Difficulty.Expert && _hasExpertPlus)
            {
                var fourLane = EnsureExists(_chart, Instrument.FourLaneDrums, Difficulty.ExpertPlus);
                var fourPro = EnsureExists(_chart, Instrument.ProDrums, Difficulty.ExpertPlus);
                var fiveLane = EnsureExists(_chart, Instrument.FiveLaneDrums, Difficulty.ExpertPlus);
                DrumsHandler.FinishTrack(_chart, _settings, Difficulty.ExpertPlus, tempoTracker,
                    fourLane, fourPro, fiveLane, _intermediateNotes);
            }
        }

        protected override void FinishTick(uint tick)
        {
            FinishNotes(tick);
            FinishPhrases(tick);
        }

        private void FinishNotes(uint tick)
        {
            if (_kickLength is {} kickLength)
                FinishNote(tick, kickLength, IntermediateDrumPad.Kick, IntermediateDrumsNoteFlags.None);
            if (_kickPlusLength is {} kickPlusLength)
                FinishNote(tick, kickPlusLength, IntermediateDrumPad.KickPlus, IntermediateDrumsNoteFlags.None);

            if (_note1Length is {} note1Length)
                FinishNote(tick, note1Length, IntermediateDrumPad.Lane1, _note1Flags);
            if (_note2Length is {} note2Length)
                FinishNote(tick, note2Length, IntermediateDrumPad.Lane2, _note2Flags);
            if (_note3Length is {} note3Length)
                FinishNote(tick, note3Length, IntermediateDrumPad.Lane3, _note3Flags);
            if (_note4Length is {} note4Length)
                FinishNote(tick, note4Length, IntermediateDrumPad.Lane4, _note4Flags);
            if (_note5Length is {} note5Length)
                FinishNote(tick, note5Length, IntermediateDrumPad.Lane5, _note5Flags);
        }

        private void FinishNote(uint tick, uint length, IntermediateDrumPad pad, IntermediateDrumsNoteFlags flags)
        {
            if (_discoFlip)
                flags |= IntermediateDrumsNoteFlags.DiscoFlip;

            _intermediateNotes.Add(new(tick, length, pad, flags));
        }

        private void FinishPhrases(uint tick)
        {
            if (_activationPhrase is {} activationLength)
                AddPhrase(tick, activationLength, PhraseType.DrumFill);
            if (_tremoloPhrase is {} tremoloLength)
                AddPhrase(tick, tremoloLength, PhraseType.TremoloLane);
            if (_trillPhrase is {} trillLength)
                AddPhrase(tick, trillLength, PhraseType.TrillLane);
        }

        protected override bool OnNoteEvent(uint note, uint length)
        {
            switch (note)
            {
                case 0: _kickLength = length; break;
                case 1: _note1Length = length; break;
                case 2: _note2Length = length; break;
                case 3: _note3Length = length; break;
                case 4: _note4Length = length; break;
                case 5: _note5Length = length; break;

                case 32:
                    _kickPlusLength = length;
                    _hasExpertPlus = true;
                    break;

                // case 33: _kickFlags |= IntermediateDrumsNoteFlags.Accent; break;
                case 34: _note1Flags |= IntermediateDrumsNoteFlags.Accent; break;
                case 35: _note2Flags |= IntermediateDrumsNoteFlags.Accent; break;
                case 36: _note3Flags |= IntermediateDrumsNoteFlags.Accent; break;
                case 37: _note4Flags |= IntermediateDrumsNoteFlags.Accent; break;
                case 38: _note5Flags |= IntermediateDrumsNoteFlags.Accent; break;

                // case 39: _kickFlags |= IntermediateDrumsNoteFlags.Ghost; break;
                case 40: _note1Flags |= IntermediateDrumsNoteFlags.Ghost; break;
                case 41: _note2Flags |= IntermediateDrumsNoteFlags.Ghost; break;
                case 42: _note3Flags |= IntermediateDrumsNoteFlags.Ghost; break;
                case 43: _note4Flags |= IntermediateDrumsNoteFlags.Ghost; break;
                case 44: _note5Flags |= IntermediateDrumsNoteFlags.Ghost; break;

                // case 64: _kickFlags |= IntermediateDrumsNoteFlags.Ghost; break;
                // case 65: _note1Flags |= IntermediateDrumsNoteFlags.Cymbal; break;
                case 66: _note2Flags |= IntermediateDrumsNoteFlags.Cymbal; break;
                case 67: _note3Flags |= IntermediateDrumsNoteFlags.Cymbal; break;
                case 68: _note4Flags |= IntermediateDrumsNoteFlags.Cymbal; break;
                // case 69: _note5Flags |= IntermediateDrumsNoteFlags.Cymbal; break;

                default: return false;
            }

            return false;
        }

        protected override bool OnPhraseEvent(uint type, uint length)
        {
            switch (type)
            {
                case 64: _activationPhrase = length; break;
                case 65: _tremoloPhrase = length; break;
                case 66: _trillPhrase = length; break;

                default: return base.OnPhraseEvent(type, length);
            }

            return true;
        }

        protected override void OnTextEvent(ReadOnlySpan<char> text)
        {
            if (TextEvents.TryParseDrumsMixEvent(text, out var difficulty, out _, out var setting))
            {
                _discoFlip = setting == DrumsMixSetting.DiscoFlip && difficulty == _difficulty;
            }
            else
            {
                base.OnTextEvent(text);
            }
        }

        protected override void AddPhrase(Phrase phrase)
        {
            _fourLane.Phrases.Add(phrase);
            _fourPro.Phrases.Add(phrase);
            _fiveLane.Phrases.Add(phrase);
        }
    }
}
using MoonscraperChartEditor.Song;
using System;
using System.Collections.Generic;
using YARG.Core.Parsing;
using static YARG.Core.Chart.EliteDrumNote;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public InstrumentTrack<EliteDrumNote> LoadEliteDrumsTrack(Instrument instrument)
        {
            return instrument.ToGameMode() is GameMode.EliteDrums ?
                LoadEliteDrumsTrack(instrument, CreateEliteDrumNote) :
                throw new ArgumentException($"Instrument {instrument} is not Elite Drums!", nameof(instrument));
        }

        private InstrumentTrack<EliteDrumNote> LoadEliteDrumsTrack(Instrument instrument, CreateNoteDelegate<EliteDrumNote> createNote)
        {
            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<EliteDrumNote>>()
            {
                { Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, createNote, HandleEliteDrumsTextEvent) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote, HandleEliteDrumsTextEvent) },
                { Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, createNote, HandleEliteDrumsTextEvent) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote, HandleEliteDrumsTextEvent) },
                { Difficulty.ExpertPlus, LoadDifficulty(instrument, Difficulty.ExpertPlus, createNote, HandleEliteDrumsTextEvent) },
            };
            return new(instrument, difficulties);
        }

        private EliteDrumNote CreateEliteDrumNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var pad = GetEliteDrumPad(moonNote);
            var noteDynamics = GetEliteDrumNoteDynamics(moonNote);
            var hatState = GetEliteDrumHatState(moonNote);
            var isFlam = GetEliteDrumNoteIsFlam(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new(pad, noteDynamics, drumFlags, isFlam, generalFlags, time, moonNote.tick);
        }

        private void HandleEliteDrumsTextEvent(MoonText text)
        {
            if (TextEvents.TryParseEliteDrumsSnareStemEvent(text.text, out var setting, out var difficulty))
            {
                // Handle [snare_stem <pad> <diff?>] event
                return;
            }

            /* Post-MVP
            if (TextEvents.TryParseEliteDrumsPositionalModeEvent(text.text, out var difficulty, out var cymbals, out var drums))
            {
                // Handle [positional <on/off> <diff?>] event
                return;
            }
            */
        }

        private EliteDrumPad GetEliteDrumPad(MoonNote moonNote)
        {
            return moonNote.eliteDrumPad switch
            {
                MoonNote.EliteDrumPad.HatPedal => EliteDrumPad.HatPedal,
                MoonNote.EliteDrumPad.Kick => EliteDrumPad.Kick,
                MoonNote.EliteDrumPad.Snare => EliteDrumPad.Snare,
                MoonNote.EliteDrumPad.HiHat => EliteDrumPad.HiHat,
                MoonNote.EliteDrumPad.LeftCrash => EliteDrumPad.LeftCrash,
                MoonNote.EliteDrumPad.Tom1 => EliteDrumPad.Tom1,
                MoonNote.EliteDrumPad.Tom2 => EliteDrumPad.Tom2,
                MoonNote.EliteDrumPad.Tom3 => EliteDrumPad.Tom3,
                MoonNote.EliteDrumPad.Ride => EliteDrumPad.Ride,
                MoonNote.EliteDrumPad.RightCrash => EliteDrumPad.RightCrash,
                _ => throw new ArgumentException($"Invalid Moonscraper drum pad {moonNote.drumPad}!", nameof(moonNote))
            };
        }

        private DrumNoteType GetEliteDrumNoteDynamics(MoonNote moonNote)
        {
            var dynamics = DrumNoteType.Neutral;

            // Accents/ghosts
            if ((moonNote.flags & MoonNote.Flags.ProDrums_Accent) != 0)
                dynamics = DrumNoteType.Accent;
            else if ((moonNote.flags & MoonNote.Flags.ProDrums_Ghost) != 0)
                dynamics = DrumNoteType.Ghost;

            return dynamics;
        }

        private EliteDrumsHatState GetEliteDrumHatState(MoonNote moonNote)
        {
            if (moonNote.eliteDrumPad is not MoonNote.EliteDrumPad.HiHat)
            {
                return EliteDrumsHatState.Indifferent;
            }

            var hatState = EliteDrumsHatState.Open;

            if ((moonNote.flags & MoonNote.Flags.EliteDrums_ForcedClosed) != 0)
                hatState = EliteDrumsHatState.Closed;
            else if ((moonNote.flags & MoonNote.Flags.EliteDrums_ForcedIndifferent) != 0)
                hatState = EliteDrumsHatState.Indifferent;

            return hatState;
        }

        private bool GetEliteDrumNoteIsFlam(MoonNote moonNote)
        {
            return (moonNote.flags & MoonNote.Flags.EliteDrums_Flam) != 0;
        }
    }
}

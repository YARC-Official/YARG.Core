using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument instrument)
        {
            return instrument.ToGameMode() switch
            {
                GameMode.FiveFretGuitar => LoadGuitarTrack(instrument, CreateFiveFretGuitarNote),
                GameMode.SixFretGuitar  => LoadGuitarTrack(instrument, CreateSixFretGuitarNote),
                _ => throw new ArgumentException($"Instrument {instrument} is not a guitar instrument!")
            };
        }

        private InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument instrument, CreateNoteDelegate<GuitarNote> createNote)
        {
            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<GuitarNote>>()
            {
                { Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, createNote) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote) },
                { Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, createNote) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote) },
            };
            return new(instrument, difficulties);
        }

        private GuitarNote CreateFiveFretGuitarNote(MoonNote moonNote, Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases)
        {
            var fret = GetFiveFretGuitarFret(moonNote);
            var noteType = GetGuitarNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var guitarFlags = GetGuitarNoteFlags(moonNote);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new GuitarNote(fret, noteType, guitarFlags, generalFlags, time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
        }

        private GuitarNote CreateSixFretGuitarNote(MoonNote moonNote, Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases)
        {
            var fret = GetSixFretGuitarFret(moonNote);
            var noteType = GetGuitarNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var guitarFlags = GetGuitarNoteFlags(moonNote);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new GuitarNote(fret, noteType, guitarFlags, generalFlags, time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
        }

        private FiveFretGuitarFret GetFiveFretGuitarFret(MoonNote moonNote)
        {
            return moonNote.guitarFret switch
            {
                MoonNote.GuitarFret.Open   => FiveFretGuitarFret.Open,
                MoonNote.GuitarFret.Green  => FiveFretGuitarFret.Green,
                MoonNote.GuitarFret.Red    => FiveFretGuitarFret.Red,
                MoonNote.GuitarFret.Yellow => FiveFretGuitarFret.Yellow,
                MoonNote.GuitarFret.Blue   => FiveFretGuitarFret.Blue,
                MoonNote.GuitarFret.Orange => FiveFretGuitarFret.Orange,
                _ => throw new InvalidOperationException($"Invalid Moonscraper guitar fret {moonNote.guitarFret}!")
            };
        }

        private SixFretGuitarFret GetSixFretGuitarFret(MoonNote moonNote)
        {
            return moonNote.ghliveGuitarFret switch
            {
                MoonNote.GHLiveGuitarFret.Open   => SixFretGuitarFret.Open,
                MoonNote.GHLiveGuitarFret.Black1 => SixFretGuitarFret.Black1,
                MoonNote.GHLiveGuitarFret.Black2 => SixFretGuitarFret.Black2,
                MoonNote.GHLiveGuitarFret.Black3 => SixFretGuitarFret.Black3,
                MoonNote.GHLiveGuitarFret.White1 => SixFretGuitarFret.White1,
                MoonNote.GHLiveGuitarFret.White2 => SixFretGuitarFret.White2,
                MoonNote.GHLiveGuitarFret.White3 => SixFretGuitarFret.White3,
                _ => throw new InvalidOperationException($"Invalid Moonscraper guitar fret {moonNote.ghliveGuitarFret}!")
            };
        }

        private GuitarNoteType GetGuitarNoteType(MoonNote moonNote)
        {
            var type = moonNote.GetGuitarType(_moonSong.hopoThreshold);
            return type switch
            {
                MoonNote.MoonNoteType.Strum => GuitarNoteType.Strum,
                MoonNote.MoonNoteType.Hopo  => GuitarNoteType.Hopo,
                MoonNote.MoonNoteType.Tap   => GuitarNoteType.Tap,
                _ => throw new InvalidOperationException($"Unhandled Moonscraper note type {type}!")
            };
        }

        private GuitarNoteFlags GetGuitarNoteFlags(MoonNote moonNote)
        {
            var flags = GuitarNoteFlags.None;

            // Extended sustains
            var nextNote = moonNote.NextSeperateMoonNote;
            if (nextNote is not null && (moonNote.tick + moonNote.length) > nextNote.tick)
            {
                flags |= GuitarNoteFlags.ExtendedSustain;
            }

            // Disjoint chords
            foreach (var note in moonNote.chord)
            {
                if (note.length != moonNote.length)
                {
                    flags |= GuitarNoteFlags.Disjoint;
                    break;
                }
            }

            return flags;
        }
    }
}
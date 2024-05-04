using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument instrument)
        {
            return LoadProKeysTrack(instrument, CreateProKeysNote);
        }

        private InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument instrument, CreateNoteDelegate<ProKeysNote> createNote)
        {
            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<ProKeysNote>>
            {
                { Difficulty.Easy,   LoadDifficulty(instrument, Difficulty.Easy, createNote) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote) },
                { Difficulty.Hard,   LoadDifficulty(instrument, Difficulty.Hard, createNote) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote) },
            };
            return new(instrument, difficulties);
        }

        private ProKeysNote CreateProKeysNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var key = moonNote.proKeysKey;
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var proKeysFlags = GetProKeysNoteFlags(moonNote);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new ProKeysNote(key, proKeysFlags, generalFlags, time, GetLengthInTime(moonNote),
                moonNote.tick, moonNote.length);
        }

        private ProKeysNoteFlags GetProKeysNoteFlags(MoonNote moonNote)
        {
            var flags = ProKeysNoteFlags.None;

            // Extended sustains
            var nextNote = moonNote.NextSeperateMoonNote;
            if (nextNote is not null && (moonNote.tick + moonNote.length) > nextNote.tick &&
                (nextNote.tick - moonNote.tick) > _settings.NoteSnapThreshold)
            {
                flags |= ProKeysNoteFlags.ExtendedSustain;
            }

            // Disjoint chords
            foreach (var note in moonNote.chord)
            {
                if (note.length != moonNote.length)
                {
                    flags |= ProKeysNoteFlags.Disjoint;
                    break;
                }
            }

            return flags;
        }
    }
}
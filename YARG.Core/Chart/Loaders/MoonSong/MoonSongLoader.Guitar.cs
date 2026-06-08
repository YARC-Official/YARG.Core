using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using YARG.Core.Chart.Events;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument instrument)
        {
            return instrument.ToNativeGameMode() switch
            {
                GameMode.FiveFretGuitar => LoadGuitarTrack(instrument, CreateFiveFretGuitarNote),
                GameMode.SixFretGuitar => LoadGuitarTrack(instrument, CreateSixFretGuitarNote),
                _ => throw new ArgumentException($"Instrument {instrument} is not a guitar instrument!")
            };
        }

        private InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument instrument, CreateNoteDelegate<GuitarNote> createNote)
        {
            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<GuitarNote>>()
            {
                { Difficulty.Beginner, LoadDifficulty<GuitarNote>(instrument, Difficulty.Beginner, CreateFiveFretBeginnerNote, null, ValidateGuitarPhrase, null) }, // No lanes on Beginner, so no final pass
                { Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, createNote, null, ValidateGuitarPhrase, null) }, // No lanes on Easy, so no final pass
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote, null, ValidateGuitarPhrase, null) }, // No lanes on Medium, so no final pass
                { Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, createNote, null, ValidateGuitarPhrase, GuitarFinalPass) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote, null, ValidateGuitarPhrase, GuitarFinalPass) },
            };

            var track = new InstrumentTrack<GuitarNote>(instrument, difficulties, GetAnimationTrack(instrument));

            return track;
        }

        private GuitarNote CreateFiveFretGuitarNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases,
            List<GuitarNote> notes)
        {
            var fret = GetFiveFretGuitarFret(moonNote);
            var noteType = GetGuitarNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var guitarFlags = GetGuitarNoteFlags(moonNote);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new GuitarNote(fret, noteType, guitarFlags, generalFlags, time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
        }

        private GuitarNote CreateFiveFretBeginnerNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases,
            List<GuitarNote> notes)
        {
            var fret = FiveFretGuitarFret.Wildcard;
            var noteType = GuitarNoteType.Strum;
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var guitarFlags = GuitarNoteFlags.None;

            double time = _moonSong.TickToTime(moonNote.tick);

            uint tickLength;

            // If Easy contains extended sustains, we'll want to clip them so we don't have wildcards on top of wildcard sustains
            if (moonNote.NextSeperateMoonNote is not null && moonNote.NextSeperateMoonNote.tick < moonNote.tick + moonNote.length)
            {
                tickLength = moonNote.NextSeperateMoonNote.tick - moonNote.tick;
            }
            else
            {
                tickLength = moonNote.length;
            }

            var timeLength = GetLengthInTime(time, moonNote.tick, tickLength);

            return new GuitarNote(fret, noteType, guitarFlags, generalFlags, time, timeLength, moonNote.tick, tickLength);
        }

        private GuitarNote CreateSixFretGuitarNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases,
            List<GuitarNote> notes)
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
                MoonNote.GuitarFret.Open => FiveFretGuitarFret.Open,
                MoonNote.GuitarFret.Green => FiveFretGuitarFret.Green,
                MoonNote.GuitarFret.Red => FiveFretGuitarFret.Red,
                MoonNote.GuitarFret.Yellow => FiveFretGuitarFret.Yellow,
                MoonNote.GuitarFret.Blue => FiveFretGuitarFret.Blue,
                MoonNote.GuitarFret.Orange => FiveFretGuitarFret.Orange,
                _ => throw new InvalidOperationException($"Invalid Moonscraper guitar fret {moonNote.guitarFret}!")
            };
        }

        private SixFretGuitarFret GetSixFretGuitarFret(MoonNote moonNote)
        {
            return moonNote.ghliveGuitarFret switch
            {
                MoonNote.GHLiveGuitarFret.Open => SixFretGuitarFret.Open,
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
            var type = moonNote.GetGuitarNoteType(_moonSong.hopoThreshold);

            // Apply chord HOPO cancellation, if enabled
            if (_settings.ChordHopoCancellation && type == MoonNote.MoonNoteType.Hopo &&
                !moonNote.isChord && (moonNote.flags & MoonNote.Flags.Forced_Hopo) == 0)
            {
                var previous = moonNote.PreviousSeperateMoonNote;
                if (previous is not null && previous.isChord)
                {
                    foreach (var note in previous.chord)
                    {
                        if (note.guitarFret == moonNote.guitarFret)
                        {
                            type = MoonNote.MoonNoteType.Strum;
                            break;
                        }
                    }
                }
            }

            return type switch
            {
                MoonNote.MoonNoteType.Strum => GuitarNoteType.Strum,
                MoonNote.MoonNoteType.Hopo => GuitarNoteType.Hopo,
                MoonNote.MoonNoteType.Tap => GuitarNoteType.Tap,
                _ => throw new InvalidOperationException($"Unhandled Moonscraper note type {type}!")
            };
        }

        private GuitarNoteFlags GetGuitarNoteFlags(MoonNote moonNote)
        {
            var flags = GuitarNoteFlags.None;

            var noteEndTick = moonNote.tick + moonNote.length;

            // Extended sustains (Forwards)
            var nextNote = moonNote.NextSeperateMoonNote;
            var ticksToNextNote = nextNote?.tick - moonNote.tick ?? 0;

            if (nextNote is not null &&
                noteEndTick > nextNote.tick &&
                ticksToNextNote > _settings.NoteSnapThreshold)
            {
                flags |= GuitarNoteFlags.ExtendedSustain;
            }

            // Extended sustains (Backwards)
            var prevNote = moonNote.PreviousSeperateMoonNote;

            if (prevNote is not null)
            {
                var prevNoteTick = prevNote.tick;
                uint largestLength = 0;

                // Must find the longest length of previous note (disjoint chords)
                while (prevNote is not null && prevNote.previous?.tick == prevNote.tick)
                {
                    largestLength = Math.Max(largestLength, prevNote.length);
                    prevNote = prevNote.previous;
                }

                var prevNoteEndTick = prevNoteTick + largestLength;
                var ticksToPrevNote = moonNote.tick - prevNoteTick;

                if (prevNoteEndTick > moonNote.tick &&
                    moonNote.length > 0 &&
                    ticksToPrevNote > _settings.NoteSnapThreshold)
                {
                    flags |= GuitarNoteFlags.ExtendedSustain;
                }
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

        private Phrase? ValidateGuitarPhrase(Phrase phrase, List<Phrase> phrases)
        {
            if (phrase.Type == PhraseType.BigRockEnding)
            {
                // BRE Phrases aren't allowed outside a coda event
                if (!IsWithinCoda(phrase))
                {
                    return null;
                }
            }

            // Check that we don't already have an identical phrase
            foreach (var otherPhrase in phrases)
            {
                if (otherPhrase.Type == phrase.Type && otherPhrase.Tick == phrase.Tick && otherPhrase.TickEnd == phrase.TickEnd)
                {
                    return null;
                }
            }

            return phrase;
        }

        private bool IsWithinCoda(Phrase phrase)
        {
            foreach ((uint start, uint end) in _codaTicks)
            {
                if (phrase.Tick >= start && phrase.TickEnd <= end)
                {
                    return true;
                }
            }

            return false;
        }

        private static void GuitarFinalPass(InstrumentDifficulty<GuitarNote> chart)
        {
            var noteIndex = 0;

            // All we're here to do is assemble lane phrases, so if there aren't any notes or phrases, then we have nothing to do
            if (chart.Phrases.Count == 0 || chart.Notes.Count == 0)
            {
                return;
            }

            foreach (var phrase in chart.Phrases)
            {
                if (phrase.Type is not (PhraseType.TremoloLane or PhraseType.TrillLane))
                {
                    continue;
                }

                var notesInPhrase = GetNotesInPhrase(phrase, chart.Notes, noteIndex, out noteIndex);

                List<GuitarNote> laneNotes;

                switch (phrase.Type)
                {
                    case PhraseType.TremoloLane:
                        laneNotes = GetGuitarTremoloNotes(notesInPhrase);
                        break;
                    case PhraseType.TrillLane:
                        laneNotes = GetGuitarTrillNotes(notesInPhrase);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(phrase.Type), phrase.Type, "Invalid phrase type for Guitar lane");
                }

                if (laneNotes.Count > 0)
                {
                    // Apply lane start flag to entire starting note
                    foreach (var startChild in laneNotes[0].AllNotes)
                    {
                        startChild.ActivateFlag(NoteFlags.LaneStart);
                    }

                    // Cut sustains for all but the last note
                    foreach (var laneNote in laneNotes.GetRange(0, laneNotes.Count - 1))
                    {
                        foreach (var child in laneNote.AllNotes)
                        {
                            child.TickLength = 0;
                            child.TimeLength = 0;
                        }
                    }

                    // Apply lane end flag to entire ending note
                    foreach (var endChild in laneNotes[^1].AllNotes)
                    {
                        endChild.ActivateFlag(NoteFlags.LaneEnd);
                    }
                }
            }
        }

        // Takes all notes that are supposedly inside a guitar tremolo phrase and validates them.
        // Activates the Tremolo flag for all notes in the phrase that constitute a valid tremolo
        //   -For a well-formed chart, this will be all of them
        //   -If the chart is malformed, the tremolo might terminate earlier than the supposed end of the phrase or be invalidated altogether
        // Returns the list of all marked notes. GuitarFinalPass will assign the LaneStart and LaneEnd flags (shared behavior with trills)
        //
        // A guitar tremolo must consist of exactly one notemask, repeated at least twice
        // If the tremolo's notemask changes on the second note, it's not a valid tremolo
        // If the notemask changes later in the tremolo, the tremolo is terminated there
        private static List<GuitarNote> GetGuitarTremoloNotes(List<GuitarNote> notesInPhrase)
        {
            static void AddToTremolo(GuitarNote note, List<GuitarNote> tremolo)
            {
                foreach (var child in note.AllNotes)
                {
                    child.ActivateFlag(NoteFlags.Tremolo);
                }

                tremolo.Add(note);
            }


            List<GuitarNote> tremoloNotes = new();

            if (notesInPhrase.Count < 2 || notesInPhrase[0].NoteMask != notesInPhrase[1].NoteMask)
            {
                // This tremolo doesn't start with two matching masks, so it's invalid. Return an empty list (no tremolo)
                return tremoloNotes;
            }

            // The first two notes are now pre-cleared, so no need to check their masks again
            AddToTremolo(notesInPhrase[0], tremoloNotes);
            AddToTremolo(notesInPhrase[1], tremoloNotes);

            // Go through all further notes in the phrase and add them to the tremolo as long as the mask doesn't change

            for (var i = 2; i < notesInPhrase.Count; i++)
            {
                var note = notesInPhrase[i];

                if (notesInPhrase[i].NoteMask != notesInPhrase[0].NoteMask)
                {
                    // We found a mask that doesn't fit the tremolo; terminate early
                    break;
                }

                AddToTremolo(note, tremoloNotes);
            }

            return tremoloNotes;
        }

        // Takes all notes that are supposedly inside a guitar trill phrase and validates them
        // Activates the Trill flag for all notes in the phrase that constitute a valid trill
        //   -For a well-formed chart, this will be all of them
        //   -If the chart is malformed, the trill might terminate earlier than the supposed end of the phrase or be invalidated altogether
        // Returns the list of all marked notes. GuitarFinalPass will assign the LaneStart and LaneEnd flags (shared behavior with tremolos)
        //
        // A guitar trill must begin with two different single notes - if they match or either is a chord, the trill is invalid
        // The third note of the trill must match the first - if not, the trill is invalid
        // From there, the trill is valid as long as it continues alternating those two notes
        // If it repeats the same note twice, the trill terminates
        // If anything other than the two established notes occurs, the trill terminates
        private static List<GuitarNote> GetGuitarTrillNotes(List<GuitarNote> notesInPhrase)
        {
            List<GuitarNote> trillNotes = new();

            if (notesInPhrase.Count < 3)
            {
                // Need at least three notes to constitute a trill; this is invalid. Return empty (no trill)
                return trillNotes;
            }

            if (notesInPhrase[0].IsChord || notesInPhrase[1].IsChord)
            {
                // Trills can't contain chords; this is invalid. Return empty (no trill)
                return trillNotes;
            }

            if (notesInPhrase[0].NoteMask == notesInPhrase[1].NoteMask)
            {
                // Trills must be two different notes; this is invalid. Return empty (no trill)
                return trillNotes;
            }

            if (notesInPhrase[0].NoteMask != notesInPhrase[2].NoteMask)
            {
                // The third note must match the first; this is invalid. Return empty (no trill)
                return trillNotes;
            }

            // We now know that we have a valid trill, and we've pre-approved the first three notes. Flag them and add them to the list
            notesInPhrase[0].ActivateFlag(NoteFlags.Trill);
            notesInPhrase[1].ActivateFlag(NoteFlags.Trill);
            notesInPhrase[2].ActivateFlag(NoteFlags.Trill);
            trillNotes.Add(notesInPhrase[0]);
            trillNotes.Add(notesInPhrase[1]);
            trillNotes.Add(notesInPhrase[2]);

            // The trill will continue for as long as it keeps alternating these two masks
            var mask = notesInPhrase[0].NoteMask;
            var otherMask = notesInPhrase[1].NoteMask;

            for (var i = 3; i < notesInPhrase.Count; i++)
            {
                var note = notesInPhrase[i];
                if (note.NoteMask == otherMask)
                {
                    note.ActivateFlag(NoteFlags.Trill);
                    trillNotes.Add(note);
                }
                else
                {
                    // We've stopped properly alternating; terminate the trill
                    break;
                }
                (mask, otherMask) = (otherMask, mask);
            }

            return trillNotes;
        }
    }
}

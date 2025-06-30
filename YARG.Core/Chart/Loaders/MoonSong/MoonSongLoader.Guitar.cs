using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;

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

            var track = new InstrumentTrack<GuitarNote>(instrument, difficulties);

            // Add animation events
            var animationEvents = GetGuitarAnimationEvents(track);
            track.AddAnimationEvent(animationEvents);

            return track;
        }

        private GuitarNote CreateFiveFretGuitarNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var fret = GetFiveFretGuitarFret(moonNote);
            var noteType = GetGuitarNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var guitarFlags = GetGuitarNoteFlags(moonNote);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new GuitarNote(fret, noteType, guitarFlags, generalFlags, time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
        }

        private GuitarNote CreateSixFretGuitarNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
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
                MoonNote.MoonNoteType.Hopo  => GuitarNoteType.Hopo,
                MoonNote.MoonNoteType.Tap   => GuitarNoteType.Tap,
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
                while(prevNote is not null && prevNote.previous?.tick == prevNote.tick)
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

        private List<AnimationEvent> GetGuitarAnimationEvents(InstrumentTrack<GuitarNote> track)
        {
            var events = new List<AnimationEvent>();
            var instrument = track.Instrument;

            // Find a difficulty
            // var difficulty = track.FirstDifficulty().Difficulty;
            var difficulty = Difficulty.Expert;

            // Get the relevant MoonChart
            var chart = GetMoonChart(instrument, difficulty);

            foreach (var animNote in chart.animationNotes)
            {
                // Look up the note number and create an appropriate animation event
                var animType = GetGuitarAnimationType((byte) animNote.noteNumber);

                if (!animType.HasValue) continue;

                events.Add(new AnimationEvent(animType.Value,
                    _moonSong.TickToTime(animNote.tick), GetLengthInTime(animNote), animNote.tick, animNote.length));
            }

            return events;
        }

        private AnimationEvent.AnimationType? GetGuitarAnimationType(byte noteNumber)
        {
            return noteNumber switch
            {
                MidIOHelper.LEFT_HAND_POSITION_1 => AnimationEvent.AnimationType.LeftHandPosition1,
                MidIOHelper.LEFT_HAND_POSITION_2 => AnimationEvent.AnimationType.LeftHandPosition2,
                MidIOHelper.LEFT_HAND_POSITION_3 => AnimationEvent.AnimationType.LeftHandPosition3,
                MidIOHelper.LEFT_HAND_POSITION_4 => AnimationEvent.AnimationType.LeftHandPosition4,
                MidIOHelper.LEFT_HAND_POSITION_5 => AnimationEvent.AnimationType.LeftHandPosition5,
                MidIOHelper.LEFT_HAND_POSITION_6 => AnimationEvent.AnimationType.LeftHandPosition6,
                MidIOHelper.LEFT_HAND_POSITION_7 => AnimationEvent.AnimationType.LeftHandPosition7,
                MidIOHelper.LEFT_HAND_POSITION_8 => AnimationEvent.AnimationType.LeftHandPosition8,
                MidIOHelper.LEFT_HAND_POSITION_9 => AnimationEvent.AnimationType.LeftHandPosition9,
                MidIOHelper.LEFT_HAND_POSITION_10 => AnimationEvent.AnimationType.LeftHandPosition10,
                MidIOHelper.LEFT_HAND_POSITION_11 => AnimationEvent.AnimationType.LeftHandPosition11,
                MidIOHelper.LEFT_HAND_POSITION_12 => AnimationEvent.AnimationType.LeftHandPosition12,
                MidIOHelper.LEFT_HAND_POSITION_13 => AnimationEvent.AnimationType.LeftHandPosition13,
                MidIOHelper.LEFT_HAND_POSITION_14 => AnimationEvent.AnimationType.LeftHandPosition14,
                MidIOHelper.LEFT_HAND_POSITION_15 => AnimationEvent.AnimationType.LeftHandPosition15,
                MidIOHelper.LEFT_HAND_POSITION_16 => AnimationEvent.AnimationType.LeftHandPosition16,
                MidIOHelper.LEFT_HAND_POSITION_17 => AnimationEvent.AnimationType.LeftHandPosition17,
                MidIOHelper.LEFT_HAND_POSITION_18 => AnimationEvent.AnimationType.LeftHandPosition18,
                MidIOHelper.LEFT_HAND_POSITION_19 => AnimationEvent.AnimationType.LeftHandPosition19,
                MidIOHelper.LEFT_HAND_POSITION_20 => AnimationEvent.AnimationType.LeftHandPosition20,
                _ => null
            };
        }
    }
}
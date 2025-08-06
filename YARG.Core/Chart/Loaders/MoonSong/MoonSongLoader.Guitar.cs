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

            var animationTrack = GetGuitarAnimationTrack(instrument);
            var track = new InstrumentTrack<GuitarNote>(instrument, difficulties, animationTrack);

            // Add animation events
            var animationEvents = GetGuitarAnimationEvents(instrument);
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

        private readonly Dictionary<Instrument, List<CharacterState>> _characterStateCache = new();
        private readonly Dictionary<Instrument, List<HandMap>> _handMapCache = new();
        private readonly Dictionary<Instrument, List<StrumMap>> _strumMapCache = new();
        private readonly Dictionary<Instrument, List<AnimationEvent>> _animationEventCache = new();

        private static readonly Dictionary<string, CharacterState.CharacterStateType> CharacterStateLookup = new()
        {
            { AnimationLookup.ANIMATION_STATE_IDLE,          CharacterState.CharacterStateType.Idle },
            { AnimationLookup.ANIMATION_STATE_IDLE_INTENSE,  CharacterState.CharacterStateType.IdleIntense },
            { AnimationLookup.ANIMATION_STATE_IDLE_REALTIME, CharacterState.CharacterStateType.IdleRealtime },
            { AnimationLookup.ANIMATION_STATE_PLAY,          CharacterState.CharacterStateType.Play },
            { AnimationLookup.ANIMATION_STATE_PLAY_SOLO,     CharacterState.CharacterStateType.PlaySolo },
            { AnimationLookup.ANIMATION_STATE_PLAY_INTENSE,  CharacterState.CharacterStateType.Intense },
            { AnimationLookup.ANIMATION_STATE_PLAY_MELLOW,   CharacterState.CharacterStateType.Mellow },
        };

        private void ProcessGuitarAnimationData(Instrument instrument)
        {
            if (_characterStateCache.ContainsKey(instrument))
            {
                return;
            }

            var characterStates = new List<CharacterState>();
            var handMaps = new List<HandMap>();
            var strumMaps = new List<StrumMap>();
            var animationEvents = new List<AnimationEvent>();

            // TODO: What if expert doesn't exist?
            var chart = GetMoonChart(instrument, Difficulty.Expert);

            // Process text events
            foreach (var textEvent in chart.events)
            {
                double time = _moonSong.TickToTime(textEvent.tick);
                string eventName = textEvent.text;

                // Event format is `[text]`
                // if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
                // {
                //     continue;
                // }

                // string eventName = text[1..^1];

                // Character States
                if (CharacterStateLookup.TryGetValue(eventName, out var characterType))
                {
                    characterStates.Add(new CharacterState(characterType, time, textEvent.tick));
                    continue;
                }

                // Hand Maps
                if (AnimationLookup.LeftHandMapLookup.TryGetValue(eventName, out var handMapType))
                {
                    handMaps.Add(new HandMap(handMapType, time, textEvent.tick));
                    continue;
                }

                // Strum Maps (only for bass)
                if (instrument == Instrument.FiveFretBass && AnimationLookup.RightHandMapLookup.TryGetValue(eventName, out var strumMapType))
                {
                    strumMaps.Add(new StrumMap(strumMapType, time, textEvent.tick));
                }
            }

            // Process animation notes
            foreach (var animNote in chart.animationNotes)
            {
                var animType = GetGuitarAnimationType(animNote.text);
                if (animType.HasValue)
                {
                    animationEvents.Add(new AnimationEvent(animType.Value,
                        _moonSong.TickToTime(animNote.tick), GetLengthInTime(animNote), animNote.tick, animNote.length));
                }
            }

            _characterStateCache[instrument] = characterStates;
            _handMapCache[instrument] = handMaps;
            _strumMapCache[instrument] = strumMaps;
            _animationEventCache[instrument] = animationEvents;
        }

        private AnimationTrack GetGuitarAnimationTrack(Instrument instrument)
        {
            var characterStates = GetCharacterStates(instrument);
            var handMaps = GetGuitarHandMaps(instrument);
            var strumMaps = GetGuitarStrumMaps(instrument);
            var animationEvents = GetGuitarAnimationEvents(instrument);

            return new AnimationTrack(characterStates, handMaps, strumMaps, animationEvents);
        }

        private List<CharacterState> GetCharacterStates(Instrument instrument)
        {
            ProcessGuitarAnimationData(instrument);
            return _characterStateCache[instrument];
        }

        private List<HandMap> GetGuitarHandMaps(Instrument instrument)
        {
            ProcessGuitarAnimationData(instrument);
            return _handMapCache[instrument];
        }

        private List<StrumMap> GetGuitarStrumMaps(Instrument instrument)
        {
            ProcessGuitarAnimationData(instrument);
            return _strumMapCache[instrument];
        }

        private List<AnimationEvent> GetGuitarAnimationEvents(Instrument instrument)
        {
            ProcessGuitarAnimationData(instrument);
            return _animationEventCache[instrument];
        }

        private AnimationEvent.AnimationType? GetGuitarAnimationType(string eventText)
        {
            return eventText switch
            {
                AnimationLookup.LH_POSITION_1  => AnimationEvent.AnimationType.LeftHandPosition1,
                AnimationLookup.LH_POSITION_2  => AnimationEvent.AnimationType.LeftHandPosition2,
                AnimationLookup.LH_POSITION_3  => AnimationEvent.AnimationType.LeftHandPosition3,
                AnimationLookup.LH_POSITION_4  => AnimationEvent.AnimationType.LeftHandPosition4,
                AnimationLookup.LH_POSITION_5  => AnimationEvent.AnimationType.LeftHandPosition5,
                AnimationLookup.LH_POSITION_6  => AnimationEvent.AnimationType.LeftHandPosition6,
                AnimationLookup.LH_POSITION_7  => AnimationEvent.AnimationType.LeftHandPosition7,
                AnimationLookup.LH_POSITION_8  => AnimationEvent.AnimationType.LeftHandPosition8,
                AnimationLookup.LH_POSITION_9  => AnimationEvent.AnimationType.LeftHandPosition9,
                AnimationLookup.LH_POSITION_10 => AnimationEvent.AnimationType.LeftHandPosition10,
                AnimationLookup.LH_POSITION_11 => AnimationEvent.AnimationType.LeftHandPosition11,
                AnimationLookup.LH_POSITION_12 => AnimationEvent.AnimationType.LeftHandPosition12,
                AnimationLookup.LH_POSITION_13 => AnimationEvent.AnimationType.LeftHandPosition13,
                AnimationLookup.LH_POSITION_14 => AnimationEvent.AnimationType.LeftHandPosition14,
                AnimationLookup.LH_POSITION_15 => AnimationEvent.AnimationType.LeftHandPosition15,
                AnimationLookup.LH_POSITION_16 => AnimationEvent.AnimationType.LeftHandPosition16,
                AnimationLookup.LH_POSITION_17 => AnimationEvent.AnimationType.LeftHandPosition17,
                AnimationLookup.LH_POSITION_18 => AnimationEvent.AnimationType.LeftHandPosition18,
                AnimationLookup.LH_POSITION_19 => AnimationEvent.AnimationType.LeftHandPosition19,
                AnimationLookup.LH_POSITION_20 => AnimationEvent.AnimationType.LeftHandPosition20,
                _ => null
            };
        }
    }
}

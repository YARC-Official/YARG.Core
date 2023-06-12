// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MoonscraperEngine;

namespace MoonscraperChartEditor.Song.IO
{
    using NoteEventQueue = List<(NoteEvent note, long tick)>;
    using SysExEventQueue = List<(PhaseShiftSysEx sysex, long tick)>;

    public static class MidReader
    {
        private const int SOLO_END_CORRECTION_OFFSET = -1;

        // true == override existing track, false == discard if already exists
        private static readonly Dictionary<string, bool> TrackOverrides = new()
        {
            { MidIOHelper.GUITAR_TRACK,     true },
            { MidIOHelper.GH1_GUITAR_TRACK, false },

            { MidIOHelper.DRUMS_TRACK,      true },
            { MidIOHelper.DRUMS_REAL_TRACK, false },
        };

        private static readonly Dictionary<string, bool> ExcludedTracks = new()
        {
            { MidIOHelper.BEAT_TRACK,       true },
            { MidIOHelper.VENUE_TRACK,      true },
        };

        private static readonly List<MoonSong.MoonInstrument> LegacyStarPowerFixupWhitelist = new()
        {
            MoonSong.MoonInstrument.Guitar,
            MoonSong.MoonInstrument.GuitarCoop,
            MoonSong.MoonInstrument.Bass,
            MoonSong.MoonInstrument.Rhythm,
        };

        private struct TimedMidiEvent
        {
            public MidiEvent midiEvent;
            public long startTick;
            public long endTick;

            public long length => endTick - startTick;
        }

        private struct EventProcessParams
        {
            public MoonSong song;
            public MoonSong.MoonInstrument instrument;
            public MoonChart currentUnrecognisedChart;
            public TimedMidiEvent timedEvent;
            public Dictionary<int, EventProcessFn> noteProcessMap;
            public Dictionary<string, ProcessModificationProcessFn> textProcessMap;
            public Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> sysexProcessMap;
            public List<EventProcessFn> delayedProcessesList;
        }

        // Delegate for functions that parse something into the chart
        private delegate void EventProcessFn(in EventProcessParams eventProcessParams);
        // Delegate for functions that modify how the chart should be parsed
        private delegate void ProcessModificationProcessFn(ref EventProcessParams eventProcessParams);

        // These dictionaries map the NoteNumber of each midi note event to a specific function of how to process them
        private static readonly Dictionary<int, EventProcessFn> GuitarMidiNoteNumberToProcessFnMap = BuildGuitarMidiNoteNumberToProcessFnDict();
        private static readonly Dictionary<int, EventProcessFn> GuitarMidiNoteNumberToProcessFnMap_EnhancedOpens = BuildGuitarMidiNoteNumberToProcessFnDict(enhancedOpens: true);
        private static readonly Dictionary<int, EventProcessFn> GhlGuitarMidiNoteNumberToProcessFnMap = BuildGhlGuitarMidiNoteNumberToProcessFnDict();
        private static readonly Dictionary<int, EventProcessFn> ProGuitarMidiNoteNumberToProcessFnMap = BuildProGuitarMidiNoteNumberToProcessFnDict();
        private static readonly Dictionary<int, EventProcessFn> DrumsMidiNoteNumberToProcessFnMap = BuildDrumsMidiNoteNumberToProcessFnDict();
        private static readonly Dictionary<int, EventProcessFn> DrumsMidiNoteNumberToProcessFnMap_Velocity = BuildDrumsMidiNoteNumberToProcessFnDict(enableVelocity: true);

        // These dictionaries map the text of a MIDI text event to a specific function that processes them
        private static readonly Dictionary<string, ProcessModificationProcessFn> GuitarTextEventToProcessFnMap = new()
        {
            { MidIOHelper.ENHANCED_OPENS_TEXT, SwitchToGuitarEnhancedOpensProcessMap },
            { MidIOHelper.ENHANCED_OPENS_TEXT_BRACKET, SwitchToGuitarEnhancedOpensProcessMap }
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> GhlGuitarTextEventToProcessFnMap = new()
        {
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> ProGuitarTextEventToProcessFnMap = new()
        {
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> DrumsTextEventToProcessFnMap = new()
        {
            { MidIOHelper.CHART_DYNAMICS_TEXT, SwitchToDrumsVelocityProcessMap },
            { MidIOHelper.CHART_DYNAMICS_TEXT_BRACKET, SwitchToDrumsVelocityProcessMap },
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> GuitarSysExEventToProcessFnMap = new()
        {
            { PhaseShiftSysEx.PhraseCode.Guitar_Open, ProcessSysExEventPairAsOpenNoteModifier },
            { PhaseShiftSysEx.PhraseCode.Guitar_Tap, (in EventProcessParams eventProcessParams) => {
                ProcessSysExEventPairAsForcedType(eventProcessParams, MoonNote.MoonNoteType.Tap);
            }},
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> GhlGuitarSysExEventToProcessFnMap = new()
        {
            { PhaseShiftSysEx.PhraseCode.Guitar_Open, ProcessSysExEventPairAsOpenNoteModifier },
            { PhaseShiftSysEx.PhraseCode.Guitar_Tap, (in EventProcessParams eventProcessParams) => {
                ProcessSysExEventPairAsForcedType(eventProcessParams, MoonNote.MoonNoteType.Tap);
            }},
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> ProGuitarSysExEventToProcessFnMap = new()
        {
        };

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> DrumsSysExEventToProcessFnMap = new()
        {
        };

        private static readonly ReadingSettings ReadSettings = new()
        {
            InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
            NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
            NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
            InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
        };

        public static MoonSong ReadMidi(string path)
        {
            MidiFile midi;
            try
            {
                midi = MidiFile.Read(path, ReadSettings);
            }
            catch (Exception e)
            {
                throw new Exception("Bad or corrupted midi file!", e);
            }

            return ReadMidi(midi);
        }

        public static MoonSong ReadMidi(MidiFile midi)
        {
            if (midi.Chunks == null || midi.Chunks.Count < 1)
                throw new InvalidOperationException("MIDI file has no tracks, unable to parse.");

            if (midi.TimeDivision is not TicksPerQuarterNoteTimeDivision ticks)
                throw new InvalidOperationException("MIDI file has no beat resolution set!");

            var song = new MoonSong()
            {
                resolution = ticks.TicksPerQuarterNote
            };

            // Read all bpm data in first. This will also allow song.TimeToTick to function properly.
            ReadSync(midi.GetTempoMap(), song);

            foreach (var track in midi.GetTrackChunks())
            {
                if (track == null || track.Events.Count < 1)
                {
                    Debug.WriteLine("Encountered an empty track!");
                    continue;
                }

                if (track.Events[0] is not SequenceTrackNameEvent trackName)
                {
                    Debug.WriteLine($"Could not determine track name! (Likely the tempo track)");
                    continue;
                }
                Debug.WriteLine("Found midi track " + trackName.Text);

                string trackNameKey = trackName.Text.ToUpper();
                if (ExcludedTracks.ContainsKey(trackNameKey))
                    continue;

                switch (trackNameKey)
                {
                    case MidIOHelper.EVENTS_TRACK:
                        ReadSongGlobalEvents(track, song);
                        break;

                    case MidIOHelper.VOCALS_TRACK:
                        ReadTextEventsIntoGlobalEventsAsLyrics(track, song);
                        break;

                    default:
                        MoonSong.MoonInstrument instrument;
                        if (!MidIOHelper.TrackNameToInstrumentMap.TryGetValue(trackNameKey, out instrument))
                        {
                            instrument = MoonSong.MoonInstrument.Unrecognised;
                        }
                        else if (song.ChartExistsForInstrument(instrument))
                        {
                            if (!TrackOverrides.TryGetValue(trackNameKey, out bool overwrite) || !overwrite)
                                continue;

                            // Overwrite existing track
                            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
                            {
                                var chart = song.GetChart(instrument, difficulty);
                                chart.Clear();
                                chart.UpdateCache();
                            }
                        }

                        Debug.WriteLine("Loading midi track {0}", instrument);
                        ReadNotes(track, song, instrument);
                        break;
                }
            }

            return song;
        }

        private static void ReadSync(TempoMap tempoMap, MoonSong song)
        {
            foreach (var tempo in tempoMap.GetTempoChanges())
            {
                song.Add(new BPM((uint)tempo.Time, (uint)(tempo.Value.BeatsPerMinute * 1000)), false);
            }
            foreach (var timesig in tempoMap.GetTimeSignatureChanges())
            {
                song.Add(new TimeSignature((uint)timesig.Time, (uint)timesig.Value.Numerator, (uint)Math.Pow(2, timesig.Value.Denominator)), false);
            }

            song.UpdateCache();
        }

        private static void ReadSongGlobalEvents(TrackChunk track, MoonSong song)
        {
            const string rb2SectionPrefix = "[" + MidIOHelper.SECTION_PREFIX_RB2;
            const string rb3SectionPrefix = "[" + MidIOHelper.SECTION_PREFIX_RB3;

            if (track.Events.Count < 1)
                return;

            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                if (trackEvent is BaseTextEvent text)
                {
                    if (text.Text.Contains(rb2SectionPrefix))
                    {
                        song.Add(new Section(text.Text[9..^10], (uint)absoluteTime), false);
                    }
                    else if (text.Text.Contains(rb3SectionPrefix) && text.Text.Length > 1)
                    {
                        string sectionText = string.Empty;
                        char lastChar = text.Text[^1];
                        if (lastChar == ']')
                        {
                            sectionText = text.Text[5..^1];
                        }
                        else if (lastChar == '"')
                        {
                            // Is in the format [prc_intro] "Intro". Strip for just the quoted section
                            int startIndex = text.Text.IndexOf('"') + 1;
                            sectionText = text.Text.Substring(startIndex, text.Text.Length - (startIndex + 1));
                        }
                        else
                        {
                            Debug.WriteLine("Found section name in an unknown format: " + text.Text);
                        }

                        song.Add(new Section(sectionText, (uint)absoluteTime), false);
                    }
                    else
                    {
                        song.Add(new Event(text.Text.Trim(new char[] { '[', ']' }), (uint)absoluteTime), false);
                    }
                }
            }

            song.UpdateCache();
        }

        private static void ReadTextEventsIntoGlobalEventsAsLyrics(TrackChunk track, MoonSong song)
        {
            if (track.Events.Count < 1)
                return;

            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                if (trackEvent is LyricEvent lyric && lyric.Text.Length > 0)
                {
                    string lyricEvent = MidIOHelper.LYRIC_EVENT_PREFIX + lyric.Text;
                    song.Add(new Event(lyricEvent, (uint)absoluteTime), false);
                }

                if (trackEvent is NoteEvent note && (byte)note.NoteNumber is MidIOHelper.LYRICS_PHRASE_1 or MidIOHelper.LYRICS_PHRASE_2)
                {
                    if (note.EventType == MidiEventType.NoteOn)
                        song.Add(new Event(MidIOHelper.LYRICS_PHRASE_START_TEXT, (uint)absoluteTime), false);
                    else if (note.EventType == MidiEventType.NoteOff)
                        song.Add(new Event(MidIOHelper.LYRICS_PHRASE_END_TEXT, (uint)absoluteTime), false);
                }
            }

            song.UpdateCache();
        }

        private static void ReadNotes(TrackChunk track, MoonSong song, MoonSong.MoonInstrument instrument)
        {
            if (track == null || track.Events.Count < 1)
            {
                Debug.WriteLine($"Attempted to load null or empty track.");
                return;
            }

            var unpairedNoteQueue = new NoteEventQueue();
            var unpairedSysexQueue = new SysExEventQueue();

            var unrecognised = new MoonChart(song, MoonSong.MoonInstrument.Unrecognised);
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);

            var processParams = new EventProcessParams()
            {
                song = song,
                currentUnrecognisedChart = unrecognised,
                instrument = instrument,
                noteProcessMap = GetNoteProcessDict(gameMode),
                textProcessMap = GetTextEventProcessDict(gameMode),
                sysexProcessMap = GetSysExEventProcessDict(gameMode),
                delayedProcessesList = new List<EventProcessFn>(),
            };

            if (instrument == MoonSong.MoonInstrument.Unrecognised)
            {
                song.unrecognisedCharts.Add(unrecognised);
            }

            // Load all the notes
            long absoluteTick = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTick += trackEvent.DeltaTime;

                processParams.timedEvent = new TimedMidiEvent()
                {
                    midiEvent = trackEvent,
                    startTick = absoluteTick
                };

                if (trackEvent is NoteEvent note)
                {
                    ProcessNoteEvent(ref processParams, unpairedNoteQueue, note, absoluteTick);
                }
                else if (trackEvent is BaseTextEvent text)
                {
                    ProcessTextEvent(ref processParams, text, absoluteTick);
                }
                else if (trackEvent is SysExEvent sysex)
                {
                    ProcessSysExEvent(ref processParams, unpairedSysexQueue, sysex, absoluteTick);
                }
            }

            Debug.Assert(unpairedNoteQueue.Count == 0, $"Note queue was not fully processed! Remaining event count: {unpairedNoteQueue.Count}");
            Debug.Assert(unpairedSysexQueue.Count == 0, $"SysEx event queue was not fully processed! Remaining event count: {unpairedSysexQueue.Count}");

            // Update all chart arrays
            if (instrument != MoonSong.MoonInstrument.Unrecognised)
            {
                foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
                {
                    song.GetChart(instrument, diff).UpdateCache();
                }
            }
            else
            {
                unrecognised.UpdateCache();
            }

            // Apply forcing events
            foreach (var process in processParams.delayedProcessesList)
            {
                process(processParams);
            }

            // Legacy star power fixup
            FixupStarPowerIfNeeded(ref processParams);
        }

        private static void FixupStarPowerIfNeeded(ref EventProcessParams processParams)
        {
            // Check if instrument is allowed to be fixed up
            if (!LegacyStarPowerFixupWhitelist.Contains(processParams.instrument))
                return;

            // Only need to check one difficulty since Star Power gets copied to all difficulties
            var chart = processParams.song.GetChart(processParams.instrument, MoonSong.Difficulty.Expert);
            if (chart.specialPhrases.Any((sp) => sp.type == SpecialPhrase.Type.Starpower)
                || !chart.events.Any((text) => text.eventName is MidIOHelper.SOLO_EVENT_TEXT or MidIOHelper.SOLO_END_EVENT_TEXT))
            {
                return;
            }

            ProcessTextEventPairAsSpecialPhrase(processParams, MidIOHelper.SOLO_EVENT_TEXT, MidIOHelper.SOLO_END_EVENT_TEXT, SpecialPhrase.Type.Starpower);
            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                chart = processParams.song.GetChart(processParams.instrument, diff);
                chart.UpdateCache();
            }
        }

        private static void ProcessNoteEvent(ref EventProcessParams processParams, NoteEventQueue unpairedNotes,
            NoteEvent note, long absoluteTick)
        {
            if (note.EventType == MidiEventType.NoteOn)
            {
                // Check for duplicates
                if (TryFindMatchingNote(unpairedNotes, note, out _, out _, out _))
                    Debug.WriteLine($"Found duplicate note on at tick {absoluteTick}!");
                else
                    unpairedNotes.Add((note, absoluteTick));
            }
            else if (note.EventType == MidiEventType.NoteOff)
            {
                if (!TryFindMatchingNote(unpairedNotes, note, out var noteStart, out long startTick, out int startIndex))
                {
                    Debug.WriteLine($"Found note off with no corresponding note on at tick {absoluteTick}!");
                    return;
                }
                unpairedNotes.RemoveAt(startIndex);

                if (processParams.instrument == MoonSong.MoonInstrument.Unrecognised)
                {
                    uint tick = (uint)absoluteTick;
                    uint sus = ApplySustainCutoff(processParams.song, (uint)(absoluteTick - startTick));

                    int rawNote = noteStart.NoteNumber;
                    var newNote = new MoonNote(tick, rawNote, sus);
                    processParams.currentUnrecognisedChart.Add(newNote);
                    return;
                }

                processParams.timedEvent.midiEvent = noteStart;
                processParams.timedEvent.startTick = startTick;
                processParams.timedEvent.endTick = absoluteTick;

                if (processParams.noteProcessMap.TryGetValue(noteStart.NoteNumber, out var processFn))
                {
                    processFn(processParams);
                }
            }
        }

        private static void ProcessTextEvent(ref EventProcessParams processParams, BaseTextEvent text, long absoluteTick)
        {
            uint tick = (uint)absoluteTick;
            string eventName = text.Text;

            var chartEvent = new ChartEvent(tick, eventName);

            if (processParams.instrument == MoonSong.MoonInstrument.Unrecognised)
            {
                processParams.currentUnrecognisedChart.Add(chartEvent);
            }
            else
            {
                if (processParams.textProcessMap.TryGetValue(eventName, out var processFn))
                {
                    // This text event affects parsing of the .mid file, run its function and don't parse it into the chart
                    processFn(ref processParams);
                }
                else
                {
                    // Copy text event to all difficulties so that .chart format can store these properly. Midi writer will strip duplicate events just fine anyway.
                    foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
                    {
                        processParams.song.GetChart(processParams.instrument, difficulty).Add(chartEvent);
                    }
                }
            }
        }

        private static void ProcessSysExEvent(ref EventProcessParams processParams, SysExEventQueue unpairedSysex,
            SysExEvent sysex, long absoluteTick)
        {
            if (!PhaseShiftSysEx.TryParse(sysex, out var psEvent))
            {
                // SysEx event is not a Phase Shift SysEx event
                Debug.WriteLine($"Encountered unknown SysEx event: {BitConverter.ToString(sysex.Data)}");
                return;
            }

            if (psEvent.type != PhaseShiftSysEx.Type.Phrase)
            {
                Debug.WriteLine($"Encountered unknown Phase Shift SysEx event type {psEvent.type}");
                return;
            }

            if (psEvent.phraseValue == PhaseShiftSysEx.PhraseValue.Start)
            {
                // Check for duplicates
                if (TryFindMatchingSysEx(unpairedSysex, psEvent, out _, out _, out _))
                    Debug.WriteLine($"Found duplicate SysEx start event at tick {absoluteTick}!");
                else
                    unpairedSysex.Add((psEvent, absoluteTick));
            }
            else if (psEvent.phraseValue == PhaseShiftSysEx.PhraseValue.End)
            {
                if (!TryFindMatchingSysEx(unpairedSysex, psEvent, out var sysexStart, out long startTick, out int startIndex))
                {
                    Debug.WriteLine($"Found PS SysEx end with no corresponding start at tick {absoluteTick}!");
                    return;
                }
                unpairedSysex.RemoveAt(startIndex);

                processParams.timedEvent.midiEvent = sysexStart;
                processParams.timedEvent.startTick = startTick;
                processParams.timedEvent.endTick = absoluteTick;

                if (processParams.sysexProcessMap.TryGetValue(psEvent.phraseCode, out var processFn))
                {
                    processFn(processParams);
                }
            }
        }

        private static bool TryFindMatchingNote(NoteEventQueue unpairedNotes, NoteEvent noteToMatch,
            out NoteEvent matchingNote, out long matchTick, out int matchIndex)
        {
            for (int i = 0; i < unpairedNotes.Count; i++)
            {
                var queued = unpairedNotes[i];
                if (queued.note.NoteNumber == noteToMatch.NoteNumber && queued.note.Channel == noteToMatch.Channel)
                {
                    (matchingNote, matchTick) = queued;
                    matchIndex = i;
                    return true;
                }
            }

            matchingNote = null;
            matchTick = -1;
            matchIndex = -1;
            return false;
        }

        private static bool TryFindMatchingSysEx(SysExEventQueue unpairedSysex, PhaseShiftSysEx sysexToMatch,
            out PhaseShiftSysEx matchingSysex, out long matchTick, out int matchIndex)
        {
            for (int i = 0; i < unpairedSysex.Count; i++)
            {
                var queued = unpairedSysex[i];
                if (queued.sysex.MatchesWith(sysexToMatch))
                {
                    (matchingSysex, matchTick) = queued;
                    matchIndex = i;
                    return true;
                }
            }

            matchingSysex = null;
            matchTick = -1;
            matchIndex = -1;
            return false;
        }

        private static bool ContainsTextEvent(IList<ChartEvent> events, string text)
        {
            foreach (var textEvent in events)
            {
                if (textEvent.eventName == text)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<int, EventProcessFn> GetNoteProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.GHLGuitar => GhlGuitarMidiNoteNumberToProcessFnMap,
                MoonChart.GameMode.ProGuitar => ProGuitarMidiNoteNumberToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsMidiNoteNumberToProcessFnMap,
                _ => GuitarMidiNoteNumberToProcessFnMap
            };
        }

        private static Dictionary<string, ProcessModificationProcessFn> GetTextEventProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.GHLGuitar => GhlGuitarTextEventToProcessFnMap,
                MoonChart.GameMode.ProGuitar => ProGuitarTextEventToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsTextEventToProcessFnMap,
                _ => GuitarTextEventToProcessFnMap
            };
        }

        private static Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> GetSysExEventProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarSysExEventToProcessFnMap,
                MoonChart.GameMode.GHLGuitar => GhlGuitarSysExEventToProcessFnMap,
                MoonChart.GameMode.ProGuitar => ProGuitarSysExEventToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsSysExEventToProcessFnMap,
                // Don't process any SysEx events on unrecognized tracks
                _ => new()
            };
        }

        private static void SwitchToGuitarEnhancedOpensProcessMap(ref EventProcessParams processParams)
        {
            var gameMode = MoonSong.InstumentToChartGameMode(processParams.instrument);
            if (gameMode != MoonChart.GameMode.Guitar)
            {
                Debug.WriteLine($"Attempted to apply guitar enhanced opens process map to non-guitar instrument: {processParams.instrument}");
                return;
            }

            // Switch process map to guitar enhanced opens process map
            processParams.noteProcessMap = GuitarMidiNoteNumberToProcessFnMap_EnhancedOpens;
        }

        private static void SwitchToDrumsVelocityProcessMap(ref EventProcessParams processParams)
        {
            if (processParams.instrument != MoonSong.MoonInstrument.Drums)
            {
                Debug.WriteLine($"Attempted to apply drums velocity process map to non-drums instrument: {processParams.instrument}");
                return;
            }

            // Switch process map to drums velocity process map
            processParams.noteProcessMap = DrumsMidiNoteNumberToProcessFnMap_Velocity;
        }

        private static Dictionary<int, EventProcessFn> BuildGuitarMidiNoteNumberToProcessFnDict(bool enhancedOpens = false)
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, ProcessNoteOnEventAsStarpower },
                { MidIOHelper.TAP_NOTE_CH, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsForcedType(eventProcessParams, MoonNote.MoonNoteType.Tap);
                }},
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsEvent(eventProcessParams, MidIOHelper.SOLO_EVENT_TEXT, MidIOHelper.SOLO_END_EVENT_TEXT, tickEndOffset: SOLO_END_CORRECTION_OFFSET);
                }},

                { MidIOHelper.VERSUS_PHRASE_PLAYER_1, ProcessNoteOnEventAsVersusPlayerOne },
                { MidIOHelper.VERSUS_PHRASE_PLAYER_2, ProcessNoteOnEventAsVersusPlayerTwo },
                { MidIOHelper.TREMOLO_LANE_NOTE, ProcessNoteOnEventAsTremoloLane },
                { MidIOHelper.TRILL_LANE_NOTE, ProcessNoteOnEventAsTrillLane },
            };

            var FretToMidiKey = new Dictionary<MoonNote.GuitarFret, int>()
            {
                { MoonNote.GuitarFret.Green, 0 },
                { MoonNote.GuitarFret.Red, 1 },
                { MoonNote.GuitarFret.Yellow, 2 },
                { MoonNote.GuitarFret.Blue, 3 },
                { MoonNote.GuitarFret.Orange, 4 },
            };

            if (enhancedOpens)
                FretToMidiKey.Add(MoonNote.GuitarFret.Open, -1);

            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var guitarFret in EnumX<MoonNote.GuitarFret>.Values)
                {
                    if (FretToMidiKey.TryGetValue(guitarFret, out int fretOffset))
                    {
                        int key = fretOffset + difficultyStartRange;
                        int fret = (int)guitarFret;

                        processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                        {
                            ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret);
                        });
                    }
                }

                // Process forced hopo or forced strum
                {
                    int flagKey = difficultyStartRange + 5;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, MoonNote.MoonNoteType.Hopo);
                    });
                }
                {
                    int flagKey = difficultyStartRange + 6;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, MoonNote.MoonNoteType.Strum);
                    });
                }
            };

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildGhlGuitarMidiNoteNumberToProcessFnDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, ProcessNoteOnEventAsStarpower },
                { MidIOHelper.TAP_NOTE_CH, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsForcedType(eventProcessParams, MoonNote.MoonNoteType.Tap);
                }},
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsEvent(eventProcessParams, MidIOHelper.SOLO_EVENT_TEXT, MidIOHelper.SOLO_END_EVENT_TEXT, tickEndOffset: SOLO_END_CORRECTION_OFFSET);
                }},
            };

            var FretToMidiKey = new Dictionary<MoonNote.GHLiveGuitarFret, int>()
            {
                { MoonNote.GHLiveGuitarFret.Open, 0 },
                { MoonNote.GHLiveGuitarFret.White1, 1 },
                { MoonNote.GHLiveGuitarFret.White2, 2 },
                { MoonNote.GHLiveGuitarFret.White3, 3 },
                { MoonNote.GHLiveGuitarFret.Black1, 4 },
                { MoonNote.GHLiveGuitarFret.Black2, 5 },
                { MoonNote.GHLiveGuitarFret.Black3, 6 },
            };

            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.GHL_GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var guitarFret in EnumX<MoonNote.GHLiveGuitarFret>.Values)
                {
                    if (FretToMidiKey.TryGetValue(guitarFret, out int fretOffset))
                    {
                        int key = fretOffset + difficultyStartRange;
                        int fret = (int)guitarFret;

                        processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                        {
                            ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret);
                        });
                    }
                }

                // Process forced hopo or forced strum
                {
                    int flagKey = difficultyStartRange + 7;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, MoonNote.MoonNoteType.Hopo);
                    });
                }
                {
                    int flagKey = difficultyStartRange + 8;
                    processFnDict.Add(flagKey, (in EventProcessParams eventProcessParams) =>
                    {
                        ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, MoonNote.MoonNoteType.Strum);
                    });
                }
            };

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildProGuitarMidiNoteNumberToProcessFnDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, ProcessNoteOnEventAsStarpower },
                { MidIOHelper.SOLO_NOTE_PRO_GUITAR, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsEvent(eventProcessParams, MidIOHelper.SOLO_EVENT_TEXT, MidIOHelper.SOLO_END_EVENT_TEXT, tickEndOffset: SOLO_END_CORRECTION_OFFSET);
                }},
            };

            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.PRO_GUITAR_DIFF_START_LOOKUP[difficulty];
                foreach (var proString in EnumX<MoonNote.ProGuitarString>.Values)
                {
                    int key = (int)proString + difficultyStartRange;
                    processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                    {
                        var noteEvent = eventProcessParams.timedEvent.midiEvent as NoteEvent;
                        Debug.Assert(noteEvent != null, $"Wrong note event type passed to Pro Guitar note process. Expected: {typeof(NoteEvent)}, Actual: {eventProcessParams.timedEvent.midiEvent.GetType()}");

                        if (noteEvent.Velocity < 100)
                        {
                            Debug.WriteLine($"Encountered Pro Guitar note with invalid fret velocity {noteEvent.Velocity}! Must be at least 100");
                            return;
                        }

                        int fret = noteEvent.Velocity - 100;
                        int rawNote = MoonNote.MakeProGuitarRawNote(proString, fret);
                        if (!MidIOHelper.PRO_GUITAR_CHANNEL_FLAG_LOOKUP.TryGetValue(noteEvent.Channel, out var flags))
                            flags = MoonNote.Flags.None;

                        ProcessNoteOnEventAsNote(eventProcessParams, difficulty, rawNote, flags);
                    });
                }

                // Process forced hopo
                processFnDict.Add(difficultyStartRange + 6, (in EventProcessParams eventProcessParams) =>
                {
                    ProcessNoteOnEventAsForcedType(eventProcessParams, difficulty, MoonNote.MoonNoteType.Hopo);
                });
            };

            return processFnDict;
        }

        private static Dictionary<int, EventProcessFn> BuildDrumsMidiNoteNumberToProcessFnDict(bool enableVelocity = false)
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, ProcessNoteOnEventAsStarpower },
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsEvent(eventProcessParams, MidIOHelper.SOLO_EVENT_TEXT, MidIOHelper.SOLO_END_EVENT_TEXT, tickEndOffset: SOLO_END_CORRECTION_OFFSET);
                }},

                { MidIOHelper.DRUM_FILL_NOTE_0, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.DRUM_FILL_NOTE_1, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.DRUM_FILL_NOTE_2, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.DRUM_FILL_NOTE_3, ProcessNoteOnEventAsDrumFill },
                { MidIOHelper.DRUM_FILL_NOTE_4, ProcessNoteOnEventAsDrumFill },

                { MidIOHelper.VERSUS_PHRASE_PLAYER_1, ProcessNoteOnEventAsVersusPlayerOne },
                { MidIOHelper.VERSUS_PHRASE_PLAYER_2, ProcessNoteOnEventAsVersusPlayerTwo },
                { MidIOHelper.TREMOLO_LANE_NOTE, ProcessNoteOnEventAsTremoloLane },
                { MidIOHelper.TRILL_LANE_NOTE, ProcessNoteOnEventAsTrillLane },
            };

            var DrumPadToMidiKey = new Dictionary<MoonNote.DrumPad, int>()
            {
                { MoonNote.DrumPad.Kick, 0 },
                { MoonNote.DrumPad.Red, 1 },
                { MoonNote.DrumPad.Yellow, 2 },
                { MoonNote.DrumPad.Blue, 3 },
                { MoonNote.DrumPad.Orange, 4 },
                { MoonNote.DrumPad.Green, 5 },
            };

            var DrumPadDefaultFlags = new Dictionary<MoonNote.DrumPad, MoonNote.Flags>()
            {
                { MoonNote.DrumPad.Yellow, MoonNote.Flags.ProDrums_Cymbal },
                { MoonNote.DrumPad.Blue, MoonNote.Flags.ProDrums_Cymbal },
                { MoonNote.DrumPad.Orange, MoonNote.Flags.ProDrums_Cymbal },
            };

            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
            {
                int difficultyStartRange = MidIOHelper.DRUMS_DIFF_START_LOOKUP[difficulty];
                foreach (var pad in EnumX<MoonNote.DrumPad>.Values)
                {
                    if (DrumPadToMidiKey.TryGetValue(pad, out int padOffset))
                    {
                        int key = padOffset + difficultyStartRange;
                        int fret = (int)pad;
                        var defaultFlags = MoonNote.Flags.None;
                        DrumPadDefaultFlags.TryGetValue(pad, out defaultFlags);

                        if (enableVelocity && pad != MoonNote.DrumPad.Kick)
                        {
                            processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                            {
                                var noteEvent = eventProcessParams.timedEvent.midiEvent as NoteEvent;
                                Debug.Assert(noteEvent != null, $"Wrong note event type passed to drums note process. Expected: {typeof(NoteEvent)}, Actual: {eventProcessParams.timedEvent.midiEvent.GetType()}");

                                var flags = defaultFlags;
                                switch (noteEvent.Velocity)
                                {
                                    case MidIOHelper.VELOCITY_ACCENT:
                                        flags |= MoonNote.Flags.ProDrums_Accent;
                                        break;
                                    case MidIOHelper.VELOCITY_GHOST:
                                        flags |= MoonNote.Flags.ProDrums_Ghost;
                                        break;
                                    default:
                                        break;
                                }

                                ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret, flags);
                            });
                        }
                        else
                        {
                            processFnDict.Add(key, (in EventProcessParams eventProcessParams) =>
                            {
                                ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret, defaultFlags);
                            });
                        }

                        // Double-kick
                        if (pad == MoonNote.DrumPad.Kick)
                        {
                            processFnDict.Add(key - 1, (in EventProcessParams eventProcessParams) => {
                                ProcessNoteOnEventAsNote(eventProcessParams, difficulty, fret, MoonNote.Flags.InstrumentPlus);
                            });
                        }
                    }
                }
            };

            foreach (var keyVal in MidIOHelper.PAD_TO_CYMBAL_LOOKUP)
            {
                int pad = (int)keyVal.Key;
                int midiKey = keyVal.Value;

                processFnDict.Add(midiKey, (in EventProcessParams eventProcessParams) =>
                {
                    ProcessNoteOnEventAsFlagToggle(eventProcessParams, MoonNote.Flags.ProDrums_Cymbal, pad);
                });
            }

            return processFnDict;
        }

        private static void ProcessNoteOnEventAsNote(in EventProcessParams eventProcessParams, MoonSong.Difficulty diff, int ingameFret, MoonNote.Flags defaultFlags = MoonNote.Flags.None)
        {
            MoonChart chart;
            if (eventProcessParams.instrument == MoonSong.MoonInstrument.Unrecognised)
            {
                chart = eventProcessParams.currentUnrecognisedChart;
            }
            else
            {
                chart = eventProcessParams.song.GetChart(eventProcessParams.instrument, diff);
            }

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;
            uint sus = ApplySustainCutoff(eventProcessParams.song, (uint)timedEvent.length);

            var newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            chart.Add(newMoonNote, false);
        }

        private static void ProcessNoteOnEventAsSpecialPhrase(in EventProcessParams eventProcessParams, SpecialPhrase.Type type)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;
            uint sus = ApplySustainCutoff(eventProcessParams.song, (uint)timedEvent.length);

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                song.GetChart(instrument, diff).Add(new SpecialPhrase(tick, sus, type), false);
            }
        }

        private static void ProcessNoteOnEventAsStarpower(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Starpower);

        private static void ProcessNoteOnEventAsVersusPlayerOne(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player1);

        private static void ProcessNoteOnEventAsVersusPlayerTwo(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player2);

        private static void ProcessNoteOnEventAsDrumFill(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.ProDrums_Activation);

        private static void ProcessNoteOnEventAsTremoloLane(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TremoloLane);

        private static void ProcessNoteOnEventAsTrillLane(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TrillLane);

        private static void ProcessNoteOnEventAsForcedType(in EventProcessParams eventProcessParams, MoonNote.MoonNoteType noteType)
        {
            var timedEvent = eventProcessParams.timedEvent;
            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                // Delay the actual processing once all the notes are actually in
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, noteType);
                });
            }
        }

        private static void ProcessNoteOnEventAsForcedType(in EventProcessParams eventProcessParams, MoonSong.Difficulty difficulty, MoonNote.MoonNoteType noteType)
        {
            var timedEvent = eventProcessParams.timedEvent;
            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;

            // Delay the actual processing once all the notes are actually in
            eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
            {
                ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, difficulty, noteType);
            });
        }

        private static void ProcessEventAsForcedTypePostDelay(in EventProcessParams eventProcessParams, uint startTick, uint endTick, MoonSong.Difficulty difficulty, MoonNote.MoonNoteType noteType)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            MoonChart chart;
            if (instrument != MoonSong.MoonInstrument.Unrecognised)
                chart = song.GetChart(instrument, difficulty);
            else
                chart = eventProcessParams.currentUnrecognisedChart;

            SongObjectHelper.GetRange(chart.notes, startTick, endTick, out int index, out int length);

            for (int i = index; i < index + length; ++i)
            {
                var note = chart.notes[i];
                var newType = noteType; // The requested type might not be able to be marked for this note

                // Tap marking overrides all other forcing
                if ((note.flags & MoonNote.Flags.Tap) != 0)
                    continue;

                switch (newType)
                {
                    case MoonNote.MoonNoteType.Strum:
                        if (!note.isChord && note.isNaturalHopo)
                        {
                            note.flags |= MoonNote.Flags.Forced;
                        }
                        break;

                    case MoonNote.MoonNoteType.Hopo:
                        if (note.isChord || !note.isNaturalHopo)
                        {
                            note.flags |= MoonNote.Flags.Forced;
                        }
                        break;

                    case MoonNote.MoonNoteType.Tap:
                        if (!note.IsOpenNote())
                        {
                            note.flags |= MoonNote.Flags.Tap;
                            note.flags &= ~MoonNote.Flags.Forced;
                        }
                        else
                        {
                            // Open notes cannot become taps, mark them as HOPOs instead
                            newType = MoonNote.MoonNoteType.Hopo;
                            goto case MoonNote.MoonNoteType.Hopo;
                        }
                        break;

                    default:
                        Debug.Fail($"Unhandled note type {newType} in .mid forced type processing ({nameof(ProcessEventAsForcedTypePostDelay)})");
                        continue;
                }

                Debug.Assert(note.type == newType, $"Failed to set forced type! Expected: {newType}  Actual: {note.type}\non {difficulty} {instrument} at tick {note.tick} ({TimeSpan.FromSeconds(note.time):mm':'ss'.'ff})");
            }
        }

        private static uint ApplySustainCutoff(MoonSong song, uint length)
        {
            int susCutoff = (int)(SongConfig.MIDI_SUSTAIN_CUTOFF_THRESHOLD * song.resolution / SongConfig.STANDARD_BEAT_RESOLUTION); // 1/12th note
            if (length <= susCutoff)
                length = 0;

            return length;
        }

        private static void ProcessNoteOnEventAsEvent(EventProcessParams eventProcessParams, string eventStartText, string eventEndText, int tickStartOffset = 0, int tickEndOffset = 0)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;
            uint sus = ApplySustainCutoff(eventProcessParams.song, (uint)timedEvent.length);

            if (tick >= tickStartOffset)
                tick += (uint)tickStartOffset;

            if (sus >= tickEndOffset)
                sus += (uint)tickEndOffset;

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                var chart = song.GetChart(instrument, diff);
                chart.Add(new ChartEvent(tick, eventStartText));
                chart.Add(new ChartEvent(tick + sus, eventEndText));
            }
        }

        private static void ProcessNoteOnEventAsFlagToggle(in EventProcessParams eventProcessParams, MoonNote.Flags flags, int individualNoteSpecifier)
        {
            var timedEvent = eventProcessParams.timedEvent;
            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;

            // Delay the actual processing once all the notes are actually in
            eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
            {
                ProcessNoteOnEventAsFlagTogglePostDelay(processParams, startTick, --endTick, flags, individualNoteSpecifier);
            });
        }

        private static void ProcessNoteOnEventAsFlagTogglePostDelay(in EventProcessParams eventProcessParams, uint startTick, uint endTick, MoonNote.Flags flags, int individualNoteSpecifier)   // individualNoteSpecifier as -1 to apply to the whole chord
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
            {
                var chart = song.GetChart(instrument, difficulty);

                SongObjectHelper.GetRange(chart.notes, startTick, endTick, out int index, out int length);

                for (int i = index; i < index + length; ++i)
                {
                    var note = chart.notes[i];

                    if (individualNoteSpecifier < 0 || note.rawNote == individualNoteSpecifier)
                    {
                        // Toggle flag
                        note.flags ^= flags;
                    }
                }
            }
        }

        private static void ProcessTextEventPairAsSpecialPhrase(in EventProcessParams eventProcessParams, string startText,
            string endText, SpecialPhrase.Type type)
        {
            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
            {
                var song = eventProcessParams.song;
                var instrument = eventProcessParams.instrument;
                var chart = song.GetChart(instrument, difficulty);

                // Convert start and end events into phrases
                uint? currentStartTick = null;
                for (int i = 0; i < chart.events.Count; ++i)
                {
                    var textEvent = chart.events[i];
                    if (textEvent.eventName == startText)
                    {
                        // Remove text event
                        chart.Remove(textEvent, false);

                        uint startTick = textEvent.tick;
                        // Only one start event can be active at a time
                        if (currentStartTick != null)
                        {
                            Debug.WriteLine($"A previous start event at tick {currentStartTick.Value} is interrupted by another start event at tick {startTick}!");
                            continue;
                        }

                        currentStartTick = startTick;
                    }
                    else if (textEvent.eventName == endText)
                    {
                        // Remove text event
                        chart.Remove(textEvent, false);

                        uint endTick = textEvent.tick;
                        // Events must pair up
                        if (currentStartTick == null)
                        {
                            Debug.WriteLine($"End event at tick {endTick} does not have a corresponding start event!");
                            continue;
                        }

                        uint startTick = currentStartTick.GetValueOrDefault();
                        // Current start must occur before the current end
                        if (currentStartTick > textEvent.tick)
                        {
                            Debug.WriteLine($"Start event at tick {endTick} occurs before end event at {endTick}!");
                            continue;
                        }

                        chart.Add(new SpecialPhrase(startTick, endTick - startTick, type), false);
                        currentStartTick = null;
                    }
                }
            }
        }

        private static void ProcessSysExEventPairAsForcedType(in EventProcessParams eventProcessParams, MoonNote.MoonNoteType noteType)
        {
            var timedEvent = eventProcessParams.timedEvent;
            var startEvent = eventProcessParams.timedEvent.midiEvent as PhaseShiftSysEx;
            Debug.Assert(startEvent != null, $"Wrong note event type passed to {nameof(ProcessSysExEventPairAsForcedType)}. Expected: {typeof(PhaseShiftSysEx)}, Actual: {eventProcessParams.timedEvent.midiEvent.GetType()}");

            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;

            if (startEvent.difficulty == PhaseShiftSysEx.Difficulty.All)
            {
                foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
                {
                    eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                    {
                        ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, noteType);
                    });
                }
            }
            else
            {
                var diff = PhaseShiftSysEx.SysExDiffToMsDiff[startEvent.difficulty];
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, noteType);
                });
            }
        }

        private static void ProcessSysExEventPairAsOpenNoteModifier(in EventProcessParams eventProcessParams)
        {
            var timedEvent = eventProcessParams.timedEvent;
            var startEvent = timedEvent.midiEvent as PhaseShiftSysEx;
            Debug.Assert(startEvent != null, $"Wrong note event type passed to {nameof(ProcessSysExEventPairAsOpenNoteModifier)}. Expected: {typeof(PhaseShiftSysEx)}, Actual: {eventProcessParams.timedEvent.midiEvent.GetType()}");

            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;
            // Exclude the last tick of the phrase
            if (endTick > 0)
                --endTick;

            if (startEvent.difficulty == PhaseShiftSysEx.Difficulty.All)
            {
                foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
                {
                    eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                    {
                        ProcessEventAsOpenNoteModifierPostDelay(processParams, startTick, endTick, diff);
                    });
                }
            }
            else
            {
                var diff = PhaseShiftSysEx.SysExDiffToMsDiff[startEvent.difficulty];
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsOpenNoteModifierPostDelay(processParams, startTick, endTick, diff);
                });
            }
        }

        private static void ProcessEventAsOpenNoteModifierPostDelay(in EventProcessParams processParams, uint startTick, uint endTick, MoonSong.Difficulty difficulty)
        {
            var instrument = processParams.instrument;
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            var song = processParams.song;

            MoonChart chart;
            if (instrument == MoonSong.MoonInstrument.Unrecognised)
                chart = processParams.currentUnrecognisedChart;
            else
                chart = song.GetChart(instrument, difficulty);

            SongObjectHelper.GetRange(chart.notes, startTick, endTick, out int index, out int length);
            for (int i = index; i < index + length; ++i)
            {
                switch (gameMode)
                {
                    case MoonChart.GameMode.Guitar:
                        chart.notes[i].guitarFret = MoonNote.GuitarFret.Open;
                        break;

                    // Usually not used, but in the case that it is it should work properly
                    case MoonChart.GameMode.GHLGuitar:
                        chart.notes[i].ghliveGuitarFret = MoonNote.GHLiveGuitarFret.Open;
                        break;

                    default:
                        Debug.Assert(false, $"Unhandled game mode for open note modifier: {gameMode} (instrument: {instrument})");
                        break;
                }
            }

            chart.UpdateCache();
        }
    }
}

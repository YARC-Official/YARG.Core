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

            var moonSong = new MoonSong()
            {
                resolution = ticks.TicksPerQuarterNote
            };

            // Read all bpm data in first. This will also allow song.TimeToTick to function properly.
            ReadSync(midi.GetTempoMap(), moonSong);

            foreach (var track in midi.GetTrackChunks())
            {
                if (track == null || track.Events.Count < 1)
                {
                    Console.WriteLine("Encountered an empty track!");
                    continue;
                }

                if (track.Events[0] is not SequenceTrackNameEvent trackName)
                {
                    Console.WriteLine($"Could not determine track name! (Likely the tempo track)");
                    continue;
                }
                Console.WriteLine("Found midi track " + trackName.Text);

                string trackNameKey = trackName.Text.ToUpper();
                if (ExcludedTracks.ContainsKey(trackNameKey))
                    continue;

                switch (trackNameKey)
                {
                    case MidIOHelper.EVENTS_TRACK:
                        ReadSongGlobalEvents(track, moonSong);
                        break;

                    case MidIOHelper.VOCALS_TRACK:
                        ReadTextEventsIntoGlobalEventsAsLyrics(track, moonSong);
                        break;

                    default:
                        MoonSong.MoonInstrument moonInstrument;
                        if (!MidIOHelper.TrackNameToInstrumentMap.TryGetValue(trackNameKey, out moonInstrument))
                        {
                            moonInstrument = MoonSong.MoonInstrument.Unrecognised;
                        }
                        else if (moonSong.ChartExistsForInstrument(moonInstrument))
                        {
                            if (!TrackOverrides.TryGetValue(trackNameKey, out bool overwrite) || !overwrite)
                                continue;

                            // Overwrite existing track
                            foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
                            {
                                var chart = moonSong.GetChart(moonInstrument, difficulty);
                                chart.Clear();
                                chart.UpdateCache();
                            }
                        }

                        Console.WriteLine("Loading midi track {0}", moonInstrument);
                        ReadNotes(track, moonSong, moonInstrument);
                        break;
                }
            }

            return moonSong;
        }

        private static void ReadSync(TempoMap tempoMap, MoonSong moonSong)
        {
            foreach (var tempo in tempoMap.GetTempoChanges())
            {
                moonSong.Add(new BPM((uint)tempo.Time, (uint)(tempo.Value.BeatsPerMinute * 1000)), false);
            }
            foreach (var timesig in tempoMap.GetTimeSignatureChanges())
            {
                moonSong.Add(new TimeSignature((uint)timesig.Time, (uint)timesig.Value.Numerator, (uint)Math.Pow(2, timesig.Value.Denominator)), false);
            }

            moonSong.UpdateCache();
        }

        private static void ReadSongGlobalEvents(TrackChunk track, MoonSong moonSong)
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
                        moonSong.Add(new Section(text.Text[9..^10], (uint)absoluteTime), false);
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
                            Console.WriteLine("Found section name in an unknown format: " + text.Text);
                        }

                        moonSong.Add(new Section(sectionText, (uint)absoluteTime), false);
                    }
                    else
                    {
                        moonSong.Add(new Event(text.Text.Trim(new char[] { '[', ']' }), (uint)absoluteTime), false);
                    }
                }
            }

            moonSong.UpdateCache();
        }

        private static void ReadTextEventsIntoGlobalEventsAsLyrics(TrackChunk track, MoonSong moonSong)
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
                    moonSong.Add(new Event(lyricEvent, (uint)absoluteTime), false);
                }

                if (trackEvent is NoteEvent note && (byte)note.NoteNumber is MidIOHelper.LYRICS_PHRASE_1 or MidIOHelper.LYRICS_PHRASE_2)
                {
                    if (note.EventType == MidiEventType.NoteOn)
                        moonSong.Add(new Event(MidIOHelper.LYRICS_PHRASE_START_TEXT, (uint)absoluteTime), false);
                    else if (note.EventType == MidiEventType.NoteOff)
                        moonSong.Add(new Event(MidIOHelper.LYRICS_PHRASE_END_TEXT, (uint)absoluteTime), false);
                }
            }

            moonSong.UpdateCache();
        }

        private static void ReadNotes(TrackChunk track, MoonSong moonSong, MoonSong.MoonInstrument moonInstrument)
        {
            if (track == null || track.Events.Count < 1)
            {
                Console.WriteLine($"Attempted to load null or empty track.");
                return;
            }

            var noteQueue = new List<(NoteOnEvent note, long tick)>();
            var sysexEventQueue = new List<(PhaseShiftSysEx sysex, long tick)>();

            var unrecognised = new MoonChart(moonSong, MoonSong.MoonInstrument.Unrecognised);
            var gameMode = MoonSong.InstumentToChartGameMode(moonInstrument);

            var processParams = new EventProcessParams()
            {
                song = moonSong,
                currentUnrecognisedChart = unrecognised,
                instrument = moonInstrument,
                noteProcessMap = GetNoteProcessDict(gameMode),
                textProcessMap = GetTextEventProcessDict(gameMode),
                sysexProcessMap = GetSysExEventProcessDict(gameMode),
                delayedProcessesList = new List<EventProcessFn>(),
            };

            if (moonInstrument == MoonSong.MoonInstrument.Unrecognised)
            {
                moonSong.unrecognisedCharts.Add(unrecognised);
            }

            // Load all the notes
            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                processParams.timedEvent = new()
                {
                    midiEvent = trackEvent,
                    startTick = absoluteTime
                };

                if (trackEvent is TextEvent text)
                {
                    uint tick = (uint)absoluteTime;
                    string eventName = text.Text;

                    var chartEvent = new ChartEvent(tick, eventName);

                    if (moonInstrument == MoonSong.MoonInstrument.Unrecognised)
                    {
                        unrecognised.Add(chartEvent);
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
                                moonSong.GetChart(moonInstrument, difficulty).Add(chartEvent);
                            }
                        }
                    }
                }

                if (trackEvent is NoteOnEvent noteOn)
                {
                    if (noteQueue.Any((queued) =>
                        queued.note.NoteNumber == noteOn.NoteNumber && queued.note.Channel == noteOn.Channel))
                    {
                        Console.WriteLine($"Found duplicate note on event at tick {absoluteTime}!");
                        continue;
                    }
                    noteQueue.Add((noteOn, absoluteTime));
                }

                if (trackEvent is NoteOffEvent noteOff)
                {
                    // Get note on event
                    long noteOnTime = 0;
                    var queued = noteQueue.FirstOrDefault((queued) =>
                        queued.note.NoteNumber == noteOff.NoteNumber && queued.note.Channel == noteOff.Channel);
                    (noteOn, noteOnTime) = queued;
                    if (noteOn == null)
                    {
                        Console.WriteLine($"Found note off with no corresponding note on at tick {absoluteTime}!");
                        continue;
                    }
                    noteQueue.Remove(queued);

                    if (moonInstrument == MoonSong.MoonInstrument.Unrecognised)
                    {
                        uint tick = (uint)absoluteTime;
                        uint sus = CutoffSustainIfNeeded(moonSong, (uint)(absoluteTime - noteOnTime));

                        int rawNote = noteOff.NoteNumber;
                        var newMoonNote = new MoonNote(tick, rawNote, sus);
                        unrecognised.Add(newMoonNote);
                        continue;
                    }

                    processParams.timedEvent.midiEvent = noteOn;
                    processParams.timedEvent.startTick = noteOnTime;
                    processParams.timedEvent.endTick = absoluteTime;

                    if (processParams.noteProcessMap.TryGetValue(noteOn.NoteNumber, out var processFn))
                    {
                        processFn(processParams);
                    }
                }

                if (trackEvent is SysExEvent sysex)
                {
                    if (!PhaseShiftSysEx.TryParse(sysex, out var psEvent))
                    {
                        // SysEx event is not a Phase Shift SysEx event
                        Console.WriteLine($"Encountered unknown SysEx event: {BitConverter.ToString(sysex.Data)}");
                        continue;
                    }

                    if (psEvent.type != PhaseShiftSysEx.Type.Phrase)
                    {
                        Console.WriteLine($"Encountered unknown Phase Shift SysEx event type {psEvent.type}");
                        continue;
                    }

                    if (psEvent.phraseValue == PhaseShiftSysEx.PhraseValue.Start)
                    {
                        if (sysexEventQueue.Any((queued) => queued.sysex.MatchesWith(psEvent)))
                        {
                            Console.WriteLine($"Found duplicate PS SysEx start event at tick {absoluteTime}!");
                            continue;
                        }
                        sysexEventQueue.Add((psEvent, absoluteTime));
                    }
                    else if (psEvent.phraseValue == PhaseShiftSysEx.PhraseValue.End)
                    {
                        var queued = sysexEventQueue.FirstOrDefault((queued) => queued.sysex.MatchesWith(psEvent));
                        var (startEvent, startTick) = queued;
                        if (startEvent == null)
                        {
                            Console.WriteLine($"Found PS SysEx end with no corresponding start at tick {absoluteTime}!");
                            continue;
                        }
                        sysexEventQueue.Remove(queued);

                        processParams.timedEvent.midiEvent = psEvent;
                        processParams.timedEvent.startTick = startTick;
                        processParams.timedEvent.endTick = absoluteTime;

                        if (processParams.sysexProcessMap.TryGetValue(psEvent.phraseCode, out var processFn))
                        {
                            processFn(processParams);
                        }
                    }
                }
            }

            Debug.Assert(noteQueue.Count == 0, $"Note queue was not fully processed! Remaining event count: {noteQueue.Count}");
            Debug.Assert(sysexEventQueue.Count == 0, $"SysEx event queue was not fully processed! Remaining event count: {sysexEventQueue.Count}");

            // Update all chart arrays
            if (moonInstrument != MoonSong.MoonInstrument.Unrecognised)
            {
                foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
                {
                    moonSong.GetChart(moonInstrument, diff).UpdateCache();
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
            if (LegacyStarPowerFixupWhitelist.Contains(moonInstrument))
            {
                // Only need to check one difficulty since Star Power gets copied to all difficulties
                var chart = moonSong.GetChart(moonInstrument, MoonSong.Difficulty.Expert);
                if (!chart.specialPhrases.Any((sp) => sp.type == SpecialPhrase.Type.Starpower)
                    && chart.events.Any((text) => text.eventName is MidIOHelper.SOLO_EVENT_TEXT or MidIOHelper.SOLO_END_EVENT_TEXT))
                {
                    ProcessTextEventPairAsSpecialPhrase(processParams, MidIOHelper.SOLO_EVENT_TEXT, MidIOHelper.SOLO_END_EVENT_TEXT,
                        SpecialPhrase.Type.Starpower);
                    foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
                    {
                        moonSong.GetChart(moonInstrument, diff).UpdateCache();
                    }
                }
            }
        }

        private static Dictionary<int, EventProcessFn> GetNoteProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.GHLGuitar => GhlGuitarMidiNoteNumberToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsMidiNoteNumberToProcessFnMap,
                _ => GuitarMidiNoteNumberToProcessFnMap
            };
        }

        private static Dictionary<string, ProcessModificationProcessFn> GetTextEventProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.GHLGuitar => GhlGuitarTextEventToProcessFnMap,
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
                Console.WriteLine($"Attempted to apply guitar enhanced opens process map to non-guitar instrument: {processParams.instrument}");
                return;
            }

            // Switch process map to guitar enhanced opens process map
            processParams.noteProcessMap = GuitarMidiNoteNumberToProcessFnMap_EnhancedOpens;
        }

        private static void SwitchToDrumsVelocityProcessMap(ref EventProcessParams processParams)
        {
            if (processParams.instrument != MoonSong.MoonInstrument.Drums)
            {
                Console.WriteLine($"Attempted to apply drums velocity process map to non-drums instrument: {processParams.instrument}");
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
            MoonChart moonChart;
            if (eventProcessParams.instrument == MoonSong.MoonInstrument.Unrecognised)
            {
                moonChart = eventProcessParams.currentUnrecognisedChart;
            }
            else
            {
                moonChart = eventProcessParams.song.GetChart(eventProcessParams.instrument, diff);
            }

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;
            uint sus = CutoffSustainIfNeeded(eventProcessParams.song, (uint)timedEvent.length);

            var newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            moonChart.Add(newMoonNote, false);
        }

        private static void ProcessNoteOnEventAsSpecialPhrase(in EventProcessParams eventProcessParams, SpecialPhrase.Type type)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;
            uint sus = CutoffSustainIfNeeded(eventProcessParams.song, (uint)timedEvent.length);

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                song.GetChart(instrument, diff).Add(new SpecialPhrase(tick, sus, type), false);
            }
        }

        private static void ProcessNoteOnEventAsStarpower(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Starpower);

        private static void ProcessNoteOnEventAsDrumFill(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.ProDrums_Activation);

        private static void ProcessNoteOnEventAsTremoloLane(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TremoloLane);

        private static void ProcessNoteOnEventAsTrillLane(in EventProcessParams eventProcessParams)
            => ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TrillLane);

        private static void ProcessNoteOnEventAsForcedType(in EventProcessParams eventProcessParams, MoonNote.MoonNoteType moonNoteType)
        {
            var timedEvent = eventProcessParams.timedEvent;
            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                // Delay the actual processing once all the notes are actually in
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, moonNoteType);
                });
            }
        }

        private static void ProcessNoteOnEventAsForcedType(in EventProcessParams eventProcessParams, MoonSong.Difficulty difficulty, MoonNote.MoonNoteType moonNoteType)
        {
            var timedEvent = eventProcessParams.timedEvent;
            uint startTick = (uint)timedEvent.startTick;
            uint endTick = (uint)timedEvent.endTick;

            // Delay the actual processing once all the notes are actually in
            eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
            {
                ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, difficulty, moonNoteType);
            });
        }

        private static void ProcessEventAsForcedTypePostDelay(in EventProcessParams eventProcessParams, uint startTick, uint endTick, MoonSong.Difficulty difficulty, MoonNote.MoonNoteType moonNoteType)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            MoonChart moonChart;
            if (instrument != MoonSong.MoonInstrument.Unrecognised)
                moonChart = song.GetChart(instrument, difficulty);
            else
                moonChart = eventProcessParams.currentUnrecognisedChart;

            SongObjectHelper.GetRange(moonChart.notes, startTick, endTick, out int index, out int length);

            uint lastChordTick = uint.MaxValue;
            bool expectedForceFailure = true; // Whether or not it is expected that the actual type will not match the expected type
            bool shouldBeForced;

            for (int i = index; i < index + length; ++i)
            {
                // Tap marking overrides all other forcing
                if ((moonChart.notes[i].flags & MoonNote.Flags.Tap) != 0)
                    continue;

                var moonNote = moonChart.notes[i];

                // Check if the chord has changed
                if (lastChordTick != moonNote.tick)
                {
                    expectedForceFailure = false;
                    shouldBeForced = false;

                    switch (moonNoteType)
                    {
                        case MoonNote.MoonNoteType.Strum:
                            if (!moonNote.isChord && moonNote.isNaturalHopo)
                            {
                                shouldBeForced = true;
                            }
                            break;

                        case MoonNote.MoonNoteType.Hopo:
                            // Forcing consecutive same-fret HOPOs is possible in charts, but we do not allow it
                            // (see RB2's chart of Steely Dan - Bodhisattva)
                            if (!moonNote.isNaturalHopo && moonNote.cannotBeForced)
                            {
                                expectedForceFailure = true;
                            }

                            if (!moonNote.cannotBeForced && (moonNote.isChord || !moonNote.isNaturalHopo))
                            {
                                shouldBeForced = true;
                            }
                            break;

                        case MoonNote.MoonNoteType.Tap:
                            if (!moonNote.IsOpenNote())
                            {
                                moonNote.flags |= MoonNote.Flags.Tap;
                                // Forced flag will be removed shortly after here
                            }
                            else
                            {
                                // Open notes cannot become taps
                                // CH handles this by turning them into open HOPOs, we'll do the same here for consistency with them
                                expectedForceFailure = true;
                                // In the case that consecutive open notes are marked as taps, only the first will become a HOPO
                                if (!moonNote.cannotBeForced && !moonNote.isNaturalHopo)
                                {
                                    shouldBeForced = true;
                                }
                            }
                            break;

                        default:
                            Debug.Assert(false, $"Unhandled note type {moonNoteType} in .mid forced type processing");
                            continue; // Unhandled
                    }

                    if (shouldBeForced)
                    {
                        moonNote.flags |= MoonNote.Flags.Forced;
                    }
                    else
                    {
                        moonNote.flags &= ~MoonNote.Flags.Forced;
                    }

                    moonNote.ApplyFlagsToChord();
                }

                lastChordTick = moonNote.tick;

                Debug.Assert(moonNote.type == moonNoteType || expectedForceFailure, $"Failed to set forced type! Expected: {moonNoteType}  Actual: {moonNote.type}  Natural HOPO: {moonNote.isNaturalHopo}  Chord: {moonNote.isChord}  Forceable: {!moonNote.cannotBeForced}\non {difficulty} {instrument} at tick {moonNote.tick} ({TimeSpan.FromSeconds(moonNote.time):mm':'ss'.'ff})");
            }
        }

        private static uint CutoffSustainIfNeeded(MoonSong moonSong, uint length)
        {
            int susCutoff = (int)(SongConfig.MIDI_SUSTAIN_CUTOFF_THRESHOLD * moonSong.resolution / SongConfig.STANDARD_BEAT_RESOLUTION); // 1/12th note
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
            uint sus = CutoffSustainIfNeeded(eventProcessParams.song, (uint)timedEvent.length);

            if (tick >= tickStartOffset)
                tick += (uint)tickStartOffset;

            if (sus >= tickEndOffset)
                sus += (uint)tickEndOffset;

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                var moonChart = song.GetChart(instrument, diff);
                moonChart.Add(new ChartEvent(tick, eventStartText));
                moonChart.Add(new ChartEvent(tick + sus, eventEndText));
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
                var moonChart = song.GetChart(instrument, difficulty);

                SongObjectHelper.GetRange(moonChart.notes, startTick, endTick, out int index, out int length);

                for (int i = index; i < index + length; ++i)
                {
                    var moonNote = moonChart.notes[i];

                    if (individualNoteSpecifier < 0 || moonNote.rawNote == individualNoteSpecifier)
                    {
                        // Toggle flag
                        moonNote.flags ^= flags;
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
                            Console.WriteLine($"A previous start event at tick {currentStartTick.Value} is interrupted by another start event at tick {startTick}!");
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
                            Console.WriteLine($"End event at tick {endTick} does not have a corresponding start event!");
                            continue;
                        }

                        uint startTick = currentStartTick.GetValueOrDefault();
                        // Current start must occur before the current end
                        if (currentStartTick > textEvent.tick)
                        {
                            Console.WriteLine($"Start event at tick {endTick} occurs before end event at {endTick}!");
                            continue;
                        }

                        chart.Add(new SpecialPhrase(startTick, endTick - startTick, type), false);
                        currentStartTick = null;
                    }
                }
            }
        }

        private static void ProcessSysExEventPairAsForcedType(in EventProcessParams eventProcessParams, MoonNote.MoonNoteType moonNoteType)
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
                        ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, moonNoteType);
                    });
                }
            }
            else
            {
                var diff = PhaseShiftSysEx.SysExDiffToMsDiff[startEvent.difficulty];
                eventProcessParams.delayedProcessesList.Add((in EventProcessParams processParams) =>
                {
                    ProcessEventAsForcedTypePostDelay(processParams, startTick, endTick, diff, moonNoteType);
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

            MoonChart moonChart;
            if (instrument == MoonSong.MoonInstrument.Unrecognised)
                moonChart = processParams.currentUnrecognisedChart;
            else
                moonChart = song.GetChart(instrument, difficulty);

            SongObjectHelper.GetRange(moonChart.notes, startTick, endTick, out int index, out int length);
            for (int i = index; i < index + length; ++i)
            {
                switch (gameMode)
                {
                    case MoonChart.GameMode.Guitar:
                        moonChart.notes[i].guitarFret = MoonNote.GuitarFret.Open;
                        break;

                    // Usually not used, but in the case that it is it should work properly
                    case MoonChart.GameMode.GHLGuitar:
                        moonChart.notes[i].ghliveGuitarFret = MoonNote.GHLiveGuitarFret.Open;
                        break;

                    default:
                        Debug.Assert(false, $"Unhandled game mode for open note modifier: {gameMode} (instrument: {instrument})");
                        break;
                }
            }

            moonChart.UpdateCache();
        }
    }
}

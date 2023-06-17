// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using MoonscraperEngine;

namespace MoonscraperChartEditor.Song.IO
{
    public static partial class MidReader
    {
        private static readonly List<MoonSong.MoonInstrument> LegacyStarPowerFixupWhitelist = new()
        {
            MoonSong.MoonInstrument.Guitar,
            MoonSong.MoonInstrument.GuitarCoop,
            MoonSong.MoonInstrument.Bass,
            MoonSong.MoonInstrument.Rhythm,
        };

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
        private static readonly Dictionary<int, EventProcessFn> VocalsMidiNoteNumberToProcessFnMap = BuildVocalsMidiNoteNumberToProcessFnDict();

        // These dictionaries map the text of a MIDI text event to a specific function that processes them
        private static readonly Dictionary<string, ProcessModificationProcessFn> GuitarTextEventToProcessFnMap = new()
        {
            { MidIOHelper.ENHANCED_OPENS_TEXT, SwitchToGuitarEnhancedOpensProcessMap },
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
        };

        private static readonly Dictionary<string, ProcessModificationProcessFn> VocalsTextEventToProcessFnMap = new()
        {
        };

        // These dictionaries map the phrase code of a SysEx event to a specific function that processes them
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

        private static readonly Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> VocalsSysExEventToProcessFnMap = new()
        {
        };

        // Some post-processing events should always be carried out on certain tracks
        private static readonly List<EventProcessFn> GuitarInitialPostProcessList = new()
        {
            FixupStarPowerIfNeeded,
        };

        private static readonly List<EventProcessFn> GhlGuitarInitialPostProcessList = new()
        {
        };

        private static readonly List<EventProcessFn> ProGuitarInitialPostProcessList = new()
        {
        };

        private static readonly List<EventProcessFn> DrumsInitialPostProcessList = new()
        {
        };

        private static readonly List<EventProcessFn> VocalsInitialPostProcessList = new()
        {
            TransferHarm1StarPower,
            TransferHarm2LyricPhrases,
        };

        private static Dictionary<int, EventProcessFn> GetNoteProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarMidiNoteNumberToProcessFnMap,
                MoonChart.GameMode.GHLGuitar => GhlGuitarMidiNoteNumberToProcessFnMap,
                MoonChart.GameMode.ProGuitar => ProGuitarMidiNoteNumberToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsMidiNoteNumberToProcessFnMap,
                MoonChart.GameMode.Vocals => VocalsMidiNoteNumberToProcessFnMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static Dictionary<string, ProcessModificationProcessFn> GetTextEventProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarTextEventToProcessFnMap,
                MoonChart.GameMode.GHLGuitar => GhlGuitarTextEventToProcessFnMap,
                MoonChart.GameMode.ProGuitar => ProGuitarTextEventToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsTextEventToProcessFnMap,
                MoonChart.GameMode.Vocals => VocalsTextEventToProcessFnMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
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
                MoonChart.GameMode.Vocals => VocalsSysExEventToProcessFnMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static List<EventProcessFn> GetInitialPostProcessList(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarInitialPostProcessList,
                MoonChart.GameMode.GHLGuitar => GhlGuitarInitialPostProcessList,
                MoonChart.GameMode.ProGuitar => ProGuitarInitialPostProcessList,
                MoonChart.GameMode.Drums => DrumsInitialPostProcessList,
                MoonChart.GameMode.Vocals => VocalsInitialPostProcessList,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static void FixupStarPowerIfNeeded(in EventProcessParams processParams)
        {
            // Check if instrument is allowed to be fixed up
            if (!LegacyStarPowerFixupWhitelist.Contains(processParams.instrument))
                return;

            // Only need to check one difficulty since phrases get copied to all difficulties
            var chart = processParams.song.GetChart(processParams.instrument, MoonSong.Difficulty.Expert);
            if (chart.specialPhrases.Any((sp) => sp.type == SpecialPhrase.Type.Starpower)
                || !chart.specialPhrases.Any((sp) => sp.type == SpecialPhrase.Type.Solo))
            {
                return;
            }

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                chart = processParams.song.GetChart(processParams.instrument, diff);
                foreach (var phrase in chart.specialPhrases)
                {
                    if (phrase.type == SpecialPhrase.Type.Solo)
                        phrase.type = SpecialPhrase.Type.Starpower;
                }
            }
        }

        private static void TransferHarm1StarPower(in EventProcessParams processParams)
        {
            if (processParams.instrument is not MoonSong.MoonInstrument.Harmony2 or MoonSong.MoonInstrument.Harmony3)
                return;

            // Remove any existing star power phrases
            var chart = processParams.song.GetChart(processParams.instrument, MoonSong.Difficulty.Expert);
            foreach (var phrase in chart.specialPhrases)
            {
                if (phrase.type == SpecialPhrase.Type.Starpower)
                {
                    chart.Remove(phrase, false);
                }
            }

            // Add in phrases from HARM1
            var harm1 = processParams.song.GetChart(MoonSong.MoonInstrument.Harmony1, MoonSong.Difficulty.Expert);
            foreach (var phrase in harm1.specialPhrases)
            {
                if (phrase.type == SpecialPhrase.Type.Starpower)
                {
                    // Make a new copy instead of adding the original reference
                    chart.Add(phrase.Clone(), false);
                }
            }

            chart.UpdateCache();
        }

        private static void TransferHarm2LyricPhrases(in EventProcessParams processParams)
        {
            if (processParams.instrument is not MoonSong.MoonInstrument.Harmony3)
                return;

            // Remove any existing lyric phrases
            var chart = processParams.song.GetChart(processParams.instrument, MoonSong.Difficulty.Expert);
            foreach (var phrase in chart.specialPhrases)
            {
                if (phrase.type == SpecialPhrase.Type.Starpower)
                {
                    chart.Remove(phrase, false);
                }
            }

            // Add in phrases from HARM2
            var harm1 = processParams.song.GetChart(MoonSong.MoonInstrument.Harmony3, MoonSong.Difficulty.Expert);
            foreach (var phrase in harm1.specialPhrases)
            {
                if (phrase.type == SpecialPhrase.Type.Starpower)
                {
                    // Make a new copy instead of adding the original reference
                    chart.Add(phrase.Clone(), false);
                }
            }

            chart.UpdateCache();
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
            var gameMode = MoonSong.InstumentToChartGameMode(processParams.instrument);
            if (gameMode != MoonChart.GameMode.Drums)
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
                { MidIOHelper.STARPOWER_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Starpower);
                }},
                { MidIOHelper.TAP_NOTE_CH, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsForcedType(eventProcessParams, MoonNote.MoonNoteType.Tap);
                }},
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Solo);
                }},

                { MidIOHelper.VERSUS_PHRASE_PLAYER_1, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player1);
                }},
                { MidIOHelper.VERSUS_PHRASE_PLAYER_2, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player2);
                }},

                { MidIOHelper.TREMOLO_LANE_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TremoloLane);
                }},
                { MidIOHelper.TRILL_LANE_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TrillLane);
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
                { MidIOHelper.STARPOWER_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Starpower);
                }},
                { MidIOHelper.TAP_NOTE_CH, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsForcedType(eventProcessParams, MoonNote.MoonNoteType.Tap);
                }},
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Solo);
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
                { MidIOHelper.STARPOWER_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Starpower);
                }},
                { MidIOHelper.SOLO_NOTE_PRO_GUITAR, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Solo);
                }},

                { MidIOHelper.TREMOLO_LANE_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TremoloLane);
                }},
                { MidIOHelper.TRILL_LANE_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TrillLane);
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
                { MidIOHelper.STARPOWER_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Starpower);
                }},
                { MidIOHelper.SOLO_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Solo);
                }},

                { MidIOHelper.DRUM_FILL_NOTE_0, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_1, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_2, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_3, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.ProDrums_Activation);
                }},
                { MidIOHelper.DRUM_FILL_NOTE_4, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.ProDrums_Activation);
                }},

                { MidIOHelper.VERSUS_PHRASE_PLAYER_1, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player1);
                }},
                { MidIOHelper.VERSUS_PHRASE_PLAYER_2, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player2);
                }},

                { MidIOHelper.TREMOLO_LANE_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TremoloLane);
                }},
                { MidIOHelper.TRILL_LANE_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.TrillLane);
                }},
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

        private static Dictionary<int, EventProcessFn> BuildVocalsMidiNoteNumberToProcessFnDict()
        {
            var processFnDict = new Dictionary<int, EventProcessFn>()
            {
                { MidIOHelper.STARPOWER_NOTE, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Starpower);
                }},
                { MidIOHelper.LYRICS_PHRASE_1, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player1);
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Vocals_LyricPhrase);
                }},
                { MidIOHelper.LYRICS_PHRASE_2, (in EventProcessParams eventProcessParams) => {
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Versus_Player2);
                    ProcessNoteOnEventAsSpecialPhrase(eventProcessParams, SpecialPhrase.Type.Vocals_LyricPhrase);
                }},

                { MidIOHelper.PERCUSSION_NOTE, (in EventProcessParams eventProcessParams) => {
                    foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
                    {
                        ProcessNoteOnEventAsNote(eventProcessParams, difficulty, 0, MoonNote.Flags.Vocals_Percussion);
                    };
                }},
            };

            for (int i = MidIOHelper.VOCALS_RANGE_START; i <= MidIOHelper.VOCALS_RANGE_END; i++)
            {
                int rawNote = i; // Capture the note value
                processFnDict.Add(i, (in EventProcessParams eventProcessParams) => {
                    foreach (var difficulty in EnumX<MoonSong.Difficulty>.Values)
                    {
                        ProcessNoteOnEventAsNote(eventProcessParams, difficulty, rawNote);
                    };
                });
            }

            return processFnDict;
        }
    }
}
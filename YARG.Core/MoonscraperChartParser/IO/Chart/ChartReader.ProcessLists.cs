﻿// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

// Chart file format specifications- https://docs.google.com/document/d/1v2v0U-9HQ5qHeccpExDOLJ5CMPZZ3QytPmAG5WF0Kzs/edit?usp=sharing

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using YARG.Core;
using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song.IO
{
    internal static partial class ChartReader
    {
        private delegate void NoteEventProcessFn(in NoteProcessParams noteProcessParams);

        // These dictionaries map the number of a note event to a specific function of how to process them
        private static readonly Dictionary<int, NoteEventProcessFn> GuitarChartNoteNumberToProcessFnMap = new()
        {
            { 0, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Green); }},
            { 1, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Red); }},
            { 2, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Yellow); }},
            { 3, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Blue); }},
            { 4, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Orange); }},
            { 7, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GuitarFret.Open); }},

            { 5, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Forced); }},
            { 6, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Tap); }},
        };

        private static readonly Dictionary<int, NoteEventProcessFn> DrumsChartNoteNumberToProcessFnMap = new()
        {
            { 0, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Kick); }},
            { 1, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Red); }},
            { 2, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Yellow); }},
            { 3, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Blue); }},
            { 4, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Orange); }},
            { 5, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Green); }},

            { ChartIOHelper.NOTE_OFFSET_INSTRUMENT_PLUS, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.DrumPad.Kick, MoonNote.Flags.DoubleKick);
            } },

            { ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Cymbal);
            } },
            { ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 3, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Cymbal);
            } },
            { ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 4, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Cymbal);
            } },

            // { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 0, ... }  // Reserved for kick accents, if they should ever be a thing
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 1, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Red, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 3, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 4, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Accent);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 5, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Green, NoteFlagPriority.Accent);
            } },

            // { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 0, ... }  // Reserved for kick ghosts, if they should ever be a thing
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 1, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Red, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Yellow, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 3, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Blue, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 4, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Orange, NoteFlagPriority.Ghost);
            } },
            { ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 5, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsNoteFlagToggle(noteProcessParams, (int)MoonNote.DrumPad.Green, NoteFlagPriority.Ghost);
            } },
        };

        private static readonly Dictionary<int, NoteEventProcessFn> GhlChartNoteNumberToProcessFnMap = new()
        {
            { 0, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White1); }},
            { 1, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White2); }},
            { 2, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.White3); }},
            { 3, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black1); }},
            { 4, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black2); }},
            { 8, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Black3); }},
            { 7, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsNote(noteProcessParams, (int)MoonNote.GHLiveGuitarFret.Open); }},

            { 5, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Forced); }},
            { 6, (in NoteProcessParams noteProcessParams) => { ProcessNoteOnEventAsChordFlag(noteProcessParams, NoteFlagPriority.Tap); }},
        };

        // These dictionaries map the number of a special phrase event to a specific function of how to process them
        // Not all tracks support the same phrases, so this is done for flexibility
        private static readonly Dictionary<int, NoteEventProcessFn> GuitarChartSpecialPhraseNumberToProcessFnMap = new()
        {
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_1, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Versus_Player1);
            }},
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Versus_Player2);
            }},
            { ChartIOHelper.PHRASE_STARPOWER, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Starpower);
            }},
        };

        private static readonly Dictionary<int, NoteEventProcessFn> DrumsChartSpecialPhraseNumberToProcessFnMap = new()
        {
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_1, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Versus_Player1);
            }},
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Versus_Player2);
            }},
            { ChartIOHelper.PHRASE_STARPOWER, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Starpower);
            }},
            { ChartIOHelper.PHRASE_DRUM_FILL, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.ProDrums_Activation);
            }},
            { ChartIOHelper.PHRASE_TREMOLO_LANE, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.TremoloLane);
            }},
            { ChartIOHelper.PHRASE_TRILL_LANE, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.TrillLane);
            }},
        };

        private static readonly Dictionary<int, NoteEventProcessFn> GhlChartSpecialPhraseNumberToProcessFnMap = new()
        {
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_1, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Versus_Player1);
            }},
            { ChartIOHelper.PHRASE_VERSUS_PLAYER_2, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Versus_Player2);
            }},
            { ChartIOHelper.PHRASE_STARPOWER, (in NoteProcessParams noteProcessParams) => {
                ProcessNoteOnEventAsSpecialPhrase(noteProcessParams, MoonPhrase.Type.Starpower);
            }},
        };

        // Initial post-processing list
        private static readonly List<NoteEventProcessFn> GuitarInitialPostProcessList = new()
        {
            ConvertSoloEvents,
        };

        private static readonly List<NoteEventProcessFn> DrumsInitialPostProcessList = new()
        {
            ConvertSoloEvents,
            DisambiguateDrumsType,
        };

        private static readonly List<NoteEventProcessFn> GhlGuitarInitialPostProcessList = new()
        {
            ConvertSoloEvents,
        };

        private static Dictionary<int, NoteEventProcessFn> GetNoteProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarChartNoteNumberToProcessFnMap,
                MoonChart.GameMode.GHLGuitar => GhlChartNoteNumberToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsChartNoteNumberToProcessFnMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static Dictionary<int, NoteEventProcessFn> GetSpecialPhraseProcessDict(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => GuitarChartSpecialPhraseNumberToProcessFnMap,
                MoonChart.GameMode.GHLGuitar => GhlChartSpecialPhraseNumberToProcessFnMap,
                MoonChart.GameMode.Drums => DrumsChartSpecialPhraseNumberToProcessFnMap,
                _ => throw new NotImplementedException($"No process map for game mode {gameMode}!")
            };
        }

        private static List<NoteEventProcessFn> GetInitialPostProcessList(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => new(GuitarInitialPostProcessList),
                MoonChart.GameMode.GHLGuitar => new(GhlGuitarInitialPostProcessList),
                MoonChart.GameMode.Drums => new(DrumsInitialPostProcessList),
                _ => throw new NotImplementedException($"No process list for game mode {gameMode}!")
            };
        }

        private static void ConvertSoloEvents(in NoteProcessParams noteProcessParams)
        {
            static void AddSolo(MoonChart chart, uint startTick, uint endTick)
            {
                chart.Add(new MoonPhrase(startTick, endTick - startTick, MoonPhrase.Type.Solo));
            }

            static void ProcessSoloMarkers(MoonChart chart, uint currentTick, ref uint? currentStartTick,
                ref bool start, ref bool end)
            {
                // Four scenarios to handle:

                // - Solo starts on this tick (start = true, end = false)
                if (start && !end)
                {
                    if (currentStartTick == null)
                        currentStartTick = currentTick;
                    else
                        YargTrace.DebugWarning($"Encountered duplicate solo start event on tick {currentTick}!");

                    start = false;
                }

                // - Solo ends on this tick (start = false, end = true)
                else if (!start && end)
                {
                    if (currentStartTick != null)
                    {
                        AddSolo(chart, currentStartTick.Value, currentTick);
                        currentStartTick = null;
                    }
                    else
                    {
                        YargTrace.DebugWarning($"Encountered solo end with no solo start on tick {currentTick}!");
                    }

                    end = false;
                }

                // - Solo starts and ends on this tick (start = end = true, currentStartTick = null)
                // - Solo ends on this tick and a new one starts (start = end = true, currentStartTick != null)
                else if (start && end)
                {
                    currentStartTick ??= currentTick;
                    AddSolo(chart, currentStartTick.Value, currentTick);

                    start = end = false;
                    currentStartTick = null;
                }
            }

            var chart = noteProcessParams.chart;

            uint currentTick = 0; 
            uint? currentStartTick = null;
            bool start = false;
            bool end = false;

            for (int i = 0; i < chart.events.Count;)
            {
                var text = chart.events[i];
                // Commit found events on next tick
                if (text.tick != currentTick)
                {
                    ProcessSoloMarkers(chart, currentTick, ref currentStartTick, ref start, ref end);
                    currentTick = text.tick;
                }

                // Determine what events are present on the current tick
                if (text.text == TextEventDefinitions.SOLO_START)
                {
                    chart.events.RemoveAt(i);
                    start = true;
                }
                else if (text.text == TextEventDefinitions.SOLO_END)
                {
                    chart.events.RemoveAt(i);
                    end = true;
                }
                else
                    ++i;
            }

            // Handle final set of events
            ProcessSoloMarkers(chart, currentTick, ref currentStartTick, ref start, ref end);
        }

        private static void DisambiguateDrumsType(in NoteProcessParams processParams)
        {
            var settings = processParams.settings;
            if (settings.DrumsType is not DrumsType.Unknown)
                return;

            foreach (var note in processParams.chart.notes)
            {
                // Cymbal markers indicate 4-lane
                if ((note.flags & MoonNote.Flags.ProDrums_Cymbal) != 0)
                {
                    settings.DrumsType = DrumsType.FourLane;
                    return;
                }

                // 5-lane green indicates 5-lane
                if (note.drumPad is MoonNote.DrumPad.Green)
                {
                    settings.DrumsType = DrumsType.FiveLane;
                    return;
                }
            }

            // Assume 4-lane if otherwise undetermined
            settings.DrumsType = DrumsType.FourLane;
        }
    }
}
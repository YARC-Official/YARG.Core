// Copyright (c) 2016-2020 Alexander Ong
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

namespace MoonscraperChartEditor.Song.IO
{
    public static partial class ChartReader
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
    }
}
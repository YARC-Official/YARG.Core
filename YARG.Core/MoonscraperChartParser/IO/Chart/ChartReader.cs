﻿// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

// Chart file format specifications- https://docs.google.com/document/d/1v2v0U-9HQ5qHeccpExDOLJ5CMPZZ3QytPmAG5WF0Kzs/edit?usp=sharing

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace MoonscraperChartEditor.Song.IO
{
    using TrimSplitter = SpanSplitter<char, TrimSplitProcessor>;

    internal static partial class ChartReader
    {
        private struct Anchor
        {
            public uint   tick;
            public double anchorTime;
        }

        private struct NoteFlag
        {
            public uint           tick;
            public MoonNote.Flags flag;
            public int            noteNumber;

            public NoteFlag(uint tick, MoonNote.Flags flag, int noteNumber)
            {
                this.tick = tick;
                this.flag = flag;
                this.noteNumber = noteNumber;
            }
        }

        private struct NoteEvent
        {
            public uint tick;
            public int  noteNumber;
            public uint length;
        }

        private struct NoteProcessParams
        {
            public MoonChart                chart;
            public ParseSettings            settings;
            public NoteEvent                noteEvent;
            public List<NoteEventProcessFn> postNotesAddedProcessList;
        }

        #region Utility

        // https://cc.davelozinski.com/c-sharp/fastest-way-to-convert-a-string-to-an-int
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastInt32Parse(ReadOnlySpan<char> text)
        {
            int value = 0;
            foreach (char character in text) value = value * 10 + (character - '0');

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong FastUint64Parse(ReadOnlySpan<char> text)
        {
            ulong value = 0;
            foreach (char character in text) value = value * 10 + (ulong) (character - '0');

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char>
            GetNextWord(this ReadOnlySpan<char> buffer, out ReadOnlySpan<char> remaining) =>
            buffer.SplitOnceTrimmed(' ', out remaining);

        #endregion

        public static MoonSong ReadFromFile(string filepath)
        {
            var settings = ParseSettings.Default;
            return ReadFromFile(ref settings, filepath);
        }

        public static MoonSong ReadFromText(ReadOnlySpan<char> chartText)
        {
            var settings = ParseSettings.Default;
            return ReadFromText(ref settings, chartText);
        }

        public static MoonSong ReadFromFile(ref ParseSettings settings, string filepath)
        {
            try
            {
                if (!File.Exists(filepath)) throw new Exception("File does not exist");

                string extension = Path.GetExtension(filepath);

                if (extension != ".chart") throw new Exception("Bad file type");

                string text = File.ReadAllText(filepath);
                return ReadFromText(ref settings, text);
            }
            catch (Exception e)
            {
                throw new Exception("Could not open file!", e);
            }
        }

        public static MoonSong ReadFromText(ref ParseSettings settings, ReadOnlySpan<char> chartText)
        {
            var song = new MoonSong();

            while (!chartText.IsEmpty)
            {
                // Find section name
                int nameIndex = chartText.IndexOf('[');
                int nameEndIndex = chartText.IndexOf(']');
                if (nameIndex < 0 || nameEndIndex < 0 || nameEndIndex < nameIndex) break;

                nameIndex++; // Exclude starting bracket
                var sectionName = chartText[nameIndex..nameEndIndex];
                chartText = chartText[nameEndIndex..];

                // Find section body
                int sectionIndex = chartText.IndexOf('{');
                int sectionEndIndex = chartText.IndexOf('}');
                if (sectionIndex < 0 || sectionEndIndex < 0 || sectionEndIndex < sectionIndex) break;

                sectionIndex++; // Exclude starting bracket
                var sectionText = chartText[sectionIndex..sectionEndIndex];
                chartText = chartText[sectionEndIndex..];

                var splitter = sectionText.SplitTrimmed('\n');
                SubmitChartData(ref settings, song, sectionName, splitter);
            }

            return song;
        }

        private static void SubmitChartData(ref ParseSettings settings, MoonSong song, ReadOnlySpan<char> sectionName,
            TrimSplitter sectionLines)
        {
            if (sectionName.Equals(ChartIOHelper.SECTION_SONG, StringComparison.Ordinal))
            {
                YargLogger.LogTrace("Loading chart properties");
                SubmitDataSong(song, ref settings, sectionLines);
                return;
            }
            else if (sectionName.Equals(ChartIOHelper.SECTION_SYNC_TRACK, StringComparison.Ordinal))
            {
                YargLogger.LogTrace("Loading sync data");
                SubmitDataSync(song, sectionLines);
                return;
            }
            else if (sectionName.Equals(ChartIOHelper.SECTION_EVENTS, StringComparison.Ordinal))
            {
                YargLogger.LogTrace("Loading events data");
                SubmitDataGlobals(song, sectionLines);
                return;
            }

            // Determine what difficulty
            foreach (var (diffName, difficulty) in ChartIOHelper.TrackNameToTrackDifficultyLookup)
            {
                if (!sectionName.StartsWith(diffName, StringComparison.Ordinal)) continue;

                foreach (var (instrumentName, instrument) in ChartIOHelper.InstrumentStrToEnumLookup)
                {
                    if (!sectionName.EndsWith(instrumentName, StringComparison.Ordinal)) continue;

                    YargLogger.LogFormatDebug("Loading data for {0} {1}", difficulty, instrument);
                    LoadChart(ref settings, song, sectionLines, instrument, difficulty);
                    break;
                }

                break;
            }
        }

        private static void SubmitDataSong(MoonSong song, ref ParseSettings settings, TrimSplitter sectionLines)
        {
            ChartMetadata.ParseSongSection(song, sectionLines);
            ValidateAndApplySettings(song, ref settings);
        }

        private static void ValidateAndApplySettings(MoonSong song, ref ParseSettings settings)
        {
            // Apply HOPO threshold settings
            song.hopoThreshold = ChartIOHelper.GetHopoThreshold(in settings, song.resolution);

            // Sustain cutoff threshold is not verified, sustains are not cut off by default in .chart
            // SP note is not verified, as it is only relevant for .mid
            // Note snap threshold is not verified, as the parser doesn't use it

            // Chord HOPO cancellation does not apply in .chart
            settings.ChordHopoCancellation = false;
        }

        private static void SubmitDataSync(MoonSong song, TrimSplitter sectionLines)
        {
            var anchorData = new List<Anchor>();
            uint prevTick = 0;

            foreach (var _line in sectionLines)
            {
                var line = _line.Trim();
                if (line.IsEmpty) continue;

                try
                {
                    // Split on the equals sign
                    var tickText = line.SplitOnceTrimmed('=', out var remaining);

                    // Get tick
                    uint tick = (uint) FastInt32Parse(tickText);

                    if (prevTick > tick) throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    // Get event type
                    var typeCodeText = remaining.GetNextWord(out remaining);
                    char typeCode = typeCodeText[0];
                    switch (typeCode)
                    {
                        case 'T' when typeCodeText[1] == 'S':
                        {
                            // Get numerator
                            var numeratorText = remaining.GetNextWord(out remaining);
                            uint numerator = (uint) FastInt32Parse(numeratorText);

                            // Get denominator
                            var denominatorText = remaining.GetNextWord(out remaining);
                            uint denominator = denominatorText.IsEmpty ? 2 : (uint) FastInt32Parse(denominatorText);
                            song.timeSignatures.Add(new MoonTimeSignature(tick, numerator,
                                (uint) Math.Pow(2, denominator)));
                            break;
                        }

                        case 'B':
                        {
                            // Get tempo value
                            var tempoText = remaining.GetNextWord(out remaining);
                            uint tempo = (uint) FastInt32Parse(tempoText);

                            song.bpms.Add(new MoonTempo(tick, tempo / 1000f));
                            break;
                        }

                        case 'A':
                        {
                            // Get anchor time
                            var anchorText = remaining.GetNextWord(out remaining);
                            ulong anchorTime = FastUint64Parse(anchorText);

                            var anchor = new Anchor()
                            {
                                tick = tick,
                                anchorTime = anchorTime / 1000000.0
                            };
                            anchorData.Add(anchor);
                            break;
                        }

                        default:
                            YargLogger.LogFormatWarning("Unrecognized type code '{0}'!", typeCode);
                            break;
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                }
            }

            foreach (var anchor in anchorData)
            {
                int arrayPos = MoonObjectHelper.FindClosestPosition(anchor.tick, song.bpms);
                if (song.bpms[arrayPos].tick == anchor.tick)
                    song.bpms[arrayPos].anchor = anchor.anchorTime;
                // Create a new anchored bpm
                else if (anchor.tick < song.bpms[arrayPos].tick)
                    song.bpms.Insert(arrayPos,
                        new MoonTempo(anchor.tick, song.bpms[arrayPos - 1].value, anchor.anchorTime));
                else
                    song.bpms.Insert(arrayPos + 1,
                        new MoonTempo(anchor.tick, song.bpms[arrayPos].value, anchor.anchorTime));
            }

            song.UpdateBPMTimeValues();
        }

        private static void SubmitDataGlobals(MoonSong song, TrimSplitter sectionLines)
        {
            uint prevTick = 0;
            foreach (var _line in sectionLines)
            {
                var line = _line.Trim();
                if (line.IsEmpty) continue;

                try
                {
                    // Split on the equals sign
                    var tickText = line.SplitOnceTrimmed('=', out var remaining);

                    // Get tick
                    uint tick = (uint) FastInt32Parse(tickText);

                    if (prevTick > tick) throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    // Get event type
                    var typeCodeText = remaining.GetNextWord(out remaining);
                    if (typeCodeText[0] == 'E')
                    {
                        // Get event text
                        var eventText = TextEvents.NormalizeTextEvent(remaining.TrimOnce('"'));

                        // Check for section events
                        if (TextEvents.TryParseSectionEvent(eventText, out var sectionName))
                        {
                            song.sections.Add(new MoonText(sectionName.ToString(), tick));
                        }
                        else
                        {
                            song.events.Add(new MoonText(eventText.ToString(), tick));
                        }
                    }
                    else
                    {
                        YargLogger.LogFormatWarning("Unrecognized type code '{0}'!", typeCodeText[0]);
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                }
            }
        }

        #region Utility

        #endregion

        private static void LoadChart(ref ParseSettings settings, MoonSong song, TrimSplitter sectionLines,
            MoonSong.MoonInstrument instrument, MoonSong.Difficulty difficulty)
        {
            var chart = song.GetChart(instrument, difficulty);
            var gameMode = chart.gameMode;

            var flags = new List<NoteFlag>();
            var postNotesAddedProcessList = GetInitialPostProcessList(gameMode);

            var processParams = new NoteProcessParams()
            {
                chart = chart,
                settings = settings,
                postNotesAddedProcessList = postNotesAddedProcessList
            };

            chart.notes.Capacity = 5000;

            var noteProcessDict = GetNoteProcessDict(gameMode);
            var specialPhraseProcessDict = GetSpecialPhraseProcessDict(gameMode);

            try
            {
                uint prevTick = 0;
                // Load notes, collect flags
                foreach (var line in sectionLines)
                {
                    try
                    {
                        // Split on the equals sign
                        var tickText = line.SplitOnceTrimmed('=', out var remaining);

                        // Get tick
                        uint tick = (uint) FastInt32Parse(tickText);

                        if (prevTick > tick) throw new Exception("Tick value not in ascending order");
                        prevTick = tick;

                        // Get event type
                        char typeCode = remaining.GetNextWord(out remaining)[0];
                        switch (
                            typeCode) // Note this will need to be changed if keys are ever greater than 1 character long
                        {
                            case 'N':
                            {
                                // Get note data
                                var noteTypeText = remaining.GetNextWord(out remaining);
                                int noteType = FastInt32Parse(noteTypeText);

                                var noteLengthText = remaining.GetNextWord(out remaining);
                                uint noteLength = (uint) FastInt32Parse(noteLengthText);

                                // Process the note
                                if (noteProcessDict.TryGetValue(noteType, out var processFn))
                                {
                                    var noteEvent = new NoteEvent()
                                    {
                                        tick = tick,
                                        noteNumber = noteType,
                                        length = noteLength
                                    };
                                    processParams.noteEvent = noteEvent;
                                    processFn(ref processParams);
                                }

                                break;
                            }

                            case 'S':
                            {
                                // Get phrase data
                                var phraseTypeText = remaining.GetNextWord(out remaining);
                                int phraseType = FastInt32Parse(phraseTypeText);

                                var phraseLengthText = remaining.GetNextWord(out remaining);
                                uint phraseLength = (uint) FastInt32Parse(phraseLengthText);

                                if (specialPhraseProcessDict.TryGetValue(phraseType, out var processFn))
                                {
                                    var noteEvent = new NoteEvent()
                                    {
                                        tick = tick,
                                        noteNumber = phraseType,
                                        length = phraseLength
                                    };
                                    processParams.noteEvent = noteEvent;
                                    processFn(ref processParams);
                                }

                                break;
                            }
                            case 'E':
                            {
                                var eventText = TextEvents.NormalizeTextEvent(remaining.TrimOnce('"'));
                                chart.events.Add(new MoonText(eventText.ToString(), tick));
                                break;
                            }

                            default:
                                YargLogger.LogFormatWarning("Unrecognized type code '{0}'!", typeCode);
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        YargLogger.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                    }
                }

                foreach (var fn in postNotesAddedProcessList)
                {
                    fn(ref processParams);
                }

                chart.notes.TrimExcess();
                settings = processParams.settings;
            }
            catch (Exception e)
            {
                // Bad load, most likely a parsing error
                YargLogger.LogException(e, $"Error parsing .chart section for {difficulty} {instrument}!");
                chart.Clear();
            }
        }

        private static void ProcessNoteOnEventAsNote(ref NoteProcessParams noteProcessParams, int ingameFret,
            MoonNote.Flags defaultFlags = MoonNote.Flags.None)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = noteEvent.length;
            if (sus < noteProcessParams.settings.SustainCutoffThreshold) sus = 0;

            var newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            MoonObjectHelper.PushNote(newMoonNote, chart.notes);
        }

        private static void ProcessNoteOnEventAsSpecialPhrase(ref NoteProcessParams noteProcessParams,
            MoonPhrase.Type type)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = noteEvent.length;

            var newPhrase = new MoonPhrase(tick, sus, type);
            chart.specialPhrases.Add(newPhrase);
        }

        private static void ProcessNoteOnEventAsChordFlag(ref NoteProcessParams noteProcessParams,
            NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((ref NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsChordFlagPostDelay(ref processParams, flagEvent, flagData);
            });
        }

        private static void ProcessNoteOnEventAsChordFlagPostDelay(ref NoteProcessParams noteProcessParams,
            NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            var chart = noteProcessParams.chart;
            MoonObjectHelper.FindObjectsAtPosition(noteEvent.tick, chart.notes, out int index, out int length);
            if (length > 0)
            {
                GroupAddFlags(chart.notes, flagData, index, length);
            }
        }

        private static void ProcessNoteOnEventAsNoteFlagToggle(ref NoteProcessParams noteProcessParams, int rawNote,
            NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((ref NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsNoteFlagTogglePostDelay(ref processParams, rawNote, flagEvent, flagData);
            });
        }

        private static void ProcessNoteOnEventAsNoteFlagTogglePostDelay(ref NoteProcessParams noteProcessParams,
            int rawNote, NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            var chart = noteProcessParams.chart;
            MoonObjectHelper.FindObjectsAtPosition(noteEvent.tick, chart.notes, out int index, out int length);
            if (length > 0)
            {
                for (int i = index; i < index + length; ++i)
                {
                    var note = chart.notes[i];
                    if (note.rawNote == rawNote)
                    {
                        TryAddNoteFlags(note, flagData);
                    }
                }
            }
        }

        private static void GroupAddFlags(IList<MoonNote> notes, NoteFlagPriority flagData, int index, int length)
        {
            for (int i = index; i < index + length; ++i)
            {
                TryAddNoteFlags(notes[i], flagData);
            }
        }

        private static void TryAddNoteFlags(MoonNote note, NoteFlagPriority flagData)
        {
            if (!flagData.TryApplyToNote(note))
            {
                YargLogger.LogFormatDebug("Could not apply flag {0} to a note. It was blocked by existing flag {1}.",
                    flagData.flagToAdd, flagData.blockingFlag);
            }
        }
    }
}
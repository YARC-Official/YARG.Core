// Copyright (c) 2016-2020 Alexander Ong
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
using YARG.Core.Song;
using YARG.Core.Utility;

namespace MoonscraperChartEditor.Song.IO
{
    using TrimSplitter = SpanSplitter<char, TrimSplitProcessor>;

    internal static partial class ChartReader
    {
        private struct Anchor
        {
            public uint tick;
            public double anchorTime;
        }

        private struct NoteFlag
        {
            public uint tick;
            public MoonNote.Flags flag;
            public int noteNumber;

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
            public int noteNumber;
            public uint length;
        }

        private struct NoteProcessParams
        {
            public MoonChart chart;
            public ParseSettings settings;
            public NoteEvent noteEvent;
            public List<NoteEventProcessFn> postNotesAddedProcessList;
        }

        #region Utility
        // https://cc.davelozinski.com/c-sharp/fastest-way-to-convert-a-string-to-an-int
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastInt32Parse(ReadOnlySpan<char> text)
        {
            int value = 0;
            foreach (char character in text)
                value = value * 10 + (character - '0');

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong FastUint64Parse(ReadOnlySpan<char> text)
        {
            ulong value = 0;
            foreach (char character in text)
                value = value * 10 + (ulong)(character - '0');

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> GetNextWord(this ReadOnlySpan<char> buffer, out ReadOnlySpan<char> remaining)
            => buffer.SplitOnceTrimmed(' ', out remaining);
        #endregion

        public static MoonSong ReadFromFile(ParseSettings settings, string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                    throw new Exception("File does not exist");

                string extension = Path.GetExtension(filepath);

                if (extension != ".chart")
                    throw new Exception("Bad file type");

                string text = File.ReadAllText(filepath);
                return ReadFromText(settings, text);
            }
            catch (Exception e)
            {
                throw new Exception("Could not open file!", e);
            }
        }

        public static MoonSong ReadFromText(ParseSettings settings, ReadOnlySpan<char> chartText)
        {
            var song = new MoonSong();

            while (!chartText.IsEmpty)
            {
                // Find section name
                int nameIndex = chartText.IndexOf('[');
                int nameEndIndex = chartText.IndexOf(']');
                if (nameIndex < 0 || nameEndIndex < 0 || nameEndIndex < nameIndex)
                    break;

                nameIndex++; // Exclude starting bracket
                var sectionName = chartText[nameIndex..nameEndIndex];
                chartText = chartText[nameEndIndex..];

                // Find section body
                int sectionIndex = chartText.IndexOf('{');
                int sectionEndIndex = chartText.IndexOf('}');
                if (sectionIndex < 0 || sectionEndIndex < 0 || sectionEndIndex < sectionIndex)
                    break;

                sectionIndex++; // Exclude starting bracket
                var sectionText = chartText[sectionIndex..sectionEndIndex];
                chartText = chartText[sectionEndIndex..];

                var splitter = sectionText.SplitTrimmed('\n');
                SubmitChartData(settings, song, sectionName, splitter);
            }
            return song;
        }

        private static void SubmitChartData(ParseSettings settings, MoonSong song, ReadOnlySpan<char> sectionName,
            TrimSplitter sectionLines)
        {
            if (sectionName.Equals(ChartIOHelper.SECTION_SONG, StringComparison.Ordinal))
            {
                YargTrace.DebugInfo("Loading chart properties");
                SubmitDataSong(song, settings, sectionLines);
                return;
            }
            else if (sectionName.Equals(ChartIOHelper.SECTION_SYNC_TRACK, StringComparison.Ordinal))
            {
                YargTrace.DebugInfo("Loading sync data");
                SubmitDataSync(song, sectionLines);
                return;
            }
            else if (sectionName.Equals(ChartIOHelper.SECTION_EVENTS, StringComparison.Ordinal))
            {
                YargTrace.DebugInfo("Loading events data");
                SubmitDataGlobals(song, sectionLines);
                return;
            }

            // Determine what difficulty
            foreach (var (diffName, difficulty) in ChartIOHelper.TrackNameToTrackDifficultyLookup)
            {
                if (!sectionName.StartsWith(diffName, StringComparison.Ordinal))
                    continue;

                foreach (var (instrumentName, instrument) in ChartIOHelper.InstrumentStrToEnumLookup)
                {
                    if (!sectionName.EndsWith(instrumentName, StringComparison.Ordinal))
                        continue;

                    YargTrace.DebugInfo($"Loading data for {difficulty} {instrument}");
                    LoadChart(settings, song, sectionLines, instrument, difficulty);
                    break;
                }

                break;
            }
        }

        private static void SubmitDataSong(MoonSong song, ParseSettings settings, TrimSplitter sectionLines)
        {
            ChartMetadata.ParseSongSection(song, sectionLines);
            ValidateAndApplySettings(song, settings);
        }

        private static void ValidateAndApplySettings(MoonSong song, ParseSettings settings)
        {
            // Apply HOPO threshold settings
            MoonNote.hopoThreshold = ChartIOHelper.GetHopoThreshold(settings, song.resolution);

            // Sustain cutoff threshold is not verified, sustains are not cut off by default in .chart
            // SP note is not verified, as it is only relevant for .mid
            // Note snap threshold is not verified, as the parser doesn't use it
        }

        private static void SubmitDataSync(MoonSong song, TrimSplitter sectionLines)
        {
            var anchorData = new List<Anchor>();
            uint prevTick = 0;

            foreach (var _line in sectionLines)
            {
                var line = _line.Trim();
                if (line.IsEmpty)
                    continue;

                try
                {
                    // Split on the equals sign
                    var tickText = line.SplitOnceTrimmed('=', out var remaining);

                    // Get tick
                    uint tick = (uint)FastInt32Parse(tickText);

                    if (prevTick > tick)
                        throw new Exception("Tick value not in ascending order");
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
                            uint numerator = (uint)FastInt32Parse(numeratorText);

                            // Get denominator
                            var denominatorText = remaining.GetNextWord(out remaining);
                            uint denominator = denominatorText.IsEmpty ? 2 : (uint)FastInt32Parse(denominatorText);
                            song.timeSignatures.Add(new TimeSignature(tick, numerator, (uint) Math.Pow(2, denominator)));
                            break;
                        }

                        case 'B':
                        {
                            // Get tempo value
                            var tempoText = remaining.GetNextWord(out remaining);
                            uint tempo = (uint)FastInt32Parse(tempoText);

                            song.bpms.Add(new BPM(tick, tempo));
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
                            YargTrace.LogWarning($"Unrecognized type code '{typeCode}'!");
                            break;
                    }
                }
                catch (Exception e)
                {
                    YargTrace.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                }
            }

            foreach (var anchor in anchorData)
            {
                int arrayPos = SongObjectHelper.FindClosestPosition(anchor.tick, song.bpms);
                if (song.bpms[arrayPos].tick == anchor.tick)
                    song.bpms[arrayPos].anchor = anchor.anchorTime;
                // Create a new anchored bpm
                else if (anchor.tick < song.bpms[arrayPos].tick)
                    song.bpms.Insert(arrayPos, new BPM(anchor.tick, song.bpms[arrayPos - 1].value, anchor.anchorTime));
                else
                    song.bpms.Insert(arrayPos + 1, new BPM(anchor.tick, song.bpms[arrayPos].value, anchor.anchorTime));
            }

            song.UpdateBPMTimeValues();
        }

        private static void SubmitDataGlobals(MoonSong song, TrimSplitter sectionLines)
        {
            uint prevTick = 0;
            foreach (var _line in sectionLines)
            {
                var line = _line.Trim();
                if (line.IsEmpty)
                    continue;

                try
                {
                    // Split on the equals sign
                    var tickText = line.SplitOnceTrimmed('=', out var remaining);

                    // Get tick
                    uint tick = (uint) FastInt32Parse(tickText);

                    if (prevTick > tick)
                        throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    // Get event type
                    var typeCodeText = remaining.GetNextWord(out remaining);
                    if (typeCodeText[0] == 'E')
                    {
                        // Get event text
                        string eventText = remaining.Trim().Trim('"').ToString();

                        // Strip off brackets and any garbage outside of them
                        var match = ChartIOHelper.TextEventRegex.Match(eventText);
                        if (match.Success)
                        {
                            eventText = match.Groups[1].Value;
                        }

                        // Check for section events
                        var sectionMatch = ChartIOHelper.SectionEventRegex.Match(eventText);
                        if (sectionMatch.Success)
                        {
                            // This is a section, use the text grouped by the regex
                            string sectionText = sectionMatch.Groups[1].Value;
                            song.sections.Add(new Section(sectionText, tick));
                        }
                        else
                        {
                            song.events.Add(new Event(eventText, tick));
                        }
                    }
                    else
                        YargTrace.LogWarning($"Unrecognized type code '{typeCodeText[0]}'!");
                }
                catch (Exception e)
                {
                    YargTrace.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                }
            }
        }

        #region Utility
        #endregion

        private static void LoadChart(ParseSettings settings, MoonSong song, TrimSplitter sectionLines,
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
                        uint tick = (uint)FastInt32Parse(tickText);

                        if (prevTick > tick)
                            throw new Exception("Tick value not in ascending order");
                        prevTick = tick;

                        // Get event type
                        char typeCode = remaining.GetNextWord(out remaining)[0];
                        switch (typeCode)    // Note this will need to be changed if keys are ever greater than 1 character long
                        {
                            case 'N':
                            {
                                // Get note data
                                var noteTypeText = remaining.GetNextWord(out remaining);
                                int noteType = FastInt32Parse(noteTypeText);

                                var noteLengthText = remaining.GetNextWord(out remaining);
                                uint noteLength = (uint)FastInt32Parse(noteLengthText);

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
                                    processFn(processParams);
                                }
                                break;
                            }

                            case 'S':
                            {
                                // Get phrase data
                                var phraseTypeText = remaining.GetNextWord(out remaining);
                                int phraseType = FastInt32Parse(phraseTypeText);

                                var phraseLengthText = remaining.GetNextWord(out remaining);
                                uint phraseLength = (uint)FastInt32Parse(phraseLengthText);

                                if (specialPhraseProcessDict.TryGetValue(phraseType, out var processFn))
                                {
                                    var noteEvent = new NoteEvent()
                                    {
                                        tick = tick,
                                        noteNumber = phraseType,
                                        length = phraseLength
                                    };
                                    processParams.noteEvent = noteEvent;
                                    processFn(processParams);
                                }
                                break;
                            }
                            case 'E':
                            {
                                // Get event text
                                string eventText = remaining.Trim().Trim('"').ToString();

                                // Strip off brackets and any garbage outside of them
                                var match = ChartIOHelper.TextEventRegex.Match(eventText);
                                if (match.Success)
                                {
                                    eventText = match.Groups[1].Value;
                                }

                                chart.events.Add(new ChartEvent(tick, eventText));
                                break;
                            }

                            default:
                                YargTrace.LogWarning($"Unrecognized type code '{typeCode}'!");
                                break;
                        }

                    }
                    catch (Exception e)
                    {
                        YargTrace.LogException(e, $"Error parsing .chart line '{line.ToString()}'!");
                    }
                }

                foreach (var fn in postNotesAddedProcessList)
                {
                    fn(processParams);
                }
                chart.notes.TrimExcess();
            }
            catch (Exception e)
            {
                // Bad load, most likely a parsing error
                YargTrace.LogException(e, $"Error parsing .chart section for {difficulty} {instrument}!");
                chart.Clear();
            }
        }

        private static uint ApplySustainCutoff(ParseSettings settings, uint length)
        {
            if (length <= settings.SustainCutoffThreshold)
                length = 0;

            return length;
        }

        private static void ProcessNoteOnEventAsNote(in NoteProcessParams noteProcessParams, int ingameFret, MoonNote.Flags defaultFlags = MoonNote.Flags.None)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = ApplySustainCutoff(noteProcessParams.settings, noteEvent.length);

            var newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            SongObjectHelper.PushNote(newMoonNote, chart.notes);
        }

        private static void ProcessNoteOnEventAsSpecialPhrase(in NoteProcessParams noteProcessParams, SpecialPhrase.Type type)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = noteEvent.length;

            var newPhrase = new SpecialPhrase(tick, sus, type);
            chart.specialPhrases.Add(newPhrase);
        }

        private static void ProcessNoteOnEventAsChordFlag(in NoteProcessParams noteProcessParams, NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((in NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsChordFlagPostDelay(processParams, flagEvent, flagData);
            });
        }

        private static void ProcessNoteOnEventAsChordFlagPostDelay(in NoteProcessParams noteProcessParams, NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            var chart = noteProcessParams.chart;
            SongObjectHelper.FindObjectsAtPosition(noteEvent.tick, chart.notes, out int index, out int length);
            if (length > 0)
            {
                GroupAddFlags(chart.notes, flagData, index, length);
            }
        }

        private static void ProcessNoteOnEventAsNoteFlagToggle(in NoteProcessParams noteProcessParams, int rawNote, NoteFlagPriority flagData)
        {
            var flagEvent = noteProcessParams.noteEvent;

            // Delay the actual processing once all the notes are actually in
            noteProcessParams.postNotesAddedProcessList.Add((in NoteProcessParams processParams) =>
            {
                ProcessNoteOnEventAsNoteFlagTogglePostDelay(processParams, rawNote, flagEvent, flagData);
            });
        }

        private static void ProcessNoteOnEventAsNoteFlagTogglePostDelay(in NoteProcessParams noteProcessParams, int rawNote, NoteEvent noteEvent, NoteFlagPriority flagData)
        {
            var chart = noteProcessParams.chart;
            SongObjectHelper.FindObjectsAtPosition(noteEvent.tick, chart.notes, out int index, out int length);
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
                YargTrace.DebugWarning($"Could not apply flag {flagData.flagToAdd} to a note. It was blocked by existing flag {flagData.blockingFlag}.");
            }
        }
    }
}

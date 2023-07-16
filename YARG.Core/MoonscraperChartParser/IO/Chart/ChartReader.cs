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
using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song.IO
{
    public static partial class ChartReader
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

        public static MoonSong ReadChart(ParseSettings settings, string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                    throw new Exception("File does not exist");

                string extension = Path.GetExtension(filepath);

                if (extension != ".chart")
                    throw new Exception("Bad file type");

                var reader = File.OpenText(filepath);
                return ReadChart(settings, reader);
            }
            catch (Exception e)
            {
                throw new Exception("Could not open file!", e);
            }
        }

        public static MoonSong ReadChart(ParseSettings settings, TextReader reader)
        {
            var song = new MoonSong();
            bool open = false;
            string dataName = string.Empty;

            var dataStrings = new List<string>();

            // Gather lines between {} brackets and submit data
            for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.Length <= 0)
                    continue;

                if (trimmedLine[0] == '[' && trimmedLine[^1] == ']')
                {
                    dataName = trimmedLine;
                }
                else if (trimmedLine == "{")
                {
                    open = true;
                }
                else if (trimmedLine == "}")
                {
                    open = false;

                    // Submit data
                    SubmitChartData(settings, song, dataName, dataStrings);

                    dataName = string.Empty;
                    dataStrings.Clear();
                }
                else
                {
                    if (open)
                    {
                        // Add data into the array
                        dataStrings.Add(trimmedLine);
                    }
                    else if (dataStrings.Count > 0 && dataName != string.Empty)
                    {
                        // Submit data
                        SubmitChartData(settings, song, dataName, dataStrings);

                        dataName = string.Empty;
                        dataStrings.Clear();
                    }
                }
            }

            reader.Close();
            song.UpdateCache();
            return song;
        }

        private static void SubmitChartData(ParseSettings settings, MoonSong song, string dataName, List<string> stringData)
        {
            switch (dataName)
            {
                case ChartIOHelper.SECTION_SONG:
                    Debug.WriteLine("Loading chart properties");
                    SubmitDataSong(song, stringData);
                    break;
                case ChartIOHelper.SECTION_SYNC_TRACK:
                    Debug.WriteLine("Loading sync data");
                    goto case ChartIOHelper.SECTION_EVENTS;
                case ChartIOHelper.SECTION_EVENTS:
                    Debug.WriteLine("Loading events data");
                    SubmitDataGlobals(song, stringData);
                    break;
                default:
                    // Determine what difficulty
                    foreach (var kvPair in ChartIOHelper.TrackNameToTrackDifficultyLookup)
                    {
                        if (Regex.IsMatch(dataName, $@"\[{kvPair.Key}."))
                        {
                            var chartDiff = kvPair.Value;
                            int instumentStringOffset = 1 + kvPair.Key.Length;

                            string instrumentKey = dataName.Substring(instumentStringOffset, dataName.Length - instumentStringOffset - 1);
                            if (!ChartIOHelper.InstrumentStrToEnumLookup.TryGetValue(instrumentKey, out var instrument))
                            {
                                break;
                            }

                            LoadChart(settings, song, stringData, instrument, chartDiff);

                            // Chart loaded
                            break;
                        }
                    }
                    break;
            }
        }

        private static void SubmitDataSong(MoonSong song, List<string> stringData)
        {
            Debug.WriteLine("Loading song properties");
            var metaData = song.metaData;

            try
            {
                foreach (string line in stringData)
                {
                    // Name = "5000 Robots"
                    if (ChartMetadata.name.regex.IsMatch(line))
                    {
                        metaData.name = ChartMetadata.ParseAsString(line);
                    }

                    // Artist = "TheEruptionOffer"
                    else if (ChartMetadata.artist.regex.IsMatch(line))
                    {
                        metaData.artist = ChartMetadata.ParseAsString(line);
                    }

                    // Charter = "TheEruptionOffer"
                    else if (ChartMetadata.charter.regex.IsMatch(line))
                    {
                        metaData.charter = ChartMetadata.ParseAsString(line);
                    }

                    // Album = "Rockman Holic"
                    else if (ChartMetadata.album.regex.IsMatch(line))
                    {
                        metaData.album = ChartMetadata.ParseAsString(line);
                    }

                    // Offset = 0
                    else if (ChartMetadata.offset.regex.IsMatch(line))
                    {
                        song.offset = ChartMetadata.ParseAsFloat(line);
                    }

                    // Resolution = 192
                    else if (ChartMetadata.resolution.regex.IsMatch(line))
                    {
                        song.resolution = ChartMetadata.ParseAsShort(line);
                    }

                    // Difficulty = 0
                    else if (ChartMetadata.difficulty.regex.IsMatch(line))
                    {
                        metaData.difficulty = int.Parse(Regex.Matches(line, @"\d+")[0].ToString());
                    }

                    // Length = 300
                    else if (ChartMetadata.length.regex.IsMatch(line))
                    {
                        song.manualLength = ChartMetadata.ParseAsFloat(line);
                    }

                    // PreviewStart = 0.00
                    else if (ChartMetadata.previewStart.regex.IsMatch(line))
                    {
                        metaData.previewStart = ChartMetadata.ParseAsFloat(line);
                    }

                    // PreviewEnd = 0.00
                    else if (ChartMetadata.previewEnd.regex.IsMatch(line))
                    {
                        metaData.previewEnd = ChartMetadata.ParseAsFloat(line);
                    }

                    // Genre = "rock"
                    else if (ChartMetadata.genre.regex.IsMatch(line))
                    {
                        metaData.genre = ChartMetadata.ParseAsString(line);
                    }

                    else if (ChartMetadata.year.regex.IsMatch(line))
                        metaData.year = Regex.Replace(ChartMetadata.ParseAsString(line), @"\D", "");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error when reading chart metadata: {e.Message}");
            }
        }

        private static void SubmitDataGlobals(MoonSong song, List<string> stringData)
        {
            var anchorData = new List<Anchor>();

            foreach (string line in stringData)
            {
                try
                {
                    int stringStartIndex = 0;
                    int stringLength = 0;

                    // Advance to tick
                    AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                    uint tick = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                    // Advance to equality
                    stringStartIndex += stringLength;
                    AdvanceNextWord(line, ref stringStartIndex, ref stringLength);

                    // Advance to type
                    stringStartIndex += stringLength;
                    AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
            
                    string typeCode = line.Substring(stringStartIndex, stringLength).ToUpperInvariant();
                    switch (typeCode)
                    {
                        case "TS":
                        {
                            // Advance to numerator
                            stringStartIndex += stringLength;
                            AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                            uint numerator = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                            uint denominator = 2;
                            // Check for denominator
                            if (stringStartIndex + stringLength < line.Length)
                            {
                                // Advance to denominator
                                stringStartIndex += stringLength;
                                AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                denominator = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);
                            }

                            song.Add(new TimeSignature(tick, numerator, (uint)Math.Pow(2, denominator)), false);
                            break;
                        }

                        case "B":
                        {
                            // Advance to tempo value
                            stringStartIndex += stringLength;
                            AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                            uint tempo = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                            song.Add(new BPM(tick, tempo), false);
                            break;
                        }

                        case "E":
                        {
                            // Advance to start of event text
                            stringStartIndex += stringLength;
                            AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                            // Ignore whitespace in the text
                            stringLength = line.Length - stringStartIndex;
                            // Trim off quotation marks
                            if (line[stringStartIndex] == '"')
                                stringStartIndex++;
                            if (line[^1] == '"')
                                stringLength--;

                            string eventName = line.Substring(stringStartIndex, stringLength);

                            // Check for section events
                            var sectionMatch = ChartIOHelper.SectionEventRegex.Match(eventName);
                            if (sectionMatch.Success)
                            {
                                // This is a section, use the text grouped by the regex
                                string sectionText = sectionMatch.Groups[1].Value;
                                song.Add(new Section(sectionText, tick), false);
                            }
                            else
                            {
                                song.Add(new Event(eventName, tick), false);
                            }
                            break;
                        }

                        case "A":
                        {
                            // Advance to anchor time
                            stringStartIndex += stringLength;
                            AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                            ulong anchorTime = FastStringToUInt64Parse(line, stringStartIndex, stringLength);

                            var anchor = new Anchor()
                            {
                                tick = tick,
                                anchorTime = anchorTime / 1000000.0
                            };
                            anchorData.Add(anchor);
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error parsing .chart line '{line}': {e}");
                }
            }

            var bpms = song.syncTrack.OfType<BPM>().ToArray();        // BPMs are currently uncached
            foreach (var anchor in anchorData)
            {
                int arrayPos = SongObjectHelper.FindClosestPosition(anchor.tick, bpms);
                if (bpms[arrayPos].tick == anchor.tick)
                {
                    bpms[arrayPos].anchor = anchor.anchorTime;
                }
                else
                {
                    // Create a new anchored bpm
                    uint value;
                    if (bpms[arrayPos].tick > anchor.tick)
                        value = bpms[arrayPos - 1].value;
                    else
                        value = bpms[arrayPos].value;

                    song.Add(new BPM(anchor.tick, value, anchor.anchorTime));
                }
            }
        }

        /*************************************************************************************
            Chart Loading
        **************************************************************************************/

        private static int FastStringToIntParse(string str, int index, int length)
        {
            // https://cc.davelozinski.com/c-sharp/fastest-way-to-convert-a-string-to-an-int
            int value = 0;
            for (int i = index; i < index + length; i++)
                value = value * 10 + (str[i] - '0');

            return value;
        }

        private static ulong FastStringToUInt64Parse(string str, int index, int length)
        {
            // https://cc.davelozinski.com/c-sharp/fastest-way-to-convert-a-string-to-an-int
            ulong value = 0;
            for (int i = index; i < index + length; i++)
                value = value * 10 + (ulong)(str[i] - '0');

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdvanceNextWord(string line, ref int startIndex, ref int length)
        {
            length = 0;
            while (startIndex < line.Length && line[startIndex] == ' ') { ++startIndex; };
            while ((startIndex + ++length) < line.Length && line[startIndex + length] != ' ') ;
        }

        private static void LoadChart(ParseSettings settings, MoonSong song, IList<string> data,
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

            chart.SetCapacity(data.Count);

            var noteProcessDict = GetNoteProcessDict(gameMode);
            var specialPhraseProcessDict = GetSpecialPhraseProcessDict(gameMode);

            try
            {
                // Load notes, collect flags
                foreach (string line in data)
                {
                    try
                    {
                        int stringStartIndex = 0;
                        int stringLength = 0;

                        // Advance to tick
                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                        uint tick = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                        // Advance to equality
                        stringStartIndex += stringLength;
                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);

                        // Advance to type
                        stringStartIndex += stringLength;
                        AdvanceNextWord(line, ref stringStartIndex, ref stringLength);

                        switch (line[stringStartIndex])    // Note this will need to be changed if keys are ever greater than 1 character long
                        {
                            case 'N':
                            case 'n':
                            {
                                // Advance to note number
                                stringStartIndex += stringLength;
                                AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                int fret_type = FastStringToIntParse(line, stringStartIndex, stringLength);

                                // Advance to note length
                                stringStartIndex += stringLength;
                                AdvanceNextWord(line, ref stringStartIndex, ref stringLength);
                                uint length = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                                if (noteProcessDict.TryGetValue(fret_type, out var processFn))
                                {
                                    var noteEvent = new NoteEvent() { tick = tick, noteNumber = fret_type, length = length };
                                    processParams.noteEvent = noteEvent;
                                    processFn(processParams);
                                }
                                break;
                            }

                            case 'S':
                            case 's':
                            {
                                // Advance to note number
                                stringStartIndex += stringLength;
                                AdvanceNextWord(line, ref stringStartIndex, ref stringLength);

                                int fret_type = FastStringToIntParse(line, stringStartIndex, stringLength);

                                // Advance to note length
                                stringStartIndex += stringLength;
                                AdvanceNextWord(line, ref stringStartIndex, ref stringLength);

                                uint length = (uint)FastStringToIntParse(line, stringStartIndex, stringLength);

                                if (specialPhraseProcessDict.TryGetValue(fret_type, out var processFn))
                                {
                                    var noteEvent = new NoteEvent() { tick = tick, noteNumber = fret_type, length = length };
                                    processParams.noteEvent = noteEvent;
                                    processFn(processParams);
                                }

                                break;
                            }
                            case 'E':
                            case 'e':
                            {
                                // Advance to event
                                stringStartIndex += stringLength;
                                AdvanceNextWord(line, ref stringStartIndex, ref stringLength);

                                string eventName = line.Substring(stringStartIndex, stringLength);
                                // Strip off brackets and any garbage outside of them
                                var match = ChartIOHelper.TextEventRegex.Match(eventName);
                                if (match.Success)
                                {
                                    eventName = match.Groups[1].Value;
                                }

                                chart.Add(new ChartEvent(tick, eventName), false);
                                break;
                            }
                            default:
                                break;
                        }

                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error parsing .chart line '{line}': {e}");
                    }
                }
                chart.UpdateCache();

                foreach (var fn in postNotesAddedProcessList)
                {
                    fn(processParams);
                }
            }
            catch (Exception e)
            {
                // Bad load, most likely a parsing error
                Debug.WriteLine($"Error parsing chart reader chart data: {e}");
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
            chart.Add(newMoonNote, false);
        }

        private static void ProcessNoteOnEventAsSpecialPhrase(in NoteProcessParams noteProcessParams, SpecialPhrase.Type type)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = noteEvent.length;

            var newPhrase = new SpecialPhrase(tick, sus, type);
            chart.Add(newPhrase, false);
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
                Debug.WriteLine($"Could not apply flag {flagData.flagToAdd} to a note. It was blocked by existing flag {flagData.blockingFlag}.");
            }
        }
    }
}

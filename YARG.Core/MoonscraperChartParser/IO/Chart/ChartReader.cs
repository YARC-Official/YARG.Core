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

namespace MoonscraperChartEditor.Song.IO
{
    public static class ChartReader
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
            public NoteEvent noteEvent;
            public List<NoteEventProcessFn> postNotesAddedProcessList;
        }

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

        public static MoonSong ReadChart(string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                    throw new Exception("File does not exist");

                string extension = Path.GetExtension(filepath);

                if (extension != ".chart")
                    throw new Exception("Bad file type");

                var reader = File.OpenText(filepath);
                return ReadChart(reader);
            }
            catch (Exception e)
            {
                throw new Exception("Could not open file!", e);
            }
        }

        public static MoonSong ReadChart(TextReader reader)
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
                    SubmitChartData(song, dataName, dataStrings);

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
                        SubmitChartData(song, dataName, dataStrings);

                        dataName = string.Empty;
                        dataStrings.Clear();
                    }
                }
            }

            reader.Close();
            song.UpdateCache();
            return song;
        }

        private static void SubmitChartData(MoonSong song, string dataName, List<string> stringData)
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

                            LoadChart(song, stringData, instrument, chartDiff);

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
            const int TEXT_POS_TICK = 0;
            const int TEXT_POS_EVENT_TYPE = 2;
            const int TEXT_POS_DATA_1 = 3;

            var anchorData = new List<Anchor>();

            foreach (string line in stringData)
            {
                string[] stringSplit = line.Split(' ');
                string eventType;
                if (stringSplit.Length > TEXT_POS_DATA_1 && uint.TryParse(stringSplit[TEXT_POS_TICK], out uint tick))
                {
                    eventType = stringSplit[TEXT_POS_EVENT_TYPE];
                    eventType = eventType.ToLower();
                }
                else
                {
                    continue;
                }

                switch (eventType)
                {
                    case "ts":
                        uint numerator;
                        uint denominator = 2;

                        if (!uint.TryParse(stringSplit[TEXT_POS_DATA_1], out numerator))
                            continue;

                        if (stringSplit.Length > TEXT_POS_DATA_1 + 1 && !uint.TryParse(stringSplit[TEXT_POS_DATA_1 + 1], out denominator))
                            continue;

                        song.Add(new TimeSignature(tick, numerator, (uint)Math.Pow(2, denominator)), false);
                        break;

                    case "b":
                        uint value;
                        if (!uint.TryParse(stringSplit[TEXT_POS_DATA_1], out value))
                            continue;

                        song.Add(new BPM(tick, value), false);
                        break;

                    case "e":
                        var sb = new StringBuilder();
                        int startIndex = TEXT_POS_DATA_1;
                        bool isSection = false;

                        if (stringSplit.Length > TEXT_POS_DATA_1 + 1 && stringSplit[TEXT_POS_DATA_1] == "\"section")
                        {
                            startIndex = TEXT_POS_DATA_1 + 1;
                            isSection = true;
                        }

                        for (int i = startIndex; i < stringSplit.Length; ++i)
                        {
                            sb.Append(stringSplit[i].Trim('"'));
                            if (i < stringSplit.Length - 1)
                                sb.Append(" ");
                        }

                        if (isSection)
                        {
                            song.Add(new Section(sb.ToString(), tick), false);
                        }
                        else
                        {
                            song.Add(new Event(sb.ToString(), tick), false);
                        }

                        break;

                    case "a":
                        ulong anchorValue;
                        if (ulong.TryParse(stringSplit[TEXT_POS_DATA_1], out anchorValue))
                        {
                            Anchor a;
                            a.tick = tick;
                            a.anchorTime = (float)(anchorValue / 1000000.0d);
                            anchorData.Add(a);
                        }
                        break;

                    default:
                        break;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdvanceNextWord(string line, ref int startIndex, ref int length)
        {
            length = 0;
            while (startIndex < line.Length && line[startIndex] == ' ') { ++startIndex; };
            while ((startIndex + ++length) < line.Length && line[startIndex + length] != ' ') ;
        }

        private static void LoadChart(MoonSong song, IList<string> data, MoonSong.MoonInstrument instrument,
            MoonSong.Difficulty difficulty)
        {
            var chart = song.GetChart(instrument, difficulty);
            var gameMode = chart.gameMode;

            var flags = new List<NoteFlag>();
            var postNotesAddedProcessList = new List<NoteEventProcessFn>();

            var processParams = new NoteProcessParams()
            {
                chart = chart,
                postNotesAddedProcessList = postNotesAddedProcessList
            };

            chart.SetCapacity(data.Count);

            var noteProcessDict = GetNoteProcessDict(gameMode);

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

                                switch (fret_type)
                                {
                                    case ChartIOHelper.PHRASE_VERSUS_PLAYER_1:
                                        chart.Add(new SpecialPhrase(tick, length, SpecialPhrase.Type.Versus_Player1), false);
                                        break;

                                    case ChartIOHelper.PHRASE_VERSUS_PLAYER_2:
                                        chart.Add(new SpecialPhrase(tick, length, SpecialPhrase.Type.Versus_Player2), false);
                                        break;

                                    case ChartIOHelper.PHRASE_STARPOWER:
                                        chart.Add(new SpecialPhrase(tick, length, SpecialPhrase.Type.Starpower), false);
                                        break;

                                    case ChartIOHelper.PHRASE_DRUM_FILL:
                                        if (gameMode == MoonChart.GameMode.Drums)
                                            chart.Add(new SpecialPhrase(tick, length, SpecialPhrase.Type.ProDrums_Activation), false);
                                        else
                                            Debug.Assert(false, "Found drum fill flag on incompatible instrument.");
                                        break;

                                    case ChartIOHelper.PHRASE_TREMOLO_LANE:
                                        if (gameMode == MoonChart.GameMode.Drums)
                                            chart.Add(new SpecialPhrase(tick, length, SpecialPhrase.Type.TremoloLane), false);
                                        else
                                            Debug.Assert(false, "Found standard drum roll flag on incompatible instrument.");
                                        break;

                                    case ChartIOHelper.PHRASE_TRILL_LANE:
                                        if (gameMode == MoonChart.GameMode.Drums)
                                            chart.Add(new SpecialPhrase(tick, length, SpecialPhrase.Type.TrillLane), false);
                                        else
                                            Debug.Assert(false, "Found special drum roll flag on incompatible instrument.");
                                        break;

                                    default:
                                        continue;
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

        private static void ProcessNoteOnEventAsNote(in NoteProcessParams noteProcessParams, int ingameFret, MoonNote.Flags defaultFlags = MoonNote.Flags.None)
        {
            var chart = noteProcessParams.chart;

            var noteEvent = noteProcessParams.noteEvent;
            uint tick = noteEvent.tick;
            uint sus = noteEvent.length;

            var newMoonNote = new MoonNote(tick, ingameFret, sus, defaultFlags);
            chart.Add(newMoonNote, false);
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

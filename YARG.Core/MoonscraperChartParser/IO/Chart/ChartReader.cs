// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

// Chart file format specifications- https://docs.google.com/document/d/1v2v0U-9HQ5qHeccpExDOLJ5CMPZZ3QytPmAG5WF0Kzs/edit?usp=sharing

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;
using YARG.Core.IO.Ini;
using YARG.Core.Logging;
using YARG.Core.Parsing;
using YARG.Core.Utility;

namespace MoonscraperChartEditor.Song.IO
{
    internal static partial class ChartReader
    {
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
        private static ReadOnlySpan<char> GetNextWord(this ReadOnlySpan<char> buffer, out ReadOnlySpan<char> remaining)
            => buffer.SplitOnceTrimmed(' ', out remaining);

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
            var fileInfo = new FileInfo(filepath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("The given file path does not exist", filepath);
            if (fileInfo.Extension != ".chart")
                throw new InvalidOperationException($"Not a .chart file: {filepath}");

            using var bytes = MemoryMappedArray.Load(fileInfo);
            if (YARGTextReader.IsUTF8(bytes, out var byteContainer))
                return ReadFromText(ref settings, ref byteContainer);

            using var chars = YARGTextReader.ConvertToUTF16(bytes, out var charContainer);
            if (chars != null)
                return ReadFromText(ref settings, ref charContainer);

            using var ints = YARGTextReader.ConvertToUTF32(bytes, out var intContainer);
            return ReadFromText(ref settings, ref intContainer);
        }

        public static unsafe MoonSong ReadFromText(ref ParseSettings settings, ReadOnlySpan<char> chartText)
        {
            fixed (char* ptr = chartText)
            {
                var container = new YARGTextContainer<char>(ptr, ptr + chartText.Length, Encoding.Unicode);
                return ReadFromText(ref settings, ref container);
            }
        }

        public static MoonSong ReadFromText<TChar>(ref ParseSettings settings, ref YARGTextContainer<TChar> chartText)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            static void ExpectSection(ref YARGTextContainer<TChar> chartText, string section, int sectionIndex)
            {
                if (!YARGChartFileReader.ValidateTrack(ref chartText, section))
                    throw new InvalidDataException($"Invalid section ordering! Expected [{section}] to be section {sectionIndex + 1}");
            }

            ExpectSection(ref chartText, YARGChartFileReader.HEADERTRACK, sectionIndex: 0);
            var song = ReadMetadataSection(ref chartText, ref settings);

            ExpectSection(ref chartText, YARGChartFileReader.SYNCTRACK, sectionIndex: 1);
            ReadSyncSection(ref chartText, song);

            while (YARGChartFileReader.IsStartOfTrack(chartText))
            {
                if (YARGChartFileReader.ValidateTrack(ref chartText, YARGChartFileReader.EVENTTRACK))
                {
                    ReadEventsSection(ref chartText, song);
                }
                else if (YARGChartFileReader.ValidateInstrument(ref chartText, out var instrument, out var difficulty))
                {
                    ReadInstrumentSection(ref chartText, song, ref settings,
                        instrument.ToMoonInstrument(), difficulty.ToMoonDifficulty());
                }
            }

            return song;
        }

        private static MoonSong ReadMetadataSection<TChar>(ref YARGTextContainer<TChar> chartText,
            ref ParseSettings settings)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            YargLogger.LogTrace("Loading .chart [Song] section");

            var metadata = YARGChartFileReader.ExtractChartModifiers(ref chartText);

            // Resolution = 192
            if (!metadata.TryGet("Resolution", out uint resolution))
                throw new InvalidDataException("Could not read .chart resolution!");
            if (resolution < 1)
                throw new InvalidDataException($"Invalid .chart resolution {resolution}! Must be non-zero and non-negative");

            var song = new MoonSong(resolution);
            ValidateAndApplySettings(song, ref settings);
            return song;
        }

        private static void ValidateAndApplySettings(MoonSong song, ref ParseSettings settings)
        {
            // Apply HOPO threshold settings
            song.hopoThreshold = ChartIOHelper.GetHopoThreshold(in settings, song.resolution);

            // Sustain cutoff threshold is not verified, sustains are not cut off by default in .chart
            // SP note is not verified, as it is only relevant for .mid
            // Note snap threshold is not verified, as .chart doesn't use note snapping

            // Chord HOPO cancellation does not apply in .chart
            settings.ChordHopoCancellation = false;
        }

        private static void ReadSyncSection<TChar>(ref YARGTextContainer<TChar> chartText, MoonSong song)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            YargLogger.LogTrace("Loading .chart [SyncTrack] section");

            // This is valid since we are guaranteed to have at least one tempo event at all times
            var tempoTracker = new ChartEventTickTracker<TempoChange>(song.syncTrack.Tempos);

            uint prevTick = 0;
            var chartEvent = new DotChartEvent();
            while (YARGChartFileReader.TryParseEvent(ref chartText, ref chartEvent))
            {
                try
                {
                    uint tick = (uint) chartEvent.Position;
                    if (prevTick > tick)
                        throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    tempoTracker.Update(tick);

                    switch (chartEvent.Type)
                    {
                        case ChartEventType.Bpm:
                        {
                            ulong tempo = YARGTextReader.ExtractUInt64AndWhitespace(ref chartText);
                            song.Add(new TempoChange(tempo / 1000f, song.TickToTime(tick, tempoTracker.Current!), tick));
                            break;
                        }

                        case ChartEventType.Time_Sig:
                        {
                            uint numerator = YARGTextReader.ExtractUInt32AndWhitespace(ref chartText);

                            // Denominator is optional, and defaults to 2 (becomes 4)
                            if (!YARGTextReader.TryExtractUInt32(ref chartText, out uint denominator))
                                denominator = 2;
                            else
                                YARGTextReader.SkipWhitespace(ref chartText);

                            // Denominator is stored as a power of 2, apply it here
                            denominator = (uint) Math.Pow(2, denominator);

                            song.Add(new TimeSignatureChange(numerator, (uint) Math.Pow(2, denominator),
                                song.TickToTime(tick, tempoTracker.Current!), tick));
                            break;
                        }

                        case ChartEventType.Anchor:
                        {
                            // Ignored for now, we don't need anchors
                            // ulong anchorTime = YARGTextReader.ExtractUInt64AndWhitespace(ref chartText);
                            // var anchor = new Anchor()
                            // {
                            //     tick = tick,
                            //     anchorTime = anchorTime / 1000000.0
                            // };
                            // anchorData.Add(anchor);
                            break;
                        }

                        default:
                            YargLogger.LogFormatWarning("Unhandled .chart event type {0} in [SyncTrack] section!", chartEvent.Type);
                            break;
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, "Error while parsing .chart [SyncTrack] section!");
                    throw;
                }
            }
        }

        private static void ReadEventsSection<TChar>(ref YARGTextContainer<TChar> chartText, MoonSong song)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            YargLogger.LogTrace("Loading .chart [Events] section");

            uint prevTick = 0;
            var chartEvent = new DotChartEvent();
            while (YARGChartFileReader.TryParseEvent(ref chartText, ref chartEvent))
            {
                try
                {
                    uint tick = (uint) chartEvent.Position;
                    if (prevTick > tick)
                        throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    // Get event type
                    switch (chartEvent.Type)
                    {
                        case ChartEventType.Text:
                        {
                            string eventText = YARGTextReader.ExtractText(ref chartText, isChartFile: true);
                            var normalizedText = TextEvents.NormalizeTextEvent(eventText);

                            // Put section events into their own list for ease of access
                            if (TextEvents.TryParseSectionEvent(normalizedText, out var sectionName))
                            {
                                song.sections.Add(new MoonText(sectionName.ToString(), tick));
                            }
                            else
                            {
                                song.events.Add(new MoonText(normalizedText.ToString(), tick));
                            }
                            break;
                        }

                        default:
                            YargLogger.LogFormatWarning("Unhandled .chart event type {0} in [Events] section!", chartEvent.Type);
                            break;
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, "Error while parsing .chart [Events] section!");
                    throw;
                }
            }
        }

        private static void ReadInstrumentSection<TChar>(ref YARGTextContainer<TChar> chartText, MoonSong song,
            ref ParseSettings settings, MoonSong.MoonInstrument instrument, MoonSong.Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            YargLogger.LogFormatTrace("Loading .chart section for {0} {1}", difficulty, instrument);

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

            uint prevTick = 0;
            var chartEvent = new DotChartEvent();
            while (YARGChartFileReader.TryParseEvent(ref chartText, ref chartEvent))
            {
                try
                {
                    uint tick = (uint) chartEvent.Position;
                    if (prevTick > tick)
                        throw new Exception("Tick value not in ascending order");
                    prevTick = tick;

                    switch (chartEvent.Type)
                    {
                        case ChartEventType.Note:
                        {
                            int noteType = YARGTextReader.ExtractInt32AndWhitespace(ref chartText);
                            uint noteLength = YARGTextReader.ExtractUInt32AndWhitespace(ref chartText);

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

                        case ChartEventType.Special:
                        {
                            int phraseType = YARGTextReader.ExtractInt32AndWhitespace(ref chartText);
                            uint phraseLength = YARGTextReader.ExtractUInt32AndWhitespace(ref chartText);

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
                        case ChartEventType.Text:
                        {
                            string eventText = YARGTextReader.ExtractText(ref chartText, isChartFile: true);
                            eventText = TextEvents.NormalizeTextEvent(eventText).ToString();
                            chart.events.Add(new MoonText(eventText, tick));
                            break;
                        }

                        default:
                            YargLogger.LogFormatWarning("Unhandled .chart event type {0} in section for {1} {2}!", chartEvent.Type, difficulty, instrument);
                            break;
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error while parsing .chart section for {difficulty} {instrument}!");
                    throw;
                }
            }

            foreach (var fn in postNotesAddedProcessList)
            {
                fn(ref processParams);
            }

            chart.notes.TrimExcess();
            settings = processParams.settings;
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
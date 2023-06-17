// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MoonscraperEngine;

namespace MoonscraperChartEditor.Song.IO
{
    using NoteEventQueue = List<(NoteEvent note, long tick)>;
    using SysExEventQueue = List<(PhaseShiftSysEx sysex, long tick)>;

    public static partial class MidReader
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
            public TimedMidiEvent timedEvent;
            public Dictionary<int, EventProcessFn> noteProcessMap;
            public Dictionary<string, ProcessModificationProcessFn> textProcessMap;
            public Dictionary<PhaseShiftSysEx.PhraseCode, EventProcessFn> sysexProcessMap;
            public List<EventProcessFn> delayedProcessesList;
        }

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
                        // Parse lyrics to global track, and then parse as an instrument
                        ReadTextEventsIntoGlobalEventsAsLyrics(track, song);
                        goto default;

                    default:
                        MoonSong.MoonInstrument instrument;
                        if (!MidIOHelper.TrackNameToInstrumentMap.TryGetValue(trackNameKey, out instrument))
                        {
                            // Ignore unrecognized tracks
                            continue;
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
                song.Add(new TimeSignature((uint)timesig.Time, (uint)timesig.Value.Numerator, (uint)timesig.Value.Denominator), false);
            }

            song.UpdateCache();
        }

        private static void ReadSongGlobalEvents(TrackChunk track, MoonSong song)
        {
            if (track.Events.Count < 1)
                return;

            long absoluteTime = track.Events[0].DeltaTime;
            for (int i = 1; i < track.Events.Count; i++)
            {
                var trackEvent = track.Events[i];
                absoluteTime += trackEvent.DeltaTime;

                if (trackEvent is BaseTextEvent text)
                {
                    string eventText = text.Text;
                    // Strip off brackets and any garbage outside of them
                    var bracketMatch = MidIOHelper.TextEventRegex.Match(eventText);
                    if (bracketMatch.Success)
                    {
                        eventText = bracketMatch.Groups[1].Value;
                    }

                    // Check for section events
                    var sectionMatch = MidIOHelper.SectionEventRegex.Match(eventText);
                    if (sectionMatch.Success)
                    {
                        // This is a section, use the text grouped by the regex
                        string sectionText = sectionMatch.Groups[1].Value;
                        song.Add(new Section(sectionText, (uint)absoluteTime), false);
                        continue;
                    }

                    // Add the event as-is
                    song.Add(new Event(eventText, (uint)absoluteTime), false);
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

                if (trackEvent is BaseTextEvent text && !text.Text.Contains('['))
                {
                    string lyricEvent = MidIOHelper.LYRIC_EVENT_PREFIX + text.Text;
                    song.Add(new Event(lyricEvent, (uint)absoluteTime), false);
                }
                else if (trackEvent is NoteEvent note && (byte)note.NoteNumber is MidIOHelper.LYRICS_PHRASE_1 or MidIOHelper.LYRICS_PHRASE_2)
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

            var gameMode = MoonSong.InstumentToChartGameMode(instrument);

            var processParams = new EventProcessParams()
            {
                song = song,
                instrument = instrument,
                noteProcessMap = GetNoteProcessDict(gameMode),
                textProcessMap = GetTextEventProcessDict(gameMode),
                sysexProcessMap = GetSysExEventProcessDict(gameMode),
                delayedProcessesList = GetInitialPostProcessList(gameMode),
            };

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

            // Update chart caches
            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                song.GetChart(instrument, diff).UpdateCache();
            }

            // Apply forcing events
            foreach (var process in processParams.delayedProcessesList)
            {
                process(processParams);
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
            // Strip off brackets and any garbage outside of them
            var bracketMatch = MidIOHelper.TextEventRegex.Match(eventName);
            if (bracketMatch.Success)
            {
                eventName = bracketMatch.Groups[1].Value;
            }

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
                    var chartEvent = new ChartEvent(tick, eventName);
                    processParams.song.GetChart(processParams.instrument, difficulty).Add(chartEvent);
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

        private static void ProcessNoteOnEventAsNote(in EventProcessParams eventProcessParams, MoonSong.Difficulty diff, int ingameFret, MoonNote.Flags defaultFlags = MoonNote.Flags.None)
        {
            var chart = eventProcessParams.song.GetChart(eventProcessParams.instrument, diff);

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

            var chart = song.GetChart(instrument, difficulty);

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

        private static void ProcessNoteOnEventAsEvent(EventProcessParams eventProcessParams, string eventText, int tickOffset = 0)
        {
            var song = eventProcessParams.song;
            var instrument = eventProcessParams.instrument;

            var timedEvent = eventProcessParams.timedEvent;
            uint tick = (uint)timedEvent.startTick;

            if (tick >= tickOffset)
                tick += (uint)tickOffset;

            foreach (var diff in EnumX<MoonSong.Difficulty>.Values)
            {
                var moonChart = song.GetChart(instrument, diff);
                moonChart.Add(new ChartEvent(tick, eventText));
            }
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

            var chart = song.GetChart(instrument, difficulty);

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

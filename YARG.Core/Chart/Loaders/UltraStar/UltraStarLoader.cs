using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Chart.Loaders.UltraStar
{
    internal partial class UltraStarLoader : ISongLoader
    {
        #region Constants

        // MIDI base constant
        // UltraStar pitch is RELATIVE
        // MIDI 60 = C4
        private const int ULTRASTAR_PITCH_BASE = 60;

        #endregion

        #region Fields

        // Maximum number of independent voices supported. Matches YARG's harmony
        // vocals model (HARM1-3, see VocalNote.HarmonyPart) — a P4+ marker has no
        // slot to route into.
        private const int MAX_VOICE_PARTS = 3;

        private readonly Dictionary<string, string> _metadata     = new(StringComparer.OrdinalIgnoreCase);
        private          uint                       _ticksPerBeat = 120;
        private          double                     _bpm          = 120.0;
        private          double                     _gapMs        = 0.0;

        private List<TextEvent>? _globalEvents;
        private List<Section>? _sections;
        private SyncTrack? _syncTrack;
        private VenueTrack? _venueTrack;
        private LyricsTrack? _lyricsTrack;

        // (Beat, BPM) mid-song tempo changes from "B <beat> <bpm>" lines, sorted by beat once parsing completes.
        private readonly List<(uint Beat, double Bpm)> _tempoChanges = new();

        private readonly Dictionary<int, List<UltraStarNote>> _partNotes = new();
        private readonly HashSet<int> _seenParts = new();
        private int _currentPart = 0;

        #endregion

        #region UltraStarNote

        private class UltraStarNote
        {
            public int    PartIndex     { get; set; } = 0;
            public char   Type          { get; set; }
            public uint   StartBeat     { get; set; }
            public uint   DurationBeats { get; set; }
            public int    Pitch         { get; set; }
            public string Lyric         { get; set; } = string.Empty;

            /// <summary>
            /// When true, a hyphen ('-') should be appended to this note's lyric
            /// and JoinWithNext flag set on its LyricEvent. Set when the NEXT note
            /// in the phrase has a '~' (melisma continuation) prefix.
            /// </summary>
            public bool MelismaJoin { get; set; }

            public bool IsGolden    => Type == '*' || Type == 'G';
            public bool IsUnpitched => Type == 'F' || Type == 'R' || Type == 'G';
            public bool IsRest      => Type == '-';
        }

        #endregion

        public UltraStarLoader(FixedArray<byte> file)
        {
            ParseUltraStarFile(file);
        }

        public string? GetMetadata(string key)
            => _metadata.TryGetValue(key, out var v) ? v : null;

        #region Parsing

        private void ParseUltraStarFile(FixedArray<byte> file)
        {
            unsafe
            {
                using var stream = new UnmanagedMemoryStream(file.Ptr, file.Length);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    if (line[0] == '#') { ParseMetadataLine(line); continue; }

                    if (line.Length == 2 && line[0] == 'P' && char.IsDigit(line[1]))
                    {
                        ParseVoiceMarker(line[1] - '0');
                        continue;
                    }

                    if (line == "E")
                    {
                        break;
                    }

                    if (line[0] == 'B')
                    {
                        ParseTempoChangeLine(line);
                        continue;
                    }

                    if (line[0] is ':' or '*' or 'F' or '-' or 'R' or 'G')
                    {
                        ParseNoteLine(line);
                    }
                }
            }

            _tempoChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        }

        /// <summary>
        /// Handles voice-change markers. Per spec (§4.3), each is an
        /// independent voice, not a "both singers" combination marker. n is capped
        /// to match YARG's harmony vocals model; markers beyond that are logged and ignored.
        /// </summary>
        private void ParseVoiceMarker(int voiceNumber)
        {
            if (voiceNumber < 1 || voiceNumber > MAX_VOICE_PARTS)
            {
                YargLogger.LogFormatWarning("[UltraStar] Voice marker P{0} exceeds the {1} supported harmony parts — ignoring", voiceNumber, MAX_VOICE_PARTS);
                return;
            }

            _currentPart = voiceNumber - 1;
            GetOrCreatePart(_currentPart);
            _seenParts.Add(voiceNumber);
            _metadata["PARTS"] = _seenParts.Count.ToString();
        }

        private void ParseTempoChangeLine(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !uint.TryParse(parts[1], out uint beat))
            {
                return;
            }

            string norm = parts[2].Replace(',', '.');
            if (double.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm) && bpm > 0)
            {
                _tempoChanges.Add((beat, bpm));
            }
        }

        private void ParseMetadataLine(string line)
        {
            int colon = line.IndexOf(':');
            if (colon <= 1 || colon >= line.Length - 1)
            {
                return;
            }

            string key = line[1..colon].Trim();
            string value = line[(colon + 1)..].Trim().TrimEnd(',');
            _metadata[key] = value;

            if (key.Equals("BPM", StringComparison.OrdinalIgnoreCase))
            {
                string norm = value.Replace(',', '.');
                if (double.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm) && bpm > 0)
                {
                    _bpm = bpm;
                }
            }
            else if (key.Equals("GAP", StringComparison.OrdinalIgnoreCase))
            {
                string norm = value.Replace(',', '.');
                if (double.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out double gap))
                {
                    _gapMs = gap;
                }
            }
        }

        private void ParseNoteLine(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1)
            {
                return;
            }

            char noteType = parts[0][0];

            if (noteType == '-')
            {
                if (parts.Length >= 2 && uint.TryParse(parts[1], out uint restBeat))
                {
                    GetOrCreatePart(_currentPart).Add(new UltraStarNote
                    {
                        PartIndex = _currentPart,
                        Type = '-',
                        StartBeat = restBeat,
                        DurationBeats = 0,
                        Pitch = 0,
                        Lyric = string.Empty
                    });
                }
                return;
            }

            if (parts.Length < 4)
            {
                return;
            }

            if (!uint.TryParse(parts[1], out uint startBeat) ||
                !uint.TryParse(parts[2], out uint duration) ||
                !int.TryParse(parts[3], out int pitch))
            {
                return;
            }

            string lyric = parts.Length > 4 ? string.Join(" ", parts.Skip(4)) : string.Empty;

            if (lyric.StartsWith("~"))
            {
                lyric = lyric.Substring(1);
                bool hasText = lyric.Length > 0;
                if (hasText)
                    lyric += "+";
                else
                    lyric = "+";

                // Only mark the previous note with MelismaJoin when the '~'
                // carries actual text (e.g. ~ght.). A bare '~' is a silent
                // continuation hold and should NOT add a hyphen to the
                // previous note's lyric.
                if (hasText)
                {
                    var partNotes = GetOrCreatePart(_currentPart);
                    for (int i = partNotes.Count - 1; i >= 0; i--)
                    {
                        if (!partNotes[i].IsRest)
                        {
                            partNotes[i].MelismaJoin = true;
                            break;
                        }
                    }
                }
            }

            GetOrCreatePart(_currentPart).Add(new UltraStarNote
            {
                PartIndex = _currentPart,
                Type = noteType,
                StartBeat = startBeat,
                DurationBeats = duration,
                Pitch = pitch,
                Lyric = lyric
            });
        }

        /// <summary>
        /// Gets the note list for a voice part, creating it on first use. Only
        /// parts actually referenced by a note or voice-change marker exist as
        /// dictionary entries.
        /// </summary>
        private List<UltraStarNote> GetOrCreatePart(int index)
        {
            if (!_partNotes.TryGetValue(index, out var list))
            {
                list = new List<UltraStarNote>();
                _partNotes[index] = list;
            }
            return list;
        }

        private List<UltraStarNote> GetPart(int index)
            => _partNotes.TryGetValue(index, out var list) ? list : new List<UltraStarNote>();

        #endregion

        #region Beat Conversion

        // Ticks are a pure subdivision of beat position and don't depend on BPM,
        // so mid-song tempo changes don't affect this conversion.
        private uint BeatToTick(uint beat)
        {
            uint ticksPerUSBeat = _ticksPerBeat / 8;
            uint gapTicks = (uint) (_gapMs / 1000.0 * _bpm);
            return gapTicks + (beat * ticksPerUSBeat);
        }

        // Walks the tempo-change segments (sorted by beat) accumulating elapsed
        // time per segment, since each segment's beat-to-time rate differs.
        private double BeatToTime(uint beat)
        {
            double time = 0.0;
            double currentBpm = _bpm;
            uint currentBeat = 0;

            foreach (var (changeBeat, changeBpm) in _tempoChanges)
            {
                if (beat <= changeBeat)
                {
                    break;
                }

                time += (changeBeat - currentBeat) * 60.0 / currentBpm;
                currentBeat = changeBeat;
                currentBpm = changeBpm;
            }

            time += (beat - currentBeat) * 60.0 / currentBpm;
            return time;
        }

        #endregion

        #region Loading

        /// <summary>
        /// UltraStar format doesn't use global events
        /// but this is required by the ISongLoader interface.
        /// </summary>
        public List<TextEvent> LoadGlobalEvents() => _globalEvents ??= new();
        public List<Section> LoadSections() => _sections ??= new();
        public VenueTrack LoadVenueTrack() => _venueTrack ??= new VenueTrack();

        public InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument i, InstrumentTrack<EliteDrumNote>? e) => throw new NotSupportedException();
        public InstrumentTrack<EliteDrumNote> LoadEliteDrumsTrack(Instrument i) => throw new NotSupportedException();

        public SyncTrack LoadSyncTrack()
        {
            if (_syncTrack != null)
            {
                return _syncTrack;
            }

            // UltraStar BPM is typically 2x the real musical BPM.
            // Halve it for the SyncTrack so beatlines and crowd clapping
            // fire at the correct rate. Note timing via BeatToTime/BeatToTick
            // still uses the original _bpm and remains correct because
            // UltraStar beat positions are also in "double time".
            double gapSeconds = _gapMs / 1000.0;
            var tempos = new List<TempoChange> { new(_bpm / 2.0, -gapSeconds, 0u) };

            // Ticks/time are normalized relative to beat 0 (which BeatToTick/BeatToTime
            // place at gapTicks/0s respectively) to match the initial entry above.
            uint tickAtBeatZero = BeatToTick(0);
            foreach (var (beat, bpm) in _tempoChanges)
            {
                uint tick = BeatToTick(beat) - tickAtBeatZero;
                double time = BeatToTime(beat) - gapSeconds;
                tempos.Add(new TempoChange(bpm / 2.0, time, tick));
            }

            _syncTrack = new SyncTrack(120,
                tempos,
                new List<TimeSignatureChange> { new(4, 4, -gapSeconds, 0u, 0u, 0u, 0u, 0.0) },
                new List<Beatline>());
            return _syncTrack;
        }

        public LyricsTrack LoadLyrics()
        {
            if (_lyricsTrack != null)
            {
                return _lyricsTrack;
            }

            var phrases = new List<LyricsPhrase>();
            var lyricSource = GetPart(0);

            foreach (var group in GroupNotesIntoPhrases(lyricSource))
            {
                if (group.Count == 0)
                {
                    continue;
                }

                uint startTick = BeatToTick(group[0].StartBeat);
                uint endTick = BeatToTick(group[^1].StartBeat + group[^1].DurationBeats);
                double startTime = BeatToTime(group[0].StartBeat);
                double endTime = BeatToTime(group[^1].StartBeat + group[^1].DurationBeats);

                var events = new List<LyricEvent>();
                foreach (var n in group)
                {
                    if (string.IsNullOrWhiteSpace(n.Lyric))
                    {
                        continue;
                    }

                    // Freestyle notes get "#" appended (like SingStar)
                    string lyric = n.IsUnpitched
                        ? FormatLyric(n.Lyric) + LyricSymbols.NONPITCHED_SYMBOL
                        : FormatLyric(n.Lyric);
                    var flags = n.IsUnpitched ? LyricSymbolFlags.NonPitched : LyricSymbolFlags.None;

                    // Melisma: append '-' and set JoinWithNext when next note has '~' prefix
                    if (n.MelismaJoin)
                    {
                        lyric += LyricSymbols.LYRIC_JOIN_SYMBOL;
                        flags |= LyricSymbolFlags.JoinWithNext;
                    }

                    events.Add(new LyricEvent(flags, lyric,
                        BeatToTime(n.StartBeat), BeatToTick(n.StartBeat)));
                }

                if (events.Count > 0)
                {
                    phrases.Add(new LyricsPhrase(startTime, endTime - startTime,
                        startTick, endTick - startTick, events));
                }
            }

            _lyricsTrack = new LyricsTrack(phrases);
            return _lyricsTrack;
        }

        public VocalsTrack LoadVocalsTrack(Instrument instrument)
        {
            if (instrument != Instrument.Vocals && instrument != Instrument.Harmony)
            {
                throw new ArgumentException("UltraStar only supports Vocals and HarmonyVocals.", nameof(instrument));
            }

            var parts = new List<VocalsPart>();

            if (instrument == Instrument.Vocals)
            {
                parts.Add(BuildVocalsPart(GetPart(0), false, 0));
            }
            else if (instrument == Instrument.Harmony)
            {
                // One VocalsPart per voice actually populated (P1..P3), in order.
                foreach (var partIndex in _partNotes.Keys.OrderBy(k => k))
                {
                    if (_partNotes[partIndex].Count == 0)
                    {
                        continue;
                    }
                    parts.Add(BuildVocalsPart(_partNotes[partIndex], true, partIndex));
                }

                if (parts.Count == 0)
                {
                    parts.Add(BuildVocalsPart(GetPart(0), true, 0));
                }
            }

            return new VocalsTrack(Instrument.Vocals, parts, new List<VocalsRangeShift>());
        }

        #endregion

        #region Vocals Processing

        private VocalsPart BuildVocalsPart(List<UltraStarNote> notes, bool isHarmony, int partIndex)
        {
            var phrases = new List<VocalsPhrase>();
            var otherPhrases = new List<Phrase>();
            var textEvents = new List<TextEvent>();

            foreach (var group in GroupNotesIntoPhrases(notes))
            {
                if (group.Count == 0)
                {
                    continue;
                }

                var phrase = CreateVocalsPhrase(group, partIndex);
                if (phrase != null)
                {
                    phrases.Add(phrase);
                    // If phrase have SP, add to list
                    if (group.Any(n => n.IsGolden))
                    {
                        otherPhrases.Add(new Phrase(
                            PhraseType.StarPower,
                            phrase.Time,
                            phrase.TimeLength,
                            phrase.Tick,
                            phrase.TickLength));
                    }
                }
            }

            otherPhrases = otherPhrases.OrderBy(p => p.Tick).ToList();

            return new VocalsPart(isHarmony, phrases, new List<VocalsPhrase>(), new(), otherPhrases, textEvents);
        }

        private List<List<UltraStarNote>> GroupNotesIntoPhrases(List<UltraStarNote> notes)
        {
            // '-' is the main phrase separator in UltraStar.
            // Fallback threshold (32 beats) only for files without '-'.
            // There must be > the largest possible gap inside the phrase -
            const uint FALLBACK_GAP_THRESHOLD = 32;
            bool hasDashSeparators = notes.Any(n => n.IsRest);

            var groups = new List<List<UltraStarNote>>();
            var currentGroup = new List<UltraStarNote>();
            uint lastEndBeat = 0;

            foreach (var note in notes.OrderBy(n => n.StartBeat))
            {
                if (note.IsRest)
                {
                    if (currentGroup.Count > 0)
                    {
                        groups.Add(currentGroup);
                        currentGroup = new();
                    }

                    lastEndBeat = note.StartBeat + note.DurationBeats;
                    continue;
                }

                // Fallback when '-' not in file
                if (!hasDashSeparators &&
                    note.StartBeat > lastEndBeat + FALLBACK_GAP_THRESHOLD &&
                    currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                    currentGroup = new();
                }

                currentGroup.Add(note);
                lastEndBeat = note.StartBeat + note.DurationBeats;
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        private VocalsPhrase? CreateVocalsPhrase(List<UltraStarNote> phraseNotes, int partIndex)
        {
            if (phraseNotes.Count == 0)
            {
                return null;
            }

            uint phraseStartTick = BeatToTick(phraseNotes[0].StartBeat);
            uint phraseEndTick = BeatToTick(phraseNotes[^1].StartBeat + phraseNotes[^1].DurationBeats);
            uint phraseTickLen = phraseEndTick - phraseStartTick;
            double phraseStartTime = BeatToTime(phraseNotes[0].StartBeat);
            double phraseEndTime = BeatToTime(phraseNotes[^1].StartBeat + phraseNotes[^1].DurationBeats);
            double phraseTimeLen = phraseEndTime - phraseStartTime;

            var parentNote = new VocalNote(
                NoteFlags.None, false,
                phraseStartTime, phraseTimeLen,
                phraseStartTick, phraseTickLen);

            var lyrics = new List<LyricEvent>();
            int harmonyPart = Math.Clamp(partIndex, 0, MAX_VOICE_PARTS - 1);

            foreach (var uNote in phraseNotes)
            {
                uint ticksPerUsBeat = _ticksPerBeat / 8;
                uint noteTick = BeatToTick(uNote.StartBeat);
                uint noteTickLen = uNote.DurationBeats * ticksPerUsBeat;
                double noteTime = BeatToTime(uNote.StartBeat);
                // Computed via BeatToTime end-minus-start (not a flat beats*60/bpm)
                // so durations spanning a mid-song tempo change stay correct.
                double noteTimeLen = BeatToTime(uNote.StartBeat + uNote.DurationBeats) - noteTime;

                bool isUnpitched = uNote.IsUnpitched;

                // Pitch conversion: UltraStar relative → MIDI absolute
                // Freestyle/rap notes keep their real pitch (like SingStar)
                float midiPitch = ToMidiPitch(uNote.Pitch);

                var childNote = new VocalNote(
                    midiPitch,
                    harmonyPart,                   // harmonyPart: 0 = lead
                    VocalNoteType.Lyric,
                    noteTime,
                    noteTimeLen,
                    noteTick,
                    noteTickLen);

                parentNote.AddChildNote(childNote);

                if (!string.IsNullOrWhiteSpace(uNote.Lyric))
                {
                    // Freestyle notes get "#" appended (like SingStar)
                    string lyric = isUnpitched
                        ? FormatLyric(uNote.Lyric) + LyricSymbols.NONPITCHED_SYMBOL
                        : FormatLyric(uNote.Lyric);
                    var flags = isUnpitched ? LyricSymbolFlags.NonPitched : LyricSymbolFlags.None;

                    // Melisma: append '-' and set JoinWithNext when next note has '~' prefix
                    if (uNote.MelismaJoin)
                    {
                        lyric += LyricSymbols.LYRIC_JOIN_SYMBOL;
                        flags |= LyricSymbolFlags.JoinWithNext;
                    }

                    lyrics.Add(new LyricEvent(flags, lyric, noteTime, noteTimeLen, noteTick, noteTickLen));
                }
            }

            if (phraseNotes.Any(n => n.IsGolden))
            {
                parentNote.ActivateFlag(NoteFlags.StarPower);
            }

            if (parentNote.ChildNotes.Count == 0)
            {
                YargLogger.LogWarning($"[UltraStar] Phrase at tick {phraseStartTick} has 0 child notes — skipping");
                return null;
            }

            return new VocalsPhrase(
                phraseStartTime, phraseTimeLen,
                phraseStartTick, phraseTickLen,
                parentNote, lyrics);
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Clears lyric from UltraStar
        /// </summary>
        private static string FormatLyric(string raw)
        {
            return raw.Trim();
        }

        /// <summary>
        /// Converts UltraStar relative pitch to absolute MIDI pitch for YARG.
        /// Clamp to the range 0-127.
        /// </summary>
        private static int ToMidiPitch(int ultraStarPitch)
            => Math.Clamp(ultraStarPitch + ULTRASTAR_PITCH_BASE, 0, 127);

        public void DumpToLog()
        {
            int totalNotes = _partNotes.Values.Sum(list => list.Count);
            YargLogger.LogDebug($"[UltraStar] BPM={_bpm} GAP={_gapMs}ms TOTAL_NOTES={totalNotes}");

            foreach (var kvp in _partNotes.OrderBy(k => k.Key))
            {
                int partIndex = kvp.Key;
                var notes = kvp.Value;

                YargLogger.LogDebug($"[UltraStar] Part {partIndex + 1}: notes={notes.Count}");

                var groups = GroupNotesIntoPhrases(notes);
                YargLogger.LogDebug($"[UltraStar] Part {partIndex + 1}: phrase groups={groups.Count}");

                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var g = groups[gi];
                    YargLogger.LogDebug($"[UltraStar] Part {partIndex + 1} Phrase {gi}: {g.Count} notes, " +
                        $"beats {g[0].StartBeat}–{g[^1].StartBeat + g[^1].DurationBeats}, " +
                        $"time {BeatToTime(g[0].StartBeat):F3}s–{BeatToTime(g[^1].StartBeat + g[^1].DurationBeats):F3}s");

                    foreach (var n in g)
                    {
                        string midiText = n.IsRest || n.IsUnpitched ? "n/a" : ToMidiPitch(n.Pitch).ToString();

                        YargLogger.LogDebug($"[UltraStar]   P{partIndex + 1} {n.Type} beat={n.StartBeat} dur={n.DurationBeats} " +
                            $"pitch={n.Pitch}→midi={midiText} tick={BeatToTick(n.StartBeat)} " +
                            $"time={BeatToTime(n.StartBeat):F3}s lyric='{n.Lyric}'");
                    }
                }
            }
        }

        #endregion
    }
}
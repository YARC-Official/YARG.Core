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

        // UltraStar pitch is relative to C4 (MIDI 60).
        private const int ULTRASTAR_PITCH_BASE = 60;

        // Melisma/continuation marker on a syllable. Distinct from the rest marker '-',
        // which shares a character with LyricSymbols.LYRIC_JOIN_SYMBOL but is unrelated.
        private const char US_MELISMA_SYMBOL = '~';

        // An UltraStar beat is an eighth of the internal tick beat.
        private const uint US_BEATS_PER_TICK_BEAT = 8;

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
        private int _currentPart = 0;
        // Set by a trailing '~'; the next note consumes it as its pitch-slide marker.
        private bool _pendingPitchSlide = false;
        // Whether the most recently parsed note's raw lyric text ended with the format's
        // extra word-boundary space (or there is no previous note yet) -- if false, and the
        // next note also has no extra leading space, the two glue into one word (see
        // ParseNoteLine).
        private bool _previousHadTrailingSpace = true;

        #endregion

        #region UltraStarNote

        private class UltraStarNote
        {
            public char   Type          { get; set; }
            public uint   StartBeat     { get; set; }
            public uint   DurationBeats { get; set; }
            public int    Pitch         { get; set; }
            public string Lyric         { get; set; } = string.Empty;

            /// <summary>
            /// Glues this note's lyric directly onto the next one, with no space and no
            /// visible hyphen (LyricSymbolFlags.JoinWithNext). Set by a trailing '~', by a
            /// leading '~' with text on the following note, or by the absence of the
            /// format's word-boundary space (see ParseNoteLine's leading/trailing-space
            /// handling) on either side.
            /// </summary>
            public bool JoinWithNext { get; set; }

            /// <summary>
            /// True for a bare '~' continuation (pitched, no syllable text): it still emits
            /// a pitch-slide-only LyricEvent ("+", no letters), so a word-join must walk
            /// past it to reach the actual previous syllable (see MarkPreviousNoteJoinWithNext).
            /// </summary>
            public bool IsSilentHold { get; set; }

            public uint EndBeat => StartBeat + DurationBeats;

            // Type rules live here so adding a note type touches one place.
            public static bool IsNoteLineType(char type) => type is ':' or '*' or 'F' or '-' or 'R' or 'G';
            public static bool IsGoldenType(char type)   => type is '*' or 'G';
            public static bool IsRestType(char type)     => type == '-';

            // Freestyle (F), Rap (R), Golden Rap (G) carry no pitch requirement. All three
            // are treated as scored+unpitched; per spec Freestyle should be unscored, but
            // YARG has no zero-score vocal category (see VocalNote.IsNonPitched).
            public static bool IsUnpitchedType(char type) => type is 'F' or 'R' or 'G';

            public bool IsGolden    => IsGoldenType(Type);
            public bool IsUnpitched => IsUnpitchedType(Type);
            public bool IsRest      => IsRestType(Type);
        }

        #endregion

        public UltraStarLoader(FixedArray<byte> file)
        {
            ParseUltraStarFile(file);
        }

        public string? GetMetadata(string key)
            => _metadata.TryGetValue(key, out var v) ? v : null;

        /// <summary>Voices the note body actually uses; not the #PARTS tag's value.</summary>
        public int VoiceCount => _partNotes.Count;

        /// <summary>Parses a numeric tag. US files routinely use comma decimals.</summary>
        public static bool TryParseNumber(string? raw, out double value)
        {
            value = 0;
            return raw != null
                && double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

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
                    // Only left-trim for note lines: a trailing space is a legal, meaningful
                    // part of the note-text field signaling a word boundary.
                    // Trimming it here, before ParseNoteLine ever sees it, would destroy
                    // that signal. No other line kind has such trailing content.
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine))
                    {
                        continue;
                    }

                    if (trimmedLine[0] == '#') { ParseMetadataLine(trimmedLine); continue; }

                    // Voice markers appear both as "P1" and as "P 1"; the spaced form is
                    // the more common of the two in the wild. Rejecting it would silently
                    // merge every voice of those files into one part.
                    if (trimmedLine[0] == 'P')
                    {
                        string voice = trimmedLine[1..].TrimStart();
                        if (voice.Length == 1 && char.IsDigit(voice[0]))
                        {
                            ParseVoiceMarker(voice[0] - '0');
                            continue;
                        }
                    }

                    if (trimmedLine == "E")
                    {
                        break;
                    }

                    if (trimmedLine[0] == 'B')
                    {
                        ParseTempoChangeLine(trimmedLine);
                        continue;
                    }

                    if (UltraStarNote.IsNoteLineType(trimmedLine[0]))
                    {
                        ParseNoteLine(line.TrimStart());
                    }
                }
            }

            _tempoChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        }

        /// <summary>
        /// Per spec (§4.3) each P marker is an independent voice, not a "both singers" one.
        /// </summary>
        private void ParseVoiceMarker(int voiceNumber)
        {
            if (voiceNumber < 1 || voiceNumber > MAX_VOICE_PARTS)
            {
                YargLogger.LogFormatWarning("[UltraStar] Voice marker P{0} exceeds the {1} supported harmony parts — ignoring", voiceNumber, MAX_VOICE_PARTS);
                return;
            }

            _currentPart = voiceNumber - 1;
            // A trailing '~' or word-join shouldn't bleed into a different voice.
            _pendingPitchSlide = false;
            _previousHadTrailingSpace = true;
            GetOrCreatePart(_currentPart);
        }

        private void ParseTempoChangeLine(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !uint.TryParse(parts[1], out uint beat))
            {
                return;
            }

            if (TryParseNumber(parts[2], out double bpm) && bpm > 0)
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
                if (TryParseNumber(value, out double bpm) && bpm > 0)
                {
                    _bpm = bpm;
                }
            }
            else if (key.Equals("GAP", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseNumber(value, out double gap))
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

            if (UltraStarNote.IsRestType(noteType))
            {
                // A rest breaks the phrase; a pending trailing '~' or word-join from before it must not
                // bleed into whatever note follows (same rule as ParseVoiceMarker's reset).
                _pendingPitchSlide = false;
                _previousHadTrailingSpace = true;
                if (parts.Length >= 2 && uint.TryParse(parts[1], out uint restBeat))
                {
                    GetOrCreatePart(_currentPart).Add(new UltraStarNote
                    {
                        Type = noteType,
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

            // Locate the note-text field in the ORIGINAL line (not `parts`, which has
            // already collapsed every run of spaces) so its own leading/trailing whitespace
            // survives. The format uses an extra space on either side of the single
            // mandatory separator to signal a word boundary; its absence means the
            // syllable glues directly onto its neighbor with no separator at all.
            string rawText = ExtractRawNoteText(line);
            bool hasLeadingSpace = rawText.Length > 0 && rawText[0] == ' ';
            bool hasTrailingSpace = rawText.Length > 0 && rawText[^1] == ' ';
            string lyric = rawText.Trim();

            // '+' is YARG's pitch-slide marker but ordinary text in UltraStar. Left alone, a
            // trailing '+'' would be read downstream as a request to merge this note into the
            // previous one (LyricSymbols.GetLyricFlags), and StripForVocals would delete it
            // from the display either way -- so spell it out to keep the lyric intact.
            if (lyric.IndexOf(LyricSymbols.PITCH_SLIDE_SYMBOL) >= 0)
            {
                lyric = lyric.Replace(LyricSymbols.PITCH_SLIDE_SYMBOL.ToString(), "plus");
            }

            bool isUnpitched = UltraStarNote.IsUnpitchedType(noteType);
            bool joinWithNext = false;
            bool isSilentHold = false;

            // The format's extra space on either side of the mandatory separator marks a word
            // boundary (can be a trailing or leading space). A '~' means "this pitch continues",
            // which is only meaningful inside, a word, so no form of '~' may slide across this boundary.
            bool gluedToPrevious = !hasLeadingSpace && !_previousHadTrailingSpace;

            // Consume the previous note's trailing '~' before this note's own '~' handling
            // can set the flag again for the note after this one. Unpitched notes have no
            // pitch to slide into, so they take precedence over blending.
            bool pitchSlide = _pendingPitchSlide && !isUnpitched && gluedToPrevious;
            _pendingPitchSlide = false;

            if (lyric.Length > 0 && lyric[0] == US_MELISMA_SYMBOL)
            {
                lyric = lyric[1..];
                if (lyric.Length > 0)
                {
                    // A leading '~' blends this syllable back into the previous one; the
                    // word-boundary join itself is handled by the whitespace check below.
                    pitchSlide = gluedToPrevious;
                }
                else if (!isUnpitched)
                {
                    // A bare hold has no syllable of its own, so a space before it marks
                    // no word boundary and it always continues the note before it. Its
                    // forward blend is gated when the next note is parsed.
                    pitchSlide = true;
                    isSilentHold = true;
                    _pendingPitchSlide = true;
                }
            }
            else if (lyric.Length > 0 && lyric[^1] == US_MELISMA_SYMBOL)
            {
                // Trailing '~' ("n~" then "eed"): hyphenate here, but the pitch-slide goes
                // on the NEXT note -- MoonSongLoader.Vocals merges on the later note's flag.
                lyric = lyric[..^1];
                joinWithNext = true;
                _pendingPitchSlide = true;
            }

            // Glued to the previous syllable means no space and no hyphen between them.
            if (lyric.Length > 0 && gluedToPrevious)
            {
                MarkPreviousNoteJoinWithNext();
            }
            _previousHadTrailingSpace = hasTrailingSpace;

            if (pitchSlide)
            {
                lyric += LyricSymbols.PITCH_SLIDE_SYMBOL;
            }

            GetOrCreatePart(_currentPart).Add(new UltraStarNote
            {
                Type = noteType,
                StartBeat = startBeat,
                DurationBeats = duration,
                Pitch = pitch,
                Lyric = lyric,
                JoinWithNext = joinWithNext,
                IsSilentHold = isSilentHold
            });
        }

        /// <summary>
        /// Returns the note-text field exactly as written, past the single mandatory
        /// separator following the pitch column -- unlike `line.Split(' ', ...)`, this does
        /// not collapse the field's own meaningful leading/trailing whitespace.
        /// </summary>
        private static string ExtractRawNoteText(string line)
        {
            int idx = 0;
            for (int token = 0; token < 4; token++)
            {
                while (idx < line.Length && line[idx] == ' ') idx++;
                while (idx < line.Length && line[idx] != ' ') idx++;
            }
            if (idx < line.Length && line[idx] == ' ')
            {
                idx++;
            }
            return idx < line.Length ? line[idx..] : string.Empty;
        }

        private void MarkPreviousNoteJoinWithNext()
        {
            // Walk past a bare '~' hold (no real syllable, so a join can't attach there) to
            // find the actual previous syllable; a rest still breaks the phrase outright and
            // stops the search -- it must never be joined across.
            var partNotes = GetOrCreatePart(_currentPart);
            for (int i = partNotes.Count - 1; i >= 0; i--)
            {
                if (partNotes[i].IsRest)
                {
                    return;
                }
                if (partNotes[i].IsSilentHold)
                {
                    continue;
                }
                partNotes[i].JoinWithNext = true;
                return;
            }
        }

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

        private uint TicksPerUltraStarBeat => _ticksPerBeat / US_BEATS_PER_TICK_BEAT;

        // Ticks are a pure subdivision of beat position and don't depend on BPM,
        // so mid-song tempo changes don't affect this conversion.
        private uint BeatToTick(uint beat)
        {
            uint gapTicks = (uint) (_gapMs / 1000.0 * _bpm);
            return gapTicks + (beat * TicksPerUltraStarBeat);
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

        // ISongLoader requires these; the UltraStar path uses none of them.
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

            // UltraStar BPM is typically 2x the real musical BPM; halve it here so beatlines
            // and crowd clapping fire at the correct rate. Note timing keeps the raw _bpm,
            // since UltraStar beat positions are in the same "double time".
            double gapSeconds = _gapMs / 1000.0;
            var tempos = new List<TempoChange> { new(_bpm / 2.0, -gapSeconds, 0u) };

            // Use the same absolute tick space notes get from BeatToTick (gapTicks and
            // all) -- MoonSongLoader.UltraStar.cs feeds these ticks straight into
            // MoonSong.AddTempo, which notes are placed in too. Rebasing to "relative to
            // beat 0" here would cancel out gapTicks and land every tempo change GAP-ticks
            // early relative to the notes it's supposed to align with.
            foreach (var (beat, bpm) in _tempoChanges)
            {
                uint tick = BeatToTick(beat);
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
                uint endTick = BeatToTick(group[^1].EndBeat);
                double startTime = BeatToTime(group[0].StartBeat);
                double endTime = BeatToTime(group[^1].EndBeat);

                var events = new List<LyricEvent>();
                foreach (var n in group)
                {
                    if (TryCreateLyricEvent(n, BeatToTime(n.StartBeat), BeatToTick(n.StartBeat), out var lyricEvent))
                    {
                        events.Add(lyricEvent);
                    }
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
            if (instrument != Instrument.Vocals && instrument != Instrument.Harmony && instrument != Instrument.PartyVocals)
            {
                throw new ArgumentException("UltraStar only supports Vocals and HarmonyVocals.", nameof(instrument));
            }

            var parts = new List<VocalsPart>();

            if (instrument == Instrument.Vocals)
            {
                parts.Add(BuildVocalsPart(GetPart(0), false, 0));
            }
            else if (instrument == Instrument.Harmony || instrument == Instrument.PartyVocals)
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

            foreach (var group in GroupNotesIntoPhrases(notes))
            {
                var phrase = CreateVocalsPhrase(group, partIndex);
                if (phrase == null)
                {
                    continue;
                }

                phrases.Add(phrase);
                if (phrase.PhraseParentNote.IsStarPower)
                {
                    otherPhrases.Add(new Phrase(
                        PhraseType.StarPower,
                        phrase.Time,
                        phrase.TimeLength,
                        phrase.Tick,
                        phrase.TickLength));
                }
            }

            otherPhrases = otherPhrases.OrderBy(p => p.Tick).ToList();

            return new VocalsPart(isHarmony, phrases, new List<VocalsPhrase>(), new(), otherPhrases, new List<TextEvent>());
        }

        private List<List<UltraStarNote>> GroupNotesIntoPhrases(List<UltraStarNote> notes)
        {
            // '-' is the main phrase separator in UltraStar; this threshold only applies to
            // files without any. Must exceed the largest gap reasonably found in a phrase.
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

                    lastEndBeat = note.EndBeat;
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
                lastEndBeat = note.EndBeat;
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
            uint phraseEndTick = BeatToTick(phraseNotes[^1].EndBeat);
            uint phraseTickLen = phraseEndTick - phraseStartTick;
            double phraseStartTime = BeatToTime(phraseNotes[0].StartBeat);
            double phraseEndTime = BeatToTime(phraseNotes[^1].EndBeat);
            double phraseTimeLen = phraseEndTime - phraseStartTime;

            var parentNote = new VocalNote(
                NoteFlags.None, false,
                phraseStartTime, phraseTimeLen,
                phraseStartTick, phraseTickLen);

            var lyrics = new List<LyricEvent>();
            int harmonyPart = Math.Clamp(partIndex, 0, MAX_VOICE_PARTS - 1);

            foreach (var uNote in phraseNotes)
            {
                uint noteTick = BeatToTick(uNote.StartBeat);
                uint noteTickLen = uNote.DurationBeats * TicksPerUltraStarBeat;
                double noteTime = BeatToTime(uNote.StartBeat);
                // end-minus-start so durations spanning a tempo change stay correct.
                double noteTimeLen = BeatToTime(uNote.EndBeat) - noteTime;

                // -1 is the unpitched sentinel (see VocalNote.IsNonPitched).
                float midiPitch = uNote.IsUnpitched ? -1f : ToMidiPitch(uNote.Pitch);

                parentNote.AddChildNote(new VocalNote(
                    midiPitch,
                    harmonyPart,
                    VocalNoteType.Lyric,
                    noteTime,
                    noteTimeLen,
                    noteTick,
                    noteTickLen));

                if (TryCreateLyricEvent(uNote, noteTime, noteTick, out var lyricEvent))
                {
                    lyrics.Add(lyricEvent);
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
        /// Unpitched notes emit an event even with no syllable: MoonSongLoader.Vocals
        /// derives non-pitched status from the lyric's '#', not from VocalNote.Pitch.
        /// </summary>
        private static bool TryCreateLyricEvent(UltraStarNote note, double time, uint tick, out LyricEvent lyricEvent)
        {
            lyricEvent = default!;
            if (string.IsNullOrWhiteSpace(note.Lyric) && !note.IsUnpitched)
            {
                return false;
            }

            string lyric = note.Lyric.Trim();
            var flags = LyricSymbolFlags.None;

            // Many files already end a mid-word syllable with an FoF-style hyphen, which is
            // the same character as LYRIC_JOIN_SYMBOL. Checked before the '#' suffix below,
            // and past any pitch-slide marker ParseNoteLine already appended, either of
            // which would otherwise hide the existing hyphen from this test.
            string unmarked = lyric.TrimEnd(LyricSymbols.PITCH_SLIDE_SYMBOL);
            bool alreadyHyphenated = unmarked.Length > 0
                && unmarked[^1] == LyricSymbols.LYRIC_JOIN_SYMBOL;

            if (note.IsUnpitched)
            {
                lyric += LyricSymbols.NONPITCHED_SYMBOL;
                flags |= LyricSymbolFlags.NonPitched;
            }

            if (note.JoinWithNext)
            {
                // Appending onto an existing hyphen would render it doubled ("feed--").
                if (!alreadyHyphenated)
                {
                    lyric += LyricSymbols.LYRIC_JOIN_SYMBOL;
                }
                flags |= LyricSymbolFlags.JoinWithNext;
            }

            lyricEvent = new LyricEvent(flags, lyric, time, tick);
            return true;
        }

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
                        $"beats {g[0].StartBeat}–{g[^1].EndBeat}, " +
                        $"time {BeatToTime(g[0].StartBeat):F3}s–{BeatToTime(g[^1].EndBeat):F3}s");

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
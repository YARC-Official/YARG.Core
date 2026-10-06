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

        // YARG's harmony model has three parts (HARM1-3, see VocalNote.HarmonyPart), so a
        // P4+ marker has no part to route into.
        private const int MAX_VOICE_PARTS = 3;

        #endregion

        #region Fields

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
        // Whether the previous note's lyric ended with the word-boundary space (true before
        // the first note). Without that space on either side, two syllables glue into one word.
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
            /// Glues this lyric onto the next one with no space (LyricSymbolFlags.JoinWithNext).
            /// Set by a trailing '~', or when neither side of the boundary has the format's
            /// word-boundary space.
            /// </summary>
            public bool JoinWithNext { get; set; }

            /// <summary>
            /// A bare '~' hold: pitched, but with no syllable of its own, so a word-join walks
            /// past it to the real previous syllable (see MarkPreviousNoteJoinWithNext).
            /// </summary>
            public bool IsSilentHold { get; set; }

            public uint EndBeat => StartBeat + DurationBeats;

            public static bool IsNoteLineType(char type) => type is ':' or '*' or 'F' or '-' or 'R' or 'G';
            public static bool IsRestType(char type)     => type == '-';

            // Freestyle (F), Rap (R) and Golden Rap (G) have no pitch. All three score as
            // unpitched: YARG has no unscored vocal category for Freestyle (see VocalNote.IsNonPitched).
            public static bool IsUnpitchedType(char type) => type is 'F' or 'R' or 'G';

            public bool IsGolden    => Type is '*' or 'G';
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
            var text = DecodeText(file).AsSpan();
            while (TryReadLine(ref text, out var line))
            {
                var trimmed = line.Trim();
                var kind = ClassifyLine(trimmed, out int voiceNumber);
                if (kind == LineKind.End)
                {
                    break;
                }

                switch (kind)
                {
                    case LineKind.Metadata:
                        ParseMetadataLine(trimmed);
                        break;
                    case LineKind.VoiceMarker:
                        ParseVoiceMarker(voiceNumber);
                        break;
                    case LineKind.TempoChange:
                        ParseTempoChangeLine(trimmed);
                        break;
                    case LineKind.Note:
                        // Untrimmed: a trailing space in the lyric marks a word boundary.
                        ParseNoteLine(line);
                        break;
                }
            }

            _tempoChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        }

        // UTF-8 unless a BOM says otherwise. ScanHeader and the full parser must decode alike.
        private static string DecodeText(FixedArray<byte> file)
        {
            using var reader = new StreamReader(file.ToReferenceStream(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // Splits on "\r", "\n" or "\r\n", like StreamReader.ReadLine.
        private static bool TryReadLine(ref ReadOnlySpan<char> text, out ReadOnlySpan<char> line)
        {
            if (text.IsEmpty)
            {
                line = default;
                return false;
            }

            int end = text.IndexOfAny('\r', '\n');
            if (end < 0)
            {
                line = text;
                text = ReadOnlySpan<char>.Empty;
                return true;
            }

            line = text[..end];
            int next = end + 1;
            if (text[end] == '\r' && next < text.Length && text[next] == '\n')
            {
                next++;
            }
            text = text[next..];
            return true;
        }

        private enum LineKind { Blank, Metadata, VoiceMarker, End, TempoChange, Note, Ignored }

        // Shared by the full parser and ScanHeader, so a library scan reads a file exactly
        // the way loading it would.
        private static LineKind ClassifyLine(ReadOnlySpan<char> trimmedLine, out int voiceNumber)
        {
            voiceNumber = 0;
            if (trimmedLine.IsEmpty)
            {
                return LineKind.Blank;
            }

            char first = trimmedLine[0];
            if (first == '#')
            {
                return LineKind.Metadata;
            }

            // Voice markers appear as both "P1" and "P 1", and the spaced form is the more
            // common one. Rejecting it would merge every voice of those files into one part.
            if (first == 'P')
            {
                var voice = trimmedLine[1..].TrimStart();
                if (voice.Length == 1 && char.IsDigit(voice[0]))
                {
                    voiceNumber = voice[0] - '0';
                    return LineKind.VoiceMarker;
                }
            }

            if (trimmedLine.Length == 1 && first == 'E')
            {
                return LineKind.End;
            }

            if (first == 'B')
            {
                return LineKind.TempoChange;
            }

            return UltraStarNote.IsNoteLineType(first) ? LineKind.Note : LineKind.Ignored;
        }

        private static bool TryParseMetadataLine(ReadOnlySpan<char> trimmedLine, out string key, out string value)
        {
            int colon = trimmedLine.IndexOf(':');
            if (colon <= 1 || colon >= trimmedLine.Length - 1)
            {
                key = value = string.Empty;
                return false;
            }

            key = trimmedLine[1..colon].Trim().ToString();
            value = trimmedLine[(colon + 1)..].Trim().TrimEnd(',').ToString();
            return true;
        }

        /// <summary>
        /// Splits a note or rest line, "type beat [duration pitch text]". Fields are separated
        /// by spaces only. The text is everything after the single space that follows the
        /// pitch, with its own leading and trailing spaces kept: an extra space there marks a
        /// word boundary. A rest needs only its beat.
        /// </summary>
        /// <returns>
        /// Whether the line has every field its type needs. <paramref name="type"/> is set either way.
        /// </returns>
        private static bool TryParseNoteLine(ReadOnlySpan<char> line, out char type, out uint startBeat,
            out uint duration, out int pitch, out ReadOnlySpan<char> text)
        {
            duration = 0;
            pitch = 0;
            text = ReadOnlySpan<char>.Empty;

            var remaining = line.TrimStart();
            var typeField = NextField(ref remaining);
            type = typeField.IsEmpty ? '\0' : typeField[0];
            if (!uint.TryParse(NextField(ref remaining), out startBeat))
            {
                return false;
            }

            if (UltraStarNote.IsRestType(type))
            {
                return true;
            }

            if (!uint.TryParse(NextField(ref remaining), out duration) || !int.TryParse(NextField(ref remaining), out pitch))
            {
                return false;
            }

            text = !remaining.IsEmpty && remaining[0] == ' ' ? remaining[1..] : remaining;
            return true;
        }

        // The next space-separated field, leaving `line` at the space that follows it.
        private static ReadOnlySpan<char> NextField(ref ReadOnlySpan<char> line)
        {
            int start = 0;
            while (start < line.Length && line[start] == ' ')
            {
                start++;
            }

            int end = start;
            while (end < line.Length && line[end] != ' ')
            {
                end++;
            }

            var field = line[start..end];
            line = line[end..];
            return field;
        }

        private static bool IsSupportedVoice(int voiceNumber)
        {
            if (voiceNumber >= 1 && voiceNumber <= MAX_VOICE_PARTS)
            {
                return true;
            }

            YargLogger.LogFormatWarning("[UltraStar] Voice marker P{0} exceeds the {1} supported harmony parts — ignoring", voiceNumber, MAX_VOICE_PARTS);
            return false;
        }

        /// <summary>
        /// Per spec (§4.3) each P marker is an independent voice, not a "both singers" one.
        /// </summary>
        private void ParseVoiceMarker(int voiceNumber)
        {
            if (!IsSupportedVoice(voiceNumber))
            {
                return;
            }

            _currentPart = voiceNumber - 1;
            // A trailing '~' or word-join shouldn't bleed into a different voice.
            _pendingPitchSlide = false;
            _previousHadTrailingSpace = true;
            GetOrCreatePart(_currentPart);
        }

        private void ParseTempoChangeLine(ReadOnlySpan<char> line)
        {
            NextField(ref line); // "B"
            if (uint.TryParse(NextField(ref line), out uint beat)
                && TryParseNumber(NextField(ref line).ToString(), out double bpm) && bpm > 0)
            {
                _tempoChanges.Add((beat, bpm));
            }
        }

        private void ParseMetadataLine(ReadOnlySpan<char> line)
        {
            if (!TryParseMetadataLine(line, out string key, out string value))
            {
                return;
            }

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

        private void ParseNoteLine(ReadOnlySpan<char> line)
        {
            bool parsed = TryParseNoteLine(line, out char noteType, out uint startBeat, out uint duration, out int pitch, out var rawText);

            if (UltraStarNote.IsRestType(noteType))
            {
                // A rest breaks the phrase, even one whose beat doesn't parse: a pending '~' or
                // word-join must not carry over to the note after it.
                _pendingPitchSlide = false;
                _previousHadTrailingSpace = true;
                if (parsed)
                {
                    GetOrCreatePart(_currentPart).Add(new UltraStarNote
                    {
                        Type = noteType,
                        StartBeat = startBeat,
                    });
                }
                return;
            }

            if (!parsed)
            {
                return;
            }

            // An extra space on either side of the lyric marks a word boundary. Without one
            // on either side, this syllable glues onto the previous one with no separator.
            bool hasLeadingSpace = !rawText.IsEmpty && rawText[0] == ' ';
            bool hasTrailingSpace = !rawText.IsEmpty && rawText[^1] == ' ';
            bool gluedToPrevious = !hasLeadingSpace && !_previousHadTrailingSpace;
            string lyric = rawText.Trim().ToString();

            // '+' is ordinary text in UltraStar but YARG's pitch-slide marker: left in, it would
            // merge this note into the previous one (LyricSymbols.GetLyricFlags) and vanish
            // from the display (StripForVocals). Spell it out instead.
            if (lyric.IndexOf(LyricSymbols.PITCH_SLIDE_SYMBOL) >= 0)
            {
                lyric = lyric.Replace(LyricSymbols.PITCH_SLIDE_SYMBOL.ToString(), "plus");
            }

            bool isUnpitched = UltraStarNote.IsUnpitchedType(noteType);
            bool joinWithNext = false;
            bool isSilentHold = false;

            // A '~' means "this pitch continues", which only makes sense inside a word, so no
            // form of it slides across a word boundary. Unpitched notes have no pitch to
            // slide into. Consume the previous note's trailing '~' before this note's own '~'
            // can set it again for the next note.
            bool pitchSlide = _pendingPitchSlide && !isUnpitched && gluedToPrevious;
            _pendingPitchSlide = false;

            if (lyric.Length > 0 && lyric[0] == US_MELISMA_SYMBOL)
            {
                lyric = lyric[1..];
                if (lyric.Length > 0)
                {
                    // A leading '~' blends this syllable back into the previous one.
                    pitchSlide = gluedToPrevious;
                }
                else if (!isUnpitched)
                {
                    // A bare hold has no syllable, so a space before it marks no word boundary:
                    // it always continues the previous note. Whether it blends forward is
                    // decided when the next note is parsed.
                    pitchSlide = true;
                    isSilentHold = true;
                    _pendingPitchSlide = true;
                }
            }
            else if (lyric.Length > 0 && lyric[^1] == US_MELISMA_SYMBOL)
            {
                // Trailing '~' ("n~" then "eed"): hyphenate here, but the pitch slide goes on
                // the NEXT note, since MoonSongLoader.Vocals merges on the later note's flag.
                lyric = lyric[..^1];
                joinWithNext = true;
                _pendingPitchSlide = true;
            }

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

        private void MarkPreviousNoteJoinWithNext()
        {
            // Walk past bare '~' holds, which have no syllable to join, to the real previous
            // syllable. Never join across a rest.
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

        // Derives a note group's (tick, time) span from its first and last notes -- shared
        // by LoadLyrics and CreateVocalsPhrase, which both group notes into phrases.
        private (uint StartTick, uint TickLength, double StartTime, double TimeLength) GetPhraseSpan(
            UltraStarNote first, UltraStarNote last)
        {
            uint startTick = BeatToTick(first.StartBeat);
            uint endTick = BeatToTick(last.EndBeat);
            double startTime = BeatToTime(first.StartBeat);
            double endTime = BeatToTime(last.EndBeat);
            return (startTick, endTick - startTick, startTime, endTime - startTime);
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

                var span = GetPhraseSpan(group[0], group[^1]);

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
                    phrases.Add(new LyricsPhrase(span.StartTime, span.TimeLength,
                        span.StartTick, span.TickLength, events));
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

            var span = GetPhraseSpan(phraseNotes[0], phraseNotes[^1]);

            var parentNote = new VocalNote(
                NoteFlags.None, false,
                span.StartTime, span.TimeLength,
                span.StartTick, span.TickLength);

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
                YargLogger.LogWarning($"[UltraStar] Phrase at tick {span.StartTick} has 0 child notes — skipping");
                return null;
            }

            return new VocalsPhrase(
                span.StartTime, span.TimeLength,
                span.StartTick, span.TickLength,
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
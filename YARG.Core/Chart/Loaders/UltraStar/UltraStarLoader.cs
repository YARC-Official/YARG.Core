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
    internal partial class UltraStarLoader
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

        private SyncTrack? _syncTrack;

        // (Beat, BPM) mid-song tempo changes from "B <beat> <bpm>" lines, sorted by beat once parsing completes.
        private readonly List<(uint Beat, double Bpm)> _tempoChanges = new();

        private readonly Dictionary<int, List<UltraStarNote>> _partNotes = new();
        private int _currentPart = 0;

        // One bit per voice part (P1..P3) with at least one parsed non-rest note.
        private int _populatedVoices;
        private bool _sawFirstLine;

        /// <summary>Whether the first non-blank line of the file is a '#KEY:' tag.</summary>
        internal bool StartsWithTag { get; private set; }

        #endregion

        #region UltraStarNote

        private class UltraStarNote
        {
            public char   Type          { get; set; }
            public uint   StartBeat     { get; set; }
            public uint   DurationBeats { get; set; }
            public int    Pitch         { get; set; }
            public string Lyric         { get; set; } = string.Empty;

            // The lyric field as written, which ResolvePhrase turns into joins and slides: an
            // extra space on either side marks a word boundary, and '~' means "this pitch continues".
            public bool LeadingSpace  { get; set; }
            public bool TrailingSpace { get; set; }
            public bool LeadingTilde  { get; set; }
            public bool TrailingTilde { get; set; }

            /// <summary>A bare '~': no syllable of its own, so a word-join walks past it.</summary>
            public bool IsSilentHold { get; set; }

            /// <summary>
            /// Glues this lyric onto the next one with no space (LyricSymbolFlags.JoinWithNext).
            /// Set by ResolvePhrase.
            /// </summary>
            public bool JoinWithNext { get; set; }

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
            RunSpanDriver(file, LineProcessMode.Full);

            _tempoChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            foreach (var notes in _partNotes.Values)
            {
                foreach (var phrase in GroupNotesIntoPhrases(notes))
                {
                    ResolvePhrase(phrase);
                }
            }
        }

        // Used internally for the Header and Classify modes, which need none of the full
        // parser's constructor-time post-processing.
        private UltraStarLoader() { }

        public string? GetMetadata(string key)
            => _metadata.TryGetValue(key, out var v) ? v : null;

        /// <summary>#GAP as written, in milliseconds; may be negative.</summary>
        internal double GapMs => _gapMs;

        /// <summary>Populated voice part indices (P1..P3, as 0..2), in ascending order.</summary>
        public List<int> Voices
        {
            get
            {
                var voices = new List<int>();
                for (int i = 0; i < MAX_VOICE_PARTS; i++)
                {
                    if ((_populatedVoices & (1 << i)) != 0)
                    {
                        voices.Add(i);
                    }
                }
                return voices;
            }
        }

        /// <summary>Voices with at least one parsed non-rest note; not the #PARTS tag's value.</summary>
        public int VoiceCount => CountBits(_populatedVoices);

        private static int CountBits(int mask)
        {
            int count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }
            return count;
        }

        /// <summary>Parses a numeric tag. US files routinely use comma decimals.</summary>
        public static bool TryParseNumber(string? raw, out double value)
        {
            value = 0;
            return raw != null
                && double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        #region Parsing

        private enum LineProcessMode { Full, Header, Classify }

        // The span driver for Full/Header; ClassifyTextFile is the stream driver for Classify.
        // Both feed lines through ProcessLine, so a library scan always agrees with a full load.
        private void RunSpanDriver(FixedArray<byte> file, LineProcessMode mode)
        {
            var text = DecodeText(file).AsSpan();
            while (TryReadLine(ref text, out var line))
            {
                if (!ProcessLine(line, mode))
                {
                    break;
                }
            }
        }

        /// <returns>False once reading should stop: on "E", or in Classify mode as soon as a voice is populated.</returns>
        private bool ProcessLine(ReadOnlySpan<char> line, LineProcessMode mode)
        {
            var trimmed = line.Trim();
            if (!_sawFirstLine && !trimmed.IsEmpty)
            {
                _sawFirstLine = true;
                StartsWithTag = IsTagLine(trimmed);
            }

            var kind = ClassifyLine(trimmed, out int voiceNumber);
            switch (kind)
            {
                case LineKind.End:
                    return false;

                case LineKind.Metadata:
                    if (TryParseMetadataLine(trimmed, out string key, out string value))
                    {
                        _metadata[key] = value;
                        if (mode == LineProcessMode.Full)
                        {
                            ApplyMetadataValue(key, value);
                        }
                    }
                    break;

                case LineKind.VoiceMarker:
                    // Per spec (§4.3) each P marker is an independent voice, not a "both singers" one.
                    if (IsSupportedVoice(voiceNumber))
                    {
                        _currentPart = voiceNumber - 1;
                    }
                    break;

                case LineKind.TempoChange:
                    if (mode == LineProcessMode.Full)
                    {
                        ParseTempoChangeLine(trimmed);
                    }
                    break;

                case LineKind.Note:
                    if (mode == LineProcessMode.Full)
                    {
                        ParseNoteLine(line);
                        break;
                    }

                    // Header/Classify only need to know a voice is populated, not its notes --
                    // and never need a second note once that's already known.
                    bool alreadyPopulated = (_populatedVoices & (1 << _currentPart)) != 0;
                    if (!alreadyPopulated && TryParseNoteLine(line, out char type, out _, out _, out _, out _)
                        && !UltraStarNote.IsRestType(type))
                    {
                        _populatedVoices |= 1 << _currentPart;
                        if (mode == LineProcessMode.Classify)
                        {
                            return false;
                        }
                    }
                    break;
            }

            return true;
        }

        // UTF-8 unless a BOM says otherwise. ScanHeader and the full parser must decode alike.
        private static string DecodeText(FixedArray<byte> file)
        {
            using var reader = new StreamReader(file.ToReferenceStream(), Encoding.UTF8);
            return reader.ReadToEnd().Replace('\t', ' ');
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

        // Shared by every line-processing mode, so a library scan reads a file exactly the
        // way loading it would.
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
        /// word boundary. A rest needs only its beat, which some files write without the space ("-26").
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
            var beatField = UltraStarNote.IsRestType(type) && typeField.Length > 1 ? typeField[1..] : NextField(ref remaining);
            if (!uint.TryParse(beatField, out startBeat))
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

        private void ParseTempoChangeLine(ReadOnlySpan<char> line)
        {
            NextField(ref line); // "B"
            if (uint.TryParse(NextField(ref line), out uint beat)
                && TryParseNumber(NextField(ref line).ToString(), out double bpm) && bpm > 0)
            {
                _tempoChanges.Add((beat, bpm));
            }
        }

        private void ApplyMetadataValue(string key, string value)
        {
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
            if (!TryParseNoteLine(line, out char noteType, out uint startBeat, out uint duration, out int pitch, out var rawText))
            {
                return;
            }

            var note = new UltraStarNote
            {
                Type = noteType,
                StartBeat = startBeat,
                DurationBeats = duration,
                Pitch = pitch,
            };

            if (!note.IsRest)
            {
                note.LeadingSpace = !rawText.IsEmpty && rawText[0] == ' ';
                note.TrailingSpace = !rawText.IsEmpty && rawText[^1] == ' ';
                string lyric = rawText.Trim().ToString();

                // '+' is ordinary text in UltraStar but YARG's pitch-slide marker: left in, it would
                // merge this note into the previous one (LyricSymbols.GetLyricFlags) and vanish
                // from the display (StripForVocals). Spell it out with the fullwidth form instead,
                // which renders visibly distinct from an actual slide marker.
                lyric = lyric.Replace(LyricSymbols.PITCH_SLIDE_SYMBOL, '＋');

                note.LeadingTilde = lyric.Length > 0 && lyric[0] == US_MELISMA_SYMBOL;
                note.TrailingTilde = !note.LeadingTilde && lyric.Length > 0 && lyric[^1] == US_MELISMA_SYMBOL;
                if (note.LeadingTilde)
                {
                    lyric = lyric[1..];
                }
                else if (note.TrailingTilde)
                {
                    lyric = lyric[..^1];
                }

                note.Lyric = lyric;
                note.IsSilentHold = note.LeadingTilde && lyric.Length == 0;

                _populatedVoices |= 1 << _currentPart;
            }

            GetOrCreatePart(_currentPart).Add(note);
        }

        /// <summary>
        /// Decides a phrase's word joins and pitch slides from each note's neighbors. A phrase is
        /// the unit: nothing joins or slides across a phrase boundary.
        /// </summary>
        private static void ResolvePhrase(List<UltraStarNote> phrase)
        {
            // A trailing '~' or a bare hold passes its slide on to the next note.
            bool pendingSlide = false;

            for (int i = 0; i < phrase.Count; i++)
            {
                var note = phrase[i];
                var previous = i > 0 ? phrase[i - 1] : null;

                // No extra space on either side of the boundary glues two syllables into one word.
                bool glued = previous != null && !note.LeadingSpace && !previous.TrailingSpace;

                // A slide merges a note into the one before it, so both must be pitched. On an
                // unpitched note '~' still joins words, it just doesn't slide.
                bool canSlide = previous != null && !note.IsUnpitched && !previous.IsUnpitched;

                // A '~' means "this pitch continues", which only makes sense inside a word, so no
                // form of it slides across a word boundary. A bare hold has no syllable, so a
                // space before it marks no boundary: it always continues the previous note.
                bool slide = note.LeadingTilde
                    ? (note.IsSilentHold || glued) && canSlide
                    : pendingSlide && glued && canSlide;
                pendingSlide = (note.IsSilentHold || note.TrailingTilde) && !note.IsUnpitched;

                // Trailing '~' ("n~" then "eed"): hyphenate here, but the slide goes on the NEXT
                // note, since MoonSongLoader.Vocals merges on the later note's flag.
                note.JoinWithNext = note.TrailingTilde;

                if (glued && note.Lyric.Length > 0)
                {
                    JoinWithPreviousSyllable(phrase, i);
                }

                if (slide)
                {
                    note.Lyric += LyricSymbols.PITCH_SLIDE_SYMBOL;
                }
            }
        }

        // Skips bare holds, which have no syllable to join, back to the real previous syllable.
        private static void JoinWithPreviousSyllable(List<UltraStarNote> phrase, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (!phrase[i].IsSilentHold)
                {
                    phrase[i].JoinWithNext = true;
                    return;
                }
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

        #endregion

        #region Beat Conversion

        private uint TicksPerUltraStarBeat => _ticksPerBeat / US_BEATS_PER_TICK_BEAT;

        // Beats are a pure subdivision of ticks, so mid-song tempo changes don't affect this.
        // GAP is in milliseconds, converted at the initial tempo: with 120 ticks per beat and
        // the halved BPM LoadSyncTrack uses, that's exactly _bpm ticks per second. A negative
        // GAP would need negative ticks, so it's clamped to 0 here and becomes SongOffset instead.
        private uint BeatToTick(uint beat)
        {
            uint gapTicks = (uint) (Math.Max(0, _gapMs) / 1000.0 * _bpm);
            return gapTicks + (beat * TicksPerUltraStarBeat);
        }

        // The only source of a beat's time: the tempo map, not a parallel formula. Keeps the
        // loader's own notion of time identical to what SongChart ends up with.
        private double BeatToTime(uint beat) => LoadSyncTrack().TickToTime(BeatToTick(beat));

        #endregion

        #region Loading

        public SyncTrack LoadSyncTrack()
        {
            if (_syncTrack != null)
            {
                return _syncTrack;
            }

            // UltraStar BPM is typically 2x the real musical BPM; halve it here so beatlines
            // and crowd clapping fire at the correct rate. Note timing keeps the raw _bpm,
            // since UltraStar beat positions are in the same "double time".
            double time = 0.0;
            uint tick = 0;
            double halvedBpm = _bpm / 2.0;
            var tempos = new List<TempoChange> { new(halvedBpm, time, tick) };

            // Use the same absolute tick space notes get from BeatToTick (gapTicks and all) --
            // MoonSongLoader.UltraStar.cs feeds these ticks straight into MoonSong.AddTempo,
            // which notes are placed in too. GAP therefore lives only in ticks: tempo 0 sits
            // at time 0, exactly like MoonSong's own tick 0.
            foreach (var (beat, bpm) in _tempoChanges)
            {
                uint newTick = BeatToTick(beat);
                time += (newTick - tick) / (double) _ticksPerBeat * (60.0 / halvedBpm);
                tick = newTick;
                halvedBpm = bpm / 2.0;
                tempos.Add(new TempoChange(halvedBpm, time, tick));
            }

            _syncTrack = new SyncTrack(_ticksPerBeat,
                tempos,
                new List<TimeSignatureChange> { new(4, 4, 0.0, 0u, 0u, 0u, 0u, 0.0) },
                new List<Beatline>());
            return _syncTrack;
        }

        public VocalsTrack LoadVocalsTrack(Instrument instrument)
        {
            if (instrument != Instrument.Vocals && instrument != Instrument.Harmony && instrument != Instrument.PartyVocals)
            {
                throw new ArgumentException("UltraStar only supports Vocals, PartyVocals and Harmony.", nameof(instrument));
            }

            var voices = Voices;
            var parts = new List<VocalsPart>();

            if (voices.Count > 0)
            {
                if (instrument == Instrument.Vocals)
                {
                    parts.Add(BuildVocalsPart(_partNotes[voices[0]], false, 0));
                }
                else
                {
                    // With 2+ voices, HARM1-3 scoring phrases share one set of cut points so
                    // overlapping phrases across parts line up (see ComputeSharedPhraseCuts).
                    // A single populated voice has nothing to share bounds with.
                    List<uint>? cuts = null;
                    List<(uint Start, uint End)>? windowBounds = null;
                    if (voices.Count > 1)
                    {
                        cuts = ComputeSharedPhraseCuts(voices);
                        windowBounds = ComputeSharedWindowBounds(voices, cuts);
                    }

                    // One VocalsPart per voice actually populated (P1..P3), in order. Harmony
                    // part i is built from the voice at position i in that list, not the raw P
                    // number -- so P1+P3 maps to Harmony1+Harmony2.
                    for (int i = 0; i < voices.Count; i++)
                    {
                        parts.Add(BuildVocalsPart(_partNotes[voices[i]], true, i, cuts, windowBounds));
                    }
                }
            }

            return new VocalsTrack(Instrument.Vocals, parts, new List<VocalsRangeShift>());
        }

        #endregion

        #region Vocals Processing

        private VocalsPart BuildVocalsPart(List<UltraStarNote> notes, bool isHarmony, int partIndex,
            List<uint>? sharedCuts = null, List<(uint Start, uint End)>? sharedWindowBounds = null)
        {
            var phrases = new List<VocalsPhrase>();
            var staticLyricPhrases = new List<VocalsPhrase>();
            var otherPhrases = new List<Phrase>();

            // Scoring groups: either this voice's own lines (solo, or a lone harmony voice),
            // or its notes split into the shared windows so overlapping HARM1-3 phrases line
            // up (see ComputeSharedPhraseCuts). Either way, the display lines below always
            // come from this voice's own lines, so a shared window split never breaks a line
            // up on screen.
            List<(List<UltraStarNote> Group, (uint Start, uint End)? Bounds)> scoringGroups;
            if (sharedCuts != null && sharedWindowBounds != null)
            {
                foreach (var line in GroupNotesIntoPhrases(notes))
                {
                    var staticPhrase = CreateVocalsPhrase(line, partIndex);
                    if (staticPhrase != null)
                    {
                        staticLyricPhrases.Add(staticPhrase);
                    }
                }

                var byWindow = new SortedDictionary<int, List<UltraStarNote>>();
                foreach (var note in notes.Where(n => !n.IsRest).OrderBy(n => n.StartBeat))
                {
                    int window = WindowIndex(sharedCuts, note.StartBeat);
                    if (!byWindow.TryGetValue(window, out var group))
                    {
                        group = new List<UltraStarNote>();
                        byWindow[window] = group;
                    }
                    group.Add(note);
                }

                scoringGroups = byWindow
                    .Select(kv => (kv.Value, ((uint Start, uint End)?) sharedWindowBounds[kv.Key]))
                    .ToList();
            }
            else
            {
                scoringGroups = GroupNotesIntoPhrases(notes)
                    .Select(line => (line, ((uint Start, uint End)?) null))
                    .ToList();
            }

            foreach (var (group, bounds) in scoringGroups)
            {
                var phrase = bounds is { } b
                    ? CreateVocalsPhrase(group, partIndex, b.Start, b.End)
                    : CreateVocalsPhrase(group, partIndex);
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

            return new VocalsPart(isHarmony, phrases, staticLyricPhrases, new(), otherPhrases, new List<TextEvent>());
        }

        /// <summary>
        /// One shared list of cut beats for every populated voice, so HARM1-3 scoring phrases
        /// line up across parts instead of each voice cutting on its own '-' rests:
        /// - One cut per gap between consecutive lines of the primary voice (the lowest
        ///   populated one), found by walking back from the next line's start past any note
        ///   that straddles it. If that walk would reach into the previous line, there is no
        ///   clean cut: cut at the next line's start anyway and let the straddler hang over.
        ///   The primary voice's own lines therefore never merge.
        /// - One extra cut at each other voice's line start that falls outside every primary
        ///   line (e.g. a line sung in the primary voice's rest).
        /// </summary>
        private List<uint> ComputeSharedPhraseCuts(List<int> voices)
        {
            var primaryLines = GroupNotesIntoPhrases(_partNotes[voices[0]])
                .Select(line => (Start: line[0].StartBeat, End: line[^1].EndBeat))
                .ToList();

            var allNotes = voices
                .SelectMany(v => _partNotes[v])
                .Where(n => !n.IsRest)
                .Select(n => (n.StartBeat, n.EndBeat))
                .ToList();

            var cuts = new SortedSet<uint>();

            for (int i = 1; i < primaryLines.Count; i++)
            {
                uint prevEnd = primaryLines[i - 1].End;
                uint nextStart = primaryLines[i].Start;
                cuts.Add(FindCleanCut(allNotes, prevEnd, nextStart));
            }

            foreach (int voice in voices.Skip(1))
            {
                foreach (var line in GroupNotesIntoPhrases(_partNotes[voice]))
                {
                    uint start = line[0].StartBeat;
                    bool insidePrimaryLine = primaryLines.Any(p => start >= p.Start && start < p.End);
                    if (!insidePrimaryLine)
                    {
                        cuts.Add(start);
                    }
                }
            }

            return cuts.ToList();
        }

        // Walks back from nextStart to the latest beat no note strictly straddles. Gives up
        // (falls back to nextStart) if that walk would reach prevEnd or earlier -- there's no
        // clean cut in the gap, so the straddling note is left to hang over the boundary.
        private static uint FindCleanCut(List<(uint StartBeat, uint EndBeat)> allNotes, uint prevEnd, uint nextStart)
        {
            uint b = nextStart;
            while (true)
            {
                var straddler = FindStraddlingNote(allNotes, b);
                if (straddler == null)
                {
                    return b;
                }

                b = straddler.Value.StartBeat;
                if (b < prevEnd)
                {
                    return nextStart;
                }
            }
        }

        // The straddling note with the earliest start, if any: one with StartBeat < beat < EndBeat.
        private static (uint StartBeat, uint EndBeat)? FindStraddlingNote(List<(uint StartBeat, uint EndBeat)> allNotes, uint beat)
        {
            (uint StartBeat, uint EndBeat)? best = null;
            foreach (var note in allNotes)
            {
                if (note.StartBeat < beat && beat < note.EndBeat && (best == null || note.StartBeat < best.Value.StartBeat))
                {
                    best = note;
                }
            }
            return best;
        }

        /// <summary>
        /// Shared bounds per window (as cut by <see cref="ComputeSharedPhraseCuts"/>): the
        /// earliest start and latest end among all voices' notes that start in that window,
        /// the end capped at the next window's start so windows never overlap. Every voice
        /// with notes in a window uses these same bounds for its phrase there.
        /// </summary>
        private List<(uint Start, uint End)> ComputeSharedWindowBounds(List<int> voices, List<uint> cuts)
        {
            var bounds = new (uint Start, uint End)?[cuts.Count + 1];

            foreach (var note in voices.SelectMany(v => _partNotes[v]).Where(n => !n.IsRest))
            {
                int window = WindowIndex(cuts, note.StartBeat);
                var current = bounds[window];
                bounds[window] = current is { } c
                    ? (Math.Min(c.Start, note.StartBeat), Math.Max(c.End, note.EndBeat))
                    : (note.StartBeat, note.EndBeat);
            }

            var result = new List<(uint Start, uint End)>(bounds.Length);
            for (int window = 0; window < bounds.Length; window++)
            {
                var (start, end) = bounds[window] ?? (0u, 0u);
                if (window < cuts.Count)
                {
                    end = Math.Min(end, cuts[window]);
                }
                result.Add((start, end));
            }
            return result;
        }

        // Window i spans [cuts[i-1], cuts[i]), with the first window unbounded below and the
        // last unbounded above.
        private static int WindowIndex(List<uint> cuts, uint beat)
        {
            int window = 0;
            while (window < cuts.Count && cuts[window] <= beat)
            {
                window++;
            }
            return window;
        }

        private static List<List<UltraStarNote>> GroupNotesIntoPhrases(List<UltraStarNote> notes)
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

        // startBeat/endBeat default to the phrase's own first/last note. A shared-window
        // caller passes both explicitly, so the phrase's bounds come from the window instead
        // -- a note that runs past endBeat still gets its own full-length child note (see the
        // loop below), so it hangs over as a carried note rather than being clamped.
        private VocalsPhrase? CreateVocalsPhrase(List<UltraStarNote> phraseNotes, int partIndex,
            uint? startBeat = null, uint? endBeat = null)
        {
            if (phraseNotes.Count == 0)
            {
                return null;
            }

            uint firstBeat = startBeat ?? phraseNotes[0].StartBeat;
            uint lastBeat = endBeat ?? phraseNotes[^1].EndBeat;
            uint startTick = BeatToTick(firstBeat);
            uint endTick = BeatToTick(lastBeat);
            double startTime = BeatToTime(firstBeat);
            double endTime = BeatToTime(lastBeat);

            var parentNote = new VocalNote(
                NoteFlags.None, false,
                startTime, endTime - startTime,
                startTick, endTick - startTick);

            var lyrics = new List<LyricEvent>();

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
                    partIndex,
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
                YargLogger.LogWarning($"[UltraStar] Phrase at tick {startTick} has 0 child notes — skipping");
                return null;
            }

            return new VocalsPhrase(
                startTime, endTime - startTime,
                startTick, endTick - startTick,
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
            // and past any pitch-slide marker ResolvePhrase already appended, either of
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

        #endregion
    }
}

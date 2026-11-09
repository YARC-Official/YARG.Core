using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Chart.Loaders.SingStar
{
    internal class SingStarLoader : ISongLoader
    {
        public SingStarLoader(FixedArray<byte> file)
        {
            ParseSingStarFile(file);
        }

        public string? GetMetadata(string key) => _metadata.GetValueOrDefault(key);

        /// <summary>Whether the chart contains at least one pitched note.</summary>
        internal bool HasNotes => _partNotes.Values.Any(notes => notes.Any(note => !note.IsRest));

        #region SingStarNote

        private class SingStarNote
        {
            public int PartIndex { get; set; }
            public int MidiNote { get; set; } // Absolute MIDI pitch; 0 = rest/silence
            public uint StartUnit { get; set; } // Cumulative position in chart units
            public uint Duration { get; set; } // Length in chart units
            public string Lyric { get; set; } = string.Empty;
            public bool IsBonus { get; set; } // Bonus="Yes" -> power star
            public bool IsSentenceStart { get; set; }
            public bool IsFreeStyle { get; set; }

            /// <summary>
            ///     "word -": glue this syllable onto the next one with no space
            ///     (LyricSymbolFlags.JoinWithNext downstream).
            /// </summary>
            public bool JoinWithNext { get; set; }

            /// <summary>
            ///     A bare "-": this note holds the previous syllable, so its pitch slides on
            ///     from the note before it (LyricSymbols.PITCH_SLIDE_SYMBOL downstream).
            /// </summary>
            public bool IsSlide { get; set; }

            // A rest is a note with MidiNote == 0 AND no lyric text. A freestyle note is
            // unpitched, not a rest, even when it has no syllable.
            public bool IsRest => !IsFreeStyle && !IsSlide &&
                MidiNote == SINGSTAR_REST_NOTE && string.IsNullOrEmpty(Lyric);
            public bool IsGolden => IsBonus;
        }

        #endregion

        #region Constants

        private const int SINGSTAR_REST_NOTE = 0;

        // A chart's Resolution states what fraction of a beat one Duration unit
        // is: Demisemiquaver (1/32 note) is 8 units per beat and Semiquaver
        // (1/16 note) is 4. Files without the attribute are treated as
        // Demisemiquaver, by far the most common value.
        private const uint DEFAULT_UNITS_PER_BEAT = 8;

        #endregion

        #region Fields

        private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
        private uint _ticksPerBeat = 120;
        private uint _unitsPerBeat = DEFAULT_UNITS_PER_BEAT;
        private double _bpm = 120.0;

        private List<TextEvent>? _globalEvents;
        private List<Section>? _sections;
        private SyncTrack? _syncTrack;
        private VenueTrack? _venueTrack;
        private LyricsTrack? _lyricsTrack;

        private uint _barMarkerDelay;
        private bool _barMarkerDelaySet;
        private int  _formatVersion   = 2;
        private int  _v1CurrentSinger = 0;
        private bool _v1LastWasGroup  = false;

        private readonly uint[] _cumulativeUnits = new uint[2];
        private string? _currentPartName;
        private List<(string Name, uint StartUnit)>? _pendingSections;

        // Key 0 = Player1, Key 1 = Player2
        private readonly Dictionary<int, List<SingStarNote>> _partNotes = new()
        {
            [0] = new List<SingStarNote>(),
            [1] = new List<SingStarNote>(),
        };

        #endregion

        #region Parsing

        private void ParseSingStarFile(FixedArray<byte> file)
        {
            unsafe
            {
                using var stream = new UnmanagedMemoryStream(file.Ptr, file.Length);
                var settings = new XmlReaderSettings
                {
                    IgnoreComments = false,
                    IgnoreWhitespace = true,
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                };

                using var reader = XmlReader.Create(stream, settings);

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Comment)
                    {
                        ParseComment(reader.Value);
                        continue;
                    }

                    if (reader.NodeType != XmlNodeType.Element)
                        continue;

                    bool wasAdvanced = HandleElement(reader);

                    // If HandleElement consumed the reader (ReadOuterXml),
                    // process current position without calling Read() again
                    while (wasAdvanced && reader.NodeType == XmlNodeType.Element)
                    {
                        wasAdvanced = HandleElement(reader);
                    }
                }
            }
        }

        private bool HandleElement(XmlReader reader)
        {
            switch (reader.Name)
            {
                case "MELODY":
                    ParseMelodyAttributes(reader);
                    return false;

                case "TRACK":
                    if (_formatVersion == 2 || _formatVersion == 4) ParseTrack(reader);
                    return false;

                case "SENTENCE":
                    if (_formatVersion == 1)
                    {
                        string? singerAttr = reader.GetAttribute("Singer");
                        string? partName = reader.GetAttribute("Part");

                        bool isGroup = singerAttr != null &&
                            singerAttr.Equals("Group", StringComparison.OrdinalIgnoreCase);
                        bool isSolo1 = singerAttr != null &&
                            singerAttr.Equals("Solo 1", StringComparison.OrdinalIgnoreCase);
                        bool isSolo2 = singerAttr != null &&
                            singerAttr.Equals("Solo 2", StringComparison.OrdinalIgnoreCase);
                        bool isDuet = _metadata.TryGetValue("PARTS", out var p) && p == "2";

                        if (isSolo1 || !isDuet)
                        {
                            _v1CurrentSinger = 0;
                            _v1LastWasGroup = false;
                        }
                        else if (isSolo2)
                        {
                            _v1CurrentSinger = 1;
                            _v1LastWasGroup = false;
                        }
                        else if (isGroup)
                        {
                            _v1LastWasGroup = true;
                        }

                        if (isGroup || _v1LastWasGroup)
                        {
                            string sentenceXml = reader.ReadOuterXml();
                            ParseSentenceFromXml(sentenceXml, 0, ref _cumulativeUnits[0], partName);
                            ParseSentenceFromXml(sentenceXml, 1, ref _cumulativeUnits[1], partName);
                            return true;
                        }
                        else
                        {
                            ParseSentence(reader, _v1CurrentSinger,
                                ref _cumulativeUnits[_v1CurrentSinger], partName);
                            int other = 1 - _v1CurrentSinger;
                            if (_cumulativeUnits[other] < _cumulativeUnits[_v1CurrentSinger])
                                _cumulativeUnits[other] = _cumulativeUnits[_v1CurrentSinger];
                            return false;
                        }
                    }
                    return false;
            }
            return false;
        }

        /// <summary>
        ///     Helper for Group in v1 parses SENTENCE from a ready-made XML string.
        ///     Needed because XmlReader is forward-only and Group requires
        ///     going through the same data twice
        /// </summary>
        private void ParseSentenceFromXml(
            string sentenceXml,
            int partIndex,
            ref uint cumulativeUnits,
            string? partName)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
            };

            using var stringReader = new StringReader(sentenceXml);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            xmlReader.MoveToContent();

            ParseSentence(xmlReader, partIndex, ref cumulativeUnits, partName);
        }

        /// <summary>
        ///     Reads global metadata from the MELODY element attributes.
        ///     Example: Tempo="106.2" Genre="Pop" Year="2001" Duet="Yes"
        /// </summary>
        private void ParseMelodyAttributes(XmlReader reader)
        {
            string? tempo = reader.GetAttribute("Tempo");
            if (tempo != null && double.TryParse(
                    tempo.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double bpm) && bpm > 0)
            {
                _bpm = bpm;
            }

            string? genre = reader.GetAttribute("Genre");
            string? year = reader.GetAttribute("Year");
            string? duet = reader.GetAttribute("Duet");
            string? version = reader.GetAttribute("Version");
            if (version != null && int.TryParse(version, out int v))
            {
                _formatVersion = v;
            }

            if (genre != null)
            {
                _metadata["GENRE"] = genre;
            }

            if (year != null)
            {
                _metadata["YEAR"] = year;
            }

            if (duet != null && duet.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            {
                _metadata["PARTS"] = "2";
            }

            string? res = reader.GetAttribute("Resolution");
            if (res != null)
            {
                _metadata["RESOLUTION"] = res;

                if (res.Equals("Semiquaver", StringComparison.OrdinalIgnoreCase))
                {
                    _unitsPerBeat = 4;
                }
                else if (res.Equals("Demisemiquaver", StringComparison.OrdinalIgnoreCase))
                {
                    _unitsPerBeat = DEFAULT_UNITS_PER_BEAT;
                }
                else
                {
                    YargLogger.LogFormatWarning(
                        "[SingStar] Unknown tempo resolution '{0}', assuming Demisemiquaver", res);
                    _unitsPerBeat = DEFAULT_UNITS_PER_BEAT;
                }
            }
        }

        /// <summary>
        ///     Parses Artist and Title from XML comments
        /// </summary>
        private void ParseComment(string comment)
        {
            var parts = comment.Split(':', 2);
            if (parts.Length < 2) return;

            string key = parts[0].Trim();
            string value = parts[1].Trim();

            if (key.Equals("Artist", StringComparison.OrdinalIgnoreCase))
                _metadata["ARTIST"] = value;
            else if (key.Equals("Title", StringComparison.OrdinalIgnoreCase))
                _metadata["TITLE"] = value;

        }

        /// <summary>
        ///     Parses a single TRACK element (Player1 or Player2).
        ///     Each TRACK contains SENTENCE elements, each of which contains NOTE elements.
        ///     Notes carry a cumulative Duration, so we sum them to get absolute positions.
        /// </summary>
        private void ParseTrack(XmlReader reader)
        {
            string? name = reader.GetAttribute("Name");
            int partIndex = (name != null && name.Equals("Player2", StringComparison.OrdinalIgnoreCase))
                ? 1
                : 0;

            if (!_partNotes.ContainsKey(partIndex))
            {
                _partNotes[partIndex] = new List<SingStarNote>();
            }

            uint cumulativeUnits = 0; // Running position in chart units

            // Read through the TRACK subtree
            using var subtree = reader.ReadSubtree();
            while (subtree.Read())
            {
                if (subtree.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (subtree.Name == "SENTENCE")
                {
                    string? partName = subtree.GetAttribute("Part");
                    ParseSentence(subtree, partIndex, ref cumulativeUnits, partName);
                }
            }
        }

        /// <summary>
        ///     Parses one SENTENCE (phrase). Each SENTENCE is a lyric line.
        ///     Notes inside are positional: each note starts exactly where the previous one ended
        /// </summary>
        private void ParseSentence(XmlReader reader, int partIndex, ref uint cumulativeUnits, string? partName)
        {
            if (!string.IsNullOrEmpty(partName) && partName != _currentPartName)
            {
                // Deferred: the Bar Marker delay may be declared by a LABEL in this
                // very sentence, and section times depend on it.
                _pendingSections ??= new List<(string Name, uint StartUnit)>();
                _pendingSections.Add((partName, cumulativeUnits));
                _currentPartName = partName;
            }

            int sentenceStartIndex = _partNotes[partIndex].Count;

            using var subtree = reader.ReadSubtree();
            bool firstNote = true;

            while (subtree.Read())
            {
                if (subtree.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (subtree.Name == "LABEL")
                {
                    ParseLabel(subtree);
                    continue;
                }

                if (subtree.Name != "NOTE")
                {
                    continue;
                }

                string? midiStr = subtree.GetAttribute("MidiNote");
                string? durStr = subtree.GetAttribute("Duration");
                string lyric = subtree.GetAttribute("Lyric") ?? string.Empty;
                string? bonus = subtree.GetAttribute("Bonus");
                string? freestyle = subtree.GetAttribute("FreeStyle");
                var isFreestyle = freestyle != null && freestyle.Equals("Yes", StringComparison.OrdinalIgnoreCase);

                if (midiStr == null || durStr == null)
                {
                    continue;
                }

                if (!int.TryParse(midiStr, out int midiNote) ||
                    !uint.TryParse(durStr, out uint duration))
                {
                    continue;
                }

                bool isRest = midiNote == SINGSTAR_REST_NOTE && !isFreestyle;
                bool joinWithNext = false;
                bool isSlide = false;

                if (isRest)
                {
                    // A pause; words cannot continue through it.
                    lyric = string.Empty;
                }
                else
                {
                    lyric = ClassifyLyric(lyric, isFreestyle, out joinWithNext, out isSlide);
                }

                _partNotes[partIndex].Add(new SingStarNote
                {
                    PartIndex = partIndex,
                    MidiNote = midiNote,
                    StartUnit = cumulativeUnits,
                    Duration = duration,
                    Lyric = lyric,
                    IsBonus = bonus != null && bonus.Equals("Yes", StringComparison.OrdinalIgnoreCase),
                    IsSentenceStart = firstNote,
                    IsFreeStyle = isFreestyle,
                    JoinWithNext = joinWithNext,
                    IsSlide = isSlide,
                });

                firstNote = false;

                cumulativeUnits += duration;
            }

            // Markers can only be resolved once the whole sentence is known: a slide needs
            // a previous note, and a join needs a following syllable.
            ResolveSentenceLyrics(_partNotes[partIndex], sentenceStartIndex);
        }

        /// <summary>
        ///     Translates a SingStar lyric attribute into YARG's lyric model. SingStar marks
        ///     syllable boundaries as suffixes on the syllable text:
        ///     <list type="bullet">
        ///         <item><c>"word -"</c>: the next syllable continues this word.</item>
        ///         <item><c>"-"</c> alone: a held note, i.e. the pitch slides on from the previous note.</item>
        ///         <item><c>"word-"</c>: a literal hyphen in the displayed word.</item>
        ///     </list>
        /// </summary>
        private static string ClassifyLyric(string raw, bool isFreestyle, out bool joinWithNext, out bool isSlide)
        {
            joinWithNext = false;
            isSlide = false;

            string lyric = raw.Trim();
            if (lyric.Length == 0)
            {
                return string.Empty;
            }

            // A bare hyphen is SingStar's melisma marker, not a syllable of its own.
            if (lyric == "-")
            {
                // An unpitched note has no pitch to slide, so it keeps no syllable either.
                isSlide = !isFreestyle;
                return string.Empty;
            }

            // "word -" glues the next syllable onto this one, displaying a hyphen.
            if (lyric.EndsWith(" -", StringComparison.Ordinal))
            {
                joinWithNext = true;
                lyric = lyric[..^2].TrimEnd();
            }
            // An attached hyphen is part of the displayed word and also continues the word.
            else if (lyric.Length > 1 && lyric[^1] == LyricSymbols.LYRIC_JOIN_SYMBOL)
            {
                joinWithNext = true;
            }

            return EscapeSlideMarker(lyric);
        }

        /// <summary>
        ///     '+' is an ordinary character in SingStar lyrics but YARG's pitch-slide marker.
        ///     Spell it with the fullwidth variant so it cannot merge two notes.
        /// </summary>
        private static string EscapeSlideMarker(string lyric)
            => lyric.Replace(LyricSymbols.PITCH_SLIDE_SYMBOL, '＋');

        /// <summary>
        ///     Validates the markers classified for one SENTENCE. A pitch slide needs a
        ///     previous pitched note in the same sentence to attach to, and a word join on
        ///     the sentence's final syllable has nothing left to join.
        /// </summary>
        private static void ResolveSentenceLyrics(List<SingStarNote> notes, int fromIndex)
        {
            int lastSyllableIndex = -1;

            for (int i = fromIndex; i < notes.Count; i++)
            {
                var note = notes[i];
                if (note.IsRest)
                {
                    continue;
                }

                if (note.IsSlide)
                {
                    if (FindPreviousPitchedNote(notes, fromIndex, i) is null)
                    {
                        note.IsSlide = false;
                    }
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(note.Lyric))
                {
                    lastSyllableIndex = i;
                }
            }

            if (lastSyllableIndex >= 0)
            {
                notes[lastSyllableIndex].JoinWithNext = false;
            }
        }

        private static SingStarNote? FindPreviousPitchedNote(List<SingStarNote> notes, int fromIndex, int index)
        {
            for (int i = index - 1; i >= fromIndex; i--)
            {
                var note = notes[i];
                if (note.IsRest)
                {
                    continue;
                }

                // A freestyle note has no pitch, so nothing can slide onto it or out of it.
                return note.IsFreeStyle ? null : note;
            }

            return null;
        }

        /// <summary>
        ///     Parses a LABEL element. The "Bar Marker" label's Delay is the number of
        ///     chart units between the start of the chart and the first bar line.
        /// </summary>
        private void ParseLabel(XmlReader reader)
        {
            if (_barMarkerDelaySet ||
                !"Bar Marker".Equals(reader.GetAttribute("Name"), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string? delayStr = reader.GetAttribute("Delay");
            if (delayStr != null && uint.TryParse(delayStr, out uint delay))
            {
                _barMarkerDelay = delay;
                _barMarkerDelaySet = true;
            }
        }

        #endregion

        #region Time Conversion

        // A chart's Resolution defines how many units make up one beat. Durations
        // and positions are otherwise plain unit counts relative to the Bar Marker.
        // To convert units to beats:  beats = units / _unitsPerBeat
        // To convert beats to ticks:  ticks = beats * _ticksPerBeat
        // To convert beats to seconds: seconds = beats * 60.0 / _bpm

        private double UnitsToTime(uint units) =>
            (units - (double) _barMarkerDelay) / _unitsPerBeat * (60.0 / _bpm);

        private double UnitsDurationToSeconds(uint durationUnits) =>
            durationUnits / (double) _unitsPerBeat * (60.0 / _bpm);

        private uint UnitsToTick(uint units)
        {
            double beats = (units - (double) _barMarkerDelay) / _unitsPerBeat;
            return beats >= 0 ? (uint) (beats * _ticksPerBeat) : 0u;
        }

        #endregion

        #region Loading

        public List<TextEvent> LoadGlobalEvents() => _globalEvents ??= new List<TextEvent>();

        public List<Section> LoadSections()
        {
            if (_sections != null)
            {
                return _sections;
            }

            _sections = new List<Section>();
            if (_pendingSections != null)
            {
                foreach (var (name, startUnit) in _pendingSections)
                {
                    _sections.Add(new Section(name, UnitsToTime(startUnit), UnitsToTick(startUnit)));
                }
            }

            return _sections;
        }

        public VenueTrack LoadVenueTrack() => _venueTrack ??= new VenueTrack();

        public InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument i) => throw new NotSupportedException();

        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument i, InstrumentTrack<EliteDrumNote>? e) =>
            throw new NotSupportedException();

        public InstrumentTrack<EliteDrumNote> LoadEliteDrumsTrack(Instrument i) => throw new NotSupportedException();

        public SyncTrack LoadSyncTrack()
        {
            if (_syncTrack != null)
            {
                return _syncTrack;
            }

            // Note ticks already have the Bar Marker delay subtracted, so tick 0
            // maps to time 0. The tempo map must agree with that or tick-based and
            // time-based note positions would disagree.
            _syncTrack = new SyncTrack(_ticksPerBeat,
                new List<TempoChange>
                {
                    new(_bpm, 0.0, 0u),
                },
                new List<TimeSignatureChange>
                {
                    new(4, 4, 0.0, 0u, 0u, 0u, 0u, 0.0),
                },
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
            var lyricSource = _partNotes[0];

            foreach (var group in GroupNotesIntoPhrases(lyricSource))
            {
                if (group.Count == 0)
                {
                    continue;
                }

                uint startTick = UnitsToTick(group[0].StartUnit);
                uint endTick = UnitsToTick(group[^1].StartUnit + group[^1].Duration);
                double startTime = UnitsToTime(group[0].StartUnit);
                double endTime = UnitsToTime(group[^1].StartUnit + group[^1].Duration);

                var events = new List<LyricEvent>();
                foreach (var n in group)
                {
                    if (n.IsRest ||
                        !TryCreateLyricEvent(n, UnitsToTime(n.StartUnit), UnitsToTick(n.StartUnit), out var rawEvent))
                    {
                        continue;
                    }

                    // The lyrics track displays plain text; the flags preserve the meaning of
                    // the stripped markers. Empty events (a slide with no syllable) still
                    // carry their flags, matching how MoonSong's lyric converter handles them.
                    var flags = LyricSymbols.GetLyricFlags(rawEvent.Text);
                    string text = LyricSymbols.StripForLyrics(rawEvent.Text);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        flags |= LyricSymbolFlags.JoinWithNext;
                        text = string.Empty;
                    }

                    events.Add(new LyricEvent(flags, text, rawEvent.Time, rawEvent.Tick));
                }

                if (events.Count > 0)
                {
                    phrases.Add(new LyricsPhrase(
                        startTime, endTime - startTime,
                        startTick, endTick - startTick,
                        events));
                }
            }

            _lyricsTrack = new LyricsTrack(phrases);
            return _lyricsTrack;
        }

        public VocalsTrack LoadVocalsTrack(Instrument instrument)
        {
            if (instrument != Instrument.Vocals && instrument != Instrument.Harmony)
            {
                throw new ArgumentException("SingStar only supports Vocals and Harmony.", nameof(instrument));
            }

            var parts = new List<VocalsPart>();

            if (instrument == Instrument.Vocals)
            {
                parts.Add(BuildVocalsPart(_partNotes[0], false));
            }
            else if (instrument == Instrument.Harmony)
            {
                bool isDuet = _metadata.TryGetValue("PARTS", out var p) && p == "2";

                parts.Add(BuildVocalsPart(_partNotes[0], true));
                if (isDuet)
                {
                    parts.Add(BuildVocalsPart(_partNotes[1], true));
                }
            }

            return new VocalsTrack(Instrument.Vocals, parts, new List<VocalsRangeShift>());
        }

        #endregion

        #region Vocals Processing

        private VocalsPart BuildVocalsPart(List<SingStarNote> notes, bool isHarmony)
        {
            var phrases = new List<VocalsPhrase>();
            var otherPhrases = new List<Phrase>();
            var textEvents = new List<TextEvent>();

            int harmonyIndex = isHarmony ? 1 : 0;

            foreach (var group in GroupNotesIntoPhrases(notes))
            {
                if (group.Count == 0)
                {
                    continue;
                }

                var phrase = CreateVocalsPhrase(group, harmonyIndex);
                if (phrase == null)
                {
                    continue;
                }

                phrases.Add(phrase);

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

            otherPhrases = otherPhrases.OrderBy(p => p.Tick).ToList();

            return new VocalsPart(isHarmony, phrases, new List<VocalsPhrase>(), new List<VocalsPhrase>(), otherPhrases, textEvents);
        }

        /// <summary>
        ///     Groups notes into phrases (lyric lines). In SingStar, phrase boundaries are
        ///     implicit - a SENTENCE element defines each phrase
        /// </summary>
        private List<List<SingStarNote>> GroupNotesIntoPhrases(List<SingStarNote> notes)
        {
            uint restGapThreshold = _unitsPerBeat * 2;
            var groups = new List<List<SingStarNote>>();
            var currentGroup = new List<SingStarNote>();

            foreach (var note in notes.OrderBy(n => n.StartUnit))
            {
                var isLongRest = note.IsRest && note.Duration >= restGapThreshold;
                if (note.IsSentenceStart || isLongRest)
                {
                    if (currentGroup.Count > 0)
                    {
                        groups.Add(currentGroup);
                    }

                    currentGroup = new List<SingStarNote>();

                    if (isLongRest) continue;
                }

                currentGroup.Add(note);
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        private VocalsPhrase? CreateVocalsPhrase(List<SingStarNote> phraseNotes, int partIndex)
        {
            if (phraseNotes.Count == 0)
            {
                return null;
            }

            bool isStarPower = phraseNotes.Any(n => n.IsGolden);

            uint phraseStartTick = UnitsToTick(phraseNotes[0].StartUnit);
            uint phraseEndTick = UnitsToTick(phraseNotes[^1].StartUnit + phraseNotes[^1].Duration);
            uint phraseTickLen = phraseEndTick - phraseStartTick;
            double phraseStartTime = UnitsToTime(phraseNotes[0].StartUnit);
            double phraseEndTime = UnitsToTime(phraseNotes[^1].StartUnit + phraseNotes[^1].Duration);
            double phraseTimeLen = phraseEndTime - phraseStartTime;

            var parentNote = new VocalNote(
                isStarPower ? NoteFlags.StarPower : NoteFlags.None, false,
                phraseStartTime, phraseTimeLen,
                phraseStartTick, phraseTickLen);

            var lyrics = new List<LyricEvent>();
            int harmonyPart = partIndex == 0 ? 0 : 1;

            foreach (var sNote in phraseNotes)
            {
                uint noteTick = UnitsToTick(sNote.StartUnit);
                uint noteTickLen = UnitsToTick(sNote.StartUnit + sNote.Duration) - noteTick;
                double noteTime = UnitsToTime(sNote.StartUnit);
                double noteTimeLen = UnitsDurationToSeconds(sNote.Duration);

                if (sNote.IsRest)
                {
                    continue;
                }

                // SingStar pitch is already absolute MIDI - no conversion needed. Freestyle
                // notes are unpitched; -1 is the sentinel (see VocalNote.IsNonPitched).
                float midiPitch = sNote.IsFreeStyle
                    ? -1f
                    : Math.Clamp(sNote.MidiNote, 0, 127);

                var childNote = new VocalNote(
                    midiPitch,
                    harmonyPart,
                    VocalNoteType.Lyric,
                    noteTime,
                    noteTimeLen,
                    noteTick,
                    noteTickLen);

                parentNote.AddChildNote(childNote);

                if (TryCreateLyricEvent(sNote, noteTime, noteTick, out var lyricEvent))
                {
                    lyrics.Add(lyricEvent);
                }
            }

            if (parentNote.ChildNotes.Count == 0)
            {
                YargLogger.LogWarning($"[SingStar] Phrase at tick {phraseStartTick} has 0 child notes; skipping");
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
        ///     Builds the lyric event for a note, encoding YARG's modifier symbols the same way
        ///     the UltraStar loader does: '-' joins the next syllable, '+' slides onto the
        ///     previous note, and '#' marks the note as non-pitched. Slide and unpitched notes
        ///     emit an event even without a syllable so those markers are not lost.
        /// </summary>
        private static bool TryCreateLyricEvent(SingStarNote note, double time, uint tick, out LyricEvent lyricEvent)
        {
            lyricEvent = default!;

            if (string.IsNullOrWhiteSpace(note.Lyric) && !note.IsSlide && !note.IsFreeStyle)
            {
                return false;
            }

            string lyric = note.Lyric.Trim();
            var flags = LyricSymbolFlags.None;

            // Checked before the markers below hide it: a source hyphen already lives in the
            // text, and appending the join marker onto it would render it doubled ("feed--").
            string withoutSlideMarkers = lyric.TrimEnd(LyricSymbols.PITCH_SLIDE_SYMBOL);
            bool alreadyHyphenated = withoutSlideMarkers.Length > 0 &&
                withoutSlideMarkers[^1] == LyricSymbols.LYRIC_JOIN_SYMBOL;

            if (note.IsSlide)
            {
                lyric += LyricSymbols.PITCH_SLIDE_SYMBOL;
            }

            if (note.IsFreeStyle)
            {
                lyric += LyricSymbols.NONPITCHED_SYMBOL;
                flags |= LyricSymbolFlags.NonPitched;
            }

            if (note.JoinWithNext)
            {
                if (!alreadyHyphenated)
                {
                    lyric += LyricSymbols.LYRIC_JOIN_SYMBOL;
                }

                flags |= LyricSymbolFlags.JoinWithNext;
            }

            lyricEvent = new LyricEvent(flags, lyric, time, tick);
            return true;
        }

        #endregion
    }
}
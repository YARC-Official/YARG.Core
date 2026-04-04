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

        #region SingStarNote

        private class SingStarNote
        {
            public int PartIndex { get; set; }
            public int MidiNote { get; set; } // Absolute MIDI pitch; 0 = rest/silence
            public uint StartUnit { get; set; } // Cumulative position in 1/8-note units
            public uint Duration { get; set; } // Length in 1/8-note units
            public string Lyric { get; set; } = string.Empty;
            public bool IsBonus { get; set; } // Bonus="Yes" -> power star
            public bool IsSentenceStart { get; set; }
            public bool IsFreeStyle { get; set; }

            // A rest is a note with MidiNote == 0 AND no lyric text.
            public bool IsRest => MidiNote == SINGSTAR_REST_NOTE && string.IsNullOrEmpty(Lyric);
            public bool IsGolden => IsBonus;
        }

        #endregion

        #region Constants

        private const int SINGSTAR_REST_NOTE = 0;
        private const uint SINGSTAR_UNITS_PER_BEAT = 8;

        #endregion

        #region Fields

        private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
        private uint _ticksPerBeat = 120;
        private double _bpm = 120.0;

        private List<TextEvent>? _globalEvents;
        private List<Section>? _sections;
        private SyncTrack? _syncTrack;
        private VenueTrack? _venueTrack;
        private LyricsTrack? _lyricsTrack;

        private uint _barMarkerDelay;
        private int  _formatVersion   = 2;
        private int  _v1CurrentSinger = 0;
        private bool _v1LastWasGroup  = false;
        private bool _readerAdvanced  = false;

        private readonly uint[] _cumulativeUnits = new uint[2];
        private string? _currentPartName;

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
        ///     Notes carry cumulative Duration — we sum them to get absolute positions.
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

            uint cumulativeUnits = 0; // Running position in 1/8-note units

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
        ///     Notes inside are positional — each note starts exactly where the previous one ended
        /// </summary>
        private void ParseSentence(XmlReader reader, int partIndex, ref uint cumulativeUnits, string? partName)
        {
            if (!string.IsNullOrEmpty(partName) && partName != _currentPartName)
            {
                _sections ??= new List<Section>();
                _sections.Add(new Section(partName, UnitsToTime(cumulativeUnits), UnitsToTick(cumulativeUnits)));
                _currentPartName = partName;
            }

            int sentenceStartIndex = _partNotes[partIndex].Count;

            using var subtree = reader.ReadSubtree();
            bool inMelismaContinuation = false;
            bool firstNote = true;

            while (subtree.Read())
            {
                if (subtree.NodeType != XmlNodeType.Element || subtree.Name != "NOTE")
                {
                    continue;
                }

                if (subtree.Name == "LABEL")
                {
                    string? labelName = subtree.GetAttribute("Name");
                    string? delayStr = subtree.GetAttribute("Delay");
                    if ("Bar Marker".Equals(labelName, StringComparison.OrdinalIgnoreCase)
                        && delayStr != null
                        && uint.TryParse(delayStr, out uint delay)
                        && _barMarkerDelay == 0)
                    {
                        _barMarkerDelay = delay;
                    }
                }

                string? midiStr = subtree.GetAttribute("MidiNote");
                string? durStr = subtree.GetAttribute("Duration");
                string? lyric = subtree.GetAttribute("Lyric") ?? string.Empty;
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

                bool isRest = midiNote == SINGSTAR_REST_NOTE;

                if (!isRest && !isFreestyle)
                {
                    lyric = ProcessLyricForMelisma(lyric, ref inMelismaContinuation);
                }
                else
                {
                    inMelismaContinuation = false;
                }

                if (isFreestyle)
                {
                    // Non-Pitched Symbol
                    lyric = FormatLyric(lyric) + "#";
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
                    IsFreeStyle = isFreestyle
                });

                firstNote = false;

                cumulativeUnits += duration;
            }

            PostProcessMelismaMarkers(_partNotes[partIndex], sentenceStartIndex);
        }

        #endregion

        #region Time Conversion

        // SingStar Resolution="Demisemiquaver" means 1 unit = 1/8 of a beat.
        // To convert units to beats:  beats = units / 8
        // To convert beats to ticks:  ticks = beats * _ticksPerBeat
        // To convert beats to seconds: seconds = beats * 60.0 / _bpm

        private double UnitsToTime(uint units) =>
            (units - (double) _barMarkerDelay) / SINGSTAR_UNITS_PER_BEAT * (60.0 / _bpm);

        private double UnitsDurationToSeconds(uint durationUnits) =>
            durationUnits / (double) SINGSTAR_UNITS_PER_BEAT * (60.0 / _bpm);

        private uint UnitsToTick(uint units)
        {
            double beats = (units - (double) _barMarkerDelay) / SINGSTAR_UNITS_PER_BEAT;
            return beats >= 0 ? (uint) (beats * _ticksPerBeat) : 0u;
        }

        #endregion

        #region Loading

        public List<TextEvent> LoadGlobalEvents() => _globalEvents ??= new List<TextEvent>();
        public List<Section> LoadSections() => _sections ??= new List<Section>();
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

            double delaySeconds = _barMarkerDelay / (double) SINGSTAR_UNITS_PER_BEAT * (60.0 / _bpm);

            _syncTrack = new SyncTrack(120,
                new List<TempoChange>
                {
                    new(_bpm, -delaySeconds, 0u),
                },
                new List<TimeSignatureChange>
                {
                    new(4, 4, -delaySeconds, 0u, 0u, 0u, 0u, 0.0),
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
                    if (n.IsRest || string.IsNullOrWhiteSpace(n.Lyric))
                    {
                        continue;
                    }

                    events.Add(new LyricEvent(
                        LyricSymbolFlags.None,
                        FormatLyric(n.Lyric),
                        UnitsToTime(n.StartUnit),
                        UnitsToTick(n.StartUnit)));
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

            return new VocalsPart(isHarmony, phrases, new List<VocalsPhrase>(), otherPhrases, textEvents);
        }

        /// <summary>
        ///     Groups notes into phrases (lyric lines). In SingStar, phrase boundaries are
        ///     implicit - a SENTENCE element defines each phrase
        /// </summary>
        private List<List<SingStarNote>> GroupNotesIntoPhrases(List<SingStarNote> notes)
        {
            const uint REST_GAP_THRESHOLD = SINGSTAR_UNITS_PER_BEAT * 2;
            var groups = new List<List<SingStarNote>>();
            var currentGroup = new List<SingStarNote>();

            foreach (var note in notes.OrderBy(n => n.StartUnit))
            {
                var isLongRest = note.IsRest && note.Duration >= REST_GAP_THRESHOLD;
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

                // SingStar pitch is already absolute MIDI - no conversion needed.
                // Clamp to valid range just in case.
                float midiPitch = Math.Clamp(sNote.MidiNote, 0, 127);

                if (sNote.IsRest)
                {
                    continue;
                }

                var childNote = new VocalNote(
                    midiPitch,
                    harmonyPart,
                    VocalNoteType.Lyric,
                    noteTime,
                    noteTimeLen,
                    noteTick,
                    noteTickLen);

                parentNote.AddChildNote(childNote);

                if (!string.IsNullOrWhiteSpace(sNote.Lyric))
                {
                    lyrics.Add(new LyricEvent(
                        LyricSymbolFlags.None,
                        FormatLyric(sNote.Lyric),
                        noteTime,
                        noteTick));
                }
            }

            if (parentNote.ChildNotes.Count == 0)
            {
                YargLogger.LogWarning($"[SingStar] Phrase at tick {phraseStartTick} has 0 child notes — skipping");
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
        ///     Trims lyric text
        /// </summary>
        private static string FormatLyric(string raw)
        {
            raw = raw.Trim();
            if (raw.EndsWith(" -", StringComparison.Ordinal))
            {
                raw = raw[..^2].TrimEnd() + "-";
            }
            return raw;
        }

        private static bool EndsWithSlideMarker(string lyric)
        {
            if (string.IsNullOrWhiteSpace(lyric))
            {
                return false;
            }

            lyric = lyric.TrimEnd();

            return lyric.EndsWith(" -", StringComparison.Ordinal) ||
                (lyric.EndsWith("-", StringComparison.Ordinal) && lyric.Length == 1);
        }

        private static string RemoveSlideMarker(string lyric)
        {
            if (string.IsNullOrWhiteSpace(lyric))
            {
                return string.Empty;
            }

            lyric = lyric.TrimEnd();

            if (lyric.EndsWith(" -", StringComparison.Ordinal))
            {
                return lyric.Substring(0, lyric.Length - 2).TrimEnd() + "-";
            }

            if (lyric == "-")
            {
                return "";
            }

            return lyric;
        }

        private static string ProcessLyricForMelisma(string lyric, ref bool inMelismaContinuation)
        {
            lyric ??= string.Empty;
            string trimmed = lyric.Trim();

            bool isStandaloneDash = trimmed == "-";
            bool endsWithSlideMarker = EndsWithSlideMarker(trimmed);

            // Case 1:
            // We are already in continuation mode
            if (inMelismaContinuation)
            {
                // "-" means another continuation note => "+"
                if (isStandaloneDash)
                {
                    return "+";
                }

                // Normal lyric while continuing => "word+"
                // If it also ends with "-" then continuation remains active
                if (endsWithSlideMarker)
                {
                    string baseLyric = RemoveSlideMarker(trimmed);
                    return string.IsNullOrEmpty(baseLyric) ? "+" : baseLyric + "+";
                }

                // Normal lyric closes the continuation
                inMelismaContinuation = false;
                return string.IsNullOrEmpty(trimmed) ? "+" : trimmed + "+";
            }

            // Case 2:
            // Not in continuation mode yet

            // "word -" starts continuation
            if (endsWithSlideMarker && !isStandaloneDash)
            {
                inMelismaContinuation = true;
                return RemoveSlideMarker(trimmed);
            }

            // Standalone "-" without active continuation
            // safest behavior: treat as "+"
            if (isStandaloneDash)
            {
                return "+";
            }

            return trimmed;
        }

        /// <summary>
        ///     Removes dangling melisma dash markers that have no valid continuation.
        ///     After <see cref="ProcessLyricForMelisma"/> runs, a note may end with "-"
        ///     or "-+" if it opened a melisma chain. If the next non-rest note in the
        ///     sentence has an empty lyric or is already "+" (i.e. nothing real to
        ///     connect to), the "-" is stripped while preserving any trailing "+".
        ///     Examples:
        ///     <list type="bullet">
        ///         <item>"lone-+" → "lone+" when next lyric is empty</item>
        ///         <item>"a-"    → "a"    when there is no next note</item>
        ///     </list>
        /// </summary>
        private static void PostProcessMelismaMarkers(List<SingStarNote> notes, int fromIndex)
        {
            // fromIndex = index of the first note added in this sentence
            int count = notes.Count;
            for (int i = fromIndex; i < count; i++)
            {
                var note = notes[i];
                if (note.IsRest || string.IsNullOrWhiteSpace(note.Lyric))
                    continue;

                bool endsWithDashPlus = note.Lyric.EndsWith("-+", StringComparison.Ordinal);
                bool endsWithDash = !endsWithDashPlus && note.Lyric.EndsWith("-", StringComparison.Ordinal);

                if (!endsWithDashPlus && !endsWithDash)
                    continue;

                // Find the next non rest note in this sentence
                SingStarNote? next = null;
                for (int j = i + 1; j < count; j++)
                {
                    if (!notes[j].IsRest)
                    {
                        next = notes[j];
                        break;
                    }
                }

                bool nextIsEmpty = next == null
                    || string.IsNullOrWhiteSpace(next.Lyric)
                    || next.Lyric.Trim() == "+";

                if (!nextIsEmpty)
                    continue;

                int dashIndex = note.Lyric.Length - (endsWithDashPlus ? 2 : 1);
                note.Lyric = note.Lyric.Remove(dashIndex, 1).TrimEnd();
            }
        }

        public void DumpToLog()
        {
            int totalNotes = _partNotes.Values.Sum(list => list.Count);
            YargLogger.LogDebug($"[SingStar] BPM={_bpm} TOTAL_NOTES={totalNotes}");

            foreach (var kvp in _partNotes.OrderBy(k => k.Key))
            {
                int partIndex = kvp.Key;
                var notes = kvp.Value;

                YargLogger.LogDebug($"[SingStar] Part {partIndex + 1}: notes={notes.Count}");

                var groups = GroupNotesIntoPhrases(notes);
                YargLogger.LogDebug($"[SingStar] Part {partIndex + 1}: phrase groups={groups.Count}");

                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var g = groups[gi];
                    YargLogger.LogDebug($"[SingStar] Part {partIndex + 1} Phrase {gi}: {g.Count} notes, " +
                        $"units {g[0].StartUnit}–{g[^1].StartUnit + g[^1].Duration}, " +
                        $"time {UnitsToTime(g[0].StartUnit):F3}s–{UnitsToTime(g[^1].StartUnit + g[^1].Duration):F3}s");

                    foreach (var n in g)
                    {
                        YargLogger.LogDebug($"[SingStar] P{partIndex + 1} midi={n.MidiNote} " +
                            $"start={n.StartUnit} dur={n.Duration} " +
                            $"tick={UnitsToTick(n.StartUnit)} time={UnitsToTime(n.StartUnit):F3}s " +
                            $"lyric='{n.Lyric}' golden={n.IsGolden}");
                    }
                }
            }
        }

        #endregion
    }
}
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
        private int _formatVersion = 2;
        private int _currentPartIndex;

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

                    HandleElement(reader);
                }
            }
        }

        private void HandleElement(XmlReader reader)
        {
            switch (reader.Name)
            {
                case "MELODY":
                    ParseMelodyAttributes(reader);
                    break;

                case "TRACK":
                    string? trackName = reader.GetAttribute("Name");
                    _currentPartIndex = (trackName?.Equals("Player2",
                        StringComparison.OrdinalIgnoreCase) == true) ? 1 : 0;

                    if (_formatVersion == 2 || _formatVersion == 4)
                        ParseTrack(reader);
                    break;

                case "SENTENCE":
                    if (_formatVersion == 1)
                    {
                        string? partName = reader.GetAttribute("Part");
                        ParseSentence(reader, _currentPartIndex,
                            ref _cumulativeUnits[_currentPartIndex], partName);
                    }
                    break;
            }
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

                if (!isRest)
                {
                    lyric = ProcessLyricForMelisma(lyric, ref inMelismaContinuation);
                }
                else
                {
                    inMelismaContinuation = false;
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
                });

                firstNote = false;

                cumulativeUnits += duration;
            }
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

            uint phraseStartTick = UnitsToTick(phraseNotes[0].StartUnit);
            uint phraseEndTick = UnitsToTick(phraseNotes[^1].StartUnit + phraseNotes[^1].Duration);
            uint phraseTickLen = phraseEndTick - phraseStartTick;
            double phraseStartTime = UnitsToTime(phraseNotes[0].StartUnit);
            double phraseEndTime = UnitsToTime(phraseNotes[^1].StartUnit + phraseNotes[^1].Duration);
            double phraseTimeLen = phraseEndTime - phraseStartTime;

            var parentNote = new VocalNote(
                NoteFlags.None, false,
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

                // SingStar pitch is already absolute MIDI — no conversion needed.
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

            if (phraseNotes.Any(n => n.IsGolden))
            {
                parentNote.ActivateFlag(NoteFlags.StarPower);
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
                return lyric.Substring(0, lyric.Length - 2).TrimEnd();
            }

            if (lyric.Length == 1 && lyric.EndsWith("-", StringComparison.Ordinal))
            {
                return lyric.Substring(0, lyric.Length - 1).TrimEnd();
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
                        YargLogger.LogDebug($"[SingStar]   P{partIndex + 1} midi={n.MidiNote} " +
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
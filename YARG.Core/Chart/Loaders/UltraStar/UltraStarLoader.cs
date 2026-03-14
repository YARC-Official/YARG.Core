using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    internal partial class UltraStarLoader : ISongLoader
    {
        // MIDI base constant
        // UltraStar pitch is RELATIVE
        // We add a base to get the absolute MIDI pitch that YARG understands.
        // MIDI 48 = C3 Middle of the vocal range, suitable for most songs.
        private const int ULTRASTAR_PITCH_BASE = 48;

        private readonly Dictionary<string, string> _metadata     = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<UltraStarNote>        _notes        = new();
        private          uint                       _ticksPerBeat = 480;
        private          double                     _bpm          = 120.0;
        private          double                     _gapMs        = 0.0;

        private List<TextEvent>? _globalEvents;
        private List<Section>? _sections;
        private SyncTrack? _syncTrack;
        private VenueTrack? _venueTrack;
        private LyricsTrack? _lyricsTrack;

        private class UltraStarNote
        {
            public char Type { get; set; }
            public uint StartBeat { get; set; }
            public uint DurationBeats { get; set; }
            public int Pitch { get; set; }
            public string Lyric { get; set; } = string.Empty;

            public bool IsGolden => Type == '*';
            public bool IsFreestyle => Type == 'F';
            public bool IsRest => Type == '-';

            public bool IsMelisma => Lyric.TrimStart().StartsWith("~");
        }

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
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line[0] == '#') { ParseMetadataLine(line); continue; }
                    if (line == "E") break;
                    if (line[0] is ':' or '*' or 'F' or '-' or 'R' or 'G')
                        ParseNoteLine(line);
                }
            }
        }

        private void ParseMetadataLine(string line)
        {
            int colon = line.IndexOf(':');
            if (colon <= 1 || colon >= line.Length - 1) return;

            string key = line.Substring(1, colon - 1).Trim();
            string value = line.Substring(colon + 1).Trim().TrimEnd(',');
            _metadata[key] = value;

            if (key.Equals("BPM", StringComparison.OrdinalIgnoreCase)) 
            { 
                string norm = value.Replace(',', '.');
                if (double.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm) && bpm > 0)
                    _bpm = bpm * 4;
            }
            else if (key.Equals("GAP", StringComparison.OrdinalIgnoreCase))
            {
                string norm = value.Replace(',', '.');
                if (double.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out double gap))
                    _gapMs = gap;
            }
        }

        private void ParseNoteLine(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return;

            char noteType = parts[0][0];
            if (!uint.TryParse(parts[1], out uint startBeat) ||
                !uint.TryParse(parts[2], out uint duration) ||
                !int.TryParse(parts[3], out int pitch)) return;

            if (noteType == 'R') noteType = ':';
            if (noteType == 'G') noteType = '*';

            string lyric = parts.Length > 4 ? string.Join(" ", parts.Skip(4)) : string.Empty;

            _notes.Add(new UltraStarNote
            {
                Type = noteType,
                StartBeat = startBeat,
                DurationBeats = duration,
                Pitch = pitch,
                Lyric = lyric
            });
        }

        #endregion

        private uint BeatToTick(uint beat) =>
            (uint) (_gapMs / 1000.0 * _bpm / 60.0 * _ticksPerBeat) + beat * _ticksPerBeat;
        private double BeatToTime(uint beat) => beat * 60.0 / _bpm;
        private double BeatsToSeconds(uint beats) => beats * 60.0 / _bpm;

        /// <summary>
        /// Converts UltraStar relative pitch to absolute MIDI pitch for YARG.
        /// Clamp to the range 0-127.
        /// </summary>
        private static int ToMidiPitch(int ultraStarPitch)
            => Math.Clamp(ultraStarPitch + ULTRASTAR_PITCH_BASE, 0, 127);

        public List<TextEvent> LoadGlobalEvents()
        {
            return new List<TextEvent> { new (string.Empty, 0.0, 0u) }; 
        }
        public List<Section> LoadSections() => _sections ??= new();
        public VenueTrack LoadVenueTrack() => _venueTrack ??= new VenueTrack();

        public InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument i) => throw new NotSupportedException();
        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument i, InstrumentTrack<EliteDrumNote>? e) => throw new NotSupportedException();
        public InstrumentTrack<EliteDrumNote> LoadEliteDrumsTrack(Instrument i) => throw new NotSupportedException();

        public SyncTrack LoadSyncTrack()
        {
            if (_syncTrack != null) return _syncTrack;

            _syncTrack = new SyncTrack(480u,
                new List<TempoChange> { new(_bpm, -_gapMs / 1000.0, 0u) },
                new List<TimeSignatureChange> { new(4, 4, -_gapMs / 1000.0, 0u, 0u, 0u, 0u, 0.0) },
                new List<Beatline>());
            return _syncTrack;
        }

        public LyricsTrack LoadLyrics()
        {
            if (_lyricsTrack != null) return _lyricsTrack;

            var phrases = new List<LyricsPhrase>();

            foreach (var group in GroupNotesIntoPhrases())
            {
                if (group.Count == 0) continue;

                uint startTick = BeatToTick(group[0].StartBeat);
                uint endTick = BeatToTick(group[^1].StartBeat + group[^1].DurationBeats);
                double startTime = BeatToTime(group[0].StartBeat);
                double endTime = BeatToTime(group[^1].StartBeat + group[^1].DurationBeats);

                var events = new List<LyricEvent>();
                foreach (var n in group)
                {
                    if (n.IsMelisma) continue;
                    if (string.IsNullOrWhiteSpace(n.Lyric)) continue;

                    var flags = n.IsFreestyle ? LyricSymbolFlags.NonPitched : LyricSymbolFlags.None;
                    events.Add(new LyricEvent(flags, FormatLyric(n.Lyric),
                        BeatToTime(n.StartBeat), BeatToTick(n.StartBeat)));
                }

                if (events.Count > 0)
                    phrases.Add(new LyricsPhrase(startTime, endTime - startTime,
                        startTick, endTick - startTick, events));
            }

            _lyricsTrack = new LyricsTrack(phrases);
            return _lyricsTrack;
        }

        public VocalsTrack LoadVocalsTrack(Instrument instrument)
        {
            if (instrument != Instrument.Vocals)
                throw new ArgumentException("UltraStar only supports Instrument.Vocals.", nameof(instrument));

            var parts = new List<VocalsPart> { BuildVocalsPart() };
            return new VocalsTrack(Instrument.Vocals, parts, new List<VocalsRangeShift>());
        }

        private VocalsPart BuildVocalsPart()
        {
            var phrases = new List<VocalsPhrase>();
            var textEvents = new List<TextEvent>();

            foreach (var group in GroupNotesIntoPhrases())
            {
                if (group.Count == 0) continue;
                var phrase = CreateVocalsPhrase(group);
                if (phrase != null) phrases.Add(phrase);
            }

            YargLogger.LogDebug($"[UltraStar] Built {phrases.Count} vocal phrases");
            return new VocalsPart(false, phrases, new List<VocalsPhrase>(), new List<Phrase>(), textEvents);
        }

        private List<List<UltraStarNote>> GroupNotesIntoPhrases()
        {
            // '-' is the main phrase separator in UltraStar.
            // Fallback threshold (32 beats) only for files without '-'.
            // There must be > the largest possible gap inside the phrase -
            const uint FALLBACK_GAP_THRESHOLD = 32;
            bool hasDashSeparators = _notes.Any(n => n.IsRest);

            var groups = new List<List<UltraStarNote>>();
            var currentGroup = new List<UltraStarNote>();
            uint lastEndBeat = 0;

            foreach (var note in _notes)
            {
                if (note.IsRest)
                {
                    if (currentGroup.Count > 0) { groups.Add(currentGroup); currentGroup = new(); }
                    lastEndBeat = note.StartBeat + note.DurationBeats;
                    continue;
                }

                // Fallback when '-' not in file
                if (!hasDashSeparators
                    && note.StartBeat > lastEndBeat + FALLBACK_GAP_THRESHOLD
                    && currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                    currentGroup = new();
                }

                currentGroup.Add(note);
                lastEndBeat = note.StartBeat + note.DurationBeats;
            }

            if (currentGroup.Count > 0) groups.Add(currentGroup);
            return groups;
        }

        private VocalsPhrase? CreateVocalsPhrase(List<UltraStarNote> phraseNotes)
        {
            if (phraseNotes.Count == 0) return null;

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

            foreach (var uNote in phraseNotes)
            {
                uint noteTick = BeatToTick(uNote.StartBeat);
                uint noteTickLen = uNote.DurationBeats * _ticksPerBeat;
                double noteTime = BeatToTime(uNote.StartBeat);
                double noteTimeLen = BeatsToSeconds(uNote.DurationBeats);

                bool isUnpitched = uNote.IsFreestyle;

                // Pitch conversion: UltraStar relative → MIDI absolute
                // Unpitched (freestyle, pitch=-1): pass -1 to VocalNote
                float midiPitch = isUnpitched ? -1f : (float) ToMidiPitch(uNote.Pitch);

                var childNote = new VocalNote(
                    midiPitch,
                    0,                   // harmonyPart: 0 = lead
                    VocalNoteType.Lyric,
                    noteTime,
                    noteTimeLen,
                    noteTick,
                    noteTickLen);

                parentNote.AddChildNote(childNote);

                if (!string.IsNullOrWhiteSpace(uNote.Lyric))
                {
                    var flags = isUnpitched ? LyricSymbolFlags.NonPitched : LyricSymbolFlags.None;
                    lyrics.Add(new LyricEvent(flags, FormatLyric(uNote.Lyric), noteTime, noteTick));
                }
            }

            if (parentNote.ChildNotes.Count == 0)
            {
                YargLogger.LogWarning($"[UltraStar] Phrase at tick {phraseStartTick} has 0 child notes — skipping");
                return null;
            }

            YargLogger.LogDebug($"[UltraStar] Phrase tick={phraseStartTick} " +
                $"notes={parentNote.ChildNotes.Count} lyrics={lyrics.Count} " +
                $"time={phraseStartTime:F2}s");

            return new VocalsPhrase(
                phraseStartTime, phraseTimeLen,
                phraseStartTick, phraseTickLen,
                parentNote, lyrics);
        }

        /// <summary>
        /// Clears lyric from UltraStar:
        /// trailing space = JoinWithNext (next syllable without spaces)
        /// - at the end = HyphenateWithNext
        /// ~ = melisma (handled above, shouldn't go here)
        /// </summary>
        private static string FormatLyric(string raw)
        {
            return raw.Trim();
        }

        public void DumpToLog()
        {
            YargLogger.LogDebug($"[UltraStar] BPM={_bpm} GAP={_gapMs}ms NOTES={_notes.Count}");

            var groups = GroupNotesIntoPhrases();
            YargLogger.LogDebug($"[UltraStar] Phrase groups: {groups.Count}");

            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                YargLogger.LogDebug($"[UltraStar] Phrase {gi}: {g.Count} notes, " +
                    $"beats {g[0].StartBeat}–{g[^1].StartBeat + g[^1].DurationBeats}, " +
                    $"time {BeatToTime(g[0].StartBeat):F3}s–{BeatToTime(g[^1].StartBeat + g[^1].DurationBeats):F3}s");

                foreach (var n in g)
                    YargLogger.LogDebug($"[UltraStar]   {n.Type} beat={n.StartBeat} dur={n.DurationBeats} " +
                        $"pitch={n.Pitch}→midi={ToMidiPitch(n.Pitch)} tick={BeatToTick(n.StartBeat)} " +
                        $"time={BeatToTime(n.StartBeat):F3}s lyric='{n.Lyric}' melisma={n.IsMelisma}");
            }
        }
    }
}
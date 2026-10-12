using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Chart.Loaders.UltraStar;
using YARG.Core.IO;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        #region Loading

        /// <param name="harmonyFile">
        /// A duet file merged with this solo one (see CacheHandler.ScanUltraStarCharts).
        /// When given, the sync track, solo Vocals chart and song-level lyrics still come
        /// from <paramref name="file"/>, but Harmony1-3 come from this file instead.
        /// </param>
        internal static MoonSongLoader LoadUltraStar(ParseSettings settings, FixedArray<byte> file, FixedArray<byte>? harmonyFile = null)
        {
            var soloLoader = new UltraStarLoader(file);
            var duetLoader = harmonyFile is { } harmonyData ? new UltraStarLoader(harmonyData) : null;
            var moonSong = ConvertUltraStarToMoonSong(soloLoader, duetLoader);

            return new MoonSongLoader(moonSong, settings) { _matchSlideChildrenByExactTick = true };
        }

        #endregion

        #region Conversion

        private static void AddPartToChart(IEnumerable<VocalsPart> parts, MoonChart chart, Func<uint, uint>? remapTick)
        {
            var allPhrases = new List<MoonPhrase>();
            var allText = new List<MoonText>();
            var allNotes = new List<MoonNote>();

            foreach (var part in parts)
            {
                foreach (var phrase in part.NotePhrases)
                {
                    var parent = phrase.PhraseParentNote;
                    var (phraseTick, phraseLength) = RemapTickRange(parent.Tick, parent.TickLength, remapTick);

                    allPhrases.Add(new MoonPhrase(phraseTick, phraseLength, MoonPhrase.Type.Vocals_ScoringPhrase));
                    // HARM2/3 static (and merged) lyrics are built from these, as with a .mid phrase note
                    allPhrases.Add(new MoonPhrase(phraseTick, phraseLength, MoonPhrase.Type.Vocals_StaticLyricPhrase));

                    foreach (var lyric in phrase.Lyrics)
                    {
                        uint lyricTick = remapTick?.Invoke(lyric.Tick) ?? lyric.Tick;
                        allText.Add(new MoonText($"lyric {lyric.Text}", lyricTick));
                    }

                    foreach (var child in parent.ChildNotes)
                    {
                        if (child.Type != VocalNoteType.Lyric && child.Type != VocalNoteType.Percussion)
                        {
                            continue;
                        }

                        // UltraStar freestyle (F), rap (R), and golden rap (G) notes are
                        // unpitched lyrical notes, NOT percussion. Keep as None so they stay
                        // VocalNoteType.Lyric downstream.
                        var flags = MoonNote.Flags.None;

                        int rawNote = child.IsNonPitched ? 0 : (int) child.Pitch;
                        var (noteTick, noteLength) = RemapTickRange(child.Tick, child.TickLength, remapTick);

                        allNotes.Add(new MoonNote(noteTick, rawNote, noteLength, flags));
                    }
                }
            }

            var allPhrasesList = new List<MoonPhrase>();

            var lyricPhrases = allPhrases
                .Where(p => p.type == MoonPhrase.Type.Vocals_ScoringPhrase || p.type == MoonPhrase.Type.Vocals_StaticLyricPhrase)
                .GroupBy(p => new { p.tick, p.type })
                .Select(g => g.OrderByDescending(p => p.length).First());
            allPhrasesList.AddRange(lyricPhrases);

            foreach (var part in parts)
            {
                foreach (var phrase in part.OtherPhrases)
                {
                    if (phrase.Type == PhraseType.StarPower)
                    {
                        var (spTick, spLength) = RemapTickRange(phrase.Tick, phrase.TickLength, remapTick);
                        allPhrasesList.Add(new MoonPhrase(spTick, spLength, MoonPhrase.Type.Starpower));
                    }
                }
            }

            foreach (var p in allPhrasesList.OrderBy(p => p.tick))
            {
                chart.Add(p);
            }

            var uniqueText = allText
                .OrderBy(t => t.tick)
                .GroupBy(t => t.tick)
                .Select(g => g.First());

            foreach (var t in uniqueText)
            {
                chart.Add(t);
            }

            var uniqueNotes = allNotes
                .OrderBy(n => n.tick)
                .ThenByDescending(n => n.length)
                .GroupBy(n => new { n.tick, n.vocalsPitch })
                .Select(g => g.First());

            foreach (var n in uniqueNotes)
            {
                chart.Add(n);
            }
        }

        /// <summary>
        /// Maps a tick range [tick, tick + tickLength) through <paramref name="remapTick"/>,
        /// mapping both endpoints rather than just scaling the length -- the map isn't
        /// necessarily linear once two different tempo maps are involved.
        /// </summary>
        private static (uint Tick, uint TickLength) RemapTickRange(uint tick, uint tickLength, Func<uint, uint>? remapTick)
        {
            if (remapTick == null)
            {
                return (tick, tickLength);
            }

            uint newStart = remapTick(tick);
            uint newEnd = remapTick(tick + tickLength);
            return (newStart, newEnd > newStart ? newEnd - newStart : 0);
        }

        /// <summary>
        /// SongChart.Lyrics (and the lipsync generated from it) is built from song-level text
        /// events, which a .mid fills from its vocals track (see MidReader) -- add the same
        /// events here, from the part the solo Vocals chart shows.
        /// </summary>
        private static void AddSongLyrics(MoonSong song, VocalsPart part)
        {
            foreach (var phrase in part.NotePhrases)
            {
                var parent = phrase.PhraseParentNote;
                song.InsertText(new MoonText(TextEvents.LYRIC_PHRASE_START, parent.Tick));
                foreach (var lyric in phrase.Lyrics)
                {
                    song.InsertText(new MoonText(TextEvents.LYRIC_PREFIX_WITH_SPACE + lyric.Text, lyric.Tick));
                }
                song.InsertText(new MoonText(TextEvents.LYRIC_PHRASE_END, parent.Tick + parent.TickLength));
            }
        }

        private static MoonSong ConvertUltraStarToMoonSong(UltraStarLoader loader, UltraStarLoader? duetLoader = null)
        {
            const uint RESOLUTION = 120;
            var moonSong = new MoonSong(RESOLUTION);

            // Sync track: always the solo loader's, even when merged -- the duet's notes get
            // retimed onto it below instead.
            var syncTrack = loader.LoadSyncTrack();
            foreach (var t in syncTrack.Tempos)
            {
                moonSong.AddTempo(t.BeatsPerMinute, t.Tick);
            }

            foreach (var ts in syncTrack.TimeSignatures)
            {
                moonSong.AddTimeSignature(ts.Numerator, ts.Denominator, ts.Tick);
            }

            // The loader whose voices become Harmony1-3: a merged entry's own duet file, or
            // this loader itself when it's a standalone duet (#PARTS >= 2) with no separate file.
            var harmonyLoader = duetLoader ?? loader;
            bool hasHarmony = harmonyLoader.VoiceCount >= 2;

            // A standalone duet's harmony voices already share this sync track (they're the
            // same file); a merged entry's duet file generally doesn't, so its ticks need
            // remapping onto the solo file's tempo map.
            Func<uint, uint>? remapDuetTick = duetLoader != null ? BuildDuetTickRemap(loader, duetLoader) : null;

            bool soloIsAlsoHarmonySource = duetLoader == null && hasHarmony;
            var soloVocalTrack = loader.LoadVocalsTrack(soloIsAlsoHarmonySource ? Instrument.Harmony : Instrument.Vocals);
            var soloChart = moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            if (soloVocalTrack.Parts.Count > 0)
            {
                // For a standalone duet, only show its first part in the solo Vocals chart.
                // All parts are still available separately in Harmony1/2/3.
                var soloParts = soloIsAlsoHarmonySource
                    ? new List<VocalsPart> { soloVocalTrack.Parts[0] }
                    : soloVocalTrack.Parts;
                AddPartToChart(soloParts, soloChart, null);
                AddSongLyrics(moonSong, soloVocalTrack.Parts[0]);
            }

            if (hasHarmony)
            {
                // Reuse the Harmony-mode load above when this loader is its own harmony
                // source, rather than parsing it a second time.
                var harmonyTrack = soloIsAlsoHarmonySource ? soloVocalTrack : harmonyLoader.LoadVocalsTrack(Instrument.Harmony);
                var harmonyInstruments = new[]
                {
                    MoonSong.MoonInstrument.Harmony1,
                    MoonSong.MoonInstrument.Harmony2,
                    MoonSong.MoonInstrument.Harmony3,
                };

                for (int i = 0; i < harmonyTrack.Parts.Count && i < harmonyInstruments.Length; i++)
                {
                    var chart = moonSong.GetChart(harmonyInstruments[i], MoonSong.Difficulty.Expert);
                    AddPartToChart(new[] { harmonyTrack.Parts[i] }, chart, remapDuetTick);
                }
            }

            return moonSong;
        }

        /// <summary>
        /// Builds a tick-mapping function from a duet file's tick space onto the solo file's,
        /// for a merged entry whose two files don't share a tempo map. Returns the identity
        /// map when they already agree, since GAP is baked into ticks and tick 0 = time 0
        /// for both (see UltraStarLoader.LoadSyncTrack) -- so ticks already line up.
        /// </summary>
        private static Func<uint, uint> BuildDuetTickRemap(UltraStarLoader soloLoader, UltraStarLoader duetLoader)
        {
            var soloSync = soloLoader.LoadSyncTrack();
            var duetSync = duetLoader.LoadSyncTrack();

            // Only a negative GAP becomes SongOffset (see ScanUltraStar). A positive GAP is
            // already baked into both files' ticks the same way, so it needs no correction
            // here -- but a negative one is clamped to tick 0 (see ApplyMetadataValue) and
            // loses that information, so it has to be reapplied as a time shift.
            UltraStarLoader.TryParseNumber(soloLoader.GetMetadata("GAP"), out double soloGapMs);
            UltraStarLoader.TryParseNumber(duetLoader.GetMetadata("GAP"), out double duetGapMs);
            double soloOffsetSeconds = Math.Min(soloGapMs, 0.0) / 1000.0;
            double duetOffsetSeconds = Math.Min(duetGapMs, 0.0) / 1000.0;
            double offsetDiffSeconds = duetOffsetSeconds - soloOffsetSeconds;

            if (offsetDiffSeconds == 0.0 && SyncTracksMatch(soloSync, duetSync))
            {
                return tick => tick;
            }

            return tick => soloSync.TimeToTick(duetSync.TickToTime(tick) + offsetDiffSeconds);
        }

        private static bool SyncTracksMatch(SyncTrack a, SyncTrack b)
        {
            if (a.Tempos.Count != b.Tempos.Count || a.TimeSignatures.Count != b.TimeSignatures.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Tempos.Count; i++)
            {
                if (a.Tempos[i].Tick != b.Tempos[i].Tick || a.Tempos[i].BeatsPerMinute != b.Tempos[i].BeatsPerMinute)
                {
                    return false;
                }
            }

            for (int i = 0; i < a.TimeSignatures.Count; i++)
            {
                if (a.TimeSignatures[i].Tick != b.TimeSignatures[i].Tick
                    || a.TimeSignatures[i].Numerator != b.TimeSignatures[i].Numerator
                    || a.TimeSignatures[i].Denominator != b.TimeSignatures[i].Denominator)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}

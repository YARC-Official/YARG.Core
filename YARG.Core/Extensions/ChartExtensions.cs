using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Core.Extensions
{
    public static class ChartExtensions
    {

        // Gets the StarPower sections from a list of instrument tracks, excluding a specific instrument and anything in its group
        public static void GetStarpowerSections<TNote>(this IEnumerable<InstrumentTrack<TNote>> tracks,
            ref List<List<EngineManager.StarPowerSection>> acceptedSpSections,
            Instrument instrument, uint tickTolerance) where TNote : Note<TNote>
        {
            var instrumentGroup = new List<Instrument>();
            foreach (var group in EngineManager.InstrumentGroups)
            {
                if (group.Contains(instrument))
                {
                    instrumentGroup = group;
                    break;
                }
            }
            foreach (var track in tracks)
            {
                if (instrumentGroup.Contains(track.Instrument))
                {
                    continue;
                }


                if (!TryGetAnyInstrumentDifficulty(track, out var instrumentDifficulty))
                {
                    continue;
                }

                var candidateSpSections = GetStarpowerSections(instrumentDifficulty);
                if (!SpListIsDuplicate(candidateSpSections, acceptedSpSections, tickTolerance))
                {
                    acceptedSpSections.Add(candidateSpSections);
                }
            }
        }

        // Gets the starpower sections from a single InstrumentTrack, excluding the given instrument
        public static void GetStarpowerSections<TNote>(this InstrumentTrack<TNote> track,
            ref List<List<EngineManager.StarPowerSection>> acceptedSpSections,
            Instrument instrument, uint tickTolerance) where TNote : Note<TNote>
        {
            var instrumentGroup = new List<Instrument>();
            foreach (var group in EngineManager.InstrumentGroups)
            {
                if (group.Contains(instrument))
                {
                    instrumentGroup = group;
                    break;
                }
            }

            if (instrumentGroup.Contains(track.Instrument))
            {
                return;
            }

            if (!TryGetAnyInstrumentDifficulty(track, out var instrumentDifficulty))
            {
                return;
            }

            var candidateSpSections = GetStarpowerSections(instrumentDifficulty);
            if (!SpListIsDuplicate(candidateSpSections, acceptedSpSections, tickTolerance))
            {
                acceptedSpSections.Add(candidateSpSections);
            }
        }

        // Gets the starpower sections from an InstrumentDifficulty
        public static List<EngineManager.StarPowerSection> GetStarpowerSections<TNote>(
            this InstrumentDifficulty<TNote> difficulty) where TNote : Note<TNote>
        {
            var spSections = new List<EngineManager.StarPowerSection>();
            foreach (var phrase in difficulty.Phrases)
            {
                if (phrase.Type == PhraseType.StarPower)
                {
                    spSections.Add(new EngineManager.StarPowerSection(phrase.Time, phrase.TimeEnd, phrase));
                }
            }
            return spSections;
        }

        /// <summary>
        /// Checks if the proposed list of star power sections is a duplicate of any of the accepted lists.<br /><br />
        /// <b>Note:</b> Pass a tick tolerance of zero if you want only exact matches
        /// </summary>
        /// <param name="proposed"></param>
        /// <param name="accepted"></param>
        /// <param name="tickTolerance"></param>
        /// <returns></returns>
        private static bool SpListIsDuplicate(List<EngineManager.StarPowerSection> proposed,
            List<List<EngineManager.StarPowerSection>> accepted, uint tickTolerance)
        {
            foreach (var sections in accepted)
            {
                if (proposed.Count != sections.Count)
                {
                    continue;
                }

                // Count is the same, so check for a fuzzy match
                bool isMatch = true;
                for (var i = 0; i < sections.Count; i++)
                {
                    if (!proposed[i].TickAlmostEquals(sections[i], tickTolerance))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool TryGetAnyInstrumentDifficulty<TNote>(
            this InstrumentTrack<TNote> instrumentTrack, out InstrumentDifficulty<TNote>? ret) where TNote : Note<TNote>
        {
            // We don't care what difficulty, so we return the first one we find
            foreach (var difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                if (instrumentTrack.TryGetDifficulty((Difficulty) difficulty, out var instrumentDifficulty))
                {
                    ret = instrumentDifficulty;
                    return true;
                }
            }

            ret = null;
            return false;
        }
    }
}
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
            Instrument instrument) where TNote : Note<TNote>
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
                var instrumentDifficulty = GetAnyInstrumentDifficulty(track);
                var candidateSpSections = GetStarpowerSections(instrumentDifficulty);
                if (!SpListIsDuplicate(candidateSpSections, acceptedSpSections))
                {
                    acceptedSpSections.Add(candidateSpSections);
                }
            }
        }

        // Gets the starpower sections from a single InstrumentTrack, excluding the given instrument
        public static void GetStarpowerSections<TNote>(this InstrumentTrack<TNote> track,
            ref List<List<EngineManager.StarPowerSection>> acceptedSpSections,
            Instrument instrument) where TNote : Note<TNote>
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
            var instrumentDifficulty = GetAnyInstrumentDifficulty(track);
            var candidateSpSections = GetStarpowerSections(instrumentDifficulty);
            if (!SpListIsDuplicate(candidateSpSections, acceptedSpSections))
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

        private static bool SpListIsDuplicate(List<EngineManager.StarPowerSection> proposed,
            List<List<EngineManager.StarPowerSection>> accepted)
        {
            foreach (var sections in accepted)
            {
                if (proposed.Count != sections.Count)
                {
                    continue;
                }

                // Count is the same, so it could be a dupe
                var dupeCount = 0;
                for (var i = 0; i < sections.Count; i++)
                {
                    if (!proposed[i].Equals(sections[i]))
                    {
                        break;
                    }
                    dupeCount++;
                }

                if (dupeCount == sections.Count)
                {
                    return true;
                }
            }
            return false;
        }

        private static InstrumentDifficulty<TNote> GetAnyInstrumentDifficulty<TNote>(this InstrumentTrack<TNote> instrumentTrack) where TNote : Note<TNote>
        {
            // We don't care what difficulty, so we return the first one we find
            foreach (var difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                if (instrumentTrack.TryGetDifficulty((Difficulty) difficulty, out var instrumentDifficulty))
                {
                    return instrumentDifficulty;
                }
            }

            return null;
        }
    }
}
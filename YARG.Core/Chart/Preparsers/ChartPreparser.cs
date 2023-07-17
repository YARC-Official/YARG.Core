using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace YARG.Core.Chart
{
    public static class ChartPreparser
    {
        private static readonly Regex ChartEventRegex =
            new Regex(@"(\d+)\s?=\s?[NSE]\s?((\d+\s?\d+)|\w+)", RegexOptions.Compiled);

        private static readonly Dictionary<string, Difficulty> DifficultyLookup = new()
        {
            { "Easy",   Difficulty.Easy   },
            { "Medium", Difficulty.Medium },
            { "Hard",   Difficulty.Hard   },
            { "Expert", Difficulty.Expert },
        };

        private static readonly Dictionary<string, Instrument> InstrumentLookup = new()
        {
            { "Single",        Instrument.FiveFretGuitar },
            { "DoubleGuitar",  Instrument.FiveFretCoopGuitar },
            { "DoubleBass",    Instrument.FiveFretBass },
            { "DoubleRhythm",  Instrument.FiveFretRhythm },
            { "Keyboard",      Instrument.Keys },

            { "GHLGuitar", Instrument.SixFretGuitar },
            { "GHLCoop",   Instrument.SixFretCoopGuitar },
            { "GHLBass",   Instrument.SixFretBass },
            { "GHLRhythm", Instrument.SixFretRhythm },

            { "Drums", Instrument.FourLaneDrums },
        };

        // TODO: A ulong is no longer enough to store all of the tracks
        public static bool GetAvailableTracks(byte[] chartData, out ulong tracks)
        {
            try
            {
                using var stream = new MemoryStream(chartData);
                using var reader = new StreamReader(stream);
                tracks = PreparseChart(reader);
                return true;
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, "Error reading available .chart tracks!");
                tracks = 0;
                return false;
            }
        }

        public static bool GetAvailableTracks(string filePath, out ulong tracks)
        {
            try
            {
                using var reader = File.OpenText(filePath);
                tracks = PreparseChart(reader);
                return true;
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, "Error reading available .chart tracks!");
                tracks = 0;
                return false;
            }
        }

        private static ulong PreparseChart(StreamReader reader)
        {
            ulong tracks = 0;

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Ignore non-header lines
                if (line[0] != '[' && line[^1] != ']')
                    continue;
                string headerName = line[1..^1];

                // Ensure section has a body
                if (reader.ReadLine()?.Trim() != "{")
                    continue;

                // Ensure section has at least one event
                string eventLine = reader.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(eventLine) || !ChartEventRegex.IsMatch(eventLine))
                    continue;

                // Get track/difficulty from header
                if (!GetTrackFromHeader(headerName, out var track))
                    continue;

                int shiftAmount = (int) track.instrument * 4 + (int) track.difficulty;
                tracks |= 1UL << shiftAmount;
            }

            return tracks;
        }

        private static bool GetTrackFromHeader(string header, out (Instrument instrument, Difficulty difficulty) track)
        {
            foreach (var (diffName, difficulty) in DifficultyLookup)
            {
                if (!header.StartsWith(diffName))
                    continue;

                foreach (var (instrumentName, instrument) in InstrumentLookup)
                {
                    if (!header.EndsWith(instrumentName))
                        continue;

                    track = (instrument, difficulty);
                    return true;
                }
            }

            track = ((Instrument) (-1), (Difficulty) (-1));
            return false;
        }
    }
}
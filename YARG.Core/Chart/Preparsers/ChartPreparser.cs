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

        private static readonly Dictionary<string, DifficultyMask> DifficultyLookup = new()
        {
            { "Easy",   DifficultyMask.Easy   },
            { "Medium", DifficultyMask.Medium },
            { "Hard",   DifficultyMask.Hard   },
            { "Expert", DifficultyMask.Expert },
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

        public static bool GetAvailableTracks(byte[] chartData, out AvailableParts parts)
        {
            try
            {
                using var stream = new MemoryStream(chartData);
                using var reader = new StreamReader(stream);
                parts = PreparseChart(reader);
                return true;
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, "Error reading available .chart tracks!");
                parts = new();
                return false;
            }
        }

        public static bool GetAvailableTracks(string filePath, out AvailableParts parts)
        {
            try
            {
                using var reader = File.OpenText(filePath);
                parts = PreparseChart(reader);
                return true;
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, "Error reading available .chart tracks!");
                parts = new();
                return false;
            }
        }

        private static AvailableParts PreparseChart(StreamReader reader)
        {
            var parts = new AvailableParts();

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

                parts.AddAvailableDifficulty(track.instrument, track.difficulty);
            }

            return parts;
        }

        private static bool GetTrackFromHeader(string header, out (Instrument instrument, DifficultyMask difficulty) track)
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

            track = default;
            return false;
        }
    }
}
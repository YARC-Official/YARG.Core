using System;
using System.Collections.Generic;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    /// <summary>
    /// Everything found in a shortname's songs_updates/&lt;shortname&gt; folder that can be applied
    /// to an ini-format song: the update midi (already supported), plus the update mogg, album art,
    /// and whatever DTA-declared audio channel/panning info exists for that shortname.
    /// </summary>
    internal struct IniUpdateInfo
    {
        public string? MidiPath;
        public string? MoggPath;
        public string? ImagePath;
        public DTAEntry Dta;
    }

    internal static class RBAudioCalculator
    {
        /// <summary>
        /// Reads channel indices/crowd channels out of <paramref name="dta"/> and computes the
        /// per-channel pan/volume values used to split a mogg into stems. Mirrors the math in
        /// RBCONEntry.ProcessDTAs, but standalone (ini entries don't carry the rest of the RBCON
        /// metadata that ProcessDTAs also touches).
        /// </summary>
        public static void Calculate(in DTAEntry dta, ref RBAudio<int> indices, ref RBAudio<float> panning)
        {
            if (dta.Indices != null)
            {
                indices = dta.Indices.Value;
            }

            // Explicit crowd_channels always wins over whatever indices.Crowd was
            if (dta.CrowdChannels != null)
            {
                indices.Crowd = dta.CrowdChannels;
            }

            var pans = dta.Pans;
            var volumes = dta.Volumes;
            if (pans == null || volumes == null)
            {
                return;
            }

            unsafe
            {
                var usedIndices = stackalloc bool[pans.Length];
                float[] CalculateStemValues(int[] stemIndices)
                {
                    float[] values = new float[2 * stemIndices.Length];
                    for (int i = 0; i < stemIndices.Length; i++)
                    {
                        int index = stemIndices[i];
                        if (index < 0 || index >= pans.Length || index >= volumes.Length)
                        {
                            continue;
                        }

                        float theta = (pans[index] + 1) * ((float) Math.PI / 4);
                        float volRatio = (float) Math.Pow(10, volumes[index] / 20);
                        values[2 * i] = volRatio * (float) Math.Cos(theta);
                        values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                        usedIndices[index] = true;
                    }
                    return values;
                }

                if (indices.Drums.Length  > 0) panning.Drums  = CalculateStemValues(indices.Drums);
                if (indices.Bass.Length   > 0) panning.Bass   = CalculateStemValues(indices.Bass);
                if (indices.Guitar.Length > 0) panning.Guitar = CalculateStemValues(indices.Guitar);
                if (indices.Keys.Length   > 0) panning.Keys   = CalculateStemValues(indices.Keys);
                if (indices.Vocals.Length > 0) panning.Vocals = CalculateStemValues(indices.Vocals);
                if (indices.Crowd.Length  > 0) panning.Crowd  = CalculateStemValues(indices.Crowd);

                var leftover = new List<int>(pans.Length);
                for (int i = 0; i < pans.Length; i++)
                {
                    if (!usedIndices[i])
                    {
                        leftover.Add(i);
                    }
                }

                if (leftover.Count > 0)
                {
                    indices.Track = leftover.ToArray();
                    panning.Track = CalculateStemValues(indices.Track);
                }
            }
        }
    }
}

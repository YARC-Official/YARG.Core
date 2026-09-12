using System;
using System.Collections.Generic;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    /// <summary>
    /// Single source of truth for turning DTA pan/vol/track data into the per-channel
    /// L/R gain values (<see cref="RBAudio{TType}"/> of float) used to split a mogg into
    /// stems. Previously duplicated inline in RBCONEntry.ProcessDTAs, in the ini
    /// update-mogg path, and in the loose-mogg sidecar parser.
    /// </summary>
    internal static class RBAudioCalculator
    {
        /// <summary>
        /// Core computation: given parallel pan/volume arrays (one entry per raw mogg
        /// channel) and the channel indices already assigned to each stem, computes
        /// L/R gain pairs per stem and folds any unclaimed channels into Track.
        /// Mutates <paramref name="indices"/>.Track in place.
        /// </summary>
        public static void Calculate(float[] pans, float[] volumes, ref RBAudio<int> indices, ref RBAudio<float> panning)
        {
            unsafe
            {
                var usedIndices = stackalloc bool[pans.Length];
                float[] CalculateStemValues(int[] stemIndices)
                {
                    var values = new float[2 * stemIndices.Length];
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

        /// <summary>
        /// Convenience overload for callers holding a single already-parsed DTAEntry
        /// (ini update-mogg / loose-mogg sidecar). Merges indices/crowd from the dta,
        /// then delegates to the raw-array overload above.
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

            if (dta.Pans == null || dta.Volumes == null)
            {
                return;
            }

            Calculate(dta.Pans, dta.Volumes, ref indices, ref panning);
        }
    }
}

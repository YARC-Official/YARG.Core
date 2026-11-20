using System;

namespace YARG.Core.Engine.Vocals
{
    public static class PartyVocalsDisplayPartResolver
    {
        /// <summary>
        /// Maximizes the distinct valid parts displayed by a group of microphones.
        /// Earlier microphones get first choice of distinct representatives, then lower
        /// part indices break ties. Surplus microphones use their lowest valid part.
        /// Zero masks produce -1. This selection has no effect on singing eligibility or score.
        /// </summary>
        public static void Resolve(ReadOnlySpan<uint> masks, Span<int> displayParts)
        {
            if (displayParts.Length < masks.Length)
                throw new ArgumentException("destination must contain one entry per microphone", nameof(displayParts));
            displayParts.Slice(0, masks.Length).Fill(-1);

            int required = MaximumDistinct(masks, 0);
            uint represented = 0;
            for (int mic = 0; mic < masks.Length && required > 0; mic++)
            {
                for (int part = 0; part < 32; part++)
                {
                    uint bit = 1u << part;
                    if ((masks[mic] & bit) == 0 || (represented & bit) != 0) continue;
                    // Do not take the only choice of a later microphone when an
                    // alternate match can preserve the maximum distinct count.
                    if (1 + MaximumDistinct(masks.Slice(mic + 1), represented | bit) < required) continue;
                    displayParts[mic] = part;
                    represented |= bit;
                    required--;
                    break;
                }
            }

            for (int mic = 0; mic < masks.Length; mic++)
            {
                if (displayParts[mic] >= 0) continue;
                for (int part = 0; part < 32; part++)
                {
                    if ((masks[mic] & (1u << part)) == 0) continue;
                    displayParts[mic] = part;
                    break;
                }
            }
        }

        private static int MaximumDistinct(ReadOnlySpan<uint> masks, uint unavailable)
        {
            Span<int> owners = stackalloc int[32];
            owners.Fill(-1);
            int count = 0;
            for (int mic = 0; mic < masks.Length; mic++)
            {
                uint visited = unavailable;
                if (TryMatch(masks, mic, owners, ref visited)) count++;
            }
            return count;
        }

        private static bool TryMatch(ReadOnlySpan<uint> masks, int mic, Span<int> owners, ref uint visited)
        {
            for (int part = 0; part < 32; part++)
            {
                uint bit = 1u << part;
                if ((masks[mic] & bit) == 0 || (visited & bit) != 0) continue;
                visited |= bit;
                if (owners[part] >= 0 && !TryMatch(masks, owners[part], owners, ref visited)) continue;
                owners[part] = mic;
                return true;
            }
            return false;
        }
    }
}

using System.Collections.Generic;

namespace YARG.Core.Chart
{
    public class VocalNote : Note
    {
        private readonly List<PitchTimePair> _pitchesOverTime;
        public IReadOnlyList<PitchTimePair> PitchesOverTime => _pitchesOverTime;

        public bool IsNonPitched => (_flags & NoteFlags.VocalNonPitched) != 0;

        public VocalNote(Note previousNote, double time, double timeLength, uint tick,
            uint tickLength, List<PitchTimePair> pitchesOverTime, NoteFlags flags)
            : base(previousNote, time, timeLength, tick, tickLength, flags)
        {
            _pitchesOverTime = pitchesOverTime;
        }

        public float PitchAtNormalizedTime(float normalizedTime)
        {
            int firstIndex = _pitchesOverTime.FindIndex(i => i.NormalizedTime > normalizedTime);

            // If an index was not found, it must mean it's outside of the note in the forward direction
            if (firstIndex == -1)
            {
                firstIndex = _pitchesOverTime.Count - 1;
            }

            // The second index must be after the first
            int secondIndex = firstIndex + 1;

            // If it's outside of the list, just clamp to the pitch of the last index
            if (secondIndex >= _pitchesOverTime.Count)
            {
                return _pitchesOverTime[^1].Pitch;
            }

            // Transform all of the points such that firstIndex's time is 0
            // Then transform the points such that the secondIndex is 1
            float offset = _pitchesOverTime[firstIndex].NormalizedTime;
            float secondTime = _pitchesOverTime[secondIndex].NormalizedTime - offset;
            normalizedTime = (normalizedTime - offset) / secondTime;

            // Now we can lerp!
            float firstPitch = _pitchesOverTime[firstIndex].Pitch;
            float secondPitch = _pitchesOverTime[secondIndex].Pitch;
            return firstPitch + (secondPitch - firstPitch) * normalizedTime;
        }

        public float? PitchAtSongTime(double time)
        {
            // If out of bounds, return null
            if (time < Time || time > TimeEnd)
            {
                return null;
            }

            // Otherwise, convert to normalized time and return
            time = (time - Time) / TimeLength;
            return PitchAtNormalizedTime((float) time);
        }
    }

    public readonly struct PitchTimePair
    {
        public readonly float NormalizedTime;
        public readonly float Pitch;

        public PitchTimePair(float normalizedTime, float pitch)
        {
            NormalizedTime = normalizedTime;
            Pitch = pitch;
        }
    }
}
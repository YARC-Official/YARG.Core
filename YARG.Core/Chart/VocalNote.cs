using System.Collections.Generic;
using System.Linq;

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

        // public float PitchAtNormalizedTime(float normalizedTime)
        // {
        // }
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
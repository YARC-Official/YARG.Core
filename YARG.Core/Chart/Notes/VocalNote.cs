using System;

namespace YARG.Core.Chart
{
    public class VocalNote : Note<VocalNote>
    {
        private readonly VocalNoteFlags _vocalFlags;

        // 0-based: harmony part 1 is 0, harmony part 2 is 1, harmony part 3 is 2, etc.
        public int HarmonyPart { get; }

        public float Pitch { get; }

        // Total between all of the pitches
        public double TotalTimeLength { get; private set; }
        public double TotalTimeEnd    => Time + TotalTimeLength;

        public uint TotalTickLength { get; private set; }
        public uint TotalTickEnd    => Tick + TotalTickLength;

        public bool IsNonPitched => (_vocalFlags & VocalNoteFlags.NonPitched) != 0;

        public VocalNote(float pitch, int harmonyPart, VocalNoteFlags vocalFlags, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            Pitch = pitch;
            HarmonyPart = harmonyPart;
            _vocalFlags = vocalFlags;
        }

        public float PitchAtSongTime(double time)
        {
            // Clamp to start
            if (time < TimeEnd || ChildNotes.Count < 1)
                return Pitch;

            // Search child notes
            var firstNote = this;
            for (int index = 0; index < ChildNotes.Count; index++)
            {
                var secondNote = ChildNotes[index];

                // Check note bounds
                if (time >= firstNote.Time && time < secondNote.TimeEnd)
                {
                    // Check if time is in a specific pitch
                    if (time < firstNote.TimeEnd)
                        return firstNote.Pitch;

                    if (time >= secondNote.Time)
                        return secondNote.Pitch;

                    // Time is between the two pitches, lerp them
                    double lerpStart = firstNote.TimeEnd;
                    double lerpEnd = secondNote.Time - lerpStart;
                    float lerpAmount = (float) ((time - lerpStart) / lerpEnd);
                    return firstNote.Pitch + (secondNote.Pitch - firstNote.Pitch) * lerpAmount;
                }

                firstNote = secondNote;
            }

            // Clamp to end
            return ChildNotes[^1].Pitch;
        }

        public override void AddChildNote(VocalNote note)
        {
            if (note.Tick <= Tick || note.ChildNotes.Count > 0)
                return;

            _childNotes.Add(note);

            // Sort child notes by tick
            _childNotes.Sort((note1, note2) =>
            {
                if (note1.Tick > note2.Tick)
                    return 1;
                else if (note1.Tick < note2.Tick)
                    return -1;
                return 0;
            });

            // Track total length
            TotalTimeLength = _childNotes[^1].TimeEnd - Time;
            TotalTickLength = _childNotes[^1].TickEnd - Tick;
        }
    }

    [Flags]
    public enum VocalNoteFlags
    {
        None = 0,

        NonPitched = 1 << 0,
        Percussion = 1 << 1,
    }
}
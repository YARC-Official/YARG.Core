using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A note on a vocals track.
    /// </summary>
    public class VocalNote : Note<VocalNote>
    {
        // private readonly VocalNoteFlags _vocalFlags; // Left for convenience later

        /// <summary>
        /// The type of vocals note (either a lyrical note or a percussion hit).
        /// </summary>
        public VocalNoteType Type { get; }

        /// <summary>
        /// 0-based index for the harmony part this note is a part of.
        /// HARM1 is 0, HARM2 is 1, HARM3 is 2.
        /// </summary>
        public int HarmonyPart { get; }

        /// <summary>
        /// The MIDI pitch of the note, as a float.
        /// -1 means the note is unpitched.
        /// </summary>
        public float Pitch { get; }

        /// <summary>
        /// The octave of the vocal pitch.
        /// Octaves start at -1 in MIDI: note 60 is C4, note 12 is C0, note 0 is C-1.
        /// </summary>
        public int Octave => (int) (Pitch / 12) - 1; 
        /// <summary>
        /// The pitch of the note wrapped relative to an octave (0-11).
        /// C is 0, B is 11. -1 means the note is unpitched.
        /// </summary>
        public float OctavePitch => Pitch % 12;

        /// <summary>
        /// The length of this note and all of its children, in seconds.
        /// </summary>
        public double TotalTimeLength { get; private set; }
        /// <summary>
        /// The time-based end of this note and all of its children.
        /// </summary>
        public double TotalTimeEnd    => Time + TotalTimeLength;

        /// <summary>
        /// The length of this note and all of its children, in ticks.
        /// </summary>
        public uint TotalTickLength { get; private set; }
        /// <summary>
        /// The tick-based end of this note and all of its children.
        /// </summary>
        public uint TotalTickEnd    => Tick + TotalTickLength;

        /// <summary>
        /// Whether or not this note is non-pitched.
        /// </summary>
        public bool IsNonPitched => Pitch < 0;
        /// <summary>
        /// Whether or not this note is a percussion note.
        /// </summary>
        public bool IsPercussion => Type is VocalNoteType.Percussion;

        /// <summary>
        /// Creates a new <see cref="VocalNote"/> with the given properties.
        /// </summary>
        public VocalNote(float pitch, int harmonyPart, VocalNoteType type, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            Type = type;
            Pitch = pitch;
            HarmonyPart = harmonyPart;
        }

        /// <summary>
        /// Gets the pitch of this note and its children at the specified time.
        /// Clamps to the start and end if the time is out of bounds.
        /// </summary>
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

        /// <inheritdoc/>
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

    /// <summary>
    /// Possible vocal note types.
    /// </summary>
    public enum VocalNoteType
    {
        Lyric,
        Percussion
    }

    /// <summary>
    /// Modifier flags for a vocal note.
    /// </summary>
    [Flags]
    public enum VocalNoteFlags
    {
        None = 0,
    }
}
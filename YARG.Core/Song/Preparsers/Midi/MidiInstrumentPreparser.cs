using System;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public abstract class MidiInstrument : MidiPreparser
    {
        protected int validations;
        protected override bool ParseNote()
        {
            if (ProcessSpecialNote())
                return false;

            if (IsNote())
                return ParseLaneColor();
            return ToggleExtraValues();
        }

        protected override bool ParseNote_Off()
        {
            return ProcessSpecialNote_Off() || ParseLaneColor_Off();
        }

        protected abstract bool ParseLaneColor();

        protected abstract bool ParseLaneColor_Off();

        protected virtual bool IsFullyScanned() { return validations == 15; }

        protected virtual bool IsNote() { return 60 <= note.value && note.value <= 100; }

        protected virtual bool ProcessSpecialNote() { return false; }

        protected virtual bool ProcessSpecialNote_Off() { return false; }

        protected virtual bool ToggleExtraValues() { return false; }

        protected void Validate(int diffIndex) { validations |= 1 << diffIndex; }

        public new static byte Preparse<Preparser>(YARGMidiReader reader)
            where Preparser : MidiInstrument, new()
        {
            Preparser preparser = new();
            return Preparse(preparser, reader);
        }

        public new static byte Preparse<Preparser>(Preparser preparser, YARGMidiReader reader)
            where Preparser : MidiInstrument
        {
            MidiPreparser.Preparse(preparser, reader);
            return (byte) preparser.validations;
        }
    }

    public abstract class MidiInstrument_Common : MidiInstrument
    {
        internal static readonly int[] DIFFVALUES = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
        };
        protected bool[] difficulties = new bool[4];
    }
}

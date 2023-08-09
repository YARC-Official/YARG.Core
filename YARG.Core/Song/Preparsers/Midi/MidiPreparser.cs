using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public abstract class MidiPreparser
    {
        protected static readonly byte[] SYSEXTAG = Encoding.ASCII.GetBytes("PS");
        protected MidiParseEvent currEvent;
        protected MidiNote note;

        protected abstract bool ParseNote();

        protected abstract bool ParseNote_Off();

        protected virtual void ParseSysEx(ReadOnlySpan<byte> str) { }

        protected virtual void ParseText(ReadOnlySpan<byte> str) { }

        public static bool Parse<Preparser>(YARGMidiReader reader)
            where Preparser : MidiPreparser, new()
        {
            return Parse(new Preparser(), reader);
        }

        protected static bool Parse<Preparser>(Preparser preparser, YARGMidiReader reader)
            where Preparser : MidiPreparser
        {
            bool complete = false;
            while (!complete && reader.TryParseEvent(ref preparser.currEvent))
            {
                if (preparser.currEvent.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref preparser.note);
                    complete = preparser.note.velocity > 0 ? preparser.ParseNote() : preparser.ParseNote_Off();
                }
                else if (preparser.currEvent.type == MidiEventType.Note_Off)
                {
                    reader.ExtractMidiNote(ref preparser.note);
                    complete = preparser.ParseNote_Off();
                }
                else if (preparser.currEvent.type == MidiEventType.SysEx || preparser.currEvent.type == MidiEventType.SysEx_End)
                    preparser.ParseSysEx(reader.ExtractTextOrSysEx());
                else if (preparser.currEvent.type <= MidiEventType.Text_EnumLimit)
                    preparser.ParseText(reader.ExtractTextOrSysEx());
            }
            return complete;
        }
    }
}

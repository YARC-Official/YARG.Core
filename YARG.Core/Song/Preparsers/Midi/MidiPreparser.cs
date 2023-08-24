using System;
using System.Text;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    public abstract class MidiPreparser
    {
        protected static readonly byte[] SYSEXTAG = Encoding.ASCII.GetBytes("PS");
        protected MidiParseEvent currEvent;
        protected MidiNote note;

        protected MidiPreparser() { }

        protected bool Process(YARGMidiReader reader)
        {
            while (reader.TryParseEvent(ref currEvent))
            {
                if (currEvent.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref note);
                    if (note.velocity > 0 ? ParseNote_ON() : ParseNote_Off())
                        return true;
                }
                else if (currEvent.type == MidiEventType.Note_Off)
                {
                    reader.ExtractMidiNote(ref note);
                    if (ParseNote_Off())
                        return true;
                }
                else if (currEvent.type == MidiEventType.SysEx || currEvent.type == MidiEventType.SysEx_End)
                    ParseSysEx(reader.ExtractTextOrSysEx());
                else if (currEvent.type <= MidiEventType.Text_EnumLimit)
                    ParseText(reader.ExtractTextOrSysEx());
            }
            return false;
        }

        protected abstract bool ParseNote_ON();

        protected abstract bool ParseNote_Off();

        protected virtual void ParseSysEx(ReadOnlySpan<byte> str) { }

        protected virtual void ParseText(ReadOnlySpan<byte> str) { }
    }
}

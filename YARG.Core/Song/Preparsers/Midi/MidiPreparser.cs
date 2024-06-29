using System;
using System.Text;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public abstract class Midi_Preparser
    {
        protected static readonly byte[] SYSEXTAG = Encoding.ASCII.GetBytes("PS");
        protected MidiNote note;

        protected Midi_Preparser() { }

        protected bool Process(YARGMidiTrack track)
        {
            while (track.ParseEvent())
            {
                switch (track.Type)
                {
                    case MidiEventType.Note_On:
                        track.ExtractMidiNote(ref note);
                        if (note.velocity > 0 ? ParseNote_ON(track) : ParseNote_Off(track))
                            return true;
                        break;
                    case MidiEventType.Note_Off:
                        track.ExtractMidiNote(ref note);
                        if (ParseNote_Off(track))
                            return true;
                        break;
                    case MidiEventType.SysEx:
                    case MidiEventType.SysEx_End:
                        ParseSysEx(track.ExtractTextOrSysEx());
                        break;
                    case >= MidiEventType.Text and <= MidiEventType.Text_EnumLimit:
                        ParseText(track.ExtractTextOrSysEx());
                        break;
                }
            }
            return false;
        }

        protected abstract bool ParseNote_ON(YARGMidiTrack track);

        protected abstract bool ParseNote_Off(YARGMidiTrack track);

        protected virtual void ParseSysEx(ReadOnlySpan<byte> str) { }

        protected virtual void ParseText(ReadOnlySpan<byte> str) { }
    }
}

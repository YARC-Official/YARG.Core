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
            while (track.ParseEvent(false))
            {
                if (track.Type == MidiEventType.Note_On)
                {
                    track.ExtractMidiNote(ref note);
                    if (note.velocity > 0 ? ParseNote_ON(track) : ParseNote_Off(track))
                        return true;
                }
                else if (track.Type == MidiEventType.Note_Off)
                {
                    track.ExtractMidiNote(ref note);
                    if (ParseNote_Off(track))
                        return true;
                }
                else if (track.Type == MidiEventType.SysEx || track.Type == MidiEventType.SysEx_End)
                    ParseSysEx(track.ExtractTextOrSysEx());
                else if (track.Type <= MidiEventType.Text_EnumLimit)
                    ParseText(track.ExtractTextOrSysEx());
                else
                    track.SkipEvent();
            }
            return false;
        }

        protected abstract bool ParseNote_ON(YARGMidiTrack track);

        protected abstract bool ParseNote_Off(YARGMidiTrack track);

        protected virtual void ParseSysEx(ReadOnlySpan<byte> str) { }

        protected virtual void ParseText(ReadOnlySpan<byte> str) { }
    }
}

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Song;

internal static class TestMidi
{
    /// <summary>
    /// Writes a .mid file with a 120 BPM tempo track and one track of the given notes, by MIDI note number.
    /// </summary>
    public static void Write(string path, short resolution, string trackName,
        params (long Tick, long Length, int Note)[] notes)
    {
        // At a shared tick, a note ends before the next one starts, unless it has length 0
        var events = new List<(long Tick, int Order, MidiEvent Event)>();
        foreach (var (tick, length, note) in notes)
        {
            events.Add((tick, 1, new NoteOnEvent((SevenBitNumber) (byte) note, (SevenBitNumber) 100)));
            events.Add((tick + length, length > 0 ? 0 : 2, new NoteOffEvent((SevenBitNumber) (byte) note, (SevenBitNumber) 0)));
        }

        var midi = new MidiFile(
            MakeTrack("TEMPO_TRACK", new List<(long, int, MidiEvent)>
            {
                (0, 0, new SetTempoEvent(TempoChange.BpmToMicroSeconds(120))),
                (0, 0, new TimeSignatureEvent(4, 4)),
            }),
            MakeTrack(trackName, events))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(resolution),
        };
        midi.Write(path, true);
    }

    private static TrackChunk MakeTrack(string name, List<(long Tick, int Order, MidiEvent Event)> events)
    {
        var chunk = new TrackChunk(new SequenceTrackNameEvent(name));
        long previousTick = 0;
        foreach (var (tick, _, midiEvent) in events.OrderBy(e => e.Tick).ThenBy(e => e.Order))
        {
            midiEvent.DeltaTime = tick - previousTick;
            previousTick = tick;
            chunk.Events.Add(midiEvent);
        }

        return chunk;
    }
}

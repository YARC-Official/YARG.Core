using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing;

public class HarmonyPhraseStartTests
{
    [Test]
    public void HarmonyPickupExtendsSharedPhraseWithoutMovingItsEndOrOverdriveMarker()
    {
        var midi = new MidiFile(new TrackChunk(),
            Track("HARM1", (0, 60, 105), (10, 30, 64),
                (120, 240, 105), (120, 240, 116), (140, 20, 64), (360, 240, 105), (380, 20, 64)),
            Track("HARM2", (75, 285, 105), (90, 420, 76)),
            Track("HARM3", (105, 405, 68)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480)
        };

        var harmony = SongChart.FromMidi(ParseSettings.Default_Midi, midi).Harmony;
        var lead = harmony.Parts[0].NotePhrases;
        Assert.That(lead.Select(phrase => (phrase.Tick, phrase.TickEnd)),
            Is.EqualTo(new[] { (0u, 60u), (90u, 360u), (360u, 600u) }));

        for (int partIndex = 1; partIndex <= 2; partIndex++)
        {
            var part = harmony.Parts[partIndex];
            var phrase = part.NotePhrases[0];
            Assert.That((phrase.Tick, phrase.TickEnd), Is.EqualTo((90u, 360u)));
            Assert.That(phrase.PhraseParentNote.IsStarPower, Is.True);
            Assert.That(part.OtherPhrases.Single(p => p.Type == PhraseType.StarPower).Tick, Is.EqualTo(120u));
            var sustain = phrase.PhraseParentNote.ChildNotes.Single();
            uint onset = partIndex == 1 ? 90u : 105u;
            Assert.That((sustain.Tick, sustain.TotalTickEnd), Is.EqualTo((onset, 510u)));
            Assert.That(phrase.Lyrics.Single().Tick, Is.EqualTo(onset));
            Assert.That(part.NotePhrases[1].PhraseParentNote.ChildNotes, Is.Empty);
        }
        Assert.That(harmony.Parts[1].StaticLyricPhrases[0].Tick, Is.EqualTo(75u));
    }

    private static TrackChunk Track(string name, params (long Tick, long Length, byte Pitch)[] notes)
    {
        var track = new TrackChunk(new SequenceTrackNameEvent(name));
        using var manager = track.ManageTimedEvents();
        foreach (var (tick, length, pitch) in notes)
        {
            manager.Objects.Add(new TimedEvent(new NoteOnEvent((SevenBitNumber)pitch, (SevenBitNumber)100), tick));
            manager.Objects.Add(new TimedEvent(new NoteOffEvent((SevenBitNumber)pitch, (SevenBitNumber)0), tick + length));
            if (pitch < 105)
                manager.Objects.Add(new TimedEvent(new Melanchall.DryWetMidi.Core.LyricEvent("Ah"), tick));
        }
        return track;
    }
}

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

    [Test]
    public void HarmonyTailExtendsPreviousPhraseOnlyThroughTheSlide()
    {
        var lead = Track("HARM1", (0, 2085, 105), (0, 2085, 116), (1785, 105, 63), (1920, 90, 62),
            (2100, 180, 63), (2415, 75, 58), (2610, 60, 51), (2805, 1000, 105), (2835, 105, 50));
        using (var events = lead.ManageTimedEvents())
        {
            var slide = events.Objects.Single(e => e.Time == 2610 &&
                e.Event is Melanchall.DryWetMidi.Core.LyricEvent);
            ((Melanchall.DryWetMidi.Core.LyricEvent)slide.Event).Text = "+";
        }

        var midi = new MidiFile(new TrackChunk(), lead,
            Track("HARM2", (0, 2085, 105), (1830, 210, 61)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480)
        };

        var harmony = SongChart.FromMidi(ParseSettings.Default_Midi, midi).Harmony;
        var phrases = harmony.Parts[0].NotePhrases;
        Assert.That(phrases.Select(phrase => (phrase.Tick, phrase.TickEnd)),
            Is.EqualTo(new[] { (0u, 2670u), (2805u, 3805u) }));
        var notes = phrases[0].PhraseParentNote.ChildNotes;
        Assert.That(notes.Select(note => note.Tick), Is.EqualTo(new[] { 1785u, 1920u, 2100u, 2415u }));
        Assert.That(notes[^1].ChildNotes.Single().Tick, Is.EqualTo(2610u));
        Assert.That(notes[^1].TotalTickEnd, Is.EqualTo(2670u));
        Assert.That(harmony.Parts[1].NotePhrases.Single().TickEnd, Is.EqualTo(2670u));
        Assert.That(harmony.Parts[1].OtherPhrases.Single(p => p.Type == PhraseType.StarPower).TickEnd,
            Is.EqualTo(2085u));
    }

    [Test]
    public void HarmonyTailLeavesASeparateNoteLaterInTheGapUnassigned()
    {
        var midi = new MidiFile(new TrackChunk(),
            Track("HARM1", (0, 480, 105), (330, 120, 63), (495, 60, 62), (810, 60, 65),
                (960, 480, 105), (990, 120, 64)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480)
        };

        var phrases = SongChart.FromMidi(ParseSettings.Default_Midi, midi).Harmony.Parts[0].NotePhrases;
        Assert.That(phrases.Select(phrase => (phrase.Tick, phrase.TickEnd)),
            Is.EqualTo(new[] { (0u, 555u), (960u, 1440u) }));
        Assert.That(phrases.SelectMany(phrase => phrase.PhraseParentNote.ChildNotes).Select(note => note.Tick),
            Is.EqualTo(new[] { 330u, 495u, 990u }));
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

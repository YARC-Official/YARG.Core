using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Core.UnitTests.Engine;

public class SustainListTests
{
    [Test]
    public void Add_ResizesListAndPreservesInsertionOrder()
    {
        var list = new SustainList<GuitarNote>(1);
        var first = new ActiveSustain<GuitarNote>(CreateNote(time: 1.0, tick: 100, tickLength: 120));
        var second = new ActiveSustain<GuitarNote>(CreateNote(time: 2.0, tick: 200, tickLength: 120));

        list.Add(first);
        list.Add(second);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(list, Has.Count.EqualTo(2));
            Assert.That(list[0].Note.Tick, Is.EqualTo(100));
            Assert.That(list[1].Note.Tick, Is.EqualTo(200));
        }
    }

    [Test]
    public void RemoveAt_ShiftsRemainingItemsDown()
    {
        var list = new SustainList<GuitarNote>(3)
        {
            new ActiveSustain<GuitarNote>(CreateNote(time: 1.0, tick: 100, tickLength: 120)),
            new ActiveSustain<GuitarNote>(CreateNote(time: 2.0, tick: 200, tickLength: 120)),
            new ActiveSustain<GuitarNote>(CreateNote(time: 3.0, tick: 300, tickLength: 120)),
        };

        list.RemoveAt(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(list, Has.Count.EqualTo(2));
            Assert.That(list[0].Note.Tick, Is.EqualTo(100));
            Assert.That(list[1].Note.Tick, Is.EqualTo(300));
        }
    }

    [Test]
    public void Clear_RemovesAllItems()
    {
        var list = new SustainList<GuitarNote>(1)
            { new ActiveSustain<GuitarNote>(CreateNote(time: 1.0, tick: 100, tickLength: 120)) };

        list.Clear();

        Assert.That(list, Is.Empty);
    }

    [Test]
    public void Enumerator_IteratesItemsInOrderAndResetStartsOver()
    {
        var list = new SustainList<GuitarNote>(2)
        {
            new ActiveSustain<GuitarNote>(CreateNote(time: 1.0, tick: 100, tickLength: 120)),
            new ActiveSustain<GuitarNote>(CreateNote(time: 2.0, tick: 200, tickLength: 120)),
        };

        using var enumerator = list.GetEnumerator();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current.Note.Tick, Is.EqualTo(100));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current.Note.Tick, Is.EqualTo(200));
        }

        Assert.That(enumerator.MoveNext(), Is.False);

        enumerator.Reset();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current.Note.Tick, Is.EqualTo(100));
        }
    }

    [Test]
    public void ActiveSustain_StartsWithDefaultTrackingState()
    {
        var note = CreateNote(time: 1.5, tick: 240, tickLength: 120);
        var sustain = new ActiveSustain<GuitarNote>(note);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sustain.Note, Is.SameAs(note));
            Assert.That(sustain.BaseTick, Is.EqualTo(note.Tick));
            Assert.That(sustain.BaseScore, Is.Zero);
            Assert.That(sustain.HasFinishedScoring, Is.False);
            Assert.That(sustain.IsLeniencyHeld, Is.False);
            Assert.That(sustain.LeniencyDropTime, Is.EqualTo(-9999));
        }
    }

    [Test]
    public void GetEndTime_ReturnsNoteTimeWhenBurstThresholdExceedsSustainLength()
    {
        var note = CreateNote(time: 2.5, tick: 480, tickLength: 120);
        var sustain = new ActiveSustain<GuitarNote>(note);
        var syncTrack = CreateSyncTrack();

        Assert.That(sustain
            .GetEndTime(syncTrack, sustainBurstThreshold: 240), Is.EqualTo(note.Time)
            .Within(0.0000001));
    }

    [Test]
    public void GetEndTime_UsesSyncTrackTickToTimeWhenBurstThresholdFitsInsideSustain()
    {
        var note = CreateNote(time: 1.0, tick: 480, tickLength: 240);
        var sustain = new ActiveSustain<GuitarNote>(note);
        var syncTrack = CreateSyncTrack();

        double endTime = sustain.GetEndTime(syncTrack, sustainBurstThreshold: 120);

        Assert.That(endTime, Is.EqualTo(0.625).Within(0.0000001));
    }

    private static GuitarNote CreateNote(double time, uint tick, uint tickLength)
    {
        double timeLength = tickLength / 480.0 * 0.5;
        return new GuitarNote(
            FiveFretGuitarFret.Green,
            GuitarNoteType.Strum,
            GuitarNoteFlags.None,
            NoteFlags.None,
            time,
            timeLength,
            tick,
            tickLength);
    }

    private static SyncTrack CreateSyncTrack()
    {
        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));
        return syncTrack;
    }
}

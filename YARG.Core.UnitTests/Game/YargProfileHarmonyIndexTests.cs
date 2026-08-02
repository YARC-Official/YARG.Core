using NUnit.Framework;
using YARG.Core.Game;

namespace YARG.Core.UnitTests.Game;

public class YargProfileHarmonyIndexTests
{
    private static YargProfile MakeHarmonyProfile(byte harmonyIndex)
    {
        var profile = new YargProfile
        {
            CurrentInstrument = Instrument.Harmony,
        };
        profile.HarmonyIndex = harmonyIndex;
        return profile;
    }

    [Test]
    public void ResolveClampsToHighestAvailablePart()
    {
        var profile = MakeHarmonyProfile(2); // HARM3

        profile.ResolveHarmonyIndex(2); // two-part song

        Assert.That(profile.HarmonyIndex, Is.EqualTo(1)); // HARM2
    }

    [Test]
    public void ResolveRestoresExplicitSelectionOnLargerSong()
    {
        var profile = MakeHarmonyProfile(2); // explicitly picked HARM3

        profile.ResolveHarmonyIndex(2); // looked at a two-part song
        profile.ResolveHarmonyIndex(3); // back to a three-part song

        Assert.That(profile.HarmonyIndex, Is.EqualTo(2)); // HARM3 remembered
    }

    [Test]
    public void ExplicitSelectionReplacesPreviousPreference()
    {
        var profile = MakeHarmonyProfile(2);

        profile.ResolveHarmonyIndex(2);
        profile.HarmonyIndex = 0; // player explicitly picks HARM1
        profile.ResolveHarmonyIndex(3);

        Assert.That(profile.HarmonyIndex, Is.EqualTo(0)); // not bounced back to HARM3
    }

    [Test]
    public void ResolveIgnoresNonPositivePartCount()
    {
        var profile = MakeHarmonyProfile(2);

        profile.ResolveHarmonyIndex(0);

        Assert.That(profile.HarmonyIndex, Is.EqualTo(2));
    }

    [Test]
    public void GetterStillGuardsNonHarmonyInstruments()
    {
        var profile = MakeHarmonyProfile(2);
        profile.CurrentInstrument = Instrument.Vocals;

        Assert.That(profile.HarmonyIndex, Is.EqualTo(0));
    }
}

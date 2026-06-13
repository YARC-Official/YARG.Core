using Newtonsoft.Json;
using NUnit.Framework;
using YARG.Core.Game;

namespace YARG.Core.UnitTests.Game;

public class YargProfileModifierTests
{
    private static YargProfile CreateVocalsProfile(string name)
    {
        return new YargProfile
        {
            Name = name,
            GameMode = GameMode.Vocals,
        };
    }

    private static YargProfile JsonRoundTrip(YargProfile profile)
    {
        var json = JsonConvert.SerializeObject(profile);
        return JsonConvert.DeserializeObject<YargProfile>(json)!;
    }

    [Test]
    public void ApplySessionModifiersChangesEffectiveModifiers()
    {
        var selector = CreateVocalsProfile("selector");
        selector.AddSingleModifier(Modifier.NoVocalPercussion);

        var other = CreateVocalsProfile("other");
        other.ApplySessionModifiers(selector);

        Assert.That(other.IsModifierActive(Modifier.NoVocalPercussion), Is.True);
    }

    [Test]
    public void ApplySessionModifiersDoesNotPersist()
    {
        var selector = CreateVocalsProfile("selector");

        var other = CreateVocalsProfile("other");
        other.AddSingleModifier(Modifier.UnpitchedOnly);
        other.ApplySessionModifiers(selector);

        using (Assert.EnterMultipleScope())
        {
            // Effective modifiers follow the selector for this session...
            Assert.That(other.IsModifierActive(Modifier.UnpitchedOnly), Is.False);
            // ...but the saved selection survives serialization.
            var reloaded = JsonRoundTrip(other);
            Assert.That(reloaded.IsModifierActive(Modifier.UnpitchedOnly), Is.True);
        }
    }

    [Test]
    public void RestoreSavedModifiersDiscardsSessionModifiers()
    {
        var selector = CreateVocalsProfile("selector");
        selector.AddSingleModifier(Modifier.NoVocalPercussion);

        var other = CreateVocalsProfile("other");
        other.AddSingleModifier(Modifier.UnpitchedOnly);
        other.ApplySessionModifiers(selector);

        // Mid-session, "other" runs with the selector's modifiers.
        Assert.That(other.IsModifierActive(Modifier.NoVocalPercussion), Is.True);
        Assert.That(other.IsModifierActive(Modifier.UnpitchedOnly), Is.False);

        other.RestoreSavedModifiers();

        using (Assert.EnterMultipleScope())
        {
            // Back to its own saved selection, with no trace of the session value.
            Assert.That(other.IsModifierActive(Modifier.UnpitchedOnly), Is.True);
            Assert.That(other.IsModifierActive(Modifier.NoVocalPercussion), Is.False);
        }
    }

    [Test]
    public void RestoreSavedModifiersIsIdempotentForUntouchedProfile()
    {
        var profile = CreateVocalsProfile("player");
        profile.AddSingleModifier(Modifier.NoVocalPercussion);

        profile.RestoreSavedModifiers();

        Assert.That(profile.IsModifierActive(Modifier.NoVocalPercussion), Is.True);
    }

    [Test]
    public void CopyModifiersStillPersists()
    {
        var selector = CreateVocalsProfile("selector");

        var other = CreateVocalsProfile("other");
        other.AddSingleModifier(Modifier.UnpitchedOnly);
        other.CopyModifiers(selector);

        var reloaded = JsonRoundTrip(other);
        Assert.That(reloaded.IsModifierActive(Modifier.UnpitchedOnly), Is.False);
    }

    [Test]
    public void ExplicitModifierEditsPersist()
    {
        var profile = CreateVocalsProfile("player");
        profile.AddSingleModifier(Modifier.NoVocalPercussion);
        profile.AddSingleModifier(Modifier.UnpitchedOnly);
        profile.RemoveModifiers(Modifier.NoVocalPercussion);

        var reloaded = JsonRoundTrip(profile);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.IsModifierActive(Modifier.UnpitchedOnly), Is.True);
            Assert.That(reloaded.IsModifierActive(Modifier.NoVocalPercussion), Is.False);
        }
    }

    [Test]
    public void DeserializationSeedsEffectiveModifiers()
    {
        var profile = CreateVocalsProfile("player");
        profile.AddSingleModifier(Modifier.NoVocalPercussion);

        var reloaded = JsonRoundTrip(profile);

        Assert.That(reloaded.IsModifierActive(Modifier.NoVocalPercussion), Is.True);
    }

    [Test]
    public void LegacyProfileJsonLoadsModifiers()
    {
        // Profiles written before the saved/effective split carry the modifiers
        // under the same "CurrentModifiers" property name.
        var profile = CreateVocalsProfile("player");
        var json = JsonConvert.SerializeObject(profile);
        Assert.That(json, Does.Contain("\"CurrentModifiers\""));

        var withModifier = json.Replace("\"CurrentModifiers\":0",
            $"\"CurrentModifiers\":{(ulong) Modifier.UnpitchedOnly}");
        Assert.That(withModifier, Is.Not.EqualTo(json), "test setup: expected to inject a modifier value");

        var reloaded = JsonConvert.DeserializeObject<YargProfile>(withModifier)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.IsModifierActive(Modifier.UnpitchedOnly), Is.True);
            // And it round-trips back out under the same name.
            var rewritten = JsonRoundTrip(reloaded);
            Assert.That(rewritten.IsModifierActive(Modifier.UnpitchedOnly), Is.True);
        }
    }
}

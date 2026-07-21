using NUnit.Framework;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class ScanExpectedTests
{
    [Test]
    public void SuccessValue_HasValueAndReturnsStoredResult()
    {
        var expected = new ScanExpected<int>(42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(expected.HasValue, Is.True);
            Assert.That(expected.Value, Is.EqualTo(42));
            Assert.That(expected.Error, Is.EqualTo(ScanResult.Success));
            Assert.That((bool) expected, Is.True);
        }
    }

    [Test]
    public void UnexpectedValue_HasNoValueAndExposesError()
    {
        var expected = new ScanExpected<int>(new ScanUnexpected(ScanResult.NoNotes));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(expected.HasValue, Is.False);
            Assert.That(expected.Error, Is.EqualTo(ScanResult.NoNotes));
            Assert.That((bool) expected, Is.False);
        }
    }

    [Test]
    public void UnexpectedValue_ValueThrowsInvalidOperationException()
    {
        var expected = new ScanExpected<int>(new ScanUnexpected(ScanResult.InvalidResolution));

        Assert.That(
            () => _ = expected.Value,
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void ImplicitConversions_CreateSuccessAndUnexpectedValues()
    {
        ScanExpected<string> success = "resolved";
        ScanExpected<string> failure = new ScanUnexpected(ScanResult.DirectoryError);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success.HasValue, Is.True);
            Assert.That(success.Value, Is.EqualTo("resolved"));
            Assert.That(success.Error, Is.EqualTo(ScanResult.Success));

            Assert.That(failure.HasValue, Is.False);
            Assert.That(failure.Error, Is.EqualTo(ScanResult.DirectoryError));
        }
    }
}

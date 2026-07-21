using System.Text;
using NUnit.Framework;
using YARG.Core.Engine;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.Engine;

public class HitWindowSettingsTests
{
    [Test]
    public void Constructor_NormalizesMinMaxAndClampsDynamicParameters()
    {
        var settings = new HitWindowSettings(
            maxWindow: 0.08,
            minWindow: 0.12,
            frontToBackRatio: 1.4,
            isDynamic: true,
            dwSlope: 1.5,
            dwScale: 0.1,
            dwGamma: 20,
            laneAutohitWindow: 0.6,
            laneProximityProtectionWindow: 0.6);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.MaxWindow, Is.EqualTo(0.12).Within(0.0000001));
            Assert.That(settings.MinWindow, Is.EqualTo(0.08).Within(0.0000001));
            Assert.That(settings.DynamicWindowSlope, Is.EqualTo(1.0).Within(0.0000001));
            Assert.That(settings.DynamicWindowScale, Is.EqualTo(0.3).Within(0.0000001));
            Assert.That(settings.DynamicWindowGamma, Is.EqualTo(10.0).Within(0.0000001));
            Assert.That(settings.Scale, Is.EqualTo(1.0).Within(0.0000001));
            Assert.That(settings.LaneAutohitWindow, Is.EqualTo(0.6).Within(0.0000001));
            Assert.That(settings.LaneProximityProtectionWindow, Is.EqualTo(0.6).Within(0.0000001));
        }
    }

    [Test]
    public void CalculateHitWindow_ReturnsMaxWindowWhenNotDynamic()
    {
        var settings = new HitWindowSettings(
            maxWindow: 0.14,
            minWindow: 0.08,
            frontToBackRatio: 1.0,
            isDynamic: false,
            dwSlope: 0.5,
            dwScale: 1.2,
            dwGamma: 1.1,
            laneAutohitWindow: 0.5,
            laneProximityProtectionWindow: 0.5);

        Assert.That(settings.CalculateHitWindow(0.001), Is.EqualTo(0.14).Within(0.0000001));
    }

    [Test]
    public void CalculateHitWindow_UsesDynamicCurveAndClampsToMinAndMax()
    {
        var settings = new HitWindowSettings(
            maxWindow: 0.16,
            minWindow: 0.08,
            frontToBackRatio: 1.0,
            isDynamic: true,
            dwSlope: 0.25,
            dwScale: 1.0,
            dwGamma: 1.0,
            laneAutohitWindow: 0.5,
            laneProximityProtectionWindow: 0.5);

        double atZero = settings.CalculateHitWindow(0);
        double mid = settings.CalculateHitWindow(0.08);
        double atLarge = settings.CalculateHitWindow(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(atZero, Is.EqualTo(settings.MinWindow).Within(0.0000001));
            Assert.That(mid, Is.GreaterThan(settings.MinWindow));
            Assert.That(mid, Is.LessThan(settings.MaxWindow));
            Assert.That(atLarge, Is.EqualTo(settings.MaxWindow).Within(0.0000001));
        }
    }

    [Test]
    public void FrontAndBackEnd_RespectRatioAndScale()
    {
        var settings = new HitWindowSettings(
            maxWindow: 0.10,
            minWindow: 0.05,
            frontToBackRatio: 1.2,
            isDynamic: false,
            dwSlope: 0.5,
            dwScale: 1.0,
            dwGamma: 1.0,
            laneAutohitWindow: 0.5,
            laneProximityProtectionWindow: 0.5)
        {
            Scale = 1.5
        };

        const double FULL_WINDOW = 0.10;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.GetFrontEnd(FULL_WINDOW), Is.EqualTo(-0.09).Within(0.0000001));
            Assert.That(settings.GetBackEnd(FULL_WINDOW), Is.EqualTo(0.06).Within(0.0000001));
        }
    }

    private static HitWindowSettings RoundTrip(HitWindowSettings settings, int version)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            settings.Serialize(writer);
        }

        byte[] bytes = stream.ToArray();
        using var buffer = FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(buffer.Span);

        var valueStream = new FixedArrayStream(buffer);
        return new HitWindowSettings(ref valueStream, version);
    }
}

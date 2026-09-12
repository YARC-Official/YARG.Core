using System;
using System.Collections.Generic;
using YARG.Core.Audio;

namespace YARG.Core.UnitTests.Song;

/// <summary>
/// Minimal no-op <see cref="AudioManager"/> so scan-level tests can exercise code paths
/// that call into <see cref="GlobalAudioHandler"/> (e.g. the SongLength-via-audio-duration
/// fallback in ScanUltraStar/ScanChart) without needing a real audio backend.
/// </summary>
internal sealed class NullAudioManager : AudioManager
{
    protected internal override ReadOnlySpan<string> SupportedFormats => ReadOnlySpan<string>.Empty;

    protected internal override StemMixer? CreateMixer(string name, float speed, double volume, bool clampStemVolume, bool normalize)
        => null;

    protected internal override List<InputDeviceInfo> GetAllInputDevices() => new();

    protected internal override MicDevice? CreateInputDevice(InputDeviceInfo device) => null;

    protected internal override OutputChannel? CreateOutputChannel(int channelId) => null;

    protected internal override List<(int id, string name)> GetAllOutputDevices() => new();

    protected internal override int GetOutputChannelCount() => 0;

    protected internal override void SetMasterVolume(double volume) { }

    public override void LoadVenueSample(string name, byte[] sampleData, OutputChannel? outputChannel = null) { }

    public override void ClearVenueSamples() { }

    protected internal override void PlayMetronomeSoundEffectToChannel(MetronomeSample sample, MetronomePitch pitch, int channelId) { }

    protected internal override bool SetOutputDevice(string name) => false;

    protected override void SetBufferLength_Internal(int length) { }
}

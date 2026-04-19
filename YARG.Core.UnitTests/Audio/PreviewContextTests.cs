using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Audio;

public class PreviewContextTests
{
    [Test]
    public async Task Create_ReturnsNullWhenDelayIsCancelledBeforeLoadingAudio()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var manager = new FakeAudioManager();
        var mixer = new FakeStemMixer(manager, length: 0.05);
        var entry = new TestPreviewSongEntry(() => mixer);

        var createTask = PreviewContext.Create(
            entry,
            volume: 1f,
            speed: 1f,
            delaySeconds: 30,
            fadeDuration: 0,
            cancellationTokenSource.Token);

        await cancellationTokenSource.CancelAsync();
        var context = await createTask;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context, Is.Null);
            Assert.That(entry.LoadPreviewAudioCallCount, Is.Zero);
            Assert.That(mixer.DisposeCount, Is.Zero);
        }
    }

    [Test]
    public async Task Create_DisposesMixerWhenCancelledAfterPreviewAudioLoads()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var manager = new FakeAudioManager();
        var mixer = new FakeStemMixer(manager, length: 0.05);
        var entry = new TestPreviewSongEntry(() =>
        {
            cancellationTokenSource.Cancel();
            return mixer;
        });

        var createTask = PreviewContext.Create(
            entry,
            volume: 1f,
            speed: 1f,
            delaySeconds: 0,
            fadeDuration: 0,
            cancellationTokenSource.Token);

        var context = await createTask;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context, Is.Null);
            Assert.That(entry.LoadPreviewAudioCallCount, Is.EqualTo(1));
            Assert.That(mixer.DisposeCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task WaitForCompletionAsync_WaitsForExternalCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var manager = new FakeAudioManager();
        var mixer = new FakeStemMixer(manager, length: 0.05);
        var entry = new TestPreviewSongEntry(() => mixer);

        var context = await PreviewContext.Create(
            entry,
            volume: 1f,
            speed: 1f,
            delaySeconds: 0,
            fadeDuration: 0,
            cancellationTokenSource.Token);

        Assert.That(context, Is.Not.Null);

        var stopTask = context!.WaitForCompletionAsync();
        // ReSharper disable once MethodSupportsCancellation
        await Task.Delay(25);

        Assert.That(stopTask.IsCompleted, Is.False);

        // ReSharper disable once MethodHasAsyncOverload
        cancellationTokenSource.Cancel();
        await stopTask;

        Assert.That(mixer.DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WaitForCompletionAsync_AllowsFadeOutAfterCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var manager = new FakeAudioManager();
        var mixer = new FakeStemMixer(manager, length: 0.5);
        var entry = new TestPreviewSongEntry(() => mixer);

        var context = await PreviewContext.Create(
            entry,
            volume: 1f,
            speed: 1f,
            delaySeconds: 0,
            fadeDuration: 0.05,
            cancellationTokenSource.Token);

        Assert.That(context, Is.Not.Null);

        var waitTask = context!.WaitForCompletionAsync();
        cancellationTokenSource.Cancel();

        await Task.Delay(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mixer.FadeOutCallCount, Is.EqualTo(1));
            Assert.That(waitTask.IsCompleted, Is.False);
        }

        await waitTask;

        Assert.That(mixer.DisposeCount, Is.EqualTo(1));
    }

    private sealed class TestPreviewSongEntry(Func<StemMixer?> loadPreviewAudio) : SongEntry
    {
        public int LoadPreviewAudioCallCount { get; private set; }

        public override EntryType SubType => EntryType.Ini;
        public override string SortBasedLocation => "test-location";
        public override string ActualLocation => "test-location";

        public override DateTime GetLastWriteTime() => DateTime.UnixEpoch;

        public override SongChart? LoadChart() => null;

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems) => null;

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            LoadPreviewAudioCallCount++;
            return loadPreviewAudio();
        }

        public override YARGImage? LoadAlbumData() => null;

        public override BackgroundResult? LoadBackground() => null;

        public override FixedArray<byte>? LoadMiloData() => null;
    }

    private sealed class FakeAudioManager : AudioManager
    {
        protected internal override ReadOnlySpan<string> SupportedFormats => [];

        protected internal override StemMixer? CreateMixer(string name, float speed, double volume, bool clampStemVolume, bool normalize) => null;

        protected internal override MicDevice? GetInputDevice(string name) => null;

        protected internal override List<(int id, string name)> GetAllInputDevices() => [];

        protected internal override MicDevice? CreateInputDevice(int deviceId, string name) => null;

        protected internal override OutputChannel? CreateOutputChannel(int channelId) => null;

        protected internal override OutputDevice? CreateOutputDevice(int deviceId, string name) => null;

        protected internal override List<(int id, string name)> GetAllOutputDevices() => [];

        protected internal override int GetOutputChannelCount() => 0;

        protected internal override OutputDevice? GetOutputDevice(string name) => null;

        protected internal override void SetMasterVolume(double volume) { }

        protected override void ToggleBuffer_Internal(bool enable) { }

        protected override void SetBufferLength_Internal(int length) { }
    }

    private sealed class FakeStemMixer : StemMixer
    {
        public FakeStemMixer(AudioManager manager, double length) : base("test-preview", manager, clampStemVolume: false)
        {
            _length = length;
        }

        public int DisposeCount { get; private set; }
        public int FadeOutCallCount { get; private set; }

        public override event Action SongEnd
        {
            add => _songEnd += value;
            remove => _songEnd -= value;
        }

        protected override int Play_Internal() => 0;

        protected override void FadeIn_Internal(double maxVolume, double duration) { }

        protected override void FadeOut_Internal(double duration)
        {
            FadeOutCallCount++;
        }

        protected override int Pause_Internal() => 0;

        protected override double GetPosition_Internal() => 0;

        protected override double GetVolume_Internal() => 1;

        protected override void SetPosition_Internal(double position) { }

        protected override void SetVolume_Internal(double volume) { }

        protected override int GetSampleData_Internal(float[] buffer) => 0;

        protected override int GetFFTData_Internal(float[] buffer, int fftSize, bool complex) => 0;

        protected override int GetLevel_Internal(float[] level) => 0;

        protected override void SetSpeed_Internal(float speed, bool shiftPitch) { }

        protected override bool AddChannels_Internal(Stream stream, params StemInfo[] stemInfos) => false;

        protected override bool RemoveChannel_Internal(SongStem stemToRemove) => false;

        protected override void ToggleBuffer_Internal(bool enable) { }

        protected override void SetBufferLength_Internal(int length) { }

        protected override void SetOutputChannel_Internal(OutputChannel? channel) { }

        protected override void SetOutputDevice_Internal(OutputDevice device) { }

        protected override void DisposeManagedResources()
        {
            DisposeCount++;
        }
    }
}

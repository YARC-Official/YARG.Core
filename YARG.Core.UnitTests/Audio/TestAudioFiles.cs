using System.Text;

namespace YARG.Core.UnitTests.Audio;

internal static class TestAudioFiles
{
    private const int SAMPLE_RATE = 8000;

    /// <summary>A PCM .wav (8 kHz mono 8-bit) whose header records exactly <paramref name="seconds"/>.</summary>
    public static byte[] Wav(double seconds)
    {
        var data = new byte[(int) (seconds * SAMPLE_RATE)];
        using var wav = new MemoryStream();
        using (var writer = new BinaryWriter(wav, Encoding.ASCII, true))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(4 + 8 + 16 + 8 + data.Length);
            writer.Write("WAVEfmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short) 1);    // PCM
            writer.Write((short) 1);    // mono
            writer.Write(SAMPLE_RATE);
            writer.Write(SAMPLE_RATE);  // byte rate
            writer.Write((short) 1);    // block align
            writer.Write((short) 8);    // bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(data.Length);
            writer.Write(data);
        }
        return wav.ToArray();
    }
}

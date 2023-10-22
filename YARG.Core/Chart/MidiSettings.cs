using System.Text;
using Melanchall.DryWetMidi.Core;

namespace YARG.Core.Chart
{
    public static class MidiSettings
    {
        public static readonly ReadingSettings Instance = new()
        {
            InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
            NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
            NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
            InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
            TextEncoding = Encoding.UTF8,
            // DecodeTextCallback = DecodeText,
        };

        // TODO: Use this to detect string encoding
        // private static string DecodeText(byte[] bytes, ReadingSettings settings)
        // {

        // }
	}
}
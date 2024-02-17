using System;
using YARG.Core.Utility;

namespace YARG.Core.Chart.Parsing
{
    internal static partial class DotChartParser
    {
        internal static uint ParseEventLine(ReadOnlySpan<char> line,
            out ReadOnlySpan<char> typeText, out ReadOnlySpan<char> eventText)
        {
            var tickText = line.SplitOnceTrimmedAscii('=', out eventText);
            if (!uint.TryParse(tickText, out uint tick))
                throw new Exception($"Failed to parse tick text: {tickText.ToString()}");

            typeText = eventText.SplitOnceTrimmedAscii(' ', out eventText);
            return tick;
        }

        internal static uint ReadEventInt32(ReadOnlySpan<char> text)
        {
            return uint.TryParse(text, out uint value)
                ? value
                : throw new Exception($"Failed to parse uint text: {text.ToString()}");
        }

        internal static ulong ReadEventInt64(ReadOnlySpan<char> text)
        {
            return ulong.TryParse(text, out ulong value)
                ? value
                : throw new Exception($"Failed to parse ulong text: {text.ToString()}");
        }

        internal static void ReadEventInt32Pair(ReadOnlySpan<char> text, out uint param1, out uint param2)
        {
            var param1Text = text.SplitOnceTrimmedAscii(' ', out var param2Text);
            param1 = ReadEventInt32(param1Text);
            param2 = ReadEventInt32(param2Text);
        }

        internal static void ReadEventInt32Pair(ReadOnlySpan<char> text, out uint param1, out uint? param2)
        {
            var param1Text = text.SplitOnceTrimmedAscii(' ', out var param2Text);
            param1 = ReadEventInt32(param1Text);
            param2 = param2Text.IsEmpty ? null : ReadEventInt32(param2Text);
        }

        internal static uint Pow(uint x, uint y)
        {
            if (y == 0)
                return 1;

            while (y > 1)
            {
                checked { x *= x; }
                y--;
            }

            return x;
        }
    }
}
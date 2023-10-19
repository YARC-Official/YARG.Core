using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGTextReader<TChar, TDecoder>
        where TChar : unmanaged, IConvertible
        where TDecoder : StringDecoder<TChar>, new()
    {
        private static char SkipWhitespace(YARGTextContainer<TChar> container)
        {
            while (container.Position < container.Length)
            {
                char ch = container.Current.ToChar(null);
                if (ch.IsAsciiWhitespace())
                {
                    if (ch == '\n')
                        return ch;
                }
                else if (ch != '=')
                    return ch;
                ++container.Position;
            }

            return (char)0;
        }

        public readonly YARGTextContainer<TChar> Container;
        public readonly TDecoder Decoder = new();

        public YARGTextReader(YARGTextContainer<TChar> container)
        {
            Container = container;

            SkipWhitespace(container);
            SetNextPointer();
            if (container.Current.ToChar(null) == '\n')
                GotoNextLine();
        }

        public char SkipWhitespace()
        {
            return SkipWhitespace(Container);
        }

        public void GotoNextLine()
        {
            char curr;
            do
            {
                Container.Position = Container.Next;
                if (Container.Position >= Container.Length)
                    break;

                Container.Position++;
                curr = SkipWhitespace(Container);

                if (Container.Position == Container.Length)
                    break;

                if (Container.Current.ToChar(null) == '{')
                {
                    Container.Position++;
                    curr = SkipWhitespace(Container);
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && Container[Container.Position + 1].ToChar(null) == '/');
        }

        public void SetNextPointer()
        {
            Container.Next = Container.Position;
            while (Container.Next < Container.Length && Container[Container.Next].ToChar(null) != '\n')
                ++Container.Next;
        }

        public ReadOnlySpan<TChar> PeekBasicSpan(int length)
        {
            return Container.GetSpan(Container.Position, length);
        }

        public string ExtractModifierName()
        {
            int curr = Container.Position;
            while (curr < Container.Length)
            {
                char b = Container[curr].ToChar(null);
                if (b.IsAsciiWhitespace() || b == '=')
                    break;
                ++curr;
            }

            var name = Container.GetSpan(Container.Position, curr - Container.Position);
            Container.Position = curr;
            SkipWhitespace(Container);
            return Decoder.Decode(name);
        }

        public string ExtractText(bool isChartFile)
        {
            return Decoder.ExtractText(Container, isChartFile);
        }

        public bool   ExtractBoolean() => YARGNumberExtractor.Boolean(Container);
        public short  ExtractInt16()   => YARGNumberExtractor.Int16(Container, SkipWhitespace);
        public ushort ExtractUInt16()  => YARGNumberExtractor.UInt16(Container, SkipWhitespace);
        public int    ExtractInt32()   => YARGNumberExtractor.Int32(Container, SkipWhitespace);
        public uint   ExtractUInt32()  => YARGNumberExtractor.UInt32(Container, SkipWhitespace);
        public long   ExtractInt64()   => YARGNumberExtractor.Int64(Container, SkipWhitespace);
        public ulong  ExtractUInt64()  => YARGNumberExtractor.UInt64(Container, SkipWhitespace);
        public float  ExtractFloat()   => YARGNumberExtractor.Float(Container, SkipWhitespace);
        public double ExtractDouble()  => YARGNumberExtractor.Double(Container, SkipWhitespace);

        public bool ExtractInt16(out short value)   => YARGNumberExtractor.Int16(Container, out value, SkipWhitespace);
        public bool ExtractUInt16(out ushort value) => YARGNumberExtractor.UInt16(Container, out value, SkipWhitespace);
        public bool ExtractInt32(out int value)     => YARGNumberExtractor.Int32(Container, out value, SkipWhitespace);
        public bool ExtractUInt32(out uint value)   => YARGNumberExtractor.UInt32(Container, out value, SkipWhitespace);
        public bool ExtractInt64(out long value)    => YARGNumberExtractor.Int64(Container, out value, SkipWhitespace);
        public bool ExtractUInt64(out ulong value)  => YARGNumberExtractor.UInt64(Container, out value, SkipWhitespace);
        public bool ExtractFloat(out float value)   => YARGNumberExtractor.Float(Container, out value, SkipWhitespace);
        public bool ExtractDouble(out double value) => YARGNumberExtractor.Double(Container, out value, SkipWhitespace);
    }
}

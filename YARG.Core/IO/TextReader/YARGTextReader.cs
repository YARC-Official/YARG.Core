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

            var name = Container.Slice(Container.Position, curr - Container.Position);
            Container.Position = curr;
            SkipWhitespace(Container);
            return Decoder.Decode(name);
        }

        public string ExtractText(bool isChartFile)
        {
            return Decoder.ExtractText(Container, isChartFile);
        }

        public bool   ExtractBoolean() => Container.ExtractBoolean(SkipWhitespace);
        public short  ExtractInt16()   => Container.ExtractInt16(SkipWhitespace);
        public ushort ExtractUInt16()  => Container.ExtractUInt16(SkipWhitespace);
        public int    ExtractInt32()   => Container.ExtractInt32(SkipWhitespace);
        public uint   ExtractUInt32()  => Container.ExtractUInt32(SkipWhitespace);
        public long   ExtractInt64()   => Container.ExtractInt64(SkipWhitespace);
        public ulong  ExtractUInt64()  => Container.ExtractUInt64(SkipWhitespace);
        public float  ExtractFloat()   => Container.ExtractFloat(SkipWhitespace);
        public double ExtractDouble()  => Container.ExtractDouble(SkipWhitespace);

        public bool ExtractInt16(out short value)   => Container.ExtractInt16(out value, SkipWhitespace);
        public bool ExtractUInt16(out ushort value) => Container.ExtractUInt16(out value, SkipWhitespace);
        public bool ExtractInt32(out int value)     => Container.ExtractInt32(out value, SkipWhitespace);
        public bool ExtractUInt32(out uint value)   => Container.ExtractUInt32(out value, SkipWhitespace);
        public bool ExtractInt64(out long value)    => Container.ExtractInt64(out value, SkipWhitespace);
        public bool ExtractUInt64(out ulong value)  => Container.ExtractUInt64(out value, SkipWhitespace);
        public bool ExtractFloat(out float value)   => Container.ExtractFloat(out value, SkipWhitespace);
        public bool ExtractDouble(out double value) => Container.ExtractDouble(out value, SkipWhitespace);
    }
}

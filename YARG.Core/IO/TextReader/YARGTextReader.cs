using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGTextReader<TChar>
        where TChar : unmanaged, IConvertible
    {
        private readonly YARGTextContainer<TChar> container;

        public YARGTextReader(YARGTextContainer<TChar> container)
        {
            this.container = container;

            SkipWhitespace();
            SetNextPointer();
            if (container.Current.ToChar(null) == '\n')
                GotoNextLine();
        }

        public char SkipWhitespace()
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

            return (char) 0;
        }

        public void GotoNextLine()
        {
            char curr;
            do
            {
                container.Position = container.Next;
                if (container.Position >= container.Length)
                    break;

                container.Position++;
                curr = SkipWhitespace();

                if (container.Position == container.Length)
                    break;

                if (container.Current.ToChar(null) == '{')
                {
                    container.Position++;
                    curr = SkipWhitespace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && container[container.Position + 1].ToChar(null) == '/');
        }

        public void SetNextPointer()
        {
            container.Next = container.Position;
            while (container.Next < container.Length && container[container.Next].ToChar(null) != '\n')
                ++container.Next;
        }

        public string ExtractModifierName<TDecoder>(TDecoder decoder)
            where TDecoder : StringDecoder<TChar>
        {
            int curr = container.Position;
            while (curr < container.Length)
            {
                char b = container[curr].ToChar(null);
                if (b.IsAsciiWhitespace() || b == '=')
                    break;
                ++curr;
            }

            var name = container.Slice(container.Position, curr - container.Position);
            container.Position = curr;
            SkipWhitespace();
            return decoder.Decode(name);
        }

        public string ExtractText<TDecoder>(TDecoder decoder, bool isChartFile)
            where TDecoder : StringDecoder<TChar>
        {
            return decoder.ExtractText(container, isChartFile);
        }

        public bool ExtractBoolean()
        {
            bool result = container.ExtractBoolean();
            SkipWhitespace();
            return result;
        }

        public short ExtractInt16()
        {
            short result = container.ExtractInt16();
            SkipWhitespace();
            return result;
        }

        public ushort ExtractUInt16()
        {
            ushort result = container.ExtractUInt16();
            SkipWhitespace();
            return result;
        }

        public int ExtractInt32()
        {
            int result = container.ExtractInt32();
            SkipWhitespace();
            return result;
        }

        public uint ExtractUInt32()
        {
            uint result = container.ExtractUInt32();
            SkipWhitespace();
            return result;
        }

        public long ExtractInt64()
        {
            long result = container.ExtractInt64();
            SkipWhitespace();
            return result;
        }

        public ulong ExtractUInt64()
        {
            ulong result = container.ExtractUInt64();
            SkipWhitespace();
            return result;
        }

        public float ExtractFloat()
        {
            float result = container.ExtractFloat();
            SkipWhitespace();
            return result;
        }

        public double ExtractDouble()
        {
            double result = container.ExtractDouble();
            SkipWhitespace();
            return result;
        }

        public bool ExtractInt16(out short value)
        {
            if (!container.ExtractInt16(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractUInt16(out ushort value)
        {
            if (!container.ExtractUInt16(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractInt32(out int value)
        {
            if (!container.ExtractInt32(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractUInt32(out uint value)
        {
            if (!container.ExtractUInt32(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractInt64(out long value)
        {
            if (!container.ExtractInt64(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractUInt64(out ulong value)
        {
            if (!container.ExtractUInt64(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractFloat(out float value)
        {
            if (!container.ExtractFloat(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractDouble(out double value)
        {
            if (!container.ExtractDouble(out value))
                return false;
            SkipWhitespace();
            return true;
        }
    }
}

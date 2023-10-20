using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGTextReader<TChar, TDecoder>
        where TChar : unmanaged, IConvertible
        where TDecoder : StringDecoder<TChar>, new()
    {
        public static char SkipWhitespace(YARGTextContainer<TChar> container)
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

        public bool ExtractBoolean()
        {
            bool result = Container.ExtractBoolean();
            SkipWhitespace(Container);
            return result;
        }

        public short ExtractInt16()
        {
            short result = Container.ExtractInt16();
            SkipWhitespace(Container);
            return result;
        }

        public ushort ExtractUInt16()
        {
            ushort result = Container.ExtractUInt16();
            SkipWhitespace(Container);
            return result;
        }

        public int ExtractInt32()
        {
            int result = Container.ExtractInt32();
            SkipWhitespace(Container);
            return result;
        }

        public uint ExtractUInt32()
        {
            uint result = Container.ExtractUInt32();
            SkipWhitespace(Container);
            return result;
        }

        public long ExtractInt64()
        {
            long result = Container.ExtractInt64();
            SkipWhitespace(Container);
            return result;
        }

        public ulong ExtractUInt64()
        {
            ulong result = Container.ExtractUInt64();
            SkipWhitespace(Container);
            return result;
        }

        public float ExtractFloat()
        {
            float result = Container.ExtractFloat();
            SkipWhitespace(Container);
            return result;
        }

        public double ExtractDouble()
        {
            double result = Container.ExtractDouble();
            SkipWhitespace(Container);
            return result;
        }

        public bool ExtractInt16(out short value)
        {
            if (!Container.ExtractInt16(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }

        public bool ExtractUInt16(out ushort value)
        {
            if (!Container.ExtractUInt16(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }

        public bool ExtractInt32(out int value)
        {
            if (!Container.ExtractInt32(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }

        public bool ExtractUInt32(out uint value)
        {
            if (!Container.ExtractUInt32(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }

        public bool ExtractInt64(out long value)
        {
            if (!Container.ExtractInt64(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }

        public bool ExtractUInt64(out ulong value)
        {
            if (!Container.ExtractUInt64(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }

        public bool ExtractFloat(out float value)
        {
            if (!Container.ExtractFloat(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }

        public bool ExtractDouble(out double value)
        {
            if (!Container.ExtractDouble(out value))
                return false;
            SkipWhitespace(Container);
            return true;
        }
    }
}

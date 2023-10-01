using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.IO.Ini
{
    public sealed class YARGIniReader<TType, TDecoder>
        where TType : unmanaged, IEquatable<TType>, IConvertible
        where TDecoder : IStringDecoder<TType>, new()
    {
        private readonly YARGTextReader<TType, TDecoder> reader;

        public YARGIniReader(IYARGTextReader reader)
        {
            this.reader = (YARGTextReader<TType, TDecoder>)reader;
        }

        public bool TrySection(out string section)
        {
            section = string.Empty;
            if (reader.IsEndOfFile())
                return false;

            if (!reader.IsCurrentCharacter('['))
            {
                SkipSection();
                if (reader.IsEndOfFile())
                    return false;
            }

            section = reader.Decode(reader.ExtractBasicSpan(reader.Next - reader.Position)).TrimEnd().ToLower();
            return true;
        }

        public void SkipSection()
        {
            reader.GotoNextLine();
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position)
                {
                    char character = reader.Data[point].ToChar(null);
                    if (!character.IsAsciiWhitespace() || character == '\n')
                        break;
                    --point;
                }

                if (reader.Data[point].ToChar(null) == '\n')
                {
                    reader.Position = position + next;
                    reader.SetNextPointer();
                    return;
                }

                position += next + 1;
            }

            reader.Position = reader.Length;
            reader.SetNextPointer();
        }

        public bool IsStillCurrentSection()
        {
            return !reader.IsEndOfFile() && !reader.IsCurrentCharacter('[');
        }

        public IniSection ExtractModifiers(ref Dictionary<string, IniModifierCreator> validNodes)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            reader.GotoNextLine();
            while (IsStillCurrentSection())
            {
                string name = reader.ExtractModifierName().ToLower();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(reader);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
                reader.GotoNextLine();
            }
            return new IniSection(modifiers);
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = reader.Length - position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (reader.Data[position + i].ToChar(null) == '[')
                    return true;
                ++i;
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.IO.Ini
{
    public static class YARGIniReader<TType, TDecoder>
        where TType : unmanaged, IEquatable<TType>, IConvertible
        where TDecoder : IStringDecoder<TType>, new()
    {
        public static Dictionary<string, IniSection> ProcessIni(IYARGTextReader baseReader, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
        {
            var textReader = (YARGTextReader<TType, TDecoder>) baseReader;

            Dictionary<string, IniSection> modifierMap = new();
            while (TrySection(textReader, out string section))
            {
                if (sections.TryGetValue(section, out var nodes))
                    modifierMap[section] = ExtractModifiers(textReader, ref nodes);
                else
                    SkipSection(textReader);
            }
            return modifierMap;
        }

        private static bool TrySection(YARGTextReader<TType, TDecoder> reader, out string section)
        {
            section = string.Empty;
            if (reader.IsEndOfFile())
                return false;

            if (!reader.IsCurrentCharacter('['))
            {
                SkipSection(reader);
                if (reader.IsEndOfFile())
                    return false;
            }

            section = reader.Decode(reader.PeekBasicSpan(reader.Next - reader.Position)).TrimEnd().ToLower();
            return true;
        }

        private static void SkipSection(YARGTextReader<TType, TDecoder> reader)
        {
            reader.GotoNextLine();
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(reader, position, out int next))
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

        private static bool IsStillCurrentSection(YARGTextReader<TType, TDecoder> reader)
        {
            return !reader.IsEndOfFile() && !reader.IsCurrentCharacter('[');
        }

        private static IniSection ExtractModifiers(YARGTextReader<TType, TDecoder> reader, ref Dictionary<string, IniModifierCreator> validNodes)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            reader.GotoNextLine();
            while (IsStillCurrentSection(reader))
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

        private static bool GetDistanceToTrackCharacter(YARGTextReader<TType, TDecoder> reader, int position, out int i)
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

using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.IO.Ini
{
    public static class YARGIniReader
    {
        public static Dictionary<string, IniSection> ProcessIni<TChar, TDecoder>(YARGTextReader<TChar> textReader, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
        {
            TDecoder decoder = new();
            Dictionary<string, IniSection> modifierMap = new();
            while (TrySection(textReader, decoder, out string section))
            {
                if (sections.TryGetValue(section, out var nodes))
                    modifierMap[section] = ExtractModifiers(textReader, decoder, ref nodes);
                else
                    SkipSection(textReader);
            }
            return modifierMap;
        }

        private static bool TrySection<TChar, TDecoder>(YARGTextReader<TChar> reader, TDecoder decoder, out string section)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>
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

            section = decoder.Decode(reader.PeekBasicSpan(reader.Next - reader.Position)).TrimEnd().ToLower();
            return true;
        }

        private static void SkipSection<TChar>(YARGTextReader<TChar> reader)
            where TChar : unmanaged, IConvertible
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

        private static bool IsStillCurrentSection<TChar>(YARGTextReader<TChar> reader)
            where TChar : unmanaged, IConvertible
        {
            return !reader.IsEndOfFile() && !reader.IsCurrentCharacter('[');
        }

        private static IniSection ExtractModifiers<TChar, TDecoder>(YARGTextReader<TChar> reader, TDecoder decoder, ref Dictionary<string, IniModifierCreator> validNodes)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            reader.GotoNextLine();
            while (IsStillCurrentSection(reader))
            {
                string name = decoder.ExtractModifierName(reader).ToLower();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(reader, decoder);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
                reader.GotoNextLine();
            }
            return new IniSection(modifiers);
        }

        private static bool GetDistanceToTrackCharacter<TChar>(YARGTextReader<TChar> reader, int position, out int i)
            where TChar : unmanaged, IConvertible
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

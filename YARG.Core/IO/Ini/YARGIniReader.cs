using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.IO.Ini
{
    public static class YARGIniReader
    {
        public static Dictionary<string, IniSection> ReadIniFile(string iniFile, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(iniFile);
                var byteReader = YARGTextLoader.TryLoadByteText(bytes);
                if (byteReader != null)
                    return ProcessIni(byteReader, sections);

                var charReader = YARGTextLoader.LoadCharText(bytes);
                return ProcessIni(charReader, sections);

            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, ex.Message);
                return new();
            }
        }

        private static Dictionary<string, IniSection> ProcessIni<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
        {
            Dictionary<string, IniSection> modifierMap = new();
            while (TrySection(reader, out string section))
            {
                if (sections.TryGetValue(section, out var nodes))
                    modifierMap[section] = ExtractModifiers(reader, ref nodes);
                else
                    SkipSection(reader);
            }
            return modifierMap;
        }

        private static bool TrySection<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, out string section)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
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
            section = reader.ExtractLine().ToLower();
            return true;
        }

        private static void SkipSection<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
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

        private static IniSection ExtractModifiers<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, ref Dictionary<string, IniModifierCreator> validNodes)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
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

        private static bool IsStillCurrentSection<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
        {
            return !reader.IsEndOfFile() && !reader.IsCurrentCharacter('[');
        }

        private static bool GetDistanceToTrackCharacter<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, int position, out int i)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
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

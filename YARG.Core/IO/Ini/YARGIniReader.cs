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
                var byteReader = YARGTextContainer.TryLoadByteText(bytes);
                if (byteReader != null)
                    return ProcessIni<byte, ByteStringDecoder>(byteReader, sections);

                var charReader = YARGTextContainer.LoadCharText(bytes);
                return ProcessIni<char, CharStringDecoder>(charReader, sections);

            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, ex.Message);
                return new();
            }
        }

        private static Dictionary<string, IniSection> ProcessIni<TChar, TDecoder>(YARGTextContainer<TChar> container, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>, new()
        {
            YARGIniReader<TChar, TDecoder> iniReader = new(container);
            Dictionary<string, IniSection> modifierMap = new();
            while (iniReader.TrySection(out string section))
            {
                if (sections.TryGetValue(section, out var nodes))
                    modifierMap[section] = iniReader.ExtractModifiers(ref nodes);
                else
                    iniReader.SkipSection();
            }
            return modifierMap;
        }
    }
    public sealed class YARGIniReader<TChar, TDecoder>
        where TChar : unmanaged, IConvertible
        where TDecoder : StringDecoder<TChar>, new()
    {
        private readonly YARGTextContainer<TChar> container;
        private readonly YARGTextReader<TChar, TDecoder> reader;

        public YARGIniReader(YARGTextContainer<TChar> container)
        {
            this.container = container;
            reader = new YARGTextReader<TChar, TDecoder>(container);
        }

        public bool TrySection(out string section)
        {
            section = string.Empty;
            if (container.IsEndOfFile())
                return false;

            if (!container.IsCurrentCharacter('['))
            {
                SkipSection();
                if (container.IsEndOfFile())
                    return false;
            }

            section = reader.Decoder.Decode(reader.PeekBasicSpan(container.Next - container.Position)).TrimEnd().ToLower();
            return true;
        }

        public void SkipSection()
        {
            reader.GotoNextLine();
            int position = container.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position)
                {
                    char character = container[point].ToChar(null);
                    if (!character.IsAsciiWhitespace() || character == '\n')
                        break;
                    --point;
                }

                if (container[point].ToChar(null) == '\n')
                {
                    container.Position = position + next;
                    reader.SetNextPointer();
                    return;
                }

                position += next + 1;
            }

            container.Position = container.Length;
            reader.SetNextPointer();
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

        private bool IsStillCurrentSection()
        {
            return !container.IsEndOfFile() && !container.IsCurrentCharacter('[');
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = container.Length - position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (container[position + i].ToChar(null) == '[')
                    return true;
                ++i;
            }
            return false;
        }
    }
}

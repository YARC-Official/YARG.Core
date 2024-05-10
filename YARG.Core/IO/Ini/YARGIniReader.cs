using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Logging;

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
                {
                    return ProcessIni(byteReader.Container, byteReader.Decoder, sections);
                }

                var charReader = YARGTextLoader.LoadCharText(bytes);
                return ProcessIni(charReader.Container, charReader.Decoder, sections);

            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, ex.Message);
                return new();
            }
        }

        private static Dictionary<string, IniSection> ProcessIni<TChar, TDecoder>(YARGTextContainer<TChar> container, TDecoder decoder, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            Dictionary<string, IniSection> modifierMap = new();
            while (TrySection(container, decoder, out string section))
            {
                if (sections.TryGetValue(section, out var nodes))
                    modifierMap[section] = ExtractModifiers(container, decoder, ref nodes);
                else
                    YARGTextReader.SkipLinesUntil(container, '[');
            }
            return modifierMap;
        }

        private static bool TrySection<TChar, TDecoder>(YARGTextContainer<TChar> container, TDecoder decoder, out string section)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            section = string.Empty;
            if (container.IsEndOfFile())
                return false;

            if (!container.IsCurrentCharacter('['))
            {
                YARGTextReader.SkipLinesUntil(container, '[');
                if (container.IsEndOfFile())
                    return false;
            }
            section = YARGTextReader.PeekLine(container, decoder).ToLower();
            return true;
        }

        private static IniSection ExtractModifiers<TChar, TDecoder>(YARGTextContainer<TChar> container, TDecoder decoder, ref Dictionary<string, IniModifierCreator> validNodes)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            YARGTextReader.GotoNextLine(container);
            while (IsStillCurrentSection(container))
            {
                string name = YARGTextReader.ExtractModifierName(container, decoder).ToLower();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(container, decoder);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
                YARGTextReader.GotoNextLine(container);
            }
            return new IniSection(modifiers);
        }

        private static bool IsStillCurrentSection<TChar>(YARGTextContainer<TChar> continer)
            where TChar : unmanaged, IConvertible
        {
            return !continer.IsEndOfFile() && !continer.IsCurrentCharacter('[');
        }
    }
}

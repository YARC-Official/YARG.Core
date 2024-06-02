using System;
using System.Collections.Generic;
using System.IO;
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
                var byteContainer = YARGTextReader.TryLoadByteText(bytes);
                if (byteContainer.HasValue)
                {
                    unsafe
                    {
                        return ProcessIni(byteContainer.Value, &StringDecoder.Decode, sections);
                    }
                }

                var charContainer = YARGTextReader.LoadCharText(bytes);
                unsafe
                {
                    return ProcessIni(charContainer, &StringDecoder.Decode, sections);
                }

            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, ex.Message);
                return new();
            }
        }

        private static unsafe Dictionary<string, IniSection> ProcessIni<TChar>(YARGTextContainer<TChar> container, delegate*<TChar[], int, int, string> decoder, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
            where TChar : unmanaged, IConvertible
        {
            Dictionary<string, IniSection> modifierMap = new();
            while (TrySection(ref container, decoder, out string section))
            {
                if (sections.TryGetValue(section, out var nodes))
                    modifierMap[section] = ExtractModifiers(ref container, decoder, ref nodes);
                else
                    YARGTextReader.SkipLinesUntil(ref container, '[');
            }
            return modifierMap;
        }

        private static unsafe bool TrySection<TChar>(ref YARGTextContainer<TChar> container, delegate*<TChar[], int, int, string> decoder, out string section)
            where TChar : unmanaged, IConvertible
        {
            section = string.Empty;
            if (container.IsEndOfFile())
            {
                return false;
            }

            if (!container.IsCurrentCharacter('['))
            {
                if (!YARGTextReader.SkipLinesUntil(ref container, '['))
                {
                    return false;
                }
            }
            section = YARGTextReader.PeekLine(ref container, decoder).ToLower();
            return true;
        }

        private static unsafe IniSection ExtractModifiers<TChar>(ref YARGTextContainer<TChar> container, delegate*<TChar[], int, int, string> decoder, ref Dictionary<string, IniModifierCreator> validNodes)
            where TChar : unmanaged, IConvertible
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            while (IsStillCurrentSection(ref container))
            {
                string name = YARGTextReader.ExtractModifierName(ref container, decoder).ToLower();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(ref container, decoder);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
            }
            return new IniSection(modifiers);
        }

        private static bool IsStillCurrentSection<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            YARGTextReader.GotoNextLine(ref container);
            return !container.IsEndOfFile() && !container.IsCurrentCharacter('[');
        }
    }
}

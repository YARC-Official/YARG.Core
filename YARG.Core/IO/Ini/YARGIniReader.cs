using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO.Disposables;
using YARG.Core.Logging;

namespace YARG.Core.IO.Ini
{
    public static unsafe class YARGIniReader
    {
        public static Dictionary<string, IniSection> ReadIniFile(FileInfo iniFile, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
        {
            try
            {
                using var bytes = MemoryMappedArray.Load(iniFile);
                if (YARGTextReader.TryLoadByteText(bytes, out var byteContainer))
                {
                    return ProcessIni(ref byteContainer, &StringDecoder.Decode, sections);
                }

                using var chars = YARGTextReader.ConvertToChar(bytes);
                var charContainer = new YARGTextContainer<char>(chars, 0);
                return ProcessIni(ref charContainer, &StringDecoder.Decode, sections);

            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, ex.Message);
                return new();
            }
        }

        private static Dictionary<string, IniSection> ProcessIni<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
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

        private static bool TrySection<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder, out string section)
            where TChar : unmanaged, IConvertible
        {
            section = string.Empty;
            if (container.IsAtEnd())
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

        private static IniSection ExtractModifiers<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder, ref Dictionary<string, IniModifierCreator> validNodes)
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
            return !container.IsAtEnd() && !container.IsCurrentCharacter('[');
        }
    }
}

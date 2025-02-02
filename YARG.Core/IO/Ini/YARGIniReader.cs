using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.IO.Ini
{
    public static class YARGIniReader
    {
        public unsafe static Dictionary<string, IniModifierCollection> ReadIniFile(string iniPath, Dictionary<string, Dictionary<string, IniModifierOutline>> lookups)
        {
            try
            {
                if (YARGTextReader.IsUTF8(in bytes, out var byteContainer))
                using var bytes = FixedArray.LoadFile(iniPath);
                {
                    return ProcessIni(ref byteContainer, sections);
                }

                using var chars = YARGTextReader.ConvertToUTF16(in bytes, out var charContainer);
                if (chars.IsAllocated)
                {
                    return ProcessIni(ref charContainer, sections);
                }

                using var ints = YARGTextReader.ConvertToUTF32(in bytes, out var intContainer);
                return ProcessIni(ref intContainer, sections);

            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, ex.Message);
                return new();
            }
        }

        private static Dictionary<string, IniModifierCollection> ProcessIni<TChar>(ref YARGTextContainer<TChar> container, Dictionary<string, Dictionary<string, IniModifierOutline>> lookups)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            Dictionary<string, IniModifierCollection> collections = new();
            while (TrySection(ref container, out string section))
            {
                if (lookups.TryGetValue(section, out var nodes))
                {
                    collections[section] = ExtractModifiers(ref container, ref nodes);
                }
                else
                {
                    YARGTextReader.SkipLinesUntil(ref container, TextConstants<TChar>.OPEN_BRACKET);
                }
            }
            return collections;
        }

        private static bool TrySection<TChar>(ref YARGTextContainer<TChar> container, out string section)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            if (container.IsAtEnd() || (container.Get() != '[' && !YARGTextReader.SkipLinesUntil(ref container, TextConstants<TChar>.OPEN_BRACKET)))
            {
                section = string.Empty;
                return false;
            }
            section = YARGTextReader.PeekLine(ref container).ToLower();
            return true;
        }

        private static IniModifierCollection ExtractModifiers<TChar>(ref YARGTextContainer<TChar> container, ref Dictionary<string, IniModifierOutline> outlines)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            IniModifierCollection collection = new();
            while (IsStillCurrentSection(ref container))
            {
                string name = YARGTextReader.ExtractModifierName(ref container).ToLower();
                if (outlines.TryGetValue(name, out var outline))
                {
                    collection.Add(ref container, in outline, false);
                }
            }
            return collection;
        }

        private static bool IsStillCurrentSection<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            YARGTextReader.GotoNextLine(ref container);
            return !container.IsAtEnd() && container.Get() != '[';
        }
    }
}

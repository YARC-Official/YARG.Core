using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.IO.Ini
{
    public static class YARGIniReader
    {
        public static Dictionary<string, IniModifierCollection> ReadIniFile(string iniPath, Dictionary<string, Dictionary<string, IniModifierOutline>> lookups)
        {
            try
            {
                using var bytes = FixedArray.LoadFile(iniPath);
                if (YARGTextReader.TryUTF8(in bytes, out var byteContainer))
                {
                    return ProcessIni(ref byteContainer, lookups);
                }

                using var chars = YARGTextReader.TryUTF16Cast(in bytes);
                if (chars.IsAllocated)
                {
                    var charContainer = YARGTextReader.CreateUTF16Container(in chars);
                    return ProcessIni(ref charContainer, lookups);
                }

                using var ints = YARGTextReader.CastUTF32(in bytes);
                var intContainer = YARGTextReader.CreateUTF32Container(in ints);
                return ProcessIni(ref intContainer, lookups);

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

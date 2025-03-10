using System;
using System.Collections.Generic;
using YARG.Core.Logging;

namespace YARG.Core.IO.Ultrastar
{
    public static class YARGUltrastarReader
    {
        public static UltrastarModifierCollection ReadUltrastarFile(string ultrastarPath, Dictionary<string, UltrastarModifierOutline> outlines, Dictionary<string, string> deprecations)
        {
            try
            {
                using var bytes = FixedArray.LoadFile(ultrastarPath);
                if (YARGTextReader.TryUTF8(in bytes, out var byteContainer))
                {
                    return ProcessUltrastar(ref byteContainer, outlines, deprecations);
                }

                using var chars = YARGTextReader.TryUTF16Cast(in bytes);
                if (chars.IsAllocated)
                {
                    var charContainer = YARGTextReader.CreateUTF16Container(in chars);
                    return ProcessUltrastar(ref charContainer, outlines, deprecations);
                }

                using var ints = YARGTextReader.CastUTF32(in bytes);
                var intContainer = YARGTextReader.CreateUTF32Container(in ints);
                return ProcessUltrastar(ref intContainer, outlines, deprecations);

            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, ex.Message);
                return new();
            }
        }

        private static UltrastarModifierCollection ProcessUltrastar<TChar>(ref YARGTextContainer<TChar> container, Dictionary<string, UltrastarModifierOutline> outlines, Dictionary<string, string> deprecations)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {

            UltrastarModifierCollection collection = new();

            string line = string.Empty;

            while ((line = YARGTextReader.PeekLine(ref container)).Length > 0)
            {
                if (line[0] != '#')
                {
                    YARGTextReader.SkipLinesUntil(ref container, TextConstants<TChar>.POUND_SIGN);
                    continue;
                }

                string name = YARGTextReader.ExtractModifierName(ref container, splitChar: ':').ToLower();

                // Override deprecated name with new name
                if (deprecations.TryGetValue(name, out string newName))
                {
                    name = newName;
                }

                if (outlines.TryGetValue(name, out var outline))
                {
                    container.Position++; // Account for splitChar
                    collection.Add(ref container, outline, false);
                }

                YARGTextReader.GotoNextLine(ref container);
            }

            return collection;
        }
    }
}

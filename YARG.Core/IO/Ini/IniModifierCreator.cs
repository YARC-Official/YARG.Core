using System;
using System.Text;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.IO.Ini
{
    public enum ModifierCreatorType
    {
        None,
        SortString,
        String,
        SortString_Chart,
        String_Chart,
        UInt64,
        Int64,
        UInt32,
        Int32,
        UInt16,
        Int16,
        Bool,
        Float,
        Double,
        UInt64Array,
    }

    public sealed class IniModifierCreator
    {
        public readonly string outputName;
        public readonly ModifierCreatorType type;

        public IniModifierCreator(string outputName, ModifierCreatorType type)
        {
            this.outputName = outputName;
            this.type = type;
        }

        public unsafe IniModifier CreateModifier<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder)
            where TChar : unmanaged, IConvertible
        {
            return type switch
            {
                ModifierCreatorType.SortString => new IniModifier(SortString.Convert(ExtractIniString(ref container, decoder, false))),
                ModifierCreatorType.SortString_Chart => new IniModifier(SortString.Convert(ExtractIniString(ref container, decoder, true))),
                ModifierCreatorType.String => new IniModifier(ExtractIniString(ref container, decoder, false)),
                ModifierCreatorType.String_Chart => new IniModifier(ExtractIniString(ref container, decoder, true)),
                _ => CreateNumberModifier(ref container),
            };
        }

        public IniModifier CreateSngModifier(ref YARGTextContainer<byte> sngContainer, int length)
        {
            return type switch
            {
                ModifierCreatorType.SortString => new IniModifier(SortString.Convert(ExtractSngString(ref sngContainer, length))),
                ModifierCreatorType.String => new IniModifier(ExtractSngString(ref sngContainer, length)),
                _ => CreateNumberModifier(ref sngContainer),
            };
        }

        private IniModifier CreateNumberModifier<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            switch (type)
            {
                case ModifierCreatorType.UInt64:
                    {
                        container.TryExtractUInt64(out ulong value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int64:
                    {
                        container.TryExtractInt64(out long value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt32:
                    {
                        container.TryExtractUInt32(out uint value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int32:
                    {
                        container.TryExtractInt32(out int value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt16:
                    {
                        container.TryExtractUInt16(out ushort value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int16:
                    {
                        container.TryExtractInt16(out short value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Bool:
                    {
                        return new IniModifier(container.ExtractBoolean());
                    }
                case ModifierCreatorType.Float:
                    {
                        container.TryExtractFloat(out float value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Double:
                    {
                        container.TryExtractDouble(out double value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt64Array:
                    {
                        long l2 = -1;
                        if (container.TryExtractInt64(out long l1))
                        {
                            YARGTextReader.SkipWhitespace(ref container);
                            if (!container.TryExtractInt64(out l2))
                            {
                                l2 = -1;
                            }
                        }
                        else
                        {
                            l1 = -1;
                        }
                        return new IniModifier(l1, l2);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private static unsafe string ExtractIniString<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder, bool isChartFile)
            where TChar : unmanaged, IConvertible
        {
            return RichTextUtils.ReplaceColorNames(YARGTextReader.ExtractText(ref container, decoder, isChartFile));
        }

        private static unsafe string ExtractSngString(ref YARGTextContainer<byte> sngContainer, int length)
        {
            return RichTextUtils.ReplaceColorNames(Encoding.UTF8.GetString(sngContainer.Position, length));
        }
    }
}
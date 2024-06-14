using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public IniModifier CreateModifier<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            return type switch
            {
                ModifierCreatorType.SortString       => new IniModifier(SortString.Convert(ExtractIniString(reader, false))),
                ModifierCreatorType.SortString_Chart => new IniModifier(SortString.Convert(ExtractIniString(reader, true))),
                ModifierCreatorType.String           => new IniModifier(ExtractIniString(reader, false)),
                ModifierCreatorType.String_Chart     => new IniModifier(ExtractIniString(reader, true)),
                _ => CreateNumberModifier(reader.Container),
            };
        }

        public IniModifier CreateSngModifier(YARGTextContainer<byte> sngContainer, int length)
        {
            return type switch
            {
                ModifierCreatorType.SortString => new IniModifier(SortString.Convert(ExtractSngString(sngContainer, length))),
                ModifierCreatorType.String =>     new IniModifier(ExtractSngString(sngContainer, length)),
                _ => CreateNumberModifier(sngContainer),
            };
        }

        private IniModifier CreateNumberModifier<TChar>(YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            switch (type)
            {
                case ModifierCreatorType.UInt64:
                    {
                        container.ExtractUInt64(out ulong value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int64:
                    {
                        container.ExtractInt64(out long value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt32:
                    {
                        container.ExtractUInt32(out uint value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int32:
                    {
                        container.ExtractInt32(out int value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt16:
                    {
                        container.ExtractUInt16(out ushort value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int16:
                    {
                        container.ExtractInt16(out short value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Bool:
                    {
                        return new IniModifier(container.ExtractBoolean());
                    }
                case ModifierCreatorType.Float:
                    {
                        container.ExtractFloat(out float value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Double:
                    {
                        container.ExtractDouble(out double value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt64Array:
                    {
                        long l2 = -1;
                        if (container.ExtractInt64(out long l1))
                        {
                            YARGTextReader.SkipWhitespace(container);
                            if (!container.ExtractInt64(out l2))
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

        private static string ExtractIniString<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, bool isChartFile)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            return RichTextUtils.ReplaceColorNames(reader.ExtractText(isChartFile));
        }

        private static unsafe string ExtractSngString(YARGTextContainer<byte> sngContainer, int length)
        {
            return RichTextUtils.ReplaceColorNames(Encoding.UTF8.GetString(sngContainer.Data.Ptr + sngContainer.Position, length));
        }
    }
}

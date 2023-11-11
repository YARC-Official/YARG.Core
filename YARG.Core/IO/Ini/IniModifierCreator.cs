using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Song;

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
            switch (type)
            {
                case ModifierCreatorType.SortString:       return new IniModifier(new SortString(reader.ExtractText(false)));
                case ModifierCreatorType.SortString_Chart: return new IniModifier(new SortString(reader.ExtractText(true)));
                case ModifierCreatorType.String:           return new IniModifier(reader.ExtractText(false));
                case ModifierCreatorType.String_Chart:     return new IniModifier(reader.ExtractText(true));
                case ModifierCreatorType.UInt64:
                    {
                        reader.Container.ExtractUInt64(out ulong value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int64:
                    {
                        reader.Container.ExtractInt64(out long value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt32:
                    {
                        reader.Container.ExtractUInt32(out uint value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int32:
                    {
                        reader.Container.ExtractInt32(out int value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt16:
                    {
                        reader.Container.ExtractUInt16(out ushort value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int16:
                    {
                        reader.Container.ExtractInt16(out short value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Bool:
                    {
                        return new IniModifier(reader.Container.ExtractBoolean());
                    }
                case ModifierCreatorType.Float:
                    {
                        reader.Container.ExtractFloat(out float value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Double:
                    {
                        reader.Container.ExtractDouble(out double value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt64Array:
                    {
                        ulong ul2 = 0;
                        // Use reader version for the first one to ensure whitespace removal
                        if (reader.ExtractUInt64(out ulong ul1))
                            reader.Container.ExtractUInt64(out ul2);
                        return new IniModifier(ul1, ul2);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        public IniModifier CreateSngModifier(YARGTextContainer<byte> sngContainer)
        {
            switch (type)
            {
                case ModifierCreatorType.SortString: return new IniModifier(new SortString(ExtractSngString(sngContainer)));
                case ModifierCreatorType.String:     return new IniModifier(ExtractSngString(sngContainer));
                case ModifierCreatorType.UInt64:
                    {
                        sngContainer.ExtractUInt64(out ulong value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int64:
                    {
                        sngContainer.ExtractInt64(out long value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt32:
                    {
                        sngContainer.ExtractUInt32(out uint value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int32:
                    {
                        sngContainer.ExtractInt32(out int value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt16:
                    {
                        sngContainer.ExtractUInt16(out ushort value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Int16:
                    {
                        sngContainer.ExtractInt16(out short value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Bool:
                    {
                        return new IniModifier(sngContainer.ExtractBoolean());
                    }
                case ModifierCreatorType.Float:
                    {
                        sngContainer.ExtractFloat(out float value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.Double:
                    {
                        sngContainer.ExtractDouble(out double value);
                        return new IniModifier(value);
                    }
                case ModifierCreatorType.UInt64Array:
                    {
                        ulong ul2 = 0;
                        if (sngContainer.ExtractUInt64(out ulong ul1))
                        {
                            YARGTextReader.SkipWhitespace(sngContainer);
                            sngContainer.ExtractUInt64(out ul2);
                        }
                        return new IniModifier(ul1, ul2);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private static string ExtractSngString(YARGTextContainer<byte> sngContainer)
        {
            return Encoding.UTF8.GetString(sngContainer.ExtractSpan(sngContainer.Next - sngContainer.Position));
        }
    }
}

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
                        if (reader.ExtractUInt64(out ulong value))
                            return new IniModifier(value);
                        return new IniModifier((ulong) 0);
                    }
                case ModifierCreatorType.Int64:
                    {
                        if (reader.ExtractInt64(out long value))
                            return new IniModifier(value);
                        return new IniModifier((long) 0);
                    }
                case ModifierCreatorType.UInt32:
                    {
                        if (reader.ExtractUInt32(out uint value))
                            return new IniModifier(value);
                        return new IniModifier((uint) 0);
                    }
                case ModifierCreatorType.Int32:
                    {
                        if (reader.ExtractInt32(out int value))
                            return new IniModifier(value);
                        return new IniModifier(0);
                    }
                case ModifierCreatorType.UInt16:
                    {
                        if (reader.ExtractUInt16(out ushort value))
                            return new IniModifier(value);
                        return new IniModifier((ushort) 0);
                    }
                case ModifierCreatorType.Int16:
                    {
                        if (reader.ExtractInt16(out short value))
                            return new IniModifier(value);
                        return new IniModifier((short) 0);
                    }
                case ModifierCreatorType.Bool:
                    try
                    {
                        return new IniModifier(reader.ExtractBoolean());
                    }
                    catch (Exception)
                    {
                        return new IniModifier(false);
                    }
                case ModifierCreatorType.Float:
                    {
                        if (reader.ExtractFloat(out float value))
                            return new IniModifier(value);
                        return new IniModifier(.0f);
                    }
                case ModifierCreatorType.Double:
                    {
                        if (reader.ExtractDouble(out double value))
                            return new IniModifier(value);
                        return new IniModifier(.0);
                    }
                case ModifierCreatorType.UInt64Array:
                    {
                        if (reader.ExtractUInt64(out ulong ul1) && reader.ExtractUInt64(out ulong ul2))
                            return new IniModifier(ul1, ul2);
                        return new IniModifier(0, 0);
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

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
            where TDecoder : StringDecoder<TChar>, new()
        {
            try
            {
                switch (type)
                {
                    case ModifierCreatorType.SortString:       return new(new SortString(reader.ExtractText(false)));
                    case ModifierCreatorType.SortString_Chart: return new(new SortString(reader.ExtractText(true)));
                    case ModifierCreatorType.String:           return new(reader.ExtractText(false));
                    case ModifierCreatorType.String_Chart:     return new(reader.ExtractText(true));
                    case ModifierCreatorType.UInt64:           return new(reader.ExtractUInt64());
                    case ModifierCreatorType.Int64:            return new(reader.ExtractInt64());
                    case ModifierCreatorType.UInt32:           return new(reader.ExtractUInt32());
                    case ModifierCreatorType.Int32:            return new(reader.ExtractInt32());
                    case ModifierCreatorType.UInt16:           return new(reader.ExtractUInt16());
                    case ModifierCreatorType.Int16:            return new(reader.ExtractInt16());
                    case ModifierCreatorType.Bool:             return new(reader.ExtractBoolean());
                    case ModifierCreatorType.Float:            return new(reader.ExtractFloat());
                    case ModifierCreatorType.Double:           return new(reader.ExtractDouble());
                    case ModifierCreatorType.UInt64Array:
                        {
                            ulong ul1 = reader.ExtractUInt64();
                            ulong ul2 = reader.ExtractUInt64();
                            return new(ul1, ul2);
                        }
                }
            }
            catch (Exception)
            {
                switch (type)
                {
                    case ModifierCreatorType.UInt64:      return new((ulong) 0);
                    case ModifierCreatorType.Int64:       return new((long) 0);
                    case ModifierCreatorType.UInt32:      return new((uint) 0);
                    case ModifierCreatorType.Int32:       return new(0);
                    case ModifierCreatorType.UInt16:      return new((ushort) 0);
                    case ModifierCreatorType.Int16:       return new((short) 0);
                    case ModifierCreatorType.Bool:        return new(false);
                    case ModifierCreatorType.Float:       return new(.0f);
                    case ModifierCreatorType.Double:      return new(.0);
                    case ModifierCreatorType.UInt64Array: return new(0, 0);
                }
            }
            throw new Exception("How in the fu-");
        }
    }
}

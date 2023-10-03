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

        public IniModifier CreateModifier<TChar, TDecoder>(YARGTextReader<TChar> reader, TDecoder decoder)
            where TChar : unmanaged, IConvertible
            where TDecoder : StringDecoder<TChar>
        {
            try
            {
                switch (type)
                {
                    case ModifierCreatorType.SortString:       return new(new SortString(decoder.ExtractText(reader, false)));
                    case ModifierCreatorType.SortString_Chart: return new(new SortString(decoder.ExtractText(reader, true)));
                    case ModifierCreatorType.String:           return new(decoder.ExtractText(reader, false));
                    case ModifierCreatorType.String_Chart:     return new(decoder.ExtractText(reader, true));
                    case ModifierCreatorType.UInt64:           return new(YARGNumberExtractor.UInt64(reader));
                    case ModifierCreatorType.Int64:            return new(YARGNumberExtractor.Int64(reader));
                    case ModifierCreatorType.UInt32:           return new(YARGNumberExtractor.UInt32(reader));
                    case ModifierCreatorType.Int32:            return new(YARGNumberExtractor.Int32(reader));
                    case ModifierCreatorType.UInt16:           return new(YARGNumberExtractor.UInt16(reader));
                    case ModifierCreatorType.Int16:            return new(YARGNumberExtractor.Int16(reader));
                    case ModifierCreatorType.Bool:             return new(YARGNumberExtractor.Boolean(reader));
                    case ModifierCreatorType.Float:            return new(YARGNumberExtractor.Float(reader));
                    case ModifierCreatorType.Double:           return new(YARGNumberExtractor.Double(reader));
                    case ModifierCreatorType.UInt64Array:
                        {
                            ulong ul1 = YARGNumberExtractor.UInt64(reader);
                            ulong ul2 = YARGNumberExtractor.UInt64(reader);
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

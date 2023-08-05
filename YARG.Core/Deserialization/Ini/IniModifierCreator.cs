using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Song;

namespace YARG.Core.Deserialization.Ini
{
    public enum ModifierNodeType
    {
        NONE,
        SORTSTRING,
        STRING,
        SORTSTRING_CHART,
        STRING_CHART,
        UINT64,
        INT64,
        UINT32,
        INT32,
        UINT16,
        INT16,
        BOOL,
        FLOAT,
        DOUBLE,
        UINT64ARRAY,
    }

    public class IniModifierCreator
    {
        public readonly string outputName;
        public readonly ModifierNodeType type;

        public IniModifierCreator(string outputName, ModifierNodeType type)
        {
            this.outputName = outputName;
            this.type = type;
        }

        public IniModifier CreateModifier(YARGTXTReader reader)
        {
            try
            {
                switch (type)
                {
                    case ModifierNodeType.SORTSTRING:       return new(new SortString(reader.ExtractEncodedString(false)));
                    case ModifierNodeType.SORTSTRING_CHART: return new(new SortString(reader.ExtractEncodedString(true)));
                    case ModifierNodeType.STRING:           return new(reader.ExtractEncodedString(false));
                    case ModifierNodeType.STRING_CHART:     return new(reader.ExtractEncodedString(true));
                    case ModifierNodeType.UINT64:           return new(reader.ReadUInt64());
                    case ModifierNodeType.INT64:            return new(reader.ReadInt64());
                    case ModifierNodeType.UINT32:           return new(reader.ReadUInt32());
                    case ModifierNodeType.INT32:            return new(reader.ReadInt32());
                    case ModifierNodeType.UINT16:           return new(reader.ReadUInt16());
                    case ModifierNodeType.INT16:            return new(reader.ReadInt16());
                    case ModifierNodeType.BOOL:             return new(reader.ReadBoolean());
                    case ModifierNodeType.FLOAT:            return new(reader.ReadFloat());
                    case ModifierNodeType.DOUBLE:           return new(reader.ReadDouble());
                    case ModifierNodeType.UINT64ARRAY:
                        {
                            ulong dub1 = reader.ReadUInt64();
                            ulong dub2 = reader.ReadUInt64();
                            return new(dub1, dub2);
                        }
                }
            }
            catch (Exception)
            {
                switch (type)
                {
                    case ModifierNodeType.UINT64:      return new((ulong) 0);
                    case ModifierNodeType.INT64:       return new((long) 0);
                    case ModifierNodeType.UINT32:      return new((uint) 0);
                    case ModifierNodeType.INT32:       return new((int)0);
                    case ModifierNodeType.UINT16:      return new((ushort) 0);
                    case ModifierNodeType.INT16:       return new((short) 0);
                    case ModifierNodeType.BOOL:        return new(false);
                    case ModifierNodeType.FLOAT:       return new(.0f);
                    case ModifierNodeType.DOUBLE:      return new(.0);
                    case ModifierNodeType.UINT64ARRAY: return new(0, 0);
                }
            }
            throw new Exception("How in the fu-");
        }
    }
}

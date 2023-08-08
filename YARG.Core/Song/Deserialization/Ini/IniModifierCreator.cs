using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Song;

namespace YARG.Core.Song.Deserialization.Ini
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

    public class IniModifierCreator
    {
        public readonly string outputName;
        public readonly ModifierCreatorType type;

        public IniModifierCreator(string outputName, ModifierCreatorType type)
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
                    case ModifierCreatorType.SortString: return new(new SortString(reader.ExtractEncodedString(false)));
                    case ModifierCreatorType.SortString_Chart: return new(new SortString(reader.ExtractEncodedString(true)));
                    case ModifierCreatorType.String: return new(reader.ExtractEncodedString(false));
                    case ModifierCreatorType.String_Chart: return new(reader.ExtractEncodedString(true));
                    case ModifierCreatorType.UInt64: return new(reader.ReadUInt64());
                    case ModifierCreatorType.Int64: return new(reader.ReadInt64());
                    case ModifierCreatorType.UInt32: return new(reader.ReadUInt32());
                    case ModifierCreatorType.Int32: return new(reader.ReadInt32());
                    case ModifierCreatorType.UInt16: return new(reader.ReadUInt16());
                    case ModifierCreatorType.Int16: return new(reader.ReadInt16());
                    case ModifierCreatorType.Bool: return new(reader.ReadBoolean());
                    case ModifierCreatorType.Float: return new(reader.ReadFloat());
                    case ModifierCreatorType.Double: return new(reader.ReadDouble());
                    case ModifierCreatorType.UInt64Array:
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
                    case ModifierCreatorType.UInt64: return new((ulong) 0);
                    case ModifierCreatorType.Int64: return new((long) 0);
                    case ModifierCreatorType.UInt32: return new((uint) 0);
                    case ModifierCreatorType.Int32: return new(0);
                    case ModifierCreatorType.UInt16: return new((ushort) 0);
                    case ModifierCreatorType.Int16: return new((short) 0);
                    case ModifierCreatorType.Bool: return new(false);
                    case ModifierCreatorType.Float: return new(.0f);
                    case ModifierCreatorType.Double: return new(.0);
                    case ModifierCreatorType.UInt64Array: return new(0, 0);
                }
            }
            throw new Exception("How in the fu-");
        }
    }
}

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using YARG.Core.Song;

namespace YARG.Core.Deserialization.Ini
{
    public enum ModifierType
    {
        NONE,
        SORTSTRING,
        STRING,
        UINT64,
        INT64,
        UINT32,
        INT32,
        UINT16,
        INT16,
        BOOL,
        FLOAT,
        FLOATARRAY,
    };

    public unsafe class IniModifier
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct ModifierUnion
        {
            [FieldOffset(0)] public ulong ul;
            [FieldOffset(0)] public long l;
            [FieldOffset(0)] public uint ui;
            [FieldOffset(0)] public int i;
            [FieldOffset(0)] public ushort us;
            [FieldOffset(0)] public short s;
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public bool b;
            [FieldOffset(0)] public fixed float flArr[2];
        }

        private readonly ModifierType type;

        private SortString _sortStr;
        private string _str = string.Empty;
        private ModifierUnion union;

        public IniModifier(SortString str)
        {
            type = ModifierType.SORTSTRING;
            _sortStr = str;
        }
        public IniModifier(string str)
        {
            type = ModifierType.STRING;
            _str = str;
        }
        public IniModifier(ulong value)
        {
            type = ModifierType.UINT64;
            union.ul = value;
        }
        public IniModifier(long value)
        {
            type = ModifierType.INT64;
            union.l = value;
        }
        public IniModifier(uint value)
        {
            type = ModifierType.UINT32;
            union.ui = value;
        }
        public IniModifier(int value)
        {
            type = ModifierType.INT32;
            union.i = value;
        }
        public IniModifier(ushort value)
        {
            type = ModifierType.UINT16;
            union.us = value;
        }
        public IniModifier(short value)
        {
            type = ModifierType.INT16;
            union.s = value;
        }
        public IniModifier(bool value)
        {
            type = ModifierType.BOOL;
            union.b = value;
        }
        public IniModifier(float value)
        {
            type = ModifierType.FLOAT;
            union.f = value;
        }
        public IniModifier(float fl1, float fl2)
        {
            type = ModifierType.FLOATARRAY;
            union.flArr[0] = fl1;
            union.flArr[1] = fl2;
        }

        public SortString SORTSTR
        {
            get
            {
                if (type != ModifierType.SORTSTRING)
                    throw new ArgumentException("Modifier is not a SortString");
                return _sortStr;
            }
            set
            {
                if (type != ModifierType.SORTSTRING)
                    throw new ArgumentException("Modifier is not a SortString");
                _sortStr = value;
            }
        }

        public string STR
        {
            get
            {
                if (type != ModifierType.STRING)
                    throw new ArgumentException("Modifier is not a String");
                return _str;
            }
            set
            {
                if (type != ModifierType.STRING)
                    throw new ArgumentException("Modifier is not a String");
                _str = value;
            }
        }

        public ulong UINT64
        {
            get
            {
                if (type != ModifierType.UINT64)
                    throw new ArgumentException("Modifier is not a UINT64");
                return union.ul;
            }
            set
            {
                if (type != ModifierType.UINT64)
                    throw new ArgumentException("Modifier is not a UINT64");
                union.ul = value;
            }
        }

        public long INT64
        {
            get
            {
                if (type != ModifierType.INT64)
                    throw new ArgumentException("Modifier is not a INT64");
                return union.l;
            }
            set
            {
                if (type != ModifierType.INT64)
                    throw new ArgumentException("Modifier is not a INT64");
                union.l = value;
            }
        }

        public uint UINT32
        {
            get
            {
                if (type != ModifierType.UINT32)
                    throw new ArgumentException("Modifier is not a UINT32");
                return union.ui;
            }
            set
            {
                if (type != ModifierType.UINT32)
                    throw new ArgumentException("Modifier is not a UINT32");
                union.ui = value;
            }
        }

        public int INT32
        {
            get
            {
                if (type != ModifierType.INT32)
                    throw new ArgumentException("Modifier is not a INT32");
                return union.i;
            }
            set
            {
                if (type != ModifierType.INT32)
                    throw new ArgumentException("Modifier is not a INT32");
                union.i = value;
            }
        }

        public ushort UINT16
        {
            get
            {
                if (type != ModifierType.UINT16)
                    throw new ArgumentException("Modifier is not a UINT16");
                return union.us;
            }
            set
            {
                if (type != ModifierType.UINT16)
                    throw new ArgumentException("Modifier is not a UINT16");
                union.us = value;
            }
        }

        public short INT16
        {
            get
            {
                if (type != ModifierType.INT16)
                    throw new ArgumentException("Modifier is not a INT16");
                return union.s;
            }
            set
            {
                if (type != ModifierType.INT16)
                    throw new ArgumentException("Modifier is not a INT16");
                union.s = value;
            }
        }

        public bool BOOL
        {
            get
            {
                if (type != ModifierType.BOOL)
                    throw new ArgumentException("Modifier is not a BOOL");
                return union.b;
            }
            set
            {
                if (type != ModifierType.BOOL)
                    throw new ArgumentException("Modifier is not a BOOL");
                union.b = value;
            }
        }

        public float FLOAT
        {
            get
            {
                if (type != ModifierType.FLOAT)
                    throw new ArgumentException("Modifier is not a FLOAT");
                return union.f;
            }
            set
            {
                if (type != ModifierType.FLOAT)
                    throw new ArgumentException("Modifier is not a FLOAT");
                union.f = value;
            }
        }

        public float[] FLOATARRAY
        {
            get
            {
                if (type != ModifierType.FLOAT)
                    throw new ArgumentException("Modifier is not a FLOAT");
                return new float[] { union.flArr[0], union.flArr[1] };
            }
            set
            {
                if (type != ModifierType.FLOAT)
                    throw new ArgumentException("Modifier is not a FLOAT");
                union.flArr[0] = value[0];
                union.flArr[1] = value[1];
            }
        }
    }
}

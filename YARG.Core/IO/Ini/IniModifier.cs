using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using YARG.Core.Song;

namespace YARG.Core.IO.Ini
{
    public enum ModifierType
    {
        None,
        SortString,
        String,
        UInt64,
        Int64,
        UInt32,
        Int32,
        UInt16,
        Int16,
        Bool,
        Float,
        Double,
        Int64Array,
    };

    public sealed class IniModifier
    {
        [StructLayout(LayoutKind.Explicit)]
        private unsafe struct ModifierUnion
        {
            [FieldOffset(0)] public ulong ul;
            [FieldOffset(0)] public long l;
            [FieldOffset(0)] public uint ui;
            [FieldOffset(0)] public int i;
            [FieldOffset(0)] public ushort us;
            [FieldOffset(0)] public short s;
            [FieldOffset(0)] public double d;
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public bool b;
            [FieldOffset(0)] public fixed long lArr[2];
            [FieldOffset(0)] public SortString sort;
            [FieldOffset(0)] public string str;
        }

        private readonly ModifierType type;
        private ModifierUnion union;

        public IniModifier(SortString str)
        {
            type = ModifierType.SortString;
            union.sort = str;
        }
        public IniModifier(string str)
        {
            type = ModifierType.String;
            union.str = str;
        }
        public IniModifier(ulong value)
        {
            type = ModifierType.UInt64;
            union.ul = value;
        }
        public IniModifier(long value)
        {
            type = ModifierType.Int64;
            union.l = value;
        }
        public IniModifier(uint value)
        {
            type = ModifierType.UInt32;
            union.ui = value;
        }
        public IniModifier(int value)
        {
            type = ModifierType.Int32;
            union.i = value;
        }
        public IniModifier(ushort value)
        {
            type = ModifierType.UInt16;
            union.us = value;
        }
        public IniModifier(short value)
        {
            type = ModifierType.Int16;
            union.s = value;
        }
        public IniModifier(bool value)
        {
            type = ModifierType.Bool;
            union.b = value;
        }
        public IniModifier(float value)
        {
            type = ModifierType.Float;
            union.f = value;
        }
        public IniModifier(double value)
        {
            type = ModifierType.Double;
            union.d = value;
        }
        public IniModifier(long dub1, long dub2)
        {
            type = ModifierType.Int64Array;
            unsafe
            {
                union.lArr[0] = dub1;
                union.lArr[1] = dub2;
            }
        }

        public SortString SortString
        {
            get
            {
                if (type != ModifierType.SortString)
                    throw new ArgumentException("Modifier is not a SortString");
                return union.sort;
            }
            set
            {
                if (type != ModifierType.SortString)
                    throw new ArgumentException("Modifier is not a SortString");
                union.sort = value;
            }
        }

        public string String
        {
            get
            {
                if (type != ModifierType.String)
                    throw new ArgumentException("Modifier is not a String");
                return union.str;
            }
            set
            {
                if (type != ModifierType.String)
                    throw new ArgumentException("Modifier is not a String");
                union.str = value;
            }
        }

        public ulong UInt64
        {
            get
            {
                if (type != ModifierType.UInt64)
                    throw new ArgumentException("Modifier is not a UINT64");
                return union.ul;
            }
            set
            {
                if (type != ModifierType.UInt64)
                    throw new ArgumentException("Modifier is not a UINT64");
                union.ul = value;
            }
        }

        public long Int64
        {
            get
            {
                if (type != ModifierType.Int64)
                    throw new ArgumentException("Modifier is not a INT64");
                return union.l;
            }
            set
            {
                if (type != ModifierType.Int64)
                    throw new ArgumentException("Modifier is not a INT64");
                union.l = value;
            }
        }

        public uint UInt32
        {
            get
            {
                if (type != ModifierType.UInt32)
                    throw new ArgumentException("Modifier is not a UINT32");
                return union.ui;
            }
            set
            {
                if (type != ModifierType.UInt32)
                    throw new ArgumentException("Modifier is not a UINT32");
                union.ui = value;
            }
        }

        public int Int32
        {
            get
            {
                if (type != ModifierType.Int32)
                    throw new ArgumentException("Modifier is not a INT32");
                return union.i;
            }
            set
            {
                if (type != ModifierType.Int32)
                    throw new ArgumentException("Modifier is not a INT32");
                union.i = value;
            }
        }

        public ushort UInt16
        {
            get
            {
                if (type != ModifierType.UInt16)
                    throw new ArgumentException("Modifier is not a UINT16");
                return union.us;
            }
            set
            {
                if (type != ModifierType.UInt16)
                    throw new ArgumentException("Modifier is not a UINT16");
                union.us = value;
            }
        }

        public short Int16
        {
            get
            {
                if (type != ModifierType.Int16)
                    throw new ArgumentException("Modifier is not a INT16");
                return union.s;
            }
            set
            {
                if (type != ModifierType.Int16)
                    throw new ArgumentException("Modifier is not a INT16");
                union.s = value;
            }
        }

        public bool Bool
        {
            get
            {
                if (type != ModifierType.Bool)
                    throw new ArgumentException("Modifier is not a BOOL");
                return union.b;
            }
            set
            {
                if (type != ModifierType.Bool)
                    throw new ArgumentException("Modifier is not a BOOL");
                union.b = value;
            }
        }

        public float Float
        {
            get
            {
                if (type != ModifierType.Float)
                    throw new ArgumentException("Modifier is not a FLOAT");
                return union.f;
            }
            set
            {
                if (type != ModifierType.Float)
                    throw new ArgumentException("Modifier is not a FLOAT");
                union.f = value;
            }
        }

        public double Double
        {
            get
            {
                if (type != ModifierType.Double)
                    throw new ArgumentException("Modifier is not a DOUBLE");
                return union.d;
            }
            set
            {
                if (type != ModifierType.Double)
                    throw new ArgumentException("Modifier is not a DOUBLE");
                union.d = value;
            }
        }

        public void GetInt64Array(out long l1, out long l2)
        {
            if (type != ModifierType.Int64Array)
            {
                throw new ArgumentException("Modifier is not a UINT64ARRAY");
            }

            unsafe
            {
                l1 = union.lArr[0];
                l2 = union.lArr[1];
            }
        }

        public void SetInt64Array(long l1, long l2)
        {
            if (type != ModifierType.Int64Array)
            {
                throw new ArgumentException("Modifier is not a UINT64ARRAY");
            }

            unsafe
            {
                union.lArr[0] = l1;
                union.lArr[1] = l2;
            }
        }
    }
}

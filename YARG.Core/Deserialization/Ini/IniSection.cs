using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.Deserialization.Ini
{
    public class IniSection
    {
        private readonly Dictionary<string, List<IniModifier>> modifiers;

        public int Count => modifiers.Count;

        public IniSection() { modifiers = new(); }
        public IniSection(Dictionary<string, List<IniModifier>> modifiers)
        {
            this.modifiers = modifiers;
        }

        public bool Contains(string key)
        {
            return modifiers.ContainsKey(key);
        }

        public bool TryGet(string key, ref SortString str, string defaultStr)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            for (int i = 0; i < results.Count; ++i)
            {
                str = results[i].SORTSTR;
                if (str.Str != string.Empty && str.Str != defaultStr)
                    break;
            }

            if (str.Str == string.Empty)
                str = defaultStr;
            return true;
        }

        public bool TryGet(string key, ref string str)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            str = results[0].STR;
            return true;
        }

        public bool TryGet(string key, ref ulong val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UINT64;
            return true;
        }

        public bool TryGet(string key, ref long val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].INT64;
            return true;
        }

        public bool TryGet(string key, ref uint val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UINT32;
            return true;
        }

        public bool TryGet(string key, ref int val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].INT32;
            return true;
        }

        public bool TryGet(string key, ref ushort val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UINT16;
            return true;
        }

        public bool TryGet(string key, ref short val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].INT16;
            return true;
        }

        public bool TryGet(string key, ref float val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].FLOAT;
            return true;
        }

        public bool TryGet(string key, ref double val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].DOUBLE;
            return true;
        }

        public bool TrySetDoubleArray(string key, ref double val1, ref double val2)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            var dub = results[0].DOUBLEARRAY;
            val1 = dub[0];
            val2 = dub[1];
            return true;
        }

        public bool TryGet(string key, ref bool val)
        {
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].BOOL;
            return true;
        }
    }
}

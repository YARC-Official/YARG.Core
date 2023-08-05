using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.Song.Deserialization.Ini
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

        public bool TryGet(string key, out SortString str, string defaultStr)
        {
            str = defaultStr;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            for (int i = 0; i < results.Count; ++i)
            {
                if (results[i].SORTSTR.Str != string.Empty)
                {
                    str = results[i].SORTSTR;
                    if (str.Str != defaultStr)
                        break;
                }
            }
            return true;
        }

        public bool TryGet(string key, out string str)
        {
            str = string.Empty;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            str = results[0].STR;
            return true;
        }

        public bool TryGet(string key, out ulong val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UINT64;
            return true;
        }

        public bool TryGet(string key, out long val)
        {
            val = -1;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].INT64;
            return true;
        }

        public bool TryGet(string key, out uint val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UINT32;
            return true;
        }

        public bool TryGet(string key, out int val)
        {
            val = -1;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].INT32;
            return true;
        }

        public bool TryGet(string key, out ushort val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UINT16;
            return true;
        }

        public bool TryGet(string key, out short val)
        {
            val = -1;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].INT16;
            return true;
        }

        public bool TryGet(string key, out float val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].FLOAT;
            return true;
        }

        public bool TryGet(string key, out double val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].DOUBLE;
            return true;
        }

        public bool TryGet(string key, out ulong val1, out ulong val2)
        {
            val1 = 0;
            val2 = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            ulong[] dub = results[0].UINT64ARRAY;
            val1 = dub[0];
            val2 = dub[1];
            return true;
        }

        public bool TryGet(string key, out bool val)
        {
            val = false;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].BOOL;
            return true;
        }
    }
}

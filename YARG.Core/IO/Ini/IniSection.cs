using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.IO.Ini
{
    public sealed class IniSection
    {
        private readonly Dictionary<string, List<IniModifier>> modifiers;

        public int Count => modifiers.Count;

        public IniSection() { modifiers = new(); }
        public IniSection(in Dictionary<string, List<IniModifier>> modifiers)
        {
            this.modifiers = modifiers;
        }

        public void Append(in Dictionary<string, List<IniModifier>> modsToAdd)
        {
            foreach (var node in modsToAdd)
            {
                if (modifiers.TryGetValue(node.Key, out var list))
                    list.AddRange(node.Value);
                else
                    modifiers.Add(node.Key, node.Value);
            }
        }

        public bool Contains(in string key)
        {
            return modifiers.ContainsKey(key);
        }

        public bool TryGet(in string key, out SortString str, in string defaultStr)
        {
            str = defaultStr;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            for (int i = 0; i < results.Count; ++i)
            {
                if (results[i].SortString.Str != string.Empty)
                {
                    str = results[i].SortString;
                    if (str.Str != defaultStr)
                        break;
                }
            }
            return true;
        }

        public bool TryGet(in string key, out string str)
        {
            str = string.Empty;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            str = results[0].String;
            return true;
        }

        public bool TryGet(in string key, out ulong val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UInt64;
            return true;
        }

        public bool TryGet(in string key, out long val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Int64;
            return true;
        }

        public bool TryGet(in string key, out uint val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UInt32;
            return true;
        }

        public bool TryGet(in string key, out int val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Int32;
            return true;
        }

        public bool TryGet(in string key, out ushort val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UInt16;
            return true;
        }

        public bool TryGet(in string key, out short val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Int16;
            return true;
        }

        public bool TryGet(in string key, out float val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Float;
            return true;
        }

        public bool TryGet(in string key, out double val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Double;
            return true;
        }

        public bool TryGet(in string key, out long val1, out long val2)
        {
            val1 = -1;
            val2 = -1;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            long[] dub = results[0].Int64Array;
            val1 = dub[0];
            val2 = dub[1];
            return true;
        }

        public bool TryGet(in string key, out bool val)
        {
            val = false;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Bool;
            return true;
        }
    }
}

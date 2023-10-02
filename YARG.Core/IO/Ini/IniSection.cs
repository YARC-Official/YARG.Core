using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.IO.Ini
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

        public void Append(Dictionary<string, List<IniModifier>> modsToAdd)
        {
            foreach (var node in modsToAdd)
            {
                if (modifiers.TryGetValue(node.Key, out var list))
                    list.AddRange(node.Value);
                else
                    modifiers.Add(node.Key, node.Value);
            }
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
                if (results[i].SortString.Str != string.Empty)
                {
                    str = results[i].SortString;
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

            str = results[0].String;
            return true;
        }

        public bool TryGet(string key, out ulong val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UInt64;
            return true;
        }

        public bool TryGet(string key, out long val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Int64;
            return true;
        }

        public bool TryGet(string key, out uint val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UInt32;
            return true;
        }

        public bool TryGet(string key, out int val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Int32;
            return true;
        }

        public bool TryGet(string key, out ushort val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].UInt16;
            return true;
        }

        public bool TryGet(string key, out short val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Int16;
            return true;
        }

        public bool TryGet(string key, out float val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Float;
            return true;
        }

        public bool TryGet(string key, out double val)
        {
            val = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Double;
            return true;
        }

        public bool TryGet(string key, out ulong val1, out ulong val2)
        {
            val1 = 0;
            val2 = 0;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            ulong[] dub = results[0].UInt64Array;
            val1 = dub[0];
            val2 = dub[1];
            return true;
        }

        public bool TryGet(string key, out bool val)
        {
            val = false;
            if (!modifiers.TryGetValue(key, out var results))
                return false;

            val = results[0].Bool;
            return true;
        }
    }
}

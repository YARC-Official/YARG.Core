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

        public bool TryGet(in string key, out SortString str, in SortString defaultStr)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                for (int i = 0; i < results.Count; ++i)
                {
                    if (results[i].SortString.Str != string.Empty)
                    {
                        str = results[i].SortString;
                        if (str.Str != defaultStr.Str)
                        {
                            return true;
                        }
                    }
                }
            }
            str = defaultStr;
            return false;
        }

        public bool TryGet(in string key, out SortString str, in string defaultStr)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                for (int i = 0; i < results.Count; ++i)
                {
                    if (results[i].SortString.Str != string.Empty)
                    {
                        str = results[i].SortString;
                        if (str.Str != defaultStr)
                        {
                            return true;
                        }
                    }
                }
            }
            str = defaultStr;
            return false;
        }

        public bool TryGet(in string key, out string str)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                str = results[0].String;
                return true;
            }
            str = string.Empty;
            return false;
        }

        public bool TryGet(in string key, out ulong val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].UInt64;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out long val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].Int64;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out uint val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].UInt32;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out int val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].Int32;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out ushort val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].UInt16;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out short val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].Int16;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out float val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].Float;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out double val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].Double;
                return true;
            }
            val = 0;
            return false;
        }

        public bool TryGet(in string key, out long val1, out long val2)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                results[0].GetInt64Array(out val1, out val2);
                return true;
            }
            val1 = -1;
            val2 = -1;
            return false;
        }

        public bool TryGet(in string key, out bool val)
        {
            if (modifiers.TryGetValue(key, out var results))
            {
                val = results[0].Bool;
                return true;
            }
            val = false;
            return false;
        }
    }
}

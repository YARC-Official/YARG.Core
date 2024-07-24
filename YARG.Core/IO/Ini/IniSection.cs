using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.IO.Ini
{
    public sealed class IniSection
    {
#if DEBUG
        private static readonly Dictionary<Type, ModifierType> _validations;

        static IniSection()
        {
            _validations = new()
            {
                { typeof(SortString), ModifierType.SortString },
                { typeof(string), ModifierType.String },
                { typeof(ulong), ModifierType.UInt64 },
                { typeof(long), ModifierType.Int64 },
                { typeof(uint), ModifierType.UInt32 },
                { typeof(int), ModifierType.Int32 },
                { typeof(ushort), ModifierType.UInt16 },
                { typeof(short), ModifierType.Int16 },
                { typeof(bool), ModifierType.Bool },
                { typeof(float), ModifierType.Float },
                { typeof(double), ModifierType.Double },
                { typeof(long[]), ModifierType.Int64Array },
            };
        }

        private void ThrowIfNot<T>(string key)
        {
            if (!knownModifiers.TryGetValue(key, out var mod))
            {
                throw new ArgumentException($"Dev: {key} is not a valid modifier!");
            }

            var type = typeof(T);
            var modifierType = _validations[type];
            if (modifierType != mod.Type &&
                (modifierType == ModifierType.SortString) != (mod.Type == ModifierType.SortString_Chart) &&
                (modifierType == ModifierType.String) != (mod.Type == ModifierType.String_Chart))
            {
                throw new ArgumentException($"Dev: Modifier {key} is not of type {type}");
            }
        }
#endif

        private readonly Dictionary<string, List<IniModifier>> modifiers;

#if DEBUG
        private readonly Dictionary<string, IniModifierCreator> knownModifiers;
#endif

        public int Count => modifiers.Count;


        // `knownModifiers` is always provided as an argument so other code doesn't have to do `#if DEBUG` guards
        public IniSection(in Dictionary<string, IniModifierCreator> knownModifiers)
        {
            modifiers = new();
#if DEBUG
            this.knownModifiers = knownModifiers;
#endif
        }

        public IniSection(in Dictionary<string, List<IniModifier>> modifiers,
            in Dictionary<string, IniModifierCreator> knownModifiers)
        {
            this.modifiers = modifiers;
#if DEBUG
            this.knownModifiers = knownModifiers;
#endif
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
#if DEBUG
            ThrowIfNot<SortString>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                for (int i = 0; i < results.Count; ++i)
                {
                    if (results[i].SortStr != SortString.Empty)
                    {
                        str = results[i].SortStr;
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
#if DEBUG
            ThrowIfNot<SortString>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                for (int i = 0; i < results.Count; ++i)
                {
                    if (results[i].SortStr != SortString.Empty)
                    {
                        str = results[i].SortStr;
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
#if DEBUG
            ThrowIfNot<string>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                str = results[0].Str;
                return true;
            }
            str = string.Empty;
            return false;
        }

        public bool TryGet(in string key, out long val1, out long val2)
        {
#if DEBUG
            ThrowIfNot<long[]>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                unsafe
                {
                    var mod = results[0];
                    val1 = mod.Buffer[0];
                    val2 = mod.Buffer[1];
                }
                return true;
            }
            val1 = -1;
            val2 = -1;
            return false;
        }

        public bool TryGet<T>(in string key, out T val)
            where T : unmanaged
        {
#if DEBUG
            ThrowIfNot<T>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                unsafe
                {
                    var mod = results[0];
                    val = *(T*) mod.Buffer;
                }
                return true;
            }
            val = default;
            return false;
        }
    }
}

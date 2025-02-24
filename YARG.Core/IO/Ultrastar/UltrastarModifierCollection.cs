using System;
using System.Collections.Generic;

namespace YARG.Core.IO.Ultrastar
{
    public enum ModifierType
    {
        None,
        String,
        UInt64,
        Bool,
        Double,
    };

    public readonly struct UltrastarModifierOutline
    {
        public readonly string Output;
        public readonly ModifierType Type;

        public UltrastarModifierOutline(string output, ModifierType type)
        {
            Output = output;
            Type = type;
        }
    };

    public class UltrastarModifierCollection
    {
        private Dictionary<string, string>? _strings;
        private Dictionary<string, ulong>? _uint64s;
        private Dictionary<string, bool>? _booleans;
        private Dictionary<string, double>? _doubles;

        public bool Contains(string key)
        {
            static bool DictContains<T>(Dictionary<string, T>? dict, string key)
            {
                return dict != null && dict.ContainsKey(key);
            }

            return DictContains(_strings, key)
                || DictContains(_uint64s, key)
                || DictContains(_booleans, key)
                || DictContains(_doubles, key);
        }

        public void Add<TChar>(ref YARGTextContainer<TChar> container, in UltrastarModifierOutline outline, bool isChartFile)
            where TChar : unmanaged, IConvertible
        {
            switch (outline.Type)
            {
                case ModifierType.String:
                    _strings ??= new Dictionary<string, string>();
                    _strings[outline.Output] = YARGTextReader.ExtractText(ref container, isChartFile);
                    break;
                case ModifierType.UInt64:
                {
                    if (!YARGTextReader.TryExtract(ref container, out ulong value))
                    {
                        value = 0;
                    }
                    _uint64s ??= new Dictionary<string, ulong>();
                    _uint64s[outline.Output] = value;
                    break;
                }
                case ModifierType.Bool:
                    _booleans ??= new Dictionary<string, bool>();
                    _booleans[outline.Output] = YARGTextReader.ExtractBoolean(in container, "yes");
                    break;
                case ModifierType.Double:
                {
                    if (!YARGTextReader.TryExtract(ref container, out double value))
                    {
                        value = 0;
                    }
                    _doubles ??= new Dictionary<string, double>();
                    _doubles[outline.Output] = value;
                    break;
                }
            }
        }

        public void Union(UltrastarModifierCollection source)
        {
            if (source._strings != null)
            {
                _strings ??= new Dictionary<string, string>();
                foreach (var node in source._strings)
                {
                    if (!_strings.TryGetValue(node.Key, out string str) || string.IsNullOrEmpty(str))
                    {
                        _strings[node.Key] = node.Value;
                    }
                }
            }

            Union(ref _uint64s, source._uint64s);
            Union(ref _booleans, source._booleans);
            Union(ref _doubles, source._doubles);
        }

        public bool IsEmpty()
        {
            static bool DictEmpty<T>(Dictionary<string, T>? dict)
            {
                return dict == null || dict.Count == 0;
            }

            return DictEmpty(_strings)
                && DictEmpty(_uint64s)
                && DictEmpty(_booleans)
                && DictEmpty(_doubles);
        }

        public bool Extract(string key, out string value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.String, "Mismatched modifier types - String requested");
#endif
            value = default!;
            return _strings != null && _strings.Remove(key, out value);
        }

        public bool Extract(string key, out ulong value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.UInt64, "Mismatched modifier types - UInt64 requested");
#endif
            value = default!;
            return _uint64s != null && _uint64s.Remove(key, out value);
        }


        public bool Extract(string key, out bool value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Bool, "Mismatched modifier types - Boolean requested");
#endif
            value = default!;
            return _booleans != null && _booleans.Remove(key, out value);
        }

        public bool Extract(string key, out double value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Double, "Mismatched modifier types - Double requested");
#endif
            value = default!;
            return _doubles != null && _doubles.Remove(key, out value);
        }


        private static void Union<TValue>(ref Dictionary<string, TValue>? dest, Dictionary<string, TValue>? source)
            where TValue : unmanaged, IEquatable<TValue>
        {
            if (source != null)
            {
                dest ??= new Dictionary<string, TValue>();
                foreach (var node in source)
                {
                    if (!dest.TryGetValue(node.Key, out var value) || value.Equals(default))
                    {
                        dest[node.Key] = node.Value;
                    }
                }
            }
        }

#if DEBUG
        private static Dictionary<string, ModifierType> _debugValidation = new();
        private static void ThrowIfMismatch(string key, ModifierType type, string error)
        {
            lock (_debugValidation)
            {
                if (_debugValidation.TryGetValue(key, out var value) && value != type)
                {
                    throw new InvalidOperationException(error);
                }
                _debugValidation[key] = type;
            }
        }
#endif
    }
}

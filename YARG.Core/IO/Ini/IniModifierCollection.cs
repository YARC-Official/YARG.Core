using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Utility;

namespace YARG.Core.IO.Ini
{
    public enum ModifierType
    {
        None,
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

    public readonly struct IniModifierOutline
    {
        public readonly string Output;
        public readonly ModifierType Type;

        public IniModifierOutline(string output, ModifierType type)
        {
            Output = output;
            Type = type;
        }
    };

    public class IniModifierCollection
    {
        private Dictionary<string, string>? _strings; 
        private Dictionary<string, ulong>? _uint64s; 
        private Dictionary<string, long>? _int64s; 
        private Dictionary<string, uint>? _uint32s; 
        private Dictionary<string, int>? _int32s; 
        private Dictionary<string, ushort>? _uint16s; 
        private Dictionary<string, short>? _int16s; 
        private Dictionary<string, bool>? _booleans; 
        private Dictionary<string, float>? _floats; 
        private Dictionary<string, double>? _doubles; 
        private Dictionary<string, (long, long)>? _int64Arrays; 

        public bool Contains(string key)
        {
            static bool DictContains<T>(Dictionary<string, T>? dict, string key)
            {
                return dict != null && dict.ContainsKey(key);
            }

            return DictContains(_strings, key)
                || DictContains(_uint64s, key)
                || DictContains(_int64s, key)
                || DictContains(_uint32s, key)
                || DictContains(_int32s, key)
                || DictContains(_uint16s, key)
                || DictContains(_int16s, key)
                || DictContains(_booleans, key)
                || DictContains(_floats, key)
                || DictContains(_doubles, key)
                || DictContains(_int64Arrays, key);
        }

        public void Add<TChar>(ref YARGTextContainer<TChar> container, in IniModifierOutline outline, bool isChartFile)
            where TChar : unmanaged, IConvertible
        {
            switch (outline.Type)
            {
                case ModifierType.String:
                    _strings ??= new Dictionary<string, string>();
                    _strings[outline.Output] = RichTextUtils.ReplaceColorNames(YARGTextReader.ExtractText(ref container, isChartFile));
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
                case ModifierType.Int64:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out long value))
                        {
                            value = 0;
                        }
                        _int64s ??= new Dictionary<string, long>();
                        _int64s[outline.Output] = value;
                        break;
                    }
                case ModifierType.UInt32:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out uint value))
                        {
                            value = 0;
                        }
                        _uint32s ??= new Dictionary<string, uint>();
                        _uint32s[outline.Output] = value;
                        break;
                    }
                case ModifierType.Int32:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out int value))
                        {
                            value = 0;
                        }
                        _int32s ??= new Dictionary<string, int>();
                        _int32s[outline.Output] = value;
                        break;
                    }
                case ModifierType.UInt16:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out ushort value))
                        {
                            value = 0;
                        }
                        _uint16s ??= new Dictionary<string, ushort>();
                        _uint16s[outline.Output] = value;
                        break;
                    }
                case ModifierType.Int16:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out short value))
                        {
                            value = 0;
                        }
                        _int16s ??= new Dictionary<string, short>();
                        _int16s[outline.Output] = value;
                        break;
                    }
                case ModifierType.Bool:
                    _booleans ??= new Dictionary<string, bool>();
                    _booleans[outline.Output] = YARGTextReader.ExtractBoolean(in container);
                    break;
                case ModifierType.Float:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out float value))
                        {
                            value = 0;
                        }
                        _floats ??= new Dictionary<string, float>();
                        _floats[outline.Output] = value;
                        break;
                    }
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
                case ModifierType.Int64Array:
                    long i641, i642;
                    if (YARGTextReader.TryExtract(ref container, out i641))
                    {
                        YARGTextReader.SkipWhitespaceAndEquals(ref container);
                        if (!YARGTextReader.TryExtract(ref container, out i642))
                        {
                            i642 = -1;
                        }
                    }
                    else
                    {
                        i641 = -1;
                        i642 = -1;
                    }
                    _int64Arrays ??= new Dictionary<string, (long, long)>();
                    _int64Arrays[outline.Output] = (i641, i642);
                    break;
            }
        }

        public void AddSng(ref YARGTextContainer<byte> container, int length, in IniModifierOutline outline)
        {
            switch (outline.Type)
            {
                case ModifierType.String:
                    unsafe
                    {
                        _strings ??= new Dictionary<string, string>();
                        _strings[outline.Output] = RichTextUtils.ReplaceColorNames(Encoding.UTF8.GetString(container.PositionPointer, length));
                    }
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
                case ModifierType.Int64:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out long value))
                        {
                            value = 0;
                        }
                        _int64s ??= new Dictionary<string, long>();
                        _int64s[outline.Output] = value;
                        break;
                    }
                case ModifierType.UInt32:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out uint value))
                        {
                            value = 0;
                        }
                        _uint32s ??= new Dictionary<string, uint>();
                        _uint32s[outline.Output] = value;
                        break;
                    }
                case ModifierType.Int32:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out int value))
                        {
                            value = 0;
                        }
                        _int32s ??= new Dictionary<string, int>();
                        _int32s[outline.Output] = value;
                        break;
                    }
                case ModifierType.UInt16:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out ushort value))
                        {
                            value = 0;
                        }
                        _uint16s ??= new Dictionary<string, ushort>();
                        _uint16s[outline.Output] = value;
                        break;
                    }
                case ModifierType.Int16:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out short value))
                        {
                            value = 0;
                        }
                        _int16s ??= new Dictionary<string, short>();
                        _int16s[outline.Output] = value;
                        break;
                    }
                case ModifierType.Bool:
                    _booleans ??= new Dictionary<string, bool>();
                    _booleans[outline.Output] = YARGTextReader.ExtractBoolean(in container);
                    break;
                case ModifierType.Float:
                    {
                        if (!YARGTextReader.TryExtract(ref container, out float value))
                        {
                            value = 0;
                        }
                        _floats ??= new Dictionary<string, float>();
                        _floats[outline.Output] = value;
                        break;
                    }
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
                case ModifierType.Int64Array:
                    long i641, i642;
                    if (!YARGTextReader.TryExtractWithWhitespace(ref container, out i641))
                    {
                        i641 = -1;
                        i642 = -1;
                    }
                    else if (!YARGTextReader.TryExtract(ref container, out i642))
                    {
                        i642 = -1;
                    }
                    _int64Arrays ??= new Dictionary<string, (long, long)>();
                    _int64Arrays[outline.Output] = (i641, i642);
                    break;
            }
        }

        public void Union(IniModifierCollection source)
        {
            Union(ref _strings, source._strings);
            Union(ref _uint64s, source._uint64s);
            Union(ref _int64s, source._int64s);
            Union(ref _uint32s, source._uint32s);
            Union(ref _int32s, source._int32s);
            Union(ref _uint16s, source._uint16s);
            Union(ref _int16s, source._int16s);
            Union(ref _booleans, source._booleans);
            Union(ref _floats, source._floats);
            Union(ref _doubles, source._doubles);
            Union(ref _int64Arrays, source._int64Arrays);
        }

        public bool IsEmpty()
        {
            static bool DictEmpty<T>(Dictionary<string, T>? dict)
            {
                return dict == null || dict.Count == 0;
            }

            return DictEmpty(_strings)
                && DictEmpty(_uint64s)
                && DictEmpty(_int64s)
                && DictEmpty(_uint32s)
                && DictEmpty(_int32s)
                && DictEmpty(_uint16s)
                && DictEmpty(_int16s)
                && DictEmpty(_booleans)
                && DictEmpty(_floats)
                && DictEmpty(_doubles)
                && DictEmpty(_int64Arrays);
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

        public bool Extract(string key, out long value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Int64, "Mismatched modifier types - Int64 requested");
#endif
            value = default!;
            return _int64s != null && _int64s.Remove(key, out value);
        }

        public bool Extract(string key, out uint value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.UInt32, "Mismatched modifier types - UInt32 requested");
#endif
            value = default!;
            return _uint32s != null && _uint32s.Remove(key, out value);
        }

        public bool Extract(string key, out int value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Int32, "Mismatched modifier types - Int32 requested");
#endif
            value = default!;
            return _int32s != null && _int32s.Remove(key, out value);
        }

        public bool Extract(string key, out ushort value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.UInt16, "Mismatched modifier types - UInt16 requested");
#endif
            value = default!;
            return _uint16s != null && _uint16s.Remove(key, out value);
        }

        public bool Extract(string key, out short value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Int16, "Mismatched modifier types - Int16 requested");
#endif
            value = default!;
            return _int16s != null && _int16s.Remove(key, out value);
        }

        public bool Extract(string key, out bool value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Bool, "Mismatched modifier types - Boolean requested");
#endif
            value = default!;
            return _booleans != null && _booleans.Remove(key, out value);
        }

        public bool Extract(string key, out float value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Float, "Mismatched modifier types - Float requested");
#endif
            value = default!;
            return _floats != null && _floats.Remove(key, out value);
        }

        public bool Extract(string key, out double value)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Double, "Mismatched modifier types - Double requested");
#endif
            value = default!;
            return _doubles != null && _doubles.Remove(key, out value);
        }

        public bool Extract(string key, out (long, long) values)
        {
#if DEBUG
            ThrowIfMismatch(key, ModifierType.Int64Array, "Mismatched modifier types - Int64Array requested");
#endif
            values = default!;
            return _int64Arrays != null && _int64Arrays.Remove(key, out values);
        }

        private static void Union<TValue>(ref Dictionary<string, TValue>? dest, Dictionary<string, TValue>? source)
        {
            if (source != null)
            {
                dest ??= new Dictionary<string, TValue>();
                foreach (var node in source)
                {
                    dest[node.Key] = node.Value;
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

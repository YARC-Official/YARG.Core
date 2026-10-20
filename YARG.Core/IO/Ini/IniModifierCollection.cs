using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Whole milliseconds, or seconds when written with a decimal point. Stored as Int64
        Delay,
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
            // In song.ini, -1 in a number key means not set, also over an earlier line of the key
            if (!isChartFile && IsMinusOne(container, outline.Type))
            {
                Remove(in outline);
                return;
            }

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
                case ModifierType.Delay:
                    {
                        int start = container.Position;
                        if (!TryParseSeconds(YARGTextReader.ExtractText(ref container, false), out long value))
                        {
                            container.Position = start;
                            if (!YARGTextReader.TryExtract(ref container, out value))
                            {
                                value = 0;
                            }
                            else if (value == -1)
                            {
                                Remove(in outline);
                                break;
                            }
                        }
                        _int64s ??= new Dictionary<string, long>();
                        _int64s[outline.Output] = value;
                        break;
                    }
            }
        }

        public void AddSng(ref YARGTextContainer<byte> container, int length, in IniModifierOutline outline)
        {
            // As in song.ini, -1 in a number key means not set
            if (IsMinusOne(container, outline.Type))
            {
                Remove(in outline);
                return;
            }

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
                case ModifierType.Delay:
                    {
                        string text;
                        unsafe
                        {
                            text = Encoding.UTF8.GetString(container.PositionPointer, length);
                        }

                        if (!TryParseSeconds(text, out long value))
                        {
                            if (!YARGTextReader.TryExtract(ref container, out value))
                            {
                                value = 0;
                            }
                            else if (value == -1)
                            {
                                Remove(in outline);
                                break;
                            }
                        }
                        _int64s ??= new Dictionary<string, long>();
                        _int64s[outline.Output] = value;
                        break;
                    }
            }
        }

        /// <summary>
        /// Whether a whole-number value is -1, which in song.ini and .sng metadata means not set,
        /// as if the key weren't there. A delay's -1 is checked where it's read, after its decimal point.
        /// </summary>
        private static bool IsMinusOne<TChar>(YARGTextContainer<TChar> text, ModifierType type)
            where TChar : unmanaged, IConvertible
        {
            switch (type)
            {
                case ModifierType.UInt64:
                case ModifierType.Int64:
                case ModifierType.UInt32:
                case ModifierType.Int32:
                case ModifierType.UInt16:
                case ModifierType.Int16:
                    // Read as signed, so that an unsigned key's -1 is found too
                    return YARGTextReader.TryExtract(ref text, out long value) && value == -1;
                default:
                    return false;
            }
        }

        private void Remove(in IniModifierOutline outline)
        {
            switch (outline.Type)
            {
                case ModifierType.UInt64: _uint64s?.Remove(outline.Output); break;
                case ModifierType.Int64:
                case ModifierType.Delay:  _int64s?.Remove(outline.Output);  break;
                case ModifierType.UInt32: _uint32s?.Remove(outline.Output); break;
                case ModifierType.Int32:  _int32s?.Remove(outline.Output);  break;
                case ModifierType.UInt16: _uint16s?.Remove(outline.Output); break;
                case ModifierType.Int16:  _int16s?.Remove(outline.Output);  break;
            }
        }

        /// <summary>
        /// Reads a value with a decimal point as seconds, into whole milliseconds. Any fraction of a millisecond
        /// is dropped, and a value that isn't a number is 0.
        /// </summary>
        /// <returns>Whether the value has a decimal point</returns>
        private static bool TryParseSeconds(string text, out long milliseconds)
        {
            milliseconds = 0;
            if (text.IndexOf('.') < 0)
            {
                return false;
            }

            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal seconds))
            {
                try
                {
                    milliseconds = decimal.ToInt64(seconds * 1000);
                }
                catch (OverflowException)
                {
                    milliseconds = seconds > 0 ? long.MaxValue : long.MinValue;
                }
            }
            return true;
        }

        public void Union(IniModifierCollection source)
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

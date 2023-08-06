using System;
using YARG.Core.Extensions;

namespace YARG.Core.Parsing
{
    #region General
    /// <summary>
    /// A text event that occurs at a specific tick.
    /// </summary>
    public readonly ref struct DotChartTextEvent
    {
        public const string TYPE_STRING = "E";
        public const char TYPE_CHAR = 'E';

        public readonly uint Tick;
        public readonly ReadOnlySpan<char> Text;

        public DotChartTextEvent(uint tick, ReadOnlySpan<char> text)
        {
            Tick = tick;
            Text = text;
        }

        public static bool TryParse(DotChartTickEvent tickEvent, out DotChartTextEvent textEvent)
        {
            textEvent = default;

            // Ensure event matches
            if (!tickEvent.Type.Equals(TYPE_STRING, StringComparison.Ordinal))
                return false;

            // Text events only have one parameter,
            // use the whole value and strip the start/end quotes
            var text = tickEvent.Value.TrimOnce('"');
            if (text.IsEmpty)
                return false;

            textEvent = new(tickEvent.Tick, text);
            return true;
        }
    }
    #endregion

    #region Tempo Map
    /// <summary>
    /// Specifies the current tempo of the chart, in beats per minute.
    /// </summary>
    public readonly ref struct DotChartTempoEvent
    {
        public const string TYPE_STRING = "B";
        public const char TYPE_CHAR = 'B';

        public readonly uint Tick;
        public readonly float Tempo;

        public DotChartTempoEvent(uint tick, float tempo)
        {
            Tick = tick;
            Tempo = tempo;
        }

        public static bool TryParse(DotChartTickEvent tickEvent, out DotChartTempoEvent tempoEvent)
        {
            tempoEvent = default;

            // Ensure event matches
            if (!tickEvent.Type.Equals(TYPE_STRING, StringComparison.Ordinal))
                return false;

            // Get parameters
            // Parameter count is not given so as to avoid any validation exceptions
            var parameters = tickEvent.GetParameters(/*1*/);
            var tempoStr = parameters.GetNext();
            if (tempoStr.IsEmpty)
                return false;

            if (!uint.TryParse(tempoStr, out uint tempo))
                return false;

            // Tempo is notated as a whole number, with the bottom 3 digits being the decimal value
            // For example, 120 BPM is written as 'B 120000'
            tempoEvent = new(tickEvent.Tick, tempo / 1000f);
            return true;
        }
    }

    /// <summary>
    /// Anchors a tempo to a specific microsecond value relative to the audio.
    /// </summary>
    public readonly ref struct DotChartAnchorEvent
    {
        public const string TYPE_STRING = "A";
        public const char TYPE_CHAR = 'A';

        public readonly uint Tick;
        public readonly double Time;

        public DotChartAnchorEvent(uint tick, double time)
        {
            Tick = tick;
            Time = time;
        }

        public static bool TryParse(DotChartTickEvent tickEvent, out DotChartAnchorEvent anchorEvent)
        {
            anchorEvent = default;

            // Ensure event matches
            if (!tickEvent.Type.Equals(TYPE_STRING, StringComparison.Ordinal))
                return false;

            // Get parameters
            // Parameter count is not given so as to avoid any validation exceptions
            var parameters = tickEvent.GetParameters(/*1*/);
            var timeStr = parameters.GetNext();
            if (timeStr.IsEmpty)
                return false;

            if (!ulong.TryParse(timeStr, out ulong time))
                return false;

            // The time is notated in microseconds, with the first 6 digits becoming the decimal value
            // For example, a time of 2.25 seconds is written as 'A 2250000'
            anchorEvent = new(tickEvent.Tick, time / 1000.0 / 1000.0);
            return true;
        }
    }

    /// <summary>
    /// A time signature event in a .chart section.
    /// </summary>
    public readonly ref struct DotChartTimeSignatureEvent
    {
        public const string TYPE_STRING = "TS";

        public readonly uint Tick;
        public readonly uint Numerator;
        public readonly uint Denominator;

        public DotChartTimeSignatureEvent(uint tick, uint numerator, uint denominator)
        {
            Tick = tick;
            Numerator = numerator;
            Denominator = denominator;
        }

        public static bool TryParse(DotChartTickEvent tickEvent, out DotChartTimeSignatureEvent timeSignatureEvent)
        {
            timeSignatureEvent = default;

            // Ensure event matches
            if (!tickEvent.Type.Equals(TYPE_STRING, StringComparison.Ordinal))
                return false;

            // Get parameters
            // Parameter count is not given so as to avoid any validation exceptions
            var parameters = tickEvent.GetParameters(/*1, 2*/);
            var numeratorStr = parameters.GetNext();
            var denominatorStr = parameters.GetNext();
            if (numeratorStr.IsEmpty || !uint.TryParse(numeratorStr, out uint numerator))
                return false;

            // The denominator is specified as a power of 2, and is also optional
            // If not specified, it defaults to 2, for a denominator of 4
            // 4/4 is written as 'TS 4' or 'TS 4 2', 3/8 is written as 'TS 3 3'
            uint denominatorPower = 2;
            if (!denominatorStr.IsEmpty && !uint.TryParse(denominatorStr, out denominatorPower))
                return false;

            timeSignatureEvent = new(tickEvent.Tick, numerator, (uint)Math.Pow(2, denominatorPower));
            return true;
        }
    }
    #endregion

    #region Instrument
    /// <summary>
    /// A note event in an instrument track.
    /// </summary>
    public readonly ref struct DotChartNoteEvent
    {
        public const string TYPE_STRING = "N";
        public const char TYPE_CHAR = 'N';

        public readonly uint Tick;
        public readonly uint Value;
        public readonly uint Length;

        public DotChartNoteEvent(uint tick, uint note, uint length)
        {
            Tick = tick;
            Value = note;
            Length = length;
        }

        public static bool TryParse(DotChartTickEvent tickEvent, out DotChartNoteEvent noteEvent)
        {
            noteEvent = default;

            // Ensure event matches
            if (!tickEvent.Type.Equals(TYPE_STRING, StringComparison.Ordinal))
                return false;

            // Get parameters
            // Parameter count is not given so as to avoid any validation exceptions
            var parameters = tickEvent.GetParameters(/*2*/);
            var valueStr = parameters.GetNext();
            var lengthStr = parameters.GetNext();
            if (valueStr.IsEmpty || lengthStr.IsEmpty)
                return false;

            if (!uint.TryParse(valueStr, out uint value) || !uint.TryParse(lengthStr, out uint length))
                return false;

            noteEvent = new(tickEvent.Tick, value, length);
            return true;
        }
    }

    /// <summary>
    /// A phrase event in an instrument track.
    /// </summary>
    public readonly ref struct DotChartPhraseEvent
    {
        public const string TYPE_STRING = "S";
        public const char TYPE_CHAR = 'S';

        public readonly uint Tick;
        public readonly uint Value;
        public readonly uint Length;

        public DotChartPhraseEvent(uint tick, uint value, uint length)
        {
            Tick = tick;
            Value = value;
            Length = length;
        }

        public static bool TryParse(DotChartTickEvent tickEvent, out DotChartPhraseEvent phraseEvent)
        {
            phraseEvent = default;

            // Ensure event matches
            if (!tickEvent.Type.Equals(TYPE_STRING, StringComparison.Ordinal))
                return false;

            // Get parameters
            // Parameter count is not given so as to avoid any validation exceptions
            var parameters = tickEvent.GetParameters(/*2*/);
            var valueStr = parameters.GetNext();
            var lengthStr = parameters.GetNext();
            if (valueStr.IsEmpty || lengthStr.IsEmpty)
                return false;

            if (!uint.TryParse(valueStr, out uint value) || !uint.TryParse(lengthStr, out uint length))
                return false;

            phraseEvent = new(tickEvent.Tick, value, length);
            return true;
        }
    }
    #endregion

    #region Guitar
    /// <summary>
    /// Specifies the hand position for the guitarist.
    /// </summary>
    public readonly ref struct DotChartHandPositionEvent
    {
        public const string TYPE_STRING = "H";
        public const char TYPE_CHAR = 'H';

        public readonly uint Tick;
        public readonly uint Position;
        public readonly uint Length;

        public DotChartHandPositionEvent(uint tick, uint position, uint length)
        {
            Tick = tick;
            Position = position;
            Length = length;
        }

        public static bool TryParse(DotChartTickEvent tickEvent, out DotChartHandPositionEvent positionEvent)
        {
            positionEvent = default;

            // Ensure event matches
            if (!tickEvent.Type.Equals(TYPE_STRING, StringComparison.Ordinal))
                return false;

            // Get parameters
            // Parameter count is not given so as to avoid any validation exceptions
            var parameters = tickEvent.GetParameters(/*2*/);
            var positionStr = parameters.GetNext();
            var lengthStr = parameters.GetNext();
            if (positionStr.IsEmpty || lengthStr.IsEmpty)
                return false;

            if (!uint.TryParse(positionStr, out uint position) || !uint.TryParse(lengthStr, out uint length))
                return false;

            positionEvent = new(tickEvent.Tick, position, length);
            return true;
        }
    }
    #endregion
}
using System;
using EasySharpIni.Converters;

namespace YARG.Core
{
    public class TimeConverter : Converter<double>
    {
        public double IntegerScaleFactor { get; set; }

        public bool AllowTimeSpans { get; set; } = true;
        public bool AllowDecimals { get; set; } = true;
        public bool AllowIntegers { get; set; } = true;

        public override string GetDefaultName()
        {
            return "Time";
        }

        public override double GetDefaultValue()
        {
            return 0.0;
        }

        public override bool Parse(string arg, out double result)
        {
            if (arg.Contains(':') && AllowTimeSpans && TimeSpan.TryParse(arg, out var timeSpan))
            {
                result = timeSpan.TotalSeconds;
                return true;
            }
            else if (arg.Contains('.') && AllowDecimals && double.TryParse(arg, out double timeDouble))
            {
                result = timeDouble;
                return true;
            }
            else if (AllowIntegers && int.TryParse(arg, out int timeInt))
            {
                result = timeInt * IntegerScaleFactor;
                return true;
            }

            result = GetDefaultValue();
            return false;
        }
    }
}
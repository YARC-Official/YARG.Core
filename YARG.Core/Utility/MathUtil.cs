using System.Diagnostics.CodeAnalysis;
using YARG.Core.Extensions;

namespace YARG.Core.Utility
{
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Copied code")]
    public static class MathUtil
    {
        // Backported from newer .NET
        private const ulong PositiveZeroBits = 0x0000_0000_0000_0000;
        private const ulong NegativeZeroBits = 0x8000_0000_0000_0000;

        private const ulong PositiveInfinityBits = 0x7FF0_0000_0000_0000;
        private const ulong NegativeInfinityBits = 0xFFF0_0000_0000_0000;

        // https://github.com/dotnet/runtime/blob/4bbd32d81f97096e9b51ed76ec0e202962e3a115/src/libraries/System.Private.CoreLib/src/System/Math.cs#L287
        public static double BitIncrement(double x)
        {
            ulong bits = UnsafeExtensions.DoubleToUInt64Bits(x);

            if (!double.IsFinite(x))
            {
                // NaN returns NaN
                // -Infinity returns MinValue
                // +Infinity returns +Infinity
                return (bits == NegativeInfinityBits) ? double.MinValue : x;
            }

            if (bits == NegativeZeroBits)
            {
                // -0.0 returns Epsilon
                return double.Epsilon;
            }

            // Negative values need to be decremented
            // Positive values need to be incremented

            if (double.IsNegative(x))
            {
                bits -= 1;
            }
            else
            {
                bits += 1;
            }

            return UnsafeExtensions.UInt64BitsToDouble(bits);
        }

        // https://github.com/dotnet/runtime/blob/4bbd32d81f97096e9b51ed76ec0e202962e3a115/src/libraries/System.Private.CoreLib/src/System/Math.cs#L287
        public static double BitDecrement(double x)
        {
            ulong bits = UnsafeExtensions.DoubleToUInt64Bits(x);

            if (!double.IsFinite(x))
            {
                // NaN returns NaN
                // -Infinity returns -Infinity
                // +Infinity returns MaxValue
                return (bits == PositiveInfinityBits) ? double.MaxValue : x;
            }

            if (bits == PositiveZeroBits)
            {
                // +0.0 returns -double.Epsilon
                return -double.Epsilon;
            }

            // Negative values need to be incremented
            // Positive values need to be decremented

            if (double.IsNegative(x))
            {
                bits += 1;
            }
            else
            {
                bits -= 1;
            }

            return UnsafeExtensions.UInt64BitsToDouble(bits);
        }
    }
}
using System.Collections;
using MoonscraperChartEditor.Song;

namespace YARG.Core.UnitTests.Parsing
{
    internal class SongObjectComparer : IComparer<SongObject>, IComparer
    {
        public int Compare(SongObject? x, SongObject? y)
        {
            // Some SongObject types need additional comparison logic that can't be added directly
            // without potentially impacting object sorting in a negative way
            switch ((x, y))
            {
                case (BPM bx, BPM by):
                    if (bx == by)
                    {
                        if (bx.value > by.value)
                            return 1;
                        else if (bx.value < by.value)
                            return -1;
                        return 0;
                    }
                    goto default;

                case (TimeSignature tx, TimeSignature ty):
                    if (tx == ty)
                    {
                        if (tx.numerator > ty.numerator || tx.denominator > ty.denominator)
                            return 1;
                        else if (tx.numerator < ty.numerator || tx.denominator < ty.denominator)
                            return -1;
                        return 0;
                    }
                    goto default;

                default:
                    if (x == y)
                        return 0;
                    else if (x > y)
                        return 1;
                    return -1;
            }
        }

        public int Compare(object? x, object? y)
        {
            if (x is SongObject sx && y is SongObject sy)
                return Compare(sx, sy);

            throw new InvalidOperationException();
        }
    }
}
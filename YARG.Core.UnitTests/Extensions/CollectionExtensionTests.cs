using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using YARG.Core.Extensions;

using TimeData = (double time, int value);

namespace YARG.Core.UnitTests.Extensions;

[SuppressMessage(
    "Assertion",
    "NUnit2045:Use Assert.Multiple",
    Justification = @"Does more harm than good in this particular test set.
        Obscures exact failure points, making troubleshooting and debugging harder."
)]
public class CollectionExtensionTests
{
    private static SearchComparison<TimeData, double> _timeComparer = (ev, target) => ev.time.CompareTo(target);
    private static SearchComparison<TimeData, int> _valueComparer = (ev, target) => ev.value.CompareTo(target);

    private List<TimeData> _items = [
        (0.0, 1),  // 0
        (5.0, 2),  // 1
        (8.0, 2),  // 2
        (10.0, 4), // 3
        (12.0, 4), // 4
        (16.0, 5), // 5
        (19.0, 7), // 6
        (20.0, 7), // 7
    ];

    [Test]
    public void BinarySearch()
    {
        Assert.That(_items.BinarySearch(5.0, _timeComparer), Is.EqualTo(1));
        Assert.That(_items.BinarySearch(8.0, _timeComparer), Is.EqualTo(2));
        Assert.That(_items.BinarySearch(10.0, _timeComparer), Is.EqualTo(3));
        Assert.That(_items.BinarySearch(12.0, _timeComparer), Is.EqualTo(4));
        Assert.That(_items.BinarySearch(16.0, _timeComparer), Is.EqualTo(5));
        Assert.That(_items.BinarySearch(19.0, _timeComparer), Is.EqualTo(6));
        Assert.That(_items.BinarySearch(20.0, _timeComparer), Is.EqualTo(7));

        Assert.That(_items.BinarySearch(1, _valueComparer), Is.EqualTo(0));
        Assert.That(_items.BinarySearch(2, _valueComparer), Is.EqualTo(1));
        Assert.That(_items.BinarySearch(4, _valueComparer), Is.EqualTo(3));
        Assert.That(_items.BinarySearch(5, _valueComparer), Is.EqualTo(5));
        Assert.That(_items.BinarySearch(7, _valueComparer), Is.EqualTo(6));

        // Behavior of in-between values varies depending on the length of the list
        // and the relative position of the value, so those are not tested here
    }

    [Test]
    public void LowerBound()
    {
        void AssertTimeBound(double value, int beforeIndex, int afterIndex)
        {
            Assert.That(_items.LowerBound(value, _timeComparer, before: true), Is.EqualTo(beforeIndex));
            Assert.That(_items.LowerBound(value, _timeComparer, before: false), Is.EqualTo(afterIndex));
        }

        void AssertValueBound(int value, int beforeIndex, int afterIndex)
        {
            Assert.That(_items.LowerBound(value, _valueComparer, before: true), Is.EqualTo(beforeIndex));
            Assert.That(_items.LowerBound(value, _valueComparer, before: false), Is.EqualTo(afterIndex));
        }

        // Exact values
        AssertTimeBound(0.0, 0, 0);
        AssertTimeBound(5.0, 1, 1);
        AssertTimeBound(8.0, 2, 2);
        AssertTimeBound(10.0, 3, 3);
        AssertTimeBound(12.0, 4, 4);
        AssertTimeBound(16.0, 5, 5);
        AssertTimeBound(19.0, 6, 6);
        AssertTimeBound(20.0, 7, 7);

        AssertValueBound(1, 0, 0);
        AssertValueBound(2, 1, 1);
        AssertValueBound(4, 3, 3);
        AssertValueBound(5, 5, 5);
        AssertValueBound(7, 6, 6);

        // Between/missing values
        AssertTimeBound(-1.0, -1, 0);
        AssertTimeBound(3.0, 0, 1);
        AssertTimeBound(11.0, 3, 4);
        AssertTimeBound(18.0, 5, 6);
        AssertTimeBound(23.0, 7, 8);

        AssertValueBound(-1, -1, 0);
        AssertValueBound(3, 2, 3);
        AssertValueBound(6, 5, 6);
        AssertValueBound(8, 7, 8);
    }

    [Test]
    public void UpperBound()
    {
        // Exact values
        Assert.That(_items.UpperBound(0.0, _timeComparer), Is.EqualTo(1));
        Assert.That(_items.UpperBound(5.0, _timeComparer), Is.EqualTo(2));
        Assert.That(_items.UpperBound(8.0, _timeComparer), Is.EqualTo(3));
        Assert.That(_items.UpperBound(10.0, _timeComparer), Is.EqualTo(4));
        Assert.That(_items.UpperBound(12.0, _timeComparer), Is.EqualTo(5));
        Assert.That(_items.UpperBound(16.0, _timeComparer), Is.EqualTo(6));
        Assert.That(_items.UpperBound(19.0, _timeComparer), Is.EqualTo(7));
        Assert.That(_items.UpperBound(20.0, _timeComparer), Is.EqualTo(8));

        Assert.That(_items.UpperBound(1, _valueComparer), Is.EqualTo(1));
        Assert.That(_items.UpperBound(2, _valueComparer), Is.EqualTo(3));
        Assert.That(_items.UpperBound(4, _valueComparer), Is.EqualTo(5));
        Assert.That(_items.UpperBound(5, _valueComparer), Is.EqualTo(6));
        Assert.That(_items.UpperBound(7, _valueComparer), Is.EqualTo(8));

        // Between/missing values
        Assert.That(_items.UpperBound(-1.0, _timeComparer), Is.EqualTo(0));
        Assert.That(_items.UpperBound(3.0, _timeComparer), Is.EqualTo(1));
        Assert.That(_items.UpperBound(11.0, _timeComparer), Is.EqualTo(4));
        Assert.That(_items.UpperBound(18.0, _timeComparer), Is.EqualTo(6));
        Assert.That(_items.UpperBound(23.0, _timeComparer), Is.EqualTo(8));

        Assert.That(_items.UpperBound(-1, _valueComparer), Is.EqualTo(0));
        Assert.That(_items.UpperBound(3, _valueComparer), Is.EqualTo(3));
        Assert.That(_items.UpperBound(6, _valueComparer), Is.EqualTo(6));
        Assert.That(_items.UpperBound(8, _valueComparer), Is.EqualTo(8));
    }

    [Test]
    public void FindEqualRange()
    {
        void AssertTimeRange(double value, Range range)
        {
            Assert.That(_items.FindEqualRange(value, _timeComparer, out var found), Is.True);
            Assert.That(found, Is.EqualTo(range));
        }

        void AssertValueRange(int value, Range range)
        {
            Assert.That(_items.FindEqualRange(value, _valueComparer, out var found), Is.True);
            Assert.That(found, Is.EqualTo(range));
        }

        // Exact values
        AssertTimeRange(0.0, 0..1);
        AssertTimeRange(5.0, 1..2);
        AssertTimeRange(8.0, 2..3);
        AssertTimeRange(10.0, 3..4);
        AssertTimeRange(12.0, 4..5);
        AssertTimeRange(16.0, 5..6);
        AssertTimeRange(19.0, 6..7);
        AssertTimeRange(20.0, 7..8);

        AssertValueRange(1, 0..1);
        AssertValueRange(2, 1..3);
        AssertValueRange(4, 3..5);
        AssertValueRange(5, 5..6);
        AssertValueRange(7, 6..8);

        // Between/missing values
        Assert.That(_items.FindEqualRange(-1.0, _timeComparer, out _), Is.False);
        Assert.That(_items.FindEqualRange(3.0, _timeComparer, out _), Is.False);
        Assert.That(_items.FindEqualRange(11.0, _timeComparer, out _), Is.False);
        Assert.That(_items.FindEqualRange(18.0, _timeComparer, out _), Is.False);
        Assert.That(_items.FindEqualRange(23.0, _timeComparer, out _), Is.False);

        Assert.That(_items.FindEqualRange(-1, _valueComparer, out _), Is.False);
        Assert.That(_items.FindEqualRange(3, _valueComparer, out _), Is.False);
        Assert.That(_items.FindEqualRange(6, _valueComparer, out _), Is.False);
        Assert.That(_items.FindEqualRange(8, _valueComparer, out _), Is.False);
    }

    [Test]
    public void FindRange()
    {
        void AssertRange<T>(T start, T end, SearchComparison<TimeData, T> comparison, bool endInclusive, Range? _range)
            where T : IComparable<T>
        {
            if (_range is {} range)
            {
                Assert.That(_items.FindRange(start, end, comparison, endInclusive, out var found), Is.True);
                Assert.That(found, Is.EqualTo(range));
            }
            else
            {
                Assert.That(_items.FindRange(start, end, comparison, endInclusive, out _), Is.False);
            }
        }

        void AssertTimeRange(double start, double end, Range? rangeExclusive, Range? rangeInclusive)
        {
            AssertRange(start, end, _timeComparer, endInclusive: false, rangeExclusive);
            AssertRange(start, end, _timeComparer, endInclusive: true, rangeInclusive);
        }

        void AssertValueRange(int start, int end, Range? rangeExclusive, Range? rangeInclusive)
        {
            AssertRange(start, end, _valueComparer, endInclusive: false, rangeExclusive);
            AssertRange(start, end, _valueComparer, endInclusive: true, rangeInclusive);
        }

        Assert.Throws<InvalidOperationException>(() => _items.FindRange(0.0, -1.0, _timeComparer, endInclusive: false, out _));
        Assert.Throws<InvalidOperationException>(() => _items.FindRange(0, -1, _valueComparer, endInclusive: false, out _));

        // Exact values
        AssertTimeRange(0.0, 0.0, null, 0..1);
        AssertTimeRange(5.0, 5.0, null, 1..2);
        AssertTimeRange(8.0, 8.0, null, 2..3);
        AssertTimeRange(10.0, 10.0, null, 3..4);
        AssertTimeRange(12.0, 12.0, null, 4..5);
        AssertTimeRange(16.0, 16.0, null, 5..6);
        AssertTimeRange(19.0, 19.0, null, 6..7);
        AssertTimeRange(20.0, 20.0, null, 7..8);

        AssertValueRange(1, 1, null, 0..1);
        AssertValueRange(2, 2, null, 1..3);
        AssertValueRange(4, 4, null, 3..5);
        AssertValueRange(5, 5, null, 5..6);
        AssertValueRange(7, 7, null, 6..8);

        // Broad ranges
        AssertTimeRange(-10.0, 0.0, null, 0..1);
        AssertTimeRange(0.0, 10.0, 0..3, 0..4);
        AssertTimeRange(10.0, 20.0, 3..7, 3..8);
        AssertTimeRange(20.0, 30.0, 7..8, 7..8);
        AssertTimeRange(30.0, 40.0, null, null);

        AssertValueRange(-5, 0, null, null);
        AssertValueRange(0, 5, 0..5, 0..6);
        AssertValueRange(5, 10, 5..8, 5..8);
        AssertValueRange(10, 15, null, null);

        // Specific ranges
        AssertTimeRange(0.0, 5.0, 0..1, 0..2);
        AssertTimeRange(5.0, 8.0, 1..2, 1..3);
        AssertTimeRange(8.0, 10.0, 2..3, 2..4);
        AssertTimeRange(10.0, 12.0, 3..4, 3..5);
        AssertTimeRange(12.0, 16.0, 4..5, 4..6);
        AssertTimeRange(16.0, 19.0, 5..6, 5..7);
        AssertTimeRange(19.0, 20.0, 6..7, 6..8);
        AssertTimeRange(20.0, 25.0, 7..8, 7..8);

        AssertValueRange(1, 2, 0..1, 0..3);
        AssertValueRange(2, 4, 1..3, 1..5);
        AssertValueRange(4, 5, 3..5, 3..6);
        AssertValueRange(5, 7, 5..6, 5..8);
        AssertValueRange(7, 8, 6..8, 6..8);
    }
}

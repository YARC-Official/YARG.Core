using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace YARG.Core.Extensions
{
    public delegate int SearchComparison<in TItem, in TSearch>(TItem item, TSearch target);

    public static class CollectionExtensions
    {
        /// <summary>
        /// Duplicates the list and every element inside it, such that the new list is
        /// entirely independent and shares no references with the original.
        /// </summary>
        public static List<T> Duplicate<T>(this List<T> list)
            where T : ICloneable<T>
        {
            var newlist = new List<T>();

            foreach (var ev in list)
            {
                var newEvent = ev.Clone();
                newlist.Add(newEvent);
            }

            return newlist;
        }

        /// <summary>
        /// Shuffles the list using the Fisher-Yates shuffle algorithm, using the given random number generator.
        /// </summary>
        /// <remarks>
        /// https://stackoverflow.com/questions/273313/randomize-a-listt
        /// </remarks>
        public static void Shuffle<T>(this List<T> list, Random random)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>
        /// Picks a random value from the list using the given random number generator.
        /// </summary>
        public static T PickRandom<T>(this List<T> list, Random random)
        {
            return list[random.Next(0, list.Count)];
        }

        /// <summary>
        /// Searches for an item in the sorted list, using the given target value and comparer function.
        /// </summary>
        /// <returns>
        /// The index of the item in the list, or -1 if the list contains no elements.
        /// If no match was found, or multiple matches exist, the exact pick is unspecified, but will be
        /// as close to the target value index-wise as possible.
        /// Use <see cref="LowerBound"/> or <see cref="UpperBound"/> if precise behavior is needed.
        /// </returns>
        public static int BinarySearch<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer
        )
            where TList : IReadOnlyList<TItem>
        {
            int min = 0;
            int max = list.Count - 1;
            int index = -1;

            while (min <= max)
            {
                // Select the midpoint of the current bounds
                index = (min + max) / 2;

                switch (comparer(list[index], target))
                {
                    case < 0:
                    {
                        // We're below the target, exclude current lower bound
                        min = index + 1;
                        break;
                    }
                    case > 0:
                    {
                        // We're above the target, exclude current higher bound
                        max = index - 1;
                        break;
                    }
                    default:
                    {
                        // Found a match
                        return index;
                    }
                }
            }

            return index;
        }

        /// <summary>
        /// Adjusts an index found through binary searching to the first occurence of the target value.
        /// If the target value does not exist in the list, adjusts according to the
        /// <c><paramref name="before"/></c> parameter.
        /// </summary>
        /// <param name="before">
        /// If no exact match is found, whether to further adjust the final index to be below the target value.
        /// Note that this may result in the index being outside the bounds of the list.
        /// </param>
        private static int AdjustToLowerBound<TList, TItem, TSearch>(
            TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer,
            int index,
            bool before
        )
            where TList : IReadOnlyList<TItem>
        {
            while (index - 1 >= 0 && comparer(list[index - 1], target) >= 0)
            {
                index--;
            }

            // Further correct final position based on the given bias
            if (before && comparer(list[index], target) > 0)
            {
                index--;
            }
            else if (!before && comparer(list[index], target) < 0)
            {
                index++;
            }

            return index;
        }

        /// <summary>
        /// Adjusts an index found through binary searching to the first occurence of the next value
        /// that is higher than the target value. Does nothing if the index value is already above the target.
        /// </summary>
        private static int AdjustToUpperBound<TList, TItem, TSearch>(
            TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer,
            int index
        )
            where TList : IReadOnlyList<TItem>
        {
            int count = list.Count;
            while (index < count && comparer(list[index], target) <= 0)
            {
                index++;
            }

            return index;
        }

        /// <summary>
        /// Searches for the first item in the sorted list that matches the target value,
        /// determined using the given comparer function.
        /// </summary>
        /// <param name="before">
        /// If no exact match is found, whether to further adjust the final index to be below the target value.
        /// A value of <see langword="false"/> produces an index that can be used as a sorted insertion index
        /// for the target value.
        /// </param>
        /// <returns>
        /// The index of the first matching item in the list.
        /// If multiple matches exist, the first occurrence (i.e. the lower bound) is picked.
        /// If no match was found, the index returned is that of the last item that matches the closest,
        /// with above/below being determined by the <c><paramref name="before"/></c> parameter.
        /// <br/>
        /// Note that the result index may be outside the bounds of the list depending on the exact
        /// value and the value of <c><paramref name="before"/></c>. Use
        /// <see cref="LowerBoundElement">LowerBoundElement</see> for a bounds-checked version that
        /// also retrieves the element value directly.
        /// </returns>
        public static int LowerBound<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer,
            bool before
        )
            where TList : IReadOnlyList<TItem>
        {
            int index = list.BinarySearch(target, comparer);
            if (index >= 0)
            {
                index = AdjustToLowerBound(list, target, comparer, index, before);
            }

            return index;
        }

        /// <summary>
        /// Searches for the first item in the sorted list that is greater than the target value,
        /// determined using the given comparer function.
        /// </summary>
        /// <returns>
        /// The index of the first matching item in the list, or -1 if the list contains no elements.
        /// If no higher value exists, an index past the bounds of the list is returned
        /// (i.e. <see cref="List{TItem}.Count"/>).
        /// <br/>
        /// Note that the result index may be outside the bounds of the list depending on the exact
        /// value. Use <see cref="UpperBoundElement">UpperBoundElement</see> for a bounds-checked version
        /// that also retrieves the element value directly.
        /// </returns>
        public static int UpperBound<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer
        )
            where TList : IReadOnlyList<TItem>
        {
            int index = list.BinarySearch(target, comparer);
            if (index >= 0)
            {
                index = AdjustToUpperBound(list, target, comparer, index);
            }

            return index;
        }

        /// <summary>
        /// Searches for the first item in the sorted list that matches the target value,
        /// determined using the given comparer function.
        /// </summary>
        /// <param name="before">
        /// If no exact match is found, whether to further adjust the final result to be below the target value.
        /// </param>
        /// <returns>
        /// True if the value was found, false otherwise.
        /// </returns>
        public static bool LowerBoundElement<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer,
            bool before,
            out TItem? value
        )
            where TList : IReadOnlyList<TItem>
        {
            int index = list.LowerBound(target, comparer, before);
            if (index >= 0 && index < list.Count)
            {
                value = list[index];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <inheritdoc cref="LowerBoundElement{TList, TItem, TSearch}(TList, TSearch, SearchComparison{TItem, TSearch}, bool, out TItem)"/>
        /// <returns>
        /// The value, if found; <c><see langword="default"/></c> otherwise.
        /// </returns>
        public static TItem? LowerBoundElement<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer,
            bool before
        )
            where TList : IReadOnlyList<TItem>
        {
            list.LowerBoundElement(target, comparer, before, out var value);
            return value;
        }

        /// <summary>
        /// Searches for the first item in the sorted list that is greater than the target value,
        /// determined using the given comparer function.
        /// </summary>
        /// <returns>
        /// True if the value was found, false otherwise.
        /// </returns>
        public static bool UpperBoundElement<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer,
            [MaybeNullWhen(false)] out TItem value
        )
            where TList : IReadOnlyList<TItem>
        {
            int index = list.UpperBound(target, comparer);
            if (index >= 0 && index < list.Count)
            {
                value = list[index];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <inheritdoc cref="UpperBoundElement{TList, TItem, TSearch}(TList, TSearch, SearchComparison{TItem, TSearch}, out TItem)"/>
        /// <returns>
        /// The value, if found; <c><see langword="default"/></c> otherwise.
        /// </returns>
        public static TItem? UpperBoundElement<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer
        )
            where TList : IReadOnlyList<TItem>
        {
            list.UpperBoundElement(target, comparer, out var value);
            return value;
        }

        /// <summary>
        /// Searches for all contiguous items in the sorted list that match the target value,
        /// determined using the given comparer function.
        /// </summary>
        /// <returns>
        /// True if at least one matching value was found, false otherwise.
        /// </returns>
        public static bool FindEqualRange<TList, TItem, TSearch>(
            this TList list,
            TSearch target,
            SearchComparison<TItem, TSearch> comparer,
            out Range range
        )
            where TList : IReadOnlyList<TItem>
        {
            int index = list.BinarySearch(target, comparer);
            if (index < 0 || comparer(list[index], target) != 0)
            {
                range = default;
                return false;
            }

            int startIndex = AdjustToLowerBound(list, target, comparer, index, before: false);
            int endIndex = AdjustToUpperBound(list, target, comparer, index);

            range = startIndex..endIndex;
            return true;
        }

        /// <summary>
        /// Searches for all items in the sorted list that lie between the given start and end values,
        /// determined using the given comparer function.
        /// </summary>
        /// <param name="endInclusive">
        /// Whether the end value should be treated as inclusive, rather than exclusive.
        /// </param>
        /// <returns>
        /// True if at least one value exists in the range, false otherwise.
        /// </returns>
        public static bool FindRange<TList, TItem, TSearch>(
            this TList list,
            TSearch start,
            TSearch end,
            SearchComparison<TItem, TSearch> comparer,
            bool endInclusive,
            out Range range
        )
            where TList : IReadOnlyList<TItem>
            where TSearch : IComparable<TSearch>
        {
            if (start.CompareTo(end) > 0)
            {
                throw new InvalidOperationException("Range start cannot be greater than range end");
            }

            int startIndex = list.LowerBound(start, comparer, before: false);
            int endIndex = endInclusive
                ? list.UpperBound(end, comparer)
                : list.LowerBound(end, comparer, before: false);

            if (startIndex < 0 || endIndex < 0 || startIndex >= endIndex)
            {
                range = default;
                return false;
            }

            range = startIndex..endIndex;
            return true;
        }

        /// <summary>
        /// Attempts to peek at the beginning of the queue.
        /// </summary>
        /// <returns>
        /// The peeked value, if available; otherwise the default value of <typeparamref name="T"/>.
        /// </returns>
        public static T? PeekOrDefault<T>(this Queue<T> queue)
        {
            if (queue.TryPeek(out var o))
            {
                return o;
            }

            return default;
        }

        /// <summary>
        /// Attempts to dequeue a value from the queue.
        /// </summary>
        /// <returns>
        /// The peeked value, if available; otherwise the default value of <typeparamref name="T"/>.
        /// </returns>
        public static T? DequeueOrDefault<T>(this Queue<T> queue)
        {
            if (queue.TryDequeue(out var o))
            {
                return o;
            }

            return default;
        }
    }
}
using System;
using System.Collections.Generic;

namespace YARG.Core.Extensions
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Searches for an item in the list using the given search object and comparer function.
        /// </summary>
        /// <returns>
        /// The item from the list, or default if the list contains no elements.<br/>
        /// If no exact match was found, the item returned is the one that matches the most closely.
        /// </returns>
        public static TItem BinarySearch<TItem, TSearch>(this IList<TItem> list, TSearch searchObject,
            Func<TItem, TSearch, int> comparer)
        {
            int index = list.BinarySearchIndex(searchObject, comparer);
            if (index < 0)
                return default;

            return list[index];
        }

        /// <summary>
        /// Searches for an item in the list using the given search object and comparer function.
        /// </summary>
        /// <returns>
        /// The index of the item in the list, or -1 if the list contains no elements.<br/>
        /// If no exact match was found, the index returned is that of the item that matches the most closely.
        /// </returns>
        public static int BinarySearchIndex<TItem, TSearch>(this IList<TItem> list, TSearch searchObject,
            Func<TItem, TSearch, int> comparer)
        {
            int low = 0;
            int high = list.Count - 1;
            int index = -1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                index = mid;

                var current = list[mid];
                int comparison = comparer(current, searchObject);
                if (comparison == 0)
                {
                    // The objects are equal
                    return index;
                }
                else if (comparison < 0)
                {
                    // The current object is less than the search object, exclude current lower bound
                    low = mid + 1;
                }
                else
                {
                    // The current object is greater than the search object, exclude current higher bound
                    high = mid - 1;
                }
            }

            return index;
        }
    }
}
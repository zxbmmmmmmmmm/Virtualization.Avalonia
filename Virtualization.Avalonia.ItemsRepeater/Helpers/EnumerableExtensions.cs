using System.Collections;

namespace Virtualization.Avalonia;

/// <summary>
/// <see cref="IEnumerable"/> extensions methods
/// </summary>
internal static class EnumerableExtensions
{
    extension(IEnumerable items)
    {
        /// <summary>
        /// Gets the index of an item from an IEnumerable
        /// </summary>
        public int IndexOf(object item)
        {
            if (items is IList list)
                return list.IndexOf(item);

            var index = 0;

            foreach (var i in items)
            {
                if (ReferenceEquals(i, item))
                    return index;

                ++index;
            }

            return -1;
        }

        /// <summary>
        /// Gets the item count of the IEnumerable
        /// </summary>
        public int Count()
        {
            return items is ICollection collection ? collection.Count : Enumerable.Count(items.Cast());
        }

        /// <summary>
        /// Retrieves the element at the specified index from the IEnumerable
        /// </summary>
        /// <param name="reqIndex"></param>
        /// <returns></returns>
        public object? ElementAt(int reqIndex)
        {
            return items is IList list ? list[reqIndex] : Enumerable.ElementAt(items.Cast<object>(), reqIndex);
        }

        /// <summary>
        /// Checks of the IEnumerable contains the given item
        /// </summary>
        public bool Contains(object item)
        {
            return items is IList list ? list.Contains(item) : Enumerable.Contains(items.Cast(), item);
        }

        private IEnumerable<object?> Cast() => items.Cast<object?>();
    }
}

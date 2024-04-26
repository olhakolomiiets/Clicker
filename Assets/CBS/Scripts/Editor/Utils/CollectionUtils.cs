using System.Collections.Generic;
using System.Linq;

namespace CBS.Editor.Utils
{
    public static class CollectionUtils
    {
        public static IEnumerable<IEnumerable<T>> SplitArray<T>(T[] array, int size)
        {
            for (var i = 0; i < (float)array.Length / size; i++)
            {
                yield return array.Skip(i * size).Take(size);
            }
        }
    }
}
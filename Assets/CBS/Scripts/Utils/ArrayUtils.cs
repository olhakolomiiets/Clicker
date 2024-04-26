using System;
using System.Collections.Generic;

namespace CBS.Utils
{
    public static class ArrayUtils
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var rnd = new Random();
            for (var i = list.Count; i > 0; i--)
                list.Swap(0, rnd.Next(0, i));
        }

        public static void Swap<T>(this IList<T> list, int i, int j)
        {
            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}

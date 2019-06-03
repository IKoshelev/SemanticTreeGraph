using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticTreeGraph
{
    public static class IEnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (T item in enumeration)
            {
                action(item);
            }
        }

        public static TResult Pipe<T,TResult>(this T val, Func<T, TResult> to)
        {
            return to(val);
        }

        public static void Pipe<T>(this T val, Action<T> to)
        {
            to(val);
        }
    }
}

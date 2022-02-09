using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill
{
    public static class Extensions
    {
        public static T ElementAtOrDefault<T>(this T[] array, int index, T @default)
        {
            return index >= 0 && index < array.Count() ? array[index] : @default;
        }
    }
}

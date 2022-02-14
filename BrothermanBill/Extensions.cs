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

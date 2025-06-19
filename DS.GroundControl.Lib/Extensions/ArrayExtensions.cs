namespace DS.GroundControl.Lib.Extensions
{
    public static class ArrayExtensions
    {
        public static T[] SubArray<T>(this T[] arr, int index, int length)
        {
            var value = new T[length];
            Array.Copy(arr, index, value, 0, length);
            return value;
        }
    }
}
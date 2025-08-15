namespace DS.GroundControl.Lib.Extensions
{
    public static class ListExtensions
    {
        public static bool EndsWith(this List<byte> list, byte[] value)
        {
            var endsWith = false;
            if (list.Count >= value.Length)
            {
                endsWith = true;
                for (int i = list.Count - value.Length, j = 0; j < value.Length; i++, j++)
                {
                    if (!list[i].Equals(value[j]))
                    {
                        endsWith = false;
                        break;
                    }
                }
            }
            return endsWith;
        }
    }
}
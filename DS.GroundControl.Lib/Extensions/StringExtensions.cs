using System.Text;

namespace DS.GroundControl.Lib.Extensions
{
    public static class StringExtensions
    {
        public static string RemoveSubstring(this string str, string substring)
        {
            int index = str.IndexOf(substring);
            return (index >= 0)
                ? str.Remove(index, substring.Length)
                : str;
        }
        public static string ReplaceAt(this string str, string oldValue, string newValue, int startIndex)
        {
            if (oldValue == string.Empty)
                return str;

            if (str.IndexOf(oldValue, startIndex) == -1)
                return str;
            
            var builder = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                if (i == startIndex)
                {
                    i += oldValue.Length;
                    builder.Append(newValue);
                    continue;
                }
                builder.Append(str[i]);
            }
            return builder.ToString();
        }
    }
}
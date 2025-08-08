using System.Text;

namespace DS.GroundControl.Lib.Extensions
{
    public static class StringBuilderExtensions
    {
        public static bool EndsWith(this StringBuilder builder, string value)
        {
            var result = false;
            if (builder.Length >= value.Length)
            {
                result = true;
                for (int i = builder.Length - value.Length, j = 0; i < value.Length; i++, j++)
                {
                    if (builder[i] != value[j])
                    {
                        result = false;
                        break;
                    }
                }
            }
            return result;
        }
    }
}
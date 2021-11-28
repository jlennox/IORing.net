using System.Text;

namespace IORing.Samples.Library;

public static class HexFormatter
{
    public static string GetHex(ReadOnlySpan<byte> input)
    {
        var sb = new StringBuilder(input.Length * 3);

        var counter = 0;
        foreach (var b in input)
        {
            sb.AppendFormat("{0:X2}", b);
            sb.Append(' ');

            if (counter != 0 && ++counter % 16 == 0)
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }
}
using System.Globalization;

namespace Formatter;

public class StringFormatter
{
    public static string Centered(string s, int width, char fillChar = ' ')
    {
        if (s.Length >= width) return s;

        int leftPadding = (width - s.Length) / 2;
        int rightPadding = width - s.Length - leftPadding;

        return new string(fillChar, leftPadding) + s + new string(fillChar, rightPadding);
    }

    public static string LeftAligned(string s, int width, char fillChar = ' ')
    {
        if (s.Length >= width) return s;
        return s + new string(fillChar, width - s.Length);
    }

    public static string RightAligned(string s, int width, char fillChar = ' ')
    {
        if (s.Length >= width) return s;

        int leftPadding = width - s.Length;

        return new string(fillChar, leftPadding) + s;
    }

    public static string Align(string s, int width, StringAlignment alignment)
    {
        return alignment switch
        {
            StringAlignment.Left => LeftAligned(s, width),
            StringAlignment.Centered => Centered(s, width),
            StringAlignment.Right => RightAligned(s, width),
            StringAlignment.None => s,

            _ => throw new InvalidEnumArgumentException()
        };
    }

    public static List<string> SplitToCharLimitByLines(string s, int charLimit = 2000)
    {
        if (s.Length <= charLimit) return new List<string>() { s };
        List<string> segments = new List<string>();
        foreach (string line in s.Split('\n'))
        {
            if (segments.Count == 0 || segments.Last().Length + line.Length + 1 > charLimit)
            { // if the last segment is empty or adding the line would exceed 2000 characters, create a new segment
                segments.Add(line);
            }
            else
            { // otherwise, add the line to the last segment
                segments[segments.Count - 1] += "\n" + line;
            }
        }
        return segments;
    }

    public static string BigNumberToString(double number, int decimals = 3, int? strLen = null)
    {
        // letter table (power-of-ten -> suffix)
        var letters = new Dictionary<int, string>
        {
            [0] = "",
            [3] = "k",
            [6] = "M",
            [9] = "B",
            [12] = "T",
            [15] = "q",
            [18] = "Q",
            [21] = "s",
            [24] = "S",
            [27] = "o",
            [30] = "N",
            [33] = "d",
            [36] = "U",
            [39] = "D",
            [42] = "Td",
            [45] = "qd",
            [48] = "Qd",
            [51] = "sd",
            [54] = "Sd",
            [57] = "Od",
            [60] = "Nd",
            [63] = "V",
            [66] = "uV",
            [69] = "dV",
            [72] = "tV",
            [75] = "qV",
            [78] = "QV",
            [81] = "sV",
            [84] = "SV",
            [87] = "OV",
            [90] = "NV",
            [93] = "tT"
        };

        // NaN / Infinity handling
        if (double.IsNaN(number)) return "NaN";
        if (double.IsInfinity(number)) return "Inf";

        // negative numbers
        if (number < 0) return "-" + BigNumberToString(-number, decimals, strLen);

        // zero
        if (number == 0.0) return "0";

        var culture = CultureInfo.InvariantCulture;

        // small numbers (<1000) - format and optionally truncate
        if (number < 1000.0)
        {
            string numStr = number.ToString("F" + decimals, culture);
            int lengthLimit = strLen ?? 3;
            if (lengthLimit < 0) lengthLimit = 0;
            if (lengthLimit < numStr.Length)
                numStr = numStr.Substring(0, lengthLimit);

            if (numStr.Length > 0 && numStr[numStr.Length - 1] == '.')
                numStr = numStr.TrimEnd('.');

            return numStr;
        }

        // scale the number down so that it's near 1..10 and compute power (number of times divided by 10)
        int power = 0;
        while (number >= 10.0)
        {
            number /= 10.0;
            power++;
        }

        // make sure power matches a key in letters: if not, step power down while moving number up
        while (!letters.ContainsKey(power))
        {
            number *= 10.0;
            power--;
            if (power < 0) break; // safety, though letters contains 0
        }

        string suffix = letters.ContainsKey(power) ? letters[power] : "";

        if (strLen.HasValue)
        {
            string numStr = number.ToString("F" + decimals, culture);
            int limit = Math.Max(0, strLen.Value-1);
            if (limit < numStr.Length)
                numStr = numStr.Substring(0, limit);

            if (numStr.Length > 0 && numStr[numStr.Length - 1] == '.')
                numStr = numStr.TrimEnd('.');

            return numStr + suffix;
        }
        else
        {
            return number.ToString("F" + decimals, culture) + suffix;
        }
    }
}

public enum StringAlignment
{
    Left,
    Centered,
    Right,
    None
}

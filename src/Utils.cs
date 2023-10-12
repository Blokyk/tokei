namespace Tokei;

public static class Utils
{
    public static long ParseDecimalOrHexOrBin(ReadOnlySpan<char> s) {
        // technically, there could be a base specifier at s[1],
        // but it would be invalid anyway and Int64.Parse will
        // surface that, so it's fine
        if (s.Length <= 2)
            return Int64.Parse(s);

        if (Char.IsAsciiLetter(s[1])) {
            if (s[0] is not '0')
                throw new Exception($"Unknown base specifier '{s[0]}{s[1]}'");

            if (s[1] is 'x')
                return Int64.Parse(s[2..], System.Globalization.NumberStyles.HexNumber);
            if (s[1] is 'b')
                return Convert.ToInt64(s[2..].ToString(), 2);

            throw new Exception($"Unknown base specifier '{s[0]}{s[1]}'");
        }

        return Int64.Parse(s);
    }

    public static bool Contains(this (int startIncluded, int endExcluded) t, int value)
        => value >= t.startIncluded && value < t.endExcluded;
    public static bool Contains(this (long startIncluded, long endExcluded) t, long value)
        => value >= t.startIncluded && value < t.endExcluded;
}
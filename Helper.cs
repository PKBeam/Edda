using System;
using System.Globalization;

public class Helper {
    public static double DoubleParseInvariant(string s) {
        return double.Parse(s, CultureInfo.InvariantCulture);
    }
    public static bool RangeCheck(double a, double x, double y) {
        double lower = Math.Min(x, y);
        double higher = Math.Max(x, y);
        return (lower <= a && a <= higher);
    }
}

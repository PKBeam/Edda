using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media.Imaging;

public class Helper {

    private static double threshold = 0.0001;

    public static double DoubleParseInvariant(string s) {
        return double.Parse(s, CultureInfo.InvariantCulture);
    }
    public static bool DoubleApproxGreaterEqual(double x, double y) {
        return x - y >= -threshold;
    }
    public static bool DoubleApproxEqual(double x, double y) {
        return Math.Abs(x - y) <= threshold;
    }
    public static bool DoubleRangeCheck(double a, double x, double y) {
        double lower = Math.Min(x, y);
        double higher = Math.Max(x, y);
        return lower <= a && a <= higher;
    }
    public static void InsertSortedUnique(List<Note> notes, Note note) {
        // check which index to insert the new note at (keep everything in sorted order)
        var i = 0;
        foreach (var thisNote in notes) {
            int comp = thisNote.CompareTo(note);
            if (comp == 0) {
                return;
            }
            if (comp > 0) {
                notes.Insert(i, note);
                return;
            }
            i++;
        }
        notes.Add(note);
    }
    public static string UidGenerator(Note n) {
        return $"Note({n.beat},{n.col})";
    }
    public static BitmapImage BitmapGenerator(Uri u) {
        var b = new BitmapImage();
        b.BeginInit();
        b.UriSource = u;
        b.CacheOption = BitmapCacheOption.OnLoad;
        b.EndInit();
        b.Freeze();
        return b;
    }
    public static BitmapImage BitmapGenerator(string resourceFile) {
        return BitmapGenerator(new Uri($"pack://application:,,,/resources/{resourceFile}"));
    }
    public static BitmapImage BitmapImageForBeat(double beat, bool isHighlighted = false) {
        double fracBeat = beat - (int)beat;
        string runeStr = "X";
        if (DoubleApproxEqual(fracBeat, 0.0) || 
            DoubleApproxEqual(fracBeat, 1.0)) {
            runeStr = "1";
        }
        if (DoubleApproxEqual(fracBeat, 1.0 / 4.0)) {
            runeStr = "14";
        }
        if (DoubleApproxEqual(fracBeat, 1.0 / 3.0)) {
            runeStr = "13";
        }
        if (DoubleApproxEqual(fracBeat, 1.0 / 2.0)) {
            runeStr = "12";
        }
        if (DoubleApproxEqual(fracBeat, 2.0 / 3.0)) {
            runeStr = "23";
        }
        if (DoubleApproxEqual(fracBeat, 3.0 / 4.0)) {
            runeStr = "34";
        }
        return BitmapGenerator($"rune{runeStr}{(isHighlighted ? "highlight" : "")}.png");
    }
}

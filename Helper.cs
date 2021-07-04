using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media.Imaging;

public class Helper {

    private static BitmapImage rune1  = BitmapGenerator("rune1.png");
    private static BitmapImage rune12 = BitmapGenerator("rune12.png");
    private static BitmapImage rune13 = BitmapGenerator("rune13.png");
    private static BitmapImage rune14 = BitmapGenerator("rune14.png");
    private static BitmapImage rune23 = BitmapGenerator("rune23.png");
    private static BitmapImage rune34 = BitmapGenerator("rune34.png");
    private static BitmapImage runeX  = BitmapGenerator("runeX.png");
           
    private static BitmapImage rune1Highlight  = BitmapGenerator("rune1highlight.png");
    private static BitmapImage rune12Highlight = BitmapGenerator("rune12highlight.png");
    private static BitmapImage rune13Highlight = BitmapGenerator("rune13highlight.png");
    private static BitmapImage rune14Highlight = BitmapGenerator("rune14highlight.png");
    private static BitmapImage rune23Highlight = BitmapGenerator("rune23highlight.png");
    private static BitmapImage rune34Highlight = BitmapGenerator("rune34highlight.png");
    private static BitmapImage runeXHighlight  = BitmapGenerator("runeXhighlight.png");

    public static double DoubleParseInvariant(string s) {
        return double.Parse(s, CultureInfo.InvariantCulture);
    }
    public static bool RangeCheck(double a, double x, double y) {
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
        var fracBeat = beat - (int)beat;
        switch (Math.Round(fracBeat, 5)) {
            case 0.00000: return isHighlighted ? rune1Highlight : rune1;
            case 0.25000: return isHighlighted ? rune14Highlight : rune14;
            case 0.33333: return isHighlighted ? rune13Highlight : rune13;
            case 0.50000: return isHighlighted ? rune12Highlight : rune12;
            case 0.66667: return isHighlighted ? rune23Highlight : rune23;
            case 0.75000: return isHighlighted ? rune34Highlight : rune34;
            default: return isHighlighted ? runeXHighlight : runeX;
        }
    }
}

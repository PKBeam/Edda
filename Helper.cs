using Edda;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

public class Helper {

    private static double threshold = 0.0001;

    public static double DoubleParseInvariant(string s) {
        return double.Parse(s, CultureInfo.InvariantCulture);
    }
    public static bool DoubleApproxGreaterEqual(double x, double y) {
        return x - y >= -threshold;
    }
    public static bool DoubleApproxGreater(double x, double y) {
        return x - y > -threshold;
    }
    public static bool DoubleApproxEqual(double x, double y) {
        return Math.Abs(x - y) <= threshold;
    }
    public static bool DoubleRangeCheck(double a, double x, double y) {
        double lower = Math.Min(x, y);
        double higher = Math.Max(x, y);
        return lower <= a && a <= higher;
    }
    public static Window GetFirstWindow<T>() where T : Window {
        var wins = Application.Current.Windows.OfType<T>();
        if (wins.Any()) {
            return wins.First();
        }
        return null;       
    }
    public static bool InsertSortedUnique(List<Note> notes, Note note) {
        // check which index to insert the new note at (keep everything in sorted order)
        var i = 0;
        foreach (var thisNote in notes) {
            int comp = thisNote.CompareTo(note);
            if (comp == 0) {
                return false;
            }
            if (comp > 0) {
                notes.Insert(i, note);
                return true;
            }
            i++;
        }
        notes.Add(note);
        return true;
    }
    // TODO figure out a better way than this
    public static string UidGenerator(Note n) {
        return $"Note({Math.Round(n.beat, 4)},{n.col})";
    }
    public static string NameGenerator(Note n) {
        return "N" + n.GetHashCode().ToString();
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
    public static string SanitiseFileName(string fileName) {
        //return fileName.Replace(" ", "-");
        return "song.ogg";
    }
    public static string TimeFormat(int seconds) {
        int min = seconds / 60;
        int sec = seconds % 60;

        return $"{min}:{sec:D2}";
    }
    public static string TimeFormat(double seconds) {
        return TimeFormat((int)seconds);
    }
    public static void FFmpeg(string dir, string arg) {
        // uses a custom-built version of ffmpeg with ONLY libvorbis support
        // (cuts down on filesize a lot)
        string path = Path.Combine(Path.GetTempPath(), "ffmpeg_temp.exe");
        File.WriteAllBytes(path, Edda.Properties.Resources.ffmpeg);
        
        var p = Process.Start(path, arg);
        p.WaitForExit();

        try {
            File.Delete(path);
        } catch {
            // ???
        }
    }
    public static void CheckForUpdates() {
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Edda");
        string res = client.GetStringAsync("https://api.github.com/repos/PKBeam/Edda/releases/latest").Result;
        var resJSON = JObject.Parse(res);
        string newestVersion = (string)resJSON.GetValue("tag_name");
        string currentVersion = Const.Program.VersionDisplayString;
        if (newestVersion != currentVersion) {
            MessageBox.Show($"A new release of Edda is available.\n\nNewest version: {newestVersion}\nCurrent version: {currentVersion}", "New release available", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

using Edda;
using Edda.Const;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
        return x - y > threshold;
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
    public static Uri UriForResource(string file) {
        return new Uri($"pack://application:,,,/resources/{file}");
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
    public static string SanitiseSongFileName(string fileName) {
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
    public static void CmdCopyFile(string src, string dst) {
        var p = Process.Start("cmd.exe", "/C copy \"" + src + "\" \"" + dst + "\"");
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        Console.WriteLine(p.StandardOutput.ReadToEnd());
        // p.WaitForExit();
    }
    public static void CmdCopyFiles(List<string> src, string dst) {
        var cmd = "/C ";
        foreach (var str in src) {
            cmd += "copy \"" + str + "\" \"" + dst + "\" & ";
        }
        var p = Process.Start("cmd.exe", cmd);
        //p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        //Console.WriteLine(p.StandardOutput.ReadToEnd());
        p.WaitForExit();
    }
    public static bool CheckForUpdates() {

        // turn version string into number for comparison purposes
        /* 
         *  e.g. 
         *  
         *  0.4.5   => 45
         *  0.4.5.1 => 45.1
         *  1.0     => 100
         *  1.0.0.1 => 100.1
        */
        double numerifyVersionString(string version) {
            string numerify = "";
            int counter = 0;
            foreach (var character in version) {
                if (counter == 3) {
                    numerify += '.';
                    counter++;
                }
                if (character >= '0' && character <= '9') {
                    numerify += character;
                    counter++;
                }

            }
            while (counter < 3) {
                numerify += '0';
                counter++;
            }
            return double.Parse(numerify);
        }

        bool isBeta(string version) {
            return version.Contains('b') || version.Contains('B');
        }

        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Edda-" + Program.DisplayVersionString);
        string res = client.GetStringAsync(Program.ReleasesAPI).Result;

        // get most recent non-beta release
        var resJSON = JArray.Parse(res);
        var i = 0;

        // beta versions should show all versions as updates
        if (!isBeta(Program.VersionString)) {
            // non-beta versions should not show beta versions as updates
            while (isBeta((string)resJSON[i]["tag_name"])) {
                i++;
            }
        }
        var newestRelease = resJSON[i];

        // check if this release is a newer version
        string newestVersion = (string)newestRelease["tag_name"];
        string currentVersion = "v" + Program.VersionString;
        if (numerifyVersionString(newestVersion) > numerifyVersionString(currentVersion)) {
            MessageBox.Show($"A new release of Edda is available.\n\nNewest version: {newestVersion}\nCurrent version: {currentVersion}", "New release available", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        } else {
            return false;
        }
    }
    public static void ThreadedPrint(string message) {
        new System.Threading.Thread(new System.Threading.ThreadStart(delegate {
            Trace.WriteLine(message);
        })).Start();
    }
    public static string DefaultRagnarockMapPath() {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string ragPath = Path.Combine(docPath, "Ragnarock");
        string ragSongPath = Path.Combine(ragPath, "CustomSongs");
        return Directory.Exists(ragSongPath) ? ragSongPath : null;
    }
    public static string ValidFilenameFrom(string filename) {
        string output = filename;
        foreach (char c in Path.GetInvalidFileNameChars()) {
            output = output.Replace(c, '_');
        }
        return output;
    }
    public static string ValidMapFolderNameFrom(string filename) {
        string output = "";
        foreach (char c in filename) {
            if ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z') {
                output += c;
            }
        }
        return output;
    }
    public static string ChooseNewMapFolder() {
        return ChooseNewMapFolder(GetRagnarockMapFolder());
    }
    public static string ChooseNewMapFolder(string initialDirectory) {
        // select folder for map
        var d2 = new CommonOpenFileDialog();
        d2.Title = "Select an empty folder to store your map";
        d2.IsFolderPicker = true;
        d2.InitialDirectory = initialDirectory;
        if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
            return null;
        }

        // check folder name is appropriate
        var folderName = new FileInfo(d2.FileName).Name;
        if (!Regex.IsMatch(folderName, @"^[a-zA-Z]+$")) {
            MessageBox.Show("The folder name cannot contain spaces or non-alphabetic characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        // check folder is empty
        if (Directory.GetFiles(d2.FileName).Length > 0) {
            if (MessageBoxResult.No == MessageBox.Show("The specified folder is not empty. Continue anyway?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning)) {
                return null;
            }
        }
        return d2.FileName;
    }
    public static string ChooseOpenMapFolder() {
        return ChooseOpenMapFolder(GetRagnarockMapFolder());
    }
    public static string ChooseOpenMapFolder(string initialDirectory) {
        // select folder for map
        // TODO: this dialog is sometimes hangs, is there a better way to select a folder?
        var d2 = new CommonOpenFileDialog();
        d2.Title = "Select your map's containing folder";
        d2.IsFolderPicker = true;
        d2.InitialDirectory = initialDirectory;
        if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
            return null;
        }
        return d2.FileName;
    }

    public static string GetRagnarockMapFolder()
    {
        var userSettings = new UserSettings(Program.SettingsFile);
        return int.TryParse(userSettings.GetValueForKey(Edda.Const.UserSettings.MapSaveLocationIndex), out var index) && index > 0
            ? Path.Combine(userSettings.GetValueForKey(Edda.Const.UserSettings.MapSaveLocationPath), Program.GameInstallRelativeMapFolder)
            : Helper.DefaultRagnarockMapPath();
    }
    public static void OpenWebUrl(string url) {
        Process proc = new Process();
        proc.StartInfo.UseShellExecute = true;
        proc.StartInfo.FileName = url;
        proc.Start();
    }
}

using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.IO;

namespace Edda.Const
{
    using MediaColor = Color;
    using DrawingColor = System.Drawing.Color;
    public static class Program
    {
        public const string Name = "Edda";
        public const string RepositoryURL = "https://github.com/PKBeam/Edda";
        public const string ReleasesAPI = "https://api.github.com/repos/PKBeam/Edda/releases";
        public const string VersionString = "1.1.0b1";
        public const string DisplayVersionString =
#if DEBUG
                VersionString + "-dev";
#else
                VersionString;
#endif
        public static string ProgramDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edda");
        public static string SettingsFile => Path.Combine(ProgramDataDir, "settings.txt");
        public static string RecentOpenedMapsFile => Path.Combine(ProgramDataDir, "recentMaps.txt");
        public const int MaxRecentOpenedMaps = 5;
        public static string DocumentsMapFolder => Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ragnarock"), "CustomSongs");
        public static string GameInstallRelativeMapFolder => Path.Combine("Ragnarock", "CustomSongs");
        public const string UserGuideURL = "https://pkbeam.github.io/Edda/";
        public const string OldSettingsFile = "settings.txt";
        public const string ResourcesPath = "Resources/";
        public const string BackupPath = "autosaves";
        public const int MaxBackups = 10;

    }
    public static class DefaultUserSettings
    {
        public const int AudioLatency = -20; // ms
        public const string DrumSampleFile = "snaredrum";
        public const float DefaultSongVolume = 0.4F;
        public const float DefaultNoteVolume = 1;
        public const bool PanDrumSounds = true;
        public const bool EnableDiscordRPC = true;
        public const bool EnableAutosave = true;
        public const bool CheckForUpdates = true;
        public const int MapSaveLocationIndex = 0;
        public const string MapSaveLocationPath = "";
    }

    public static class UserSettings
    {
        public const string EditorAudioLatency = "editorAudioLatency";
        public const string DrumSampleFile = "drumSampleFile";
        public const string PanDrumSounds = "panDrumSounds";
        public const string DefaultSongVolume = "defaultSongVolume";
        public const string DefaultNoteVolume = "defaultNoteVolume";
        public const string EnableDiscordRPC = "enableDiscordRPC";
        public const string EnableAutosave = "enableAutosave";
        public const string CheckForUpdates = "checkForUpdates";
        public const string MapSaveLocationIndex = "mapSaveLocationIndex";
        public const string MapSaveLocationPath = "mapSaveLocationPath";
    }
    public static class Editor
    {
        // Grid drawing
        public const double DefaultGridSpacing = 2;
        public const double DefaultGridDivision = 4;
        public const double GridDrawRange = 1;
        public const int DrawDebounceInterval = 100; // ms
        public const string MajorGridlineColour = "#333333";
        public const string MinorGridlineColour = "#555555";
        public const double MajorGridlineThickness = 1.5;
        public const double MinorGridlineThickness = 1;
        public const int GridDivisionMax = 64;
        public const int DifficultyLevelMin = 1;
        public const int DifficultyLevelMax = 99;

        // Animation Properties
        public const double DrumHitScaleFactor = 0.75; // percentage of 1.0
        public const int DrumHitDuration = 150; // ms
        public const int NoteHitDuration = 1000; // ms

        // Editor functions
        public const int HistoryMaxSize = 128; // actions
        public const double PreviewNoteOpacity = 0.30; // percentage of 1.0
        public const double DragInitThreshold = 10; // pixels
        public const int AutosaveInterval = 30; // seconds

        // Bookmarks
        public const double NavWaveformOpacity = 0.75;
        public static class GridPreviewLine
        {
            public static string Colour => Colors.FloralWhite.ToString();
            public const double Thickness = 1.0;
        }
        public static class NavPreviewLine
        {
            public static string Colour => Colors.FloralWhite.ToString();
            public const double Thickness = 1;
        }
        public static class NavBookmark
        {
            public static string DefaultName = "Bookmark";
            public static string Colour => Colors.SkyBlue.ToString();
            public static string NameColour => Colors.SkyBlue.ToString();
            public static string BackgroundColour => Colors.SkyBlue.ToString();
            public const double Thickness = 1;
            public const double NameSize = 10;
            public const double NamePadding = 1;
            public const double Opacity = 1;
        }
        public static class GridBookmark
        {
            public static string Colour => Colors.DeepSkyBlue.ToString();
            public static string NameColour => Colors.DeepSkyBlue.ToString();
            public static string BackgroundColour => Colors.DeepSkyBlue.ToString();
            public const double Thickness = 3;
            public const double NameSize = 11;
            public const double NamePadding = 3;
            public const double Opacity = 0.75;
        }
        public static class BPMChange
        {
            public static string Colour => Colors.MediumPurple.ToString();
            public static string NameColour => Colors.MediumPurple.ToString();
            public static string BackgroundColour => Colors.MediumPurple.ToString();
            public const double Thickness = 3;
            public const double NameSize = 11;
            public const double NamePadding = 3;
            public const double Opacity = 0.75;
        }

        // Waveform drawing
        public static class Waveform
        {
            public const double SampleMaxPercentile = 0.95;
            public const double Width = 0.75;
            public const int MaxDimension = 65535;
            public const double ThicknessWPF = 1;
            public const bool UseGDI = false;
            public static MediaColor ColourWPF => MediaColor.FromArgb(180, 0, 0, 255);
            public static DrawingColor ColourGDI => DrawingColor.FromArgb(180, 0, 0, 255);
        }
    }
    public static class Audio
    {
        public const float MaxPanDistance = 0.6f;
        // Latencies        
        public const int WASAPILatencyTarget = 200; // ms
        // Note Playback    
        public const int NotePlaybackStreams = 32;
        public const int NotePollRate = 15;  // ms
        public const double NoteDetectionDelta = 10;  // ms

        public const int MaxPreviewLength = 15; // sec
        public const int DefaultPreviewFadeIn = 1; // sec
        public const int DefaultPreviewFadeOut = 3; // sec

        public static string MetronomeFilename = "metronome";
        public const int MetronomeStreams = 4;
    }
    public static class BeatmapDefaults
    {
        public const double BeatsPerMinute = 120;
        public const string SongFilename = "song.ogg";
        public const int Shuffle = 0;           // what do
        public const double ShufflePeriod = 0.5;         // these do??
        public const string BeatmapCharacteristicName = "Standard";
        public const double NoteJumpMovementSpeed = 15;
        public static List<string> DifficultyNames => new() { "Easy", "Normal", "Hard" };
        public static List<string> EnvironmentNames => new() { "Midgard", "Alfheim", "Nidavellir", "Asgard", "Muspelheim", "Helheim", "Hellfest" };
        //public const string DefaultEnvironmentAlias = "Midgard";
    }

    public static class DiscordRPC
    {
        public const string AppID = "874090300207951932";
        public const string PubKey = "c2710c1d8cd4d9a2a9460cd63e048781d32a3a08b171153c5898a6fe0ddb8e76";
        public const string IconKey = "icon";
    }
}

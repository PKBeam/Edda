using System.Collections.Generic;

namespace Const {
    using MediaColor = System.Windows.Media.Color;
    using DrawingColor = System.Drawing.Color;
    public static class Program {
        public const string Name          = "Edda";
        public const string RepositoryURL = "https://github.com/PKBeam/Edda";
        public const string VersionNumber = "0.3.0";
        public const string SettingsFile  = "settings.txt";
        public const string ResourcesPath = "Resources/";
    }
    public static class DefaultUserSettings {
        public const int AudioLatency      = -20; // ms
        public const string DrumSampleFile = "bassdrum";
        public const bool EnableDiscordRPC = true;
        public const bool EnableAutosave   = true;
    }

    public static class UserSettings {
        public const string EditorAudioLatency = "editorAudioLatency";
        public const string DrumSampleFile     = "drumSampleFile";
        public const string EnableDiscordRPC   = "enableDiscordRPC";
        public const string EnableAutosave     = "enableAutosave";
    }
    public static class Editor {
        // Grid drawing
        public const double DefaultGridSpacing       = 1;
        public const double DefaultGridDivision      = 4;
        public const double GridDrawRange            = 1;
        public const int    GridDrawDebounceInterval = 100; // ms
        public const string MajorGridlineColour      = "#333333";
        public const string MinorGridlineColour      = "#555555";
        public const double MajorGridlineThickness   = 2;
        public const double MinorGridlineThickness   = 1.5;
        public const int    GridDivisionMax          = 64;

        // Animation Properties
        public const double DrumHitScaleFactor = 0.75; // percentage of 1.0
        public const int    DrumHitDuration    = 150; // ms

        // Editor functions
        public const int    HistoryMaxSize     = 128; // actions
        public const double PreviewNoteOpacity = 0.30; // percentage of 1.0
        public const double DragInitThreshold  = 10; // pixels
        public const int    AutosaveInterval   = 30; // seconds

        // Waveform drawing
        public static class Waveform {
            public const double Width        = 0.6;
            public const int    MaxDimension = 65535;
            public const double ThicknessWPF = 2;
            public const bool   UseGDI       = false;
            public static MediaColor   ColourWPF => MediaColor.FromArgb(96, 0, 0, 255);
            public static DrawingColor ColourGDI => DrawingColor.FromArgb(180, 0, 0, 255);
        }
    }
    public static class Audio {
        // Volumes
        public const float  DefaultSongVolume = 0.4f;
        public const float  DefaultNoteVolume = 1.0f;
        // Latencies        
        public const int    WASAPILatencyTarget    = 100; // ms
        // Note Playback    
        public const int    NotePlaybackStreams = 16;
        public const int    NotePollRate        = 15;  // ms
        public const double NoteDetectionDelta  = 15;  // ms

        public const int    MaxPreviewLength    = 15; // sec
        public const int    DefaultPreviewFade  = 3; // sec
    }
    public static class BeatmapDefaults {
        public const double BeatsPerMinute            = 120;
        public const string SongFilename              = "song.ogg";
        public const int    Shuffle                   = 0;           // what do
        public const double ShufflePeriod             = 0.5;         // these do??
        public const string BeatmapCharacteristicName = "Standard";
        public const double NoteJumpMovementSpeed     = 15;
        public static List<string> DifficultyNames  => new() { "Easy", "Normal", "Hard" };
        public static List<string> EnvironmentNames => new() { "Midgard", "Alfheim", "Nidavellir", "Asgard", "Muspelheim" };
        //public const string DefaultEnvironmentAlias = "Midgard";
    }

    public static class DiscordRPC {
        public const string AppID = "874090300207951932";
        public const string PubKey = "c2710c1d8cd4d9a2a9460cd63e048781d32a3a08b171153c5898a6fe0ddb8e76";
        public const string IconKey = "icon";
    }
}
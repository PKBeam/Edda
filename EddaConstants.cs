using System.Collections.Generic;

namespace Constants {
    using MediaColor = System.Windows.Media.Color;
    using DrawingColor = System.Drawing.Color;
    public static class Program {
        public const string ProgramName          = "Edda";
        public const string ProgramVersionNumber = "0.2.2.1";
        public const string SettingsFile         = "settings.txt";
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
        public const int    GridDivisionMax          = 12;

        // Editor functions
        public const int    HistorySizeMax = 128; // actions
        public const double PreviewNoteOpacity = 0.3; // percentage
        public const double DragInitThreshold = 10; // pixels

        // Waveform drawing
        public static class Waveform {
            public const double Width        = 0.6;
            public const int    MaxDimension = 50000;
            public const double ThicknessWPF = 2;
            public const bool   UseGDI       = false;
            public static MediaColor ColourWPF => MediaColor.FromArgb(96, 0, 0, 255);
            public static DrawingColor ColourGDI => DrawingColor.FromArgb(180, 0, 0, 255);
        }
    }
    public static class Audio {
        // Volumes
        public const float  DefaultSongVolume = 0.4f;
        public const float  DefaultNoteVolume = 1.0f;
        // Latencies        
        public const int    WASAPILatencyTarget    = 100; // ms
        public const int    DefaultSongNoteLatency = -20; // ms
        // Note Playback    
        public const int    NotePlaybackStreams = 16;
        public const int    NotePollRate        = 15;  // ms
        public const double NoteDetectionDelta  = 15;  // ms
    }
    public static class BeatmapDefaults {
        public const double BeatsPerMinute = 120;
        public const string SongFilename = "song.ogg";
        public const int    Shuffle = 0;
        public const double ShufflePeriod = 0.5;
        public const string BeatmapCharacteristicName = "Standard";
        public const double NoteJumpMovementSpeed = 15;
        public static List<string> DifficultyNames => new() { "Easy", "Normal", "Hard" };
        public static List<string> EnvironmentNames => new() { "DefaultEnvironment", "Alfheim", "Nidavellir", "Asgard" };
        public const string DefaultEnvironmentAlias = "Midgard";
    }
}
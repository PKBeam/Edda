using System.Collections.Generic;

namespace Constants {
    public static class Editor {
        // Grid drawing
        public const double DefaultGridSpacing = 1;
        public const double DefaultGridDivision = 4;
        public const double GridDrawRange = 1;
        public const int GridDrawDebounceInterval = 100; // ms
        public const string MajorGridlineColour = "#333333";
        public const string MinorGridlineColour = "#555555";
        public const double MajorGridlineThickness = 2;
        public const double MinorGridlineThickness = 1.5;
        public const int GridDivisionMax = 12;

        // Editor functions
        public const int HistorySizeMax = 128; // actions
        public const double PreviewNoteOpacity = 0.3; // percentage
        public const double DragInitThreshold = 10; // pixels

        // Waveform drawing
        public const double WaveformWidth = 0.60; // percentage
        public const int WaveformMaxDimension = 50000;
        public static System.Windows.Media.Color WaveformColourWPF => System.Windows.Media.Color.FromArgb(96, 0, 0, 255);
        public static System.Drawing.Color WaveformColourGDI => System.Drawing.Color.FromArgb(180, 0, 0, 255);
        public const double WaveformThicknessWPF = 2;

    }
    public static class Audio {
        // Volumes
        public const float DefaultSongVolume = 0.25f;
        public const float DefaultNoteVolume = 1.0f;
        // Latencies
        public const int WASAPILatencyTarget = 100; // ms
        public const int DefaultSongNoteLatency = -20; // ms
        // Note Playback
        public const int NotePlaybackStreams = 16;
        public const int NotePollRate = 15;  // ms
        public const double NoteDetectionDelta = 15;  // ms
    }
    public static class BeatmapDefaults {
        public const double BeatsPerMinute = 120;
        public const string SongFilename = "song.ogg";
        public const int Shuffle = 0;
        public const double ShufflePeriod = 0.5;
        public const string BeatmapCharacteristicName = "Standard";
        public const double NoteJumpMovementSpeed = 15;
        public static List<string> DifficultyNames => new List<string>() { "Easy", "Normal", "Hard" };
    }
    public static class Misc {
        public const string ProgramName = "Edda";
        public const string ProgramVersionNumber = "0.2.0";
        public const string SettingsFile = "settings.txt";
        public static List<string> EnvironmentNames => new List<string>() { "DefaultEnvironment", "Alfheim", "Nidavellir", "Asgard" };
    }
}
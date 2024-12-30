using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Edda.Const {

    using DrawingColor = System.Drawing.Color;
    using MediaColor = Color;
    public static class Program {

        public const string Name = "Edda";
        public const string RepositoryURL = "https://github.com/PKBeam/Edda";
        public const string ReleasesAPI = "https://api.github.com/repos/PKBeam/Edda/releases";
        public const string BaseVersionString = "1.2.6";
        public const string VersionString =
#if DEBUG
            BaseVersionString + "-dev";
#else
            BaseVersionString;
#endif
        public static string DisplayVersionString =>
#if DEBUG
            VersionString.Replace("b", " Beta ").Replace("-dev", " (Dev)");
#else
            VersionString.Replace("b", " Beta ");
#endif
        public static string ProgramDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edda");
        public static string SettingsFile => Path.Combine(ProgramDataDir, "settings.txt");
        public static string RecentOpenedMapsFile => Path.Combine(ProgramDataDir, "recentMaps.txt");
        public const int MaxRecentOpenedMaps = 10;
        public static string DocumentsMapFolder => Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ragnarock"), "CustomSongs");
        public static string GameInstallRelativeMapFolder => Path.Combine("Ragnarock", "CustomSongs");
        public const string UserGuideURL = "https://pkbeam.github.io/Edda/";
        public const string OldSettingsFile = "settings.txt";
        public const string ResourcesPath = "Resources/";
        public const string BackupPath = "autosaves";
        public const string CachePath = "cache";
        public const int MaxBackups = 10;

    }
    public static class DefaultUserSettings {
        public const bool EnableSpectrogram = true;
        public const string DefaultMapper = "";
        public const int DefaultNoteSpeed = Editor.DefaultNoteSpeed;
        public const int DefaultGridSpacing = Editor.DefaultGridSpacing;
        public const int AudioLatency = -20; // ms
        public const string DrumSampleFile = "snaredrum";
        public const float DefaultSongVolume = 0.4F;
        public const float DefaultNoteVolume = 1;
        public const bool PanDrumSounds = true;
        public const bool SpectrogramCache = true;
        public const string SpectrogramType = "Standard";
        public const string SpectrogramQuality = "Medium";
        public const int SpectrogramFrequency = Editor.Spectrogram.DefaultFreq;
        public const string SpectrogramColormap = "Blues";
        public const bool SpectrogramFlipped = false;
        public const bool SpectrogramChunking = false;
        public const bool EnableDiscordRPC = true;
        public const bool EnableAutosave = true;
        public const bool CheckForUpdates = true;
        public const int MapSaveLocationIndex = 0;
        public const string MapSaveLocationPath = "";
        public const string DifficultyPredictorAlgorithm = DifficultyPrediction.SupportedAlgorithms.PKBeam;
        public const bool DifficultyPredictorShowPrecise = false;
        public const bool DifficultyPredictorShowInMapStats = false;
        public const string NotePasteBehavior = Editor.DefaultNotePasteBehavior;
    }

    public static class UserSettingsKey {
        public const string EnableSpectrogram = "enableSpectrogram";
        public const string DefaultMapper = "defaultMapper";
        public const string DefaultNoteSpeed = "defaultNoteSpeed";
        public const string DefaultGridSpacing = "defaultGridSpacing";
        public const string EditorAudioLatency = "editorAudioLatency";
        public const string PlaybackDeviceID = "playbackDeviceID";
        public const string DrumSampleFile = "drumSampleFile";
        public const string PanDrumSounds = "panDrumSounds";
        public const string DefaultSongVolume = "defaultSongVolume";
        public const string DefaultNoteVolume = "defaultNoteVolume";
        public const string SpectrogramCache = "spectrogramCache";
        public const string SpectrogramType = "spectrogramType";
        public const string SpectrogramQuality = "spectrogramQuality";
        public const string SpectrogramFrequency = "spectrogramFrequency";
        public const string SpectrogramColormap = "spectrogramColormap";
        public const string SpectrogramFlipped = "spectrogramFlipped";
        public const string SpectrogramChunking = "spectrogramChunking";
        public const string EnableDiscordRPC = "enableDiscordRPC";
        public const string EnableAutosave = "enableAutosave";
        public const string CheckForUpdates = "checkForUpdates";
        public const string MapSaveLocationIndex = "mapSaveLocationIndex";
        public const string MapSaveLocationPath = "mapSaveLocationPath";
        public const string DifficultyPredictorAlgorithm = "difficultyPredictorAlgorithm";
        public const string DifficultyPredictorShowPrecise = "difficultyPredictorShowPrecise";
        public const string DifficultyPredictorShowInMapStats = "difficultyPredictorShowInMapStats";
        public const string NotePasteBehavior = "notePasteBehavior";
    }
    public static class Editor {
        // Grid drawing
        public const int DefaultNoteSpeed = 20;
        public const int DefaultGridSpacing = 2;
        public const int DefaultGridDivision = 4;
        public const string DefaultNotePasteBehavior = NotePasteBehavior.AlignToNoteBPM;
        public const double GridDrawRange = 1;
        public const int DrawDebounceInterval = 100; // ms
        public const string MajorGridlineColour = "#333333";
        public const string MinorGridlineColour = "#555555";
        public const double MajorGridlineThickness = 1.5;
        public const double MinorGridlineThickness = 1;
        public const int GridDivisionMax = 64;

        // Animation Properties
        public const double DrumHitScaleFactor = 0.75; // percentage of 1.0
        public const int DrumHitDuration = 150; // ms
        public const int NoteHitDuration = 1000; // ms

        // Editor functions
        public const int HistoryMaxSize = 128; // actions
        public const double PreviewNoteOpacity = 0.30; // percentage of 1.0
        public const double DragInitThreshold = 10; // pixels
        public const int AutosaveInterval = 30; // seconds

        // Note Paste Behavior
        public static class NotePasteBehavior {
            public const string AlignToGlobalBeat = "AlignToGlobalBeat"; // paste notes as-if they were copied from the same global BPM and disregard any BPM changes.
            public const string AlignToFirstNoteBPM = "AlignToFirstNoteBPM"; // paste notes only scaling by a factor based on the first pasted note BPM and mouse position BPM.
            public const string AlignToNoteBPM = "AlignToNoteBPM"; // paste notes by aligning each note timing based on all the BPM changes in the pasted notes and mouse position BPM.
        }

        // Hold Scrolling
        public static class HoldScroll {
            public const double Slowdown = 10;
            public const double DeadZone = 15.0;
        }

        // Difficulty
        public static class Difficulty {
            public static string SelectedColour => Colors.LightSkyBlue.ToString();
            public const int LevelMin = 1;
            public const int LevelMax = 99;
        }

        // Bookmarks
        public const double NavWaveformOpacity = 0.75;
        public static class GridPreviewLine {
            public static string Colour => Colors.FloralWhite.ToString();
            public const double Thickness = 1.0;
        }
        public static class NavPreviewLine {
            public static string Colour => Colors.FloralWhite.ToString();
            public const double Thickness = 1;
        }
        public static class NavBookmark {
            public static string DefaultName = "Bookmark";
            public static string Colour => Colors.SkyBlue.ToString();
            public static string NameColour => Colors.SkyBlue.ToString();
            public static string BackgroundColour => Colors.SkyBlue.ToString();
            public const double Thickness = 1;
            public const double NameSize = 10;
            public const double NamePadding = 1;
            public const double Opacity = 1;
        }
        public static class GridBookmark {
            public static string Colour => Colors.DeepSkyBlue.ToString();
            public static string NameColour => Colors.DeepSkyBlue.ToString();
            public static string BackgroundColour => Colors.DeepSkyBlue.ToString();
            public const double Thickness = 3;
            public const double NameSize = 11;
            public const double NamePadding = 3;
            public const double Opacity = 0.75;
        }
        public static class BPMChange {
            public static string Colour => Colors.MediumPurple.ToString();
            public static string NameColour => Colors.MediumPurple.ToString();
            public static string BackgroundColour => Colors.MediumPurple.ToString();
            public const double Thickness = 3;
            public const double NameSize = 11;
            public const double NamePadding = 3;
            public const double Opacity = 0.75;
        }
        public static class Stats {
            public static readonly MediaColor Colour = Colors.Black;
            public static readonly MediaColor WarningColour = Colors.Crimson;
            public static readonly FontWeight FontWeight = FontWeights.Regular;
            public static readonly FontWeight WarningFontWeight = FontWeights.Bold;
        }

        // Waveform drawing
        public static class Waveform {
            public const double SampleMaxPercentile = 0.95;
            public const double Width = 0.75;
            public const int MaxDimension = 65535;
            public const double ThicknessWPF = 1;
            public const bool UseGDI = false;
            public static MediaColor ColourWPF => MediaColor.FromArgb(180, 0, 0, 255);
            public static DrawingColor ColourGDI => DrawingColor.FromArgb(180, 0, 0, 255);
        }

        public static class Spectrogram {
            public const int MelBinCount = 100;
            public const int MinFreq = 100; // Hz
            public const int MaxFreq = 22_000; // Hz
            public const int DefaultFreq = 11_000; // Hz
            public const int FftSizeExp = 11; // FFT Size = 2^FftSizeExp
            public const int StepSize = 500; // samples
            public const int MaxSampleSteps = 65500; // we can handle roughly this many steps through samples before we reach the max pixel size of the bitmap for a chunk.
            public const int NumberOfChunks = 12; // Each chunk can span roughly 5 minutes before we reach the max pixel size of the bitmap.
            public const string CachedBmpFilenameFormat = "spectrogram_{0}_{1}_{2}_{3}_*.png";
        }

    }
    public static class Audio {
        public const float MaxPanDistance = 0.6f;
        // Latencies        
        public const int WASAPILatencyTarget = 200; // ms
        // Note Playback    
        public const int NotePlaybackStreams = 32;
        public const int NotePollRate = 15;  // ms
        public const double NoteDetectionDelta = 10;  // ms
        // Song Tempo
        public const double MaxSongTempo = 2.0;
        public const double DefaultSongTempo = 1.0;
        public const double MinSongTempo = 0.1;
        public const double SongTempoSmallChange = 0.05;
        public const double SongTempoLargeChange = 0.25;

        public const int MaxPreviewLength = 15; // sec
        public const int DefaultPreviewFadeIn = 1; // sec
        public const int DefaultPreviewFadeOut = 3; // sec

        public static string MetronomeFilename = "metronome";
        public const int MetronomeStreams = 4;
    }
    public static class BeatmapDefaults {
        public const double BeatsPerMinute = 120;
        public const string SongFilename = "song.ogg";
        public const string PreviewFilename = "preview.ogg";
        public const string CoverFilename = "cover";
        public const int Shuffle = 0;           // what do
        public const double ShufflePeriod = 0.5;         // these do??
        public const string BeatmapCharacteristicName = "Standard";
        public static List<string> DifficultyNames => new() { "Easy", "Normal", "Hard" };
        public static List<string> EnvironmentNames => new() { "Midgard", "Alfheim", "Nidavellir", "Asgard", "Muspelheim", "Helheim", "Hellfest", "Sabaton", "Empty", "DarkEmpty" };
        //public const string DefaultEnvironmentAlias = "Midgard";

        public static double GetPreferredNoteJumpMovementSpeed() {
            var userSettings = new UserSettingsManager(Program.SettingsFile);
            try {
                return double.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultNoteSpeed));
            } catch {
                return Editor.DefaultNoteSpeed;
            }
        }

        public static double GetPreferredGridSpacing() {
            var userSettings = new UserSettingsManager(Program.SettingsFile);
            try {
                return double.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultGridSpacing));
            } catch {
                return Editor.DefaultGridSpacing;
            }
        }

        public static string GetPreferredMapper() {
            var userSettings = new UserSettingsManager(Program.SettingsFile);
            try {
                return userSettings.GetValueForKey(UserSettingsKey.DefaultMapper);
            } catch {
                return string.Empty;
            }
        }
    }

    public static class DiscordRPC {
        public const string AppID = "874090300207951932";
        public const string PubKey = "c2710c1d8cd4d9a2a9460cd63e048781d32a3a08b171153c5898a6fe0ddb8e76";
        public const string IconKey = "icon";
    }

    public static class DifficultyPrediction {
        public static readonly MediaColor Colour = Colors.Black;
        public static readonly MediaColor WarningColour = Colors.OrangeRed;
        public static class SupportedAlgorithms {
            public const string PKBeam = "PKBeam_ML";
            public const string Nytilde = "Nytilde_ML";
            public const string Melchior = "Melchior";
        }
        public static class Nytilde {
            // These values are based on the dataset used for the ML model training.
            public const double MaxNoteDensity = 7.615101;
            public const double MinAverageTimeDifference = 0.152743;
            public const double MaxCountNoteDensityPerWindow = 1578.5;
            public const double MaxPeakNoteDensity = 25.5;
            public const double MinTypicalTimeDifference = 0.091463;
        }
        public static class Melchior {
            // These values come from a curve fitting on the available dataset.
            public const double FitCoefficient = 0.6632333348;
        }
    }
}
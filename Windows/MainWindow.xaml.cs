using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.CoreAudioApi;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundTouch.Net.NAudioSupport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;
using Timer = System.Timers.Timer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
/// 

namespace Edda {
    public partial class MainWindow : Window {

        // COMPUTED PROPERTIES
        bool songIsPlaying {
            set { btnSongPlayer.Tag = (value == false) ? 0 : 1; }
            get { return btnSongPlayer.Tag != null && (int)btnSongPlayer.Tag == 1; }
        }
        public double songSeekPosition {
            get {
                return songStream.CurrentTime.TotalMilliseconds;
            }
            set {
                sliderSongProgress.Value = value;
            }
        }
        public double? songTotalTimeInSeconds {
            get {
                return songStream?.TotalTime.TotalSeconds;
            }
        }
        bool mapIsLoaded {
            get {
                return mapEditor != null;
            }
        }
        public double globalBPM {
            get {
                return mapEditor.GlobalBPM;
            }
            set {
                mapEditor.GlobalBPM = value;
            }
        }

        public int defaultGridDivision {
            get {
                return mapEditor.defaultGridDivision;
            }
            set {
                mapEditor.defaultGridDivision = value;
            }
        }

        public string userPreferredPlaybackDeviceID {
            get {
                return userSettings.GetValueForKey(UserSettingsKey.PlaybackDeviceID);
            }
        }
        internal MMDevice playbackDevice {
            get {
                MMDevice device = null;
                if (!string.IsNullOrEmpty(playbackDeviceID)) {
                    try {
                        device = deviceEnumerator.GetDevice(playbackDeviceID);
                        if (!device.State.HasFlag(DeviceState.Active)) {
                            // Fallback to default device if the preferred device isn't available.
                            device = null;
                        }
                    } catch (Exception ex) {
                        Trace.WriteLine($"WARNING: Couldn't get the playback device with ID {playbackDeviceID} due to an error:\n{ex.Message}.\n{ex.StackTrace}", "Warning");
                        playbackDeviceID = null;
                    }
                }
                if (device == null && defaultDeviceAvailable) {
                    try {
                        device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                        playingOnDefaultDevice = true;
                    } catch (Exception ex) {
                        Trace.WriteLine($"WARNING: Couldn't get the default playback device due to an error:\n{ex.Message}.\n{ex.StackTrace}", "Warning");
                        defaultDeviceAvailable = false;
                    }
                }
                return device;
            }
        }
        public MMDeviceCollection availablePlaybackDevices {
            get {
                return deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            }
        }

        // STATE VARIABLES
        public MapEditor mapEditor;
        public EditorGridController gridController;
        SongPreviewController songPreviewController;
        Timer autosaveTimer;
        UserSettingsManager userSettings;
        public bool shiftKeyDown;
        public bool ctrlKeyDown;
        bool returnToStartMenuOnClose = false;
        bool showDifficultyPrediction = false;
        public IDifficultyPredictor difficultyPredictor = DifficultyPredictorPKBeam.SINGLETON;

        DoubleAnimation songPlayAnim;            // used for animating scroll when playing a song
        double prevScrollPercent = 0;       // percentage of scroll progress before the scroll viewport was changed

        // audio playback
        CancellationTokenSource songPlaybackCancellationTokenSource;
        internal int editorAudioLatency; // ms
        MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
        DeviceChangeListener deviceChangeListener;
        public string playbackDeviceID {
            get;
            private set;
        }
        public bool playingOnDefaultDevice {
            get;
            private set;
        } = false;
        public bool defaultDeviceAvailable {
            get;
            private set;
        } = true;
        SampleChannel songChannel;
        public VorbisWaveReader songStream;
        SoundTouchWaveStream songTempoStream;
        public WasapiOut songPlayer;
        internal ParallelAudioPlayer drummer;
        ParallelAudioPlayer metronome;
        NoteScanner noteScanner;
        BeatScanner beatScanner;

        IDisposable scrollEditorSizeChangedDebounce;
        IDisposable borderNavWaveformSizeChangedDebounce;
        IDisposable borderSpectrogramSizeChangedDebounce;

        // references to parent application
        RecentOpenedFolders recentMaps {
            get {
                return ((RagnarockEditor.App)Application.Current).RecentMaps;
            }
        }
        DiscordClient discordClient {
            get {
                return ((RagnarockEditor.App)Application.Current).DiscordClient;
            }
        }

        public MainWindow() {

            InitializeComponent();

            // disable parts of UI, as no map is loaded
            DisableUI();

            autosaveTimer = new Timer(1000 * Editor.AutosaveInterval);
            autosaveTimer.Enabled = false;
            autosaveTimer.Elapsed += (source, e) => {
                try {
                    SaveBeatmap();
                } catch {
                    Trace.WriteLine("INFO: Unable to autosave beatmap");
                }
            };

            // load editor UI
            gridController = new EditorGridController(
                this,
                EditorGrid,
                scrollEditor,
                DrumCol,
                DrumRow,
                borderNavWaveform,
                colWaveformVertical,
                imgWaveformVertical,
                scrollSpectrogram,
                panelSpectrogram,
                EditorMarginGrid,
                canvasNavInputBox,
                canvasBookmarks,
                canvasBookmarkLabels,
                lineSongMouseover
            );
            // load preview UI
            songPreviewController = new SongPreviewController(this);

            InitSettings();

            deviceChangeListener = new DeviceChangeListener(this);
            deviceEnumerator.RegisterEndpointNotificationCallback(deviceChangeListener);

            InitDrummer();
            InitMetronome();

            // load editor preview note
            InitNavMouseoverLine();

            // init environment combobox
            InitComboEnvironment();
            songPlaybackCancellationTokenSource = new();
            //debounce grid redrawing on resize
            scrollEditorSizeChangedDebounce = Observable
                .FromEventPattern<SizeChangedEventArgs>(scrollEditor, nameof(SizeChanged))
                .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(eventPattern =>
                    Dispatcher.Invoke(() =>
                        ScrollEditor_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                    )
                );

            borderNavWaveformSizeChangedDebounce = Observable
                .FromEventPattern<SizeChangedEventArgs>(borderNavWaveform, nameof(SizeChanged))
                .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(eventPattern =>
                    Dispatcher.Invoke(() =>
                        BorderNavWaveform_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                    )
                );

            borderSpectrogramSizeChangedDebounce = Observable
                .FromEventPattern<SizeChangedEventArgs>(borderSpectrogram, nameof(SizeChanged))
                .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(eventPattern =>
                    Dispatcher.Invoke(() =>
                        BorderSpectrogram_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                    )
                );
        }


        // map file I/O functions

        // initialisation entrypoints from start window ...
        internal void InitNewMap(string newMapFolder) {
            if (mapIsLoaded) {
                mapEditor.ClearSelectedDifficulty();
                ClearCoverImage();
            }

            // select an audio file
            string file = SelectSongDialog();
            if (file == null) {
                return;
            }
            mapEditor = new MapEditor(this, newMapFolder, true);
            gridController.InitMap(mapEditor);
            songPreviewController?.LoadPreview(mapEditor);

            // load audio file
            LoadSongFile(file);
            SetMapDetailsFromSongMetadata();

            recentMaps.AddRecentlyOpened((string)mapEditor.GetMapValue("_songName"), newMapFolder);
            recentMaps.Write();

            RefreshDiscordPresence();
        }
        internal void InitImportMap(string importMapFolder) {
            if (mapIsLoaded) {
                mapEditor.ClearSelectedDifficulty();
                ClearCoverImage();
                UnloadSong();
            }

            // select simfile to import
            string file = null;
            var d = new Microsoft.Win32.OpenFileDialog() { Filter = "StepMania simfile|*.sm;*.ssc" };
            d.Title = "Select a simfile to import";
            if (d.ShowDialog() == true) {
                file = d.FileName;
            } else {
                return;
            }

            RagnarockMap beatmap = new RagnarockMap(importMapFolder, true);

            // convert imported map
            IMapConverter converter;
            switch (Path.GetExtension(file)) {
                case ".sm":
                case ".ssc": {
                        converter = new StepManiaMapConverter();
                        break;
                    }
                default: throw new FileFormatException("Selected file is not in a supported format for import.");
            }
            converter.Convert(file, beatmap);
            beatmap.SaveToFile();

            InitOpenMap(importMapFolder);
        }
        internal void InitOpenMap(string mapFolder) {
            mapEditor = new MapEditor(this, mapFolder, false);
            gridController.InitMap(mapEditor);
            LoadSong(); // song file
            songPreviewController?.LoadPreview(mapEditor);
            LoadCoverImage();
            InitUI(); // cover image file

            recentMaps.AddRecentlyOpened((string)mapEditor.GetMapValue("_songName"), mapFolder);
            recentMaps.Write();

            // bandaid fix to prevent WPF from committing unnecessarily large amounts of memory
            new Thread(new ThreadStart(delegate {
                Thread.Sleep(500);
                this.Dispatcher.Invoke(() => DrawEditorGrid(false));
            })).Start();

            RefreshDiscordPresence();
        }
        // ... future actions will call these functions
        private void CreateNewMap() {
            // check if map already open
            if (mapIsLoaded) {
                if (!PromptBeatmapSave()) {
                    return;
                }

                PauseSong();
            }

            string newMapFolder = Helper.ChooseNewMapFolder();
            if (newMapFolder == null) {
                return;
            }

            InitNewMap(newMapFolder);
        }
        private void ImportMap() {
            // check if map already open
            if (mapIsLoaded) {
                if (!PromptBeatmapSave()) {
                    return;
                }

                PauseSong();
            }

            string importMapFolder = Helper.ChooseNewMapFolder();
            if (importMapFolder == null) {
                return;
            }

            try {
                InitImportMap(importMapFolder);
            } catch (Exception ex) {
                MessageBox.Show(this, $"An error occured while importing the simfile:\n{ex.Message}.\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OpenMap() {
            // check if map already open
            if (mapIsLoaded) {
                if (!PromptBeatmapSave()) {
                    return;
                }

                PauseSong();
            }

            string openMapFolder = Helper.ChooseOpenMapFolder();
            if (openMapFolder == null) {
                return;
            }

            // try to load info
            var oldMapEditor = mapEditor;
            try {

                InitOpenMap(openMapFolder);

            } catch (Exception ex) {
                MessageBox.Show(this, $"An error occured while opening the map:\n{ex.Message}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // reload previous beatmap
                if (mapIsLoaded) {
                    mapEditor = oldMapEditor;
                    gridController.InitMap(oldMapEditor);
                    LoadSong();
                    songPreviewController?.LoadPreview(oldMapEditor);
                    LoadCoverImage();
                    InitUI();
                }
                return;
            }
        }

        // ---
        private void SaveBeatmap() {
            if (!mapIsLoaded) {
                return;
            }
            mapEditor.SaveMap();
            /*
            this.Dispatcher.Invoke(() => {
                //imgSaved.Opacity = 1;
                var saveAnim = new DoubleAnimation();
                saveAnim.From = 1;
                saveAnim.To = 0;
                saveAnim.Duration = new Duration(new TimeSpan(0, 0, 5));
                Storyboard.SetTargetProperty(saveAnim, new PropertyPath("(Image.Opacity)"));
                Storyboard.SetTargetName(saveAnim, "imgSaved");

                var st = new Storyboard();
                st.Children.Add(saveAnim);
                st.Begin(this, true);
            });
            */
        }
        private void BackupAndSaveBeatmap() {
            // https://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true
            void DeleteDirectory(string target_dir) {
                string[] files = Directory.GetFiles(target_dir);
                string[] dirs = Directory.GetDirectories(target_dir);

                foreach (string file in files) {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }

                foreach (string dir in dirs) {
                    DeleteDirectory(dir);
                }

                Directory.Delete(target_dir, false);
            }

            // save beatmap
            SaveBeatmap();

            // create backup directory if it doesnt exist
            string backupFolder = Path.Combine(mapEditor.mapFolder, Program.BackupPath);
            if (!Directory.Exists(backupFolder)) {
                Directory.CreateDirectory(backupFolder);
            }

            // get names of files to backup
            List<string> files = new(BeatmapDefaults.DifficultyNames);
            files.Add("info");

            string newBackupName = DateTime.Now.ToString("Backup - dd MMMM yyyy h.mmtt");
            string newBackupPath = System.IO.Path.Combine(backupFolder, newBackupName);
            List<string> folders = new(Directory.GetDirectories(backupFolder));
            List<string> existingBackups = folders.FindAll((str) => { return str.StartsWith(System.IO.Path.Combine(backupFolder, "Backup ")); });

            // dont make another backup if one has been made very recently (1 second)
            if (existingBackups.Exists((str) => { return str == newBackupPath; })) {
                return;
            }

            // sort by oldest first
            existingBackups.Sort((a, b) => {
                DateTime dA = Directory.GetCreationTime(a);
                DateTime dB = Directory.GetCreationTime(b);
                return dA.CompareTo(dB);
            });

            // delete oldest backup if we have too many
            if (existingBackups.Count == Program.MaxBackups) {
                DeleteDirectory(existingBackups[0]);
            }

            // make new backup file
            Directory.CreateDirectory(newBackupPath);
            foreach (var diffName in files) {
                string recentSavePath = Path.Combine(mapEditor.mapFolder, $"{diffName}.dat");
                if (File.Exists(recentSavePath)) {
                    File.Copy(recentSavePath, Path.Combine(newBackupPath, $"{diffName}.dat"));
                }
            }

        }
        private void ExportMap() {
            var d = new CommonOpenFileDialog();
            d.Title = "Select a folder to export the map to";
            d.IsFolderPicker = true;
            d.InitialDirectory = Helper.GetRagnarockMapFolder();
            if (d.ShowDialog() != CommonFileDialogResult.Ok) {
                return;
            }

            SaveBeatmap();

            string songArtist = Helper.ValidFilenameFrom((string)mapEditor.GetMapValue("_songAuthorName"));
            string songName = Helper.ValidFilenameFrom((string)mapEditor.GetMapValue("_songName"));
            string baseFolder = mapEditor.mapFolder;
            string zipName = Helper.ValidMapFolderNameFrom(songArtist + songName);
            // make the temp dir for zip
            string zipFolder = Path.Combine(baseFolder, zipName + "_tempDir");
            string zipPath = Path.Combine(d.FileName, zipName + ".zip");

            try {
                Helper.FileDeleteIfExists(zipPath);

                // only select the files that need to be in the ZIP
                var copyFiles = new List<string> {
                    Path.Combine(baseFolder, "info.dat")
                };
                string songFilename = (string)mapEditor.GetMapValue("_songFilename");
                if (songFilename != null && File.Exists(Path.Combine(baseFolder, songFilename))) {
                    copyFiles.Add(Path.Combine(baseFolder, songFilename));
                }
                string coverFilename = (string)mapEditor.GetMapValue("_coverImageFilename");
                if (coverFilename != null && File.Exists(Path.Combine(baseFolder, coverFilename))) {
                    copyFiles.Add(Path.Combine(baseFolder, coverFilename));
                }
                string previewFile = Path.Combine(baseFolder, "preview.ogg");
                if (File.Exists(previewFile)) {
                    copyFiles.Add(previewFile);
                }
                for (int diffIndex = 0; diffIndex < mapEditor.numDifficulties; ++diffIndex) {
                    var filename = (string)mapEditor.GetMapValue("_beatmapFilename", (RagnarockMapDifficulties)diffIndex);
                    copyFiles.Add(Path.Combine(baseFolder, filename));
                }
                // --

                if (Directory.Exists(zipFolder)) {
                    Directory.Delete(zipFolder, true);
                }
                Directory.CreateDirectory(zipFolder);

                // need to use cmd to copy files; .NET Filesystem API throws an exception because "the file is being used"
                //Helper.CmdCopyFiles(copyFiles, zipFolder);
                foreach (var file in copyFiles) {
                    File.Copy(file, Path.Combine(zipFolder, Path.GetFileName(file)));
                }
                ZipFile.CreateFromDirectory(zipFolder, zipPath);

            } catch (Exception) {
                MessageBox.Show(this, $"An error occured while creating the zip file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally {
                if (Directory.Exists(zipFolder)) {
                    Directory.Delete(zipFolder, true);
                }
            }
        }

        // UI initialisation
        private void InitUI() {
            // reset variables
            prevScrollPercent = 0;

            bool mapDirtyState = mapEditor.needsSave; // Store the "dirty" state of the map, so we can restore it after UI is initialized.

            lineSongProgress.Y1 = borderNavWaveform.ActualHeight;
            lineSongProgress.Y2 = borderNavWaveform.ActualHeight;

            sliderSongVol.Value = float.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultSongVolume));
            sliderDrumVol.Value = float.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultNoteVolume));

            // map settings
            txtSongName.Text = (string)mapEditor.GetMapValue("_songName");
            txtArtistName.Text = (string)mapEditor.GetMapValue("_songAuthorName");
            txtMapperName.Text = (string)mapEditor.GetMapValue("_levelAuthorName");
            txtSongBPM.Text = (string)mapEditor.GetMapValue("_beatsPerMinute");
            txtSongOffset.Text = (string)mapEditor.GetMapValue("_songTimeOffset");
            checkExplicitContent.IsChecked = (string)mapEditor.GetMapValue("_explicit") == "true";
            checkWaveform.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableSpectrogram) != true;
            comboEnvironment.SelectedIndex = BeatmapDefaults.EnvironmentNames.IndexOf((string)mapEditor.GetMapValue("_environmentName"));
            MenuItemSnapToGrid.IsChecked = (checkGridSnap.IsChecked == true);
            mapEditor.SongDuration = songStream.TotalTime.TotalSeconds;
            mapEditor.GlobalBPM = Helper.DoubleParseInvariant((string)mapEditor.GetMapValue("_beatsPerMinute"));
            gridController.showWaveform = (checkWaveform.IsChecked == true);
            songTempoStream.Tempo = sliderSongTempo.Value;
            var songPath = Path.Combine(mapEditor.mapFolder, (string)mapEditor.GetMapValue("_songFilename"));
            gridController.InitWaveforms(songPath);

            // enable UI parts
            EnableUI();

            // init difficulty-specific UI 
            SwitchDifficultyMap(0);
            SetupSongTempoSlider();
            UpdateDifficultyButtons();
            DrawEditorGrid();
            scrollEditor.ScrollToBottom();
            gridController.DrawNavWaveform();

            mapEditor.needsSave = mapDirtyState;
        }
        private void EnableUI() {
            btnChangeDifficulty0.IsEnabled = true;
            btnChangeDifficulty1.IsEnabled = true;
            btnChangeDifficulty2.IsEnabled = true;
            txtSongName.IsEnabled = true;
            txtArtistName.IsEnabled = true;
            txtMapperName.IsEnabled = true;
            txtSongBPM.IsEnabled = true;
            btnChangeBPM.IsEnabled = true;
            txtSongOffset.IsEnabled = true;
            checkExplicitContent.IsEnabled = true;
            comboEnvironment.IsEnabled = true;
            btnPickSong.IsEnabled = true;
            btnMakePreview.IsEnabled = true;
            btnPickCover.IsEnabled = true;
            sliderSongVol.IsEnabled = true;
            sliderDrumVol.IsEnabled = true;
            sliderSongTempo.IsEnabled = true;
            checkMetronome.IsEnabled = true;
            checkGridSnap.IsEnabled = true;
            txtDifficultyNumber.IsEnabled = true;
            txtNoteSpeed.IsEnabled = true;
            txtDistMedal0.IsEnabled = true;
            txtDistMedal1.IsEnabled = true;
            txtDistMedal2.IsEnabled = true;
            txtGridDivision.IsEnabled = true;
            txtGridSpacing.IsEnabled = true;
            checkWaveform.IsEnabled = true;
            btnDeleteDifficulty.IsEnabled = true;
            btnSongPlayer.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            scrollEditor.IsEnabled = true;
            borderNavWaveform.IsEnabled = true;
        }
        private void DisableUI() {
            btnChangeDifficulty0.IsEnabled = false;
            btnChangeDifficulty1.IsEnabled = false;
            btnChangeDifficulty2.IsEnabled = false;
            btnAddDifficulty.IsEnabled = false;
            txtSongName.IsEnabled = false;
            txtArtistName.IsEnabled = false;
            txtMapperName.IsEnabled = false;
            txtSongBPM.IsEnabled = false;
            btnChangeBPM.IsEnabled = false;
            txtSongOffset.IsEnabled = false;
            checkExplicitContent.IsEnabled = false;
            comboEnvironment.IsEnabled = false;
            btnPickSong.IsEnabled = false;
            btnMakePreview.IsEnabled = false;
            btnPickCover.IsEnabled = false;
            sliderSongVol.IsEnabled = false;
            sliderDrumVol.IsEnabled = false;
            sliderSongTempo.IsEnabled = false;
            checkMetronome.IsEnabled = false;
            checkGridSnap.IsEnabled = false;
            txtDifficultyNumber.IsEnabled = false;
            txtNoteSpeed.IsEnabled = false;
            txtDistMedal0.IsEnabled = false;
            txtDistMedal1.IsEnabled = false;
            txtDistMedal2.IsEnabled = false;
            txtGridDivision.IsEnabled = false;
            txtGridSpacing.IsEnabled = false;
            checkWaveform.IsEnabled = false;
            btnDeleteDifficulty.IsEnabled = false;
            btnSongPlayer.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;
            scrollEditor.IsEnabled = false;
            borderNavWaveform.IsEnabled = false;
        }
        private void ToggleLeftDock() {
            if (borderLeftDock.Visibility == Visibility.Collapsed) {
                borderLeftDock.Visibility = Visibility.Visible;
                this.Width += borderLeftDock.ActualWidth;
                this.MinWidth += borderLeftDock.MinWidth;
            } else {
                this.Width -= borderLeftDock.ActualWidth;
                this.MinWidth -= borderLeftDock.MinWidth;
                borderLeftDock.Visibility = Visibility.Collapsed;
            }
        }
        private void ToggleRightDock() {
            if (borderRightDock.Visibility == Visibility.Collapsed) {
                borderRightDock.Visibility = Visibility.Visible;
                this.Width += borderRightDock.ActualWidth;
                this.MinWidth += borderRightDock.MinWidth;
            } else {
                this.Width -= borderRightDock.ActualWidth;
                this.MinWidth -= borderRightDock.MinWidth;
                borderRightDock.Visibility = Visibility.Collapsed;
            }
        }

        // config file
        internal void InitSettings() {
            // init program data dir if necessary
            if (!Directory.Exists(Program.ProgramDataDir)) {
                Directory.CreateDirectory(Program.ProgramDataDir);
            }
            // move old settings files to new centralised location
            if (File.Exists(Program.OldSettingsFile)) {
                File.Move(Program.OldSettingsFile, Program.SettingsFile);
            }

            ValidateSettingsFile();
            LoadSettingsFile();
        }
        internal void ValidateSettingsFile() {
            userSettings = new UserSettingsManager(Program.SettingsFile);

            if (userSettings.GetValueForKey(UserSettingsKey.EnableSpectrogram) == null) {
                userSettings.SetValueForKey(UserSettingsKey.EnableSpectrogram, DefaultUserSettings.EnableSpectrogram);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.DefaultMapper) == null) {
                userSettings.SetValueForKey(UserSettingsKey.DefaultMapper, DefaultUserSettings.DefaultMapper);
            }

            try {
                double.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultNoteSpeed));
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.DefaultNoteSpeed, DefaultUserSettings.DefaultNoteSpeed);
            }

            try {
                double.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultGridSpacing));
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.DefaultGridSpacing, DefaultUserSettings.DefaultGridSpacing);
            }

            try {
                int.Parse(userSettings.GetValueForKey(UserSettingsKey.EditorAudioLatency));
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.EditorAudioLatency, DefaultUserSettings.AudioLatency);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.PanDrumSounds) == null) {
                userSettings.SetValueForKey(UserSettingsKey.PanDrumSounds, DefaultUserSettings.PanDrumSounds);
            }

            try {
                float.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultNoteVolume));
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume);
            }

            try {
                float.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultSongVolume));
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.DefaultSongVolume, DefaultUserSettings.DefaultSongVolume);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.DrumSampleFile) == null) {
                userSettings.SetValueForKey(UserSettingsKey.DrumSampleFile, DefaultUserSettings.DrumSampleFile);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.DefaultSongVolume) == null) {
                userSettings.SetValueForKey(UserSettingsKey.DefaultSongVolume, DefaultUserSettings.DefaultSongVolume);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.DefaultNoteVolume) == null) {
                userSettings.SetValueForKey(UserSettingsKey.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.SpectrogramCache) == null) {
                userSettings.SetValueForKey(UserSettingsKey.SpectrogramCache, DefaultUserSettings.SpectrogramCache);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.SpectrogramType) == null) {
                userSettings.SetValueForKey(UserSettingsKey.SpectrogramType, DefaultUserSettings.SpectrogramType);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.SpectrogramQuality) == null) {
                userSettings.SetValueForKey(UserSettingsKey.SpectrogramQuality, DefaultUserSettings.SpectrogramQuality);
            }

            try {
                int.Parse(userSettings.GetValueForKey(UserSettingsKey.SpectrogramFrequency));
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.SpectrogramFrequency, DefaultUserSettings.SpectrogramFrequency);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.SpectrogramColormap) == null) {
                userSettings.SetValueForKey(UserSettingsKey.SpectrogramColormap, DefaultUserSettings.SpectrogramColormap);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.SpectrogramFlipped) == null) {
                userSettings.SetValueForKey(UserSettingsKey.SpectrogramFlipped, DefaultUserSettings.SpectrogramFlipped);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.SpectrogramChunking) == null) {
                userSettings.SetValueForKey(UserSettingsKey.SpectrogramChunking, DefaultUserSettings.SpectrogramChunking);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.EnableAutosave) == null) {
                userSettings.SetValueForKey(UserSettingsKey.EnableAutosave, DefaultUserSettings.EnableAutosave);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.CheckForUpdates) == null) {
                userSettings.SetValueForKey(UserSettingsKey.CheckForUpdates, DefaultUserSettings.CheckForUpdates);
            }

            try {
                var index = int.Parse(userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationIndex));
                // game install directory chosen
                var gameInstallPath = userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationPath);
                if (index == 1 && !Directory.Exists(gameInstallPath)) {
                    throw new Exception();
                }
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationIndex, DefaultUserSettings.MapSaveLocationIndex);
                userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, DefaultUserSettings.MapSaveLocationPath);
            }

            try {
                int.Parse(userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationIndex));
            } catch {
                userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationIndex, DefaultUserSettings.MapSaveLocationIndex);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm) == null) {
                userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm, DefaultUserSettings.DifficultyPredictorAlgorithm);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.DifficultyPredictorShowPrecise) == null) {
                userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorShowPrecise, DefaultUserSettings.DifficultyPredictorShowPrecise);
            }

            if (userSettings.GetValueForKey(UserSettingsKey.DifficultyPredictorShowInMapStats) == null) {
                userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorShowInMapStats, DefaultUserSettings.DifficultyPredictorShowInMapStats);
            }

            userSettings.Write();
        }
        internal void LoadSettingsFile(bool reloadWaveforms = false) {

            userSettings = new UserSettingsManager(Program.SettingsFile);

            var showSpectrogram = userSettings.GetBoolForKey(UserSettingsKey.EnableSpectrogram);
            gridController.showSpectrogram = showSpectrogram;
            if (showSpectrogram) {
                gridSpectrogram.Visibility = Visibility.Visible;
            } else {
                gridSpectrogram.Visibility = Visibility.Collapsed;
            }

            var cacheSpectrogram = userSettings.GetBoolForKey(UserSettingsKey.SpectrogramCache);
            gridController.spectrogramCache = cacheSpectrogram;
            if (showSpectrogram && cacheSpectrogram) {
                MenuItemClearCache.Visibility = Visibility.Visible;
            } else {
                MenuItemClearCache.Visibility = Visibility.Collapsed;
            }

            Enum.TryParse(typeof(VorbisSpectrogramGenerator.SpectrogramType), userSettings.GetValueForKey(UserSettingsKey.SpectrogramType), out object spectrogramType);
            gridController.spectrogramType = (VorbisSpectrogramGenerator.SpectrogramType)spectrogramType;
            Enum.TryParse(typeof(VorbisSpectrogramGenerator.SpectrogramQuality), userSettings.GetValueForKey(UserSettingsKey.SpectrogramQuality), out object spectrogramQuality);
            gridController.spectrogramQuality = (VorbisSpectrogramGenerator.SpectrogramQuality)spectrogramQuality;
            int.TryParse(userSettings.GetValueForKey(UserSettingsKey.SpectrogramFrequency), out int spectrogramFrequency);
            gridController.spectrogramFrequency = spectrogramFrequency;
            gridController.spectrogramColormap = userSettings.GetValueForKey(UserSettingsKey.SpectrogramColormap);
            gridController.spectrogramFlipped = userSettings.GetBoolForKey(UserSettingsKey.SpectrogramFlipped);

            var spectrogramChunking = gridController.spectrogramChunking;
            gridController.spectrogramChunking = userSettings.GetBoolForKey(UserSettingsKey.SpectrogramChunking);
            if (spectrogramChunking != gridController.spectrogramChunking) {
                gridController.SetupSpectrogramContent();
            }
            if (reloadWaveforms) {
                gridController.RefreshSpectrogramWaveform();
                gridController.DrawSpectrogram();
            }

            difficultyPredictor = userSettings.GetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm) switch {
                DifficultyPrediction.SupportedAlgorithms.PKBeam => DifficultyPredictorPKBeam.SINGLETON,
                DifficultyPrediction.SupportedAlgorithms.Nytilde => DifficultyPredictorNytilde.SINGLETON,
                DifficultyPrediction.SupportedAlgorithms.Melchior => DifficultyPredictorMelchior.SINGLETON,
                _ => DifficultyPredictorPKBeam.SINGLETON
            };

            showDifficultyPrediction = userSettings.GetBoolForKey(UserSettingsKey.DifficultyPredictorShowInMapStats);
            if (showDifficultyPrediction && difficultyPredictor.GetSupportedFeatures().HasFlag(IDifficultyPredictor.Features.RealTime)) {
                difficultyPrediction.Visibility = Visibility.Visible;
            } else {
                difficultyPrediction.Visibility = Visibility.Collapsed;
            }

            int.TryParse(userSettings.GetValueForKey(UserSettingsKey.EditorAudioLatency), out editorAudioLatency);

            playbackDeviceID = userPreferredPlaybackDeviceID;

            SetDiscordRPC(userSettings.GetBoolForKey(UserSettingsKey.EnableDiscordRPC));
            autosaveTimer.Enabled = userSettings.GetBoolForKey(UserSettingsKey.EnableAutosave);

        }

        internal string GetUserSetting(string key) {
            return userSettings.GetValueForKey(key);
        }

        // song cover image
        private void SelectNewCoverImage() {
            var d = new Microsoft.Win32.OpenFileDialog() { Filter = "JPEG Files|*.jpg;*.jpeg;*.jfif" };
            d.Title = "Select a song to map";

            if (d.ShowDialog() != true) {
                return;
            }

            string sourceFile = d.FileName;

            if (!Helper.IsValidCoverFile(sourceFile)) {
                var coverTrimResult = MessageBox.Show(this, $"Ragnarock will only display square cover images. Do you want Edda to create and use a cropped version of the selected image instead?", "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                switch (coverTrimResult) {
                    case MessageBoxResult.Yes:
                        sourceFile = Helper.TrimCoverFile(sourceFile);
                        break;
                    case MessageBoxResult.No:
                        break; // Continue
                    case MessageBoxResult.Cancel:
                        return; // Cancel the whole operation
                }
            }

            imgCover.Source = null;

            string prevPath = Path.Combine(mapEditor.mapFolder, (string)mapEditor.GetMapValue("_coverImageFilename"));
            string newFile = Helper.SanitiseCoverFileName(sourceFile);
            string newPath = Path.Combine(mapEditor.mapFolder, newFile);

            // if there is no need to copy the file, only update map value
            if (newPath == sourceFile) {
                mapEditor.SetMapValue("_coverImageFilename", newFile);
                SaveBeatmap();
            }
            // skip when the chosen file is already the map cover file
            else if (prevPath != sourceFile) {
                // remove the previous cover image
                Helper.FileDeleteIfExists(prevPath);
                // delete any existing files in the map folder with conflicting names
                Helper.FileDeleteIfExists(newPath);
                // copy image file over
                File.Copy(sourceFile, newPath);

                mapEditor.SetMapValue("_coverImageFilename", newFile);
                SaveBeatmap();
            }
            LoadCoverImage();
        }
        private void LoadCoverImage() {
            var fileName = (string)mapEditor.GetMapValue("_coverImageFilename");
            var filePath = Path.Combine(mapEditor.mapFolder, fileName);
            if (!string.IsNullOrEmpty(fileName) && !File.Exists(filePath)) {
                fileName = "";
                mapEditor.SetMapValue("_coverImageFilename", fileName);
                MessageBox.Show(this,
                    "Cover image file no longer exists",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            if (string.IsNullOrEmpty(fileName)) {
                ClearCoverImage();
            } else {
                // Need to ignore cache, since we replace the contents of the cover.* file.
                BitmapImage b = Helper.BitmapGenerator(new Uri(filePath), true);
                imgCover.Source = b;
                txtCoverFileName.Text = fileName;
                borderImgCover.BorderThickness = new(2);
            }
        }
        private void ClearCoverImage() {
            imgCover.Source = null;
            txtCoverFileName.Text = "N/A";
            borderImgCover.BorderThickness = new(0);
        }

        // map difficulties
        private void SetupSongTempoSlider() {
            sliderSongTempo.Minimum = Audio.MinSongTempo;
            sliderSongTempo.Maximum = Audio.MaxSongTempo;
            sliderSongTempo.SmallChange = Audio.SongTempoSmallChange;
            sliderSongTempo.LargeChange = Audio.SongTempoLargeChange;
            sliderSongTempo.Value = Audio.DefaultSongTempo;
        }
        public void UpdateDifficultyButtons() {
            var numDiff = mapEditor.numDifficulties;

            // update visible state
            for (var i = 0; i < numDiff; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Visible;
            }
            for (var i = numDiff; i < 3; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Hidden;
            }

            // update enabled state
            foreach (Button b in DifficultyChangePanel.Children) {
                if (b.Name == ((Button)DifficultyChangePanel.Children[gridController.currentMapDifficultyIndex]).Name) {
                    b.IsHitTestVisible = false;
                    b.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.Difficulty.SelectedColour);
                } else {
                    b.IsHitTestVisible = true;
                    b.Background = SystemColors.ControlBrush;
                }
            }

            // update button labels
            var difficultyLabels = new List<Label>() { lblDifficultyRank1, lblDifficultyRank2, lblDifficultyRank3 };
            for (int i = 0; i < 3; i++) {
                try {
                    difficultyLabels[i].Content = mapEditor.GetMapValue("_difficultyRank", (RagnarockMapDifficulties)i);
                } catch {
                    Trace.WriteLine($"INFO: difficulty index {i} not found");
                    difficultyLabels[i].Content = "";
                }
            }

            // update states of add/delete buttons
            btnDeleteDifficulty.IsEnabled = (mapEditor.numDifficulties > 1);
            btnAddDifficulty.IsEnabled = (mapEditor.numDifficulties < 3);
        }
        private void SwitchDifficultyMap(int indx) {
            PauseSong();

            mapEditor.SelectDifficulty(indx);
            bool difficultyDirtyState = mapEditor.currentMapDifficulty.needsSave; // Store the "dirty" state of the difficulty map, so we can restore it after UI is initialized.

            noteScanner = new NoteScanner(this, drummer);
            beatScanner = new BeatScanner(metronome);

            txtDifficultyNumber.Text = (string)mapEditor.GetMapValue("_difficultyRank", (RagnarockMapDifficulties)indx);
            txtNoteSpeed.Text = (string)mapEditor.GetMapValue("_noteJumpMovementSpeed", (RagnarockMapDifficulties)indx);

            int dist0 = mapEditor.GetMedalDistance(RagnarockScoreMedals.Bronze, (RagnarockMapDifficulties)indx);
            int dist1 = mapEditor.GetMedalDistance(RagnarockScoreMedals.Silver, (RagnarockMapDifficulties)indx);
            int dist2 = mapEditor.GetMedalDistance(RagnarockScoreMedals.Gold, (RagnarockMapDifficulties)indx);
            txtDistMedal0.Text = (dist0 == 0) ? "Auto" : dist0.ToString();
            txtDistMedal1.Text = (dist1 == 0) ? "Auto" : dist1.ToString();
            txtDistMedal2.Text = (dist2 == 0) ? "Auto" : dist2.ToString();

            txtGridSpacing.Text = (string)mapEditor.GetMapValue("_editorGridSpacing", (RagnarockMapDifficulties)indx, custom: true); ;
            txtGridDivision.Text = (string)mapEditor.GetMapValue("_editorGridDivision", (RagnarockMapDifficulties)indx, custom: true);
            mapEditor.defaultGridDivision = int.Parse(txtGridDivision.Text);

            // set internal values
            gridController.gridDivision = int.Parse(txtGridDivision.Text);
            gridController.gridSpacing = double.Parse(txtGridSpacing.Text);

            UpdateDifficultyButtons();
            gridController.DrawNavBookmarks();
            DrawEditorGrid(false);
            mapEditor.currentMapDifficulty.needsSave = difficultyDirtyState; // Restore the "dirty" state before UI initialization.
        }

        // song/note playback
        public void UpdatePlaybackDevice(string newPlaybackDeviceID, bool isDefaultDevice) {
            playbackDeviceID = newPlaybackDeviceID;
            playingOnDefaultDevice = isDefaultDevice;
            defaultDeviceAvailable = true; // Might not be true, but will be checked next time playback device is accessed.
            // Song is paused here in order to clean up old objects in peace. 
            // When trying to do this while the song is still playing, there's some hard-to-track issues with 
            // objects not being disposed correctly, resulting in memory leaks.
            PauseSong();
            var oldSongPlayer = songPlayer;
            InitSongPlayer();
            oldSongPlayer?.Dispose();
            songPreviewController?.Restart(mapEditor, songIsPlaying);
            RestartDrummer();
            RestartMetronome();
        }
        private string SelectSongDialog() {
            // select audio file
            var d = new Microsoft.Win32.OpenFileDialog();
            d.Title = "Select a song to map";
            d.DefaultExt = ".ogg";
            d.Filter = "OGG Vorbis (*.ogg)|*.ogg";
            if (d.ShowDialog() == true) {
                return d.FileName;
            } else {
                return null;
            }
        }
        private bool LoadSongFile(string file) {
            VorbisWaveReader vorbisStream;
            try {
                vorbisStream = new VorbisWaveReader(file);
            } catch (Exception) {
                MessageBox.Show(this, "The .ogg file is corrupted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (vorbisStream.TotalTime.TotalHours >= 1) {
                MessageBox.Show(this, "Songs over 1 hour in duration are not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // check for same file
            var songFile = System.IO.Path.GetFileName(Helper.SanitiseSongFileName(file));
            var songFilePath = Path.Combine(mapEditor.mapFolder, songFile);
            var prevSongFile = Path.Combine(mapEditor.mapFolder, (string)mapEditor.GetMapValue("_songFilename"));

            if (file == prevSongFile) {
                return false;
            }

            // update beatmap data
            UnloadSong();
            mapEditor.SetMapValue("_songApproximativeDuration", (int)vorbisStream.TotalTime.TotalSeconds + 1);
            mapEditor.SetMapValue("_songFilename", songFile);
            vorbisStream.Dispose();

            // do file I/O
            Helper.FileDeleteIfExists(prevSongFile);

            // can't copy over an existing file
            Helper.FileDeleteIfExists(songFilePath);
            File.Copy(file, songFilePath);

            LoadSong();

            // redraw waveforms
            gridController.UndrawMainWaveform();
            gridController.DrawScrollingWaveforms();
            gridController.DrawNavWaveform();

            // reload map and editor
            InitUI();

            // save map to lock in the new song
            SaveBeatmap();

            return true;
        }
        private void LoadSong() {
            var songPath = Path.Combine(mapEditor.mapFolder, (string)mapEditor.GetMapValue("_songFilename"));
            songStream = new VorbisWaveReader(songPath);
            songTempoStream = new SoundTouchWaveStream(songStream);
            songChannel = new SampleChannel(songTempoStream);
            songChannel.Volume = (float)sliderSongVol.Value;
            InitSongPlayer();
            if ((int)mapEditor.GetMapValue("_songApproximativeDuration") != (int)songStream.TotalTime.TotalSeconds + 1) {
                mapEditor.SetMapValue("_songApproximativeDuration", (int)songStream.TotalTime.TotalSeconds + 1);
            }

            // load UI
            sliderSongProgress.Minimum = 0;
            sliderSongProgress.Maximum = songStream.TotalTime.TotalSeconds * 1000;
            sliderSongProgress.Value = 0;
            txtSongDuration.Text = Helper.TimeFormat((int)songStream.TotalTime.TotalSeconds);
            txtSongFileName.Text = (string)mapEditor.GetMapValue("_songFilename");

            if (gridController != null) {
                gridController.InitWaveforms(songPath);
            }
            //awd = new AudioVisualiser_Float32(new VorbisWaveReader(songPath));
        }

        private void SetMapDetailsFromSongMetadata() {
            var songPath = Path.Combine(mapEditor.mapFolder, (string)mapEditor.GetMapValue("_songFilename"));
            var songMetadata = new VorbisSampleProvider(File.OpenRead(songPath), closeOnDispose: true).Tags;
            var dataChanged = false;
            if (songMetadata.Artist != null) {
                txtArtistName.Text = songMetadata.Artist;
                dataChanged = true;
            }
            if (songMetadata.Title != null) {
                txtSongName.Text = songMetadata.Title;
                dataChanged = true;
            }
            if (dataChanged) SaveBeatmap();
        }

        internal void ClearSongCache() {
            var cacheDirectoryPath = Path.Combine(mapEditor.mapFolder, Program.CachePath);
            if (Directory.Exists(cacheDirectoryPath)) {
                Directory.Delete(cacheDirectoryPath, true);
            }
        }
        private void InitSongPlayer() {
            var device = playbackDevice;
            if (device != null) {
                songPlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, Audio.WASAPILatencyTarget);
                songPlayer.Init(songChannel);

                // subscribe to playbackstopped
                songPlayer.PlaybackStopped += (sender, args) => { PauseSong(); };
            } else {
                songPlayer = null;
            }
        }
        private void UnloadSong() {
            if (songStream != null) {
                var oldSongStream = songStream;
                songStream = null;
                oldSongStream.Dispose();
            }
            if (songPlayer != null) {
                var oldSongPlayer = songPlayer;
                songPlayer = null;
                oldSongPlayer.Dispose();
            }
        }
        private void PlaySong() {
            // don't play the song if we're at the end already
            if (Helper.DoubleApproxGreaterEqual(sliderSongProgress.Value, songStream.TotalTime.TotalMilliseconds)) {
                return;
            }

            songIsPlaying = true;
            // toggle button appearance
            imgPlayerButton.Source = Helper.BitmapGenerator("pauseButton.png");

            // set seek position for song
            try {
                songStream.CurrentTime = TimeSpan.FromMilliseconds(sliderSongProgress.Value);
            } catch (Exception ex) {
                Trace.WriteLine($"WARNING: Could not seek correctly on song ({ex})");
                songStream.CurrentTime = TimeSpan.Zero;
            }

            // disable actions that would interrupt note scanning
            txtSongBPM.IsEnabled = false;
            btnChangeBPM.IsEnabled = false;
            btnChangeDifficulty0.IsEnabled = false;
            btnChangeDifficulty1.IsEnabled = false;
            btnChangeDifficulty2.IsEnabled = false;
            scrollEditor.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;
            borderNavWaveform.IsEnabled = false;
            sliderSongTempo.IsEnabled = false;

            // stop preview from interfering
            songPreviewController?.StopPreview();
            songPreviewController?.DisablePreviewButton();

            // hide editor
            gridController.SetPreviewNoteVisibility(Visibility.Hidden);

            // animate for smooth scrolling 
            var remainingTimeSpan = songStream.TotalTime - songStream.CurrentTime;

            // note: the DoubleAnimation induces a desync of around 0.1 seconds
            songPlayAnim = new DoubleAnimation();
            songPlayAnim.From = sliderSongProgress.Value;
            songPlayAnim.To = sliderSongProgress.Maximum;
            songPlayAnim.Duration = new Duration(remainingTimeSpan / songTempoStream.Tempo);
            //Timeline.SetDesiredFrameRate(songPlayAnim, animationFramerate);
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, songPlayAnim);

            noteScanner.Start((int)(sliderSongProgress.Value - editorAudioLatency), new List<Note>(gridController.currentMapDifficultyNotes), globalBPM);
            beatScanner.Start((int)(sliderSongProgress.Value - editorAudioLatency), gridController.GetBeats(), globalBPM);

            if (editorAudioLatency == 0 || songTempoStream.CurrentTime > new TimeSpan(0, 0, 0, 0, editorAudioLatency)) {
                songTempoStream.CurrentTime = songTempoStream.CurrentTime - new TimeSpan(0, 0, 0, 0, editorAudioLatency);
                songPlayer?.Play();
            } else {
                songTempoStream.CurrentTime = new TimeSpan(0);
                var oldSongPlaybackCancellationTokenSource = songPlaybackCancellationTokenSource;
                songPlaybackCancellationTokenSource = new();
                oldSongPlaybackCancellationTokenSource.Dispose();
                Task.Delay(new TimeSpan(0, 0, 0, 0, editorAudioLatency)).ContinueWith(o => {
                    if (!songPlaybackCancellationTokenSource.IsCancellationRequested) {
                        songPlayer?.Play();
                    }
                });
            }

            // play song
            //songPlayer.Play();
        }
        internal void PauseSong() {
            songPlaybackCancellationTokenSource.Cancel();
            if (!songIsPlaying) {
                return;
            }
            songIsPlaying = false;
            imgPlayerButton.Source = Helper.BitmapGenerator("playButton.png");

            // stop note scaning
            noteScanner.Stop();
            beatScanner.Stop();

            // re-enable actions that were disabled
            txtSongBPM.IsEnabled = true;
            btnChangeBPM.IsEnabled = true;
            btnChangeDifficulty0.IsEnabled = true;
            btnChangeDifficulty1.IsEnabled = true;
            btnChangeDifficulty2.IsEnabled = true;
            scrollEditor.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            borderNavWaveform.IsEnabled = true;
            sliderSongTempo.IsEnabled = true;
            songPreviewController?.EnablePreviewButton();

            // reset scroll animation
            songPlayAnim.BeginTime = null;
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, null);

            // show editor
            gridController.SetPreviewNoteVisibility(Visibility.Visible);

            //Trace.WriteLine($"Slider is late by {Math.Round(songStream.CurrentTime.TotalMilliseconds - sliderSongProgress.Value, 2)}ms");
            songPlayer?.Pause();
            //if (noteScanner.playedLateNote) {
            //    drummer.InitAudioOut();
            //    noteScanner.playedLateNote = false;
            //}
            //bool isPanned = userSettings.GetBoolForKey(Const.UserSettings.PanDrumSounds);
            //InitDrummer(userSettings.GetValueForKey(Const.UserSettings.DrumSampleFile), isPanned);
        }

        internal void AnimateDrum(int num) {
            // this feature doesn't work properly for now
            // (drum sizes break on window resize after the animation is performed)
            return;

            /*
            if (!Helper.DoubleRangeCheck(num, 0, 3)) {
                return;
            }
            var duration = new Duration(new TimeSpan(0, 0, 0, 0, Editor.DrumHitDuration));

            var heightAnim = new DoubleAnimation();
            heightAnim.From = DrumRow.ActualHeight * Editor.DrumHitScaleFactor;
            heightAnim.To = DrumRow.ActualHeight;
            heightAnim.Duration = duration;
            Storyboard.SetTargetProperty(heightAnim, new PropertyPath("(Image.Height)"));
            Storyboard.SetTargetName(heightAnim, $"AnimatedDrum{num}");

            var widthAnim = new DoubleAnimation();
            widthAnim.From = DrumCol.ActualWidth * Editor.DrumHitScaleFactor;
            widthAnim.To = DrumCol.ActualWidth;
            widthAnim.Duration = duration;
            Storyboard.SetTargetProperty(widthAnim, new PropertyPath("(Image.Width)"));
            Storyboard.SetTargetName(widthAnim, $"AnimatedDrum{num}");

            var heightStackAnim = new DoubleAnimation();
            heightStackAnim.From = DrumRow.ActualHeight * (1 - Editor.DrumHitScaleFactor) / 2;
            heightStackAnim.To = 0;
            heightStackAnim.Duration = duration;
            Storyboard.SetTargetProperty(heightStackAnim, new PropertyPath("(StackPanel.Height)"));
            Storyboard.SetTargetName(heightStackAnim, $"AnimatedDrumStack{num}");

            var st = new Storyboard();
            st.Children.Add(heightAnim);
            st.Children.Add(widthAnim);
            st.Children.Add(heightStackAnim);
            st.Begin(this, true);
            */
        }
        internal void AnimateNote(Note n) {
            var duration = new Duration(new TimeSpan(0, 0, 0, 0, Editor.NoteHitDuration));

            var opacityAnim = new DoubleAnimation();
            opacityAnim.From = 0;
            opacityAnim.To = 1;
            opacityAnim.Duration = duration;
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("(Image.Opacity)"));
            Storyboard.SetTargetName(opacityAnim, Helper.NameGenerator(n));

            var st = new Storyboard();
            st.Children.Add(opacityAnim);
            st.Begin(this, true);


            //AnimateDrum(n.col);
        }

        // drawing functions for the editor grid
        internal void DrawEditorGrid(bool redrawWaveform = true) {
            gridController.DrawGrid(redrawWaveform);
        }

        // helper functions
        private void InitComboEnvironment() {
            foreach (var name in BeatmapDefaults.EnvironmentNames) {
                //if (name == "DefaultEnvironment") {
                //    comboEnvironment.Items.Add(Constants.BeatmapDefaults.DefaultEnvironmentAlias);
                //} else {
                comboEnvironment.Items.Add(name);
                //}
            }
        }
        private void InitNavMouseoverLine() {
            // already initialised in the XAML, for the most part
            lineSongMouseover.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.NavPreviewLine.Colour);
            lineSongMouseover.StrokeThickness = Editor.NavPreviewLine.Thickness;
        }
        public void RestartMetronome() {
            var oldMetronome = metronome;
            InitMetronome();
            oldMetronome?.Dispose();
        }
        private void InitMetronome() {
            var device = playbackDevice;
            if (device != null) {
                try {
                    metronome = new ParallelAudioPlayer(
                        device,
                        Audio.MetronomeFilename,
                        Audio.MetronomeStreams,
                        Audio.WASAPILatencyTarget,
                        checkMetronome.IsChecked == true,
                        false,
                        float.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultNoteVolume))
                    );
                } catch (FileNotFoundException ex) {
                    Trace.TraceError(ex.Message);
                    MessageBox.Show(this, $"{ex.Message}. Make sure Edda.exe is next to Resources folder and you're starting Edda from the directory where Edda.exe is stored.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
                beatScanner?.SetAudioPlayer(metronome);
            } else {
                metronome = null;
                beatScanner?.SetAudioPlayer(null);
            }
        }
        public void RestartDrummer() {
            var oldDrummer = drummer;
            InitDrummer();
            oldDrummer?.Dispose();
        }
        private void InitDrummer() {
            var device = playbackDevice;
            if (device != null) {
                try {
                    drummer = new ParallelAudioPlayer(
                        device,
                        userSettings.GetValueForKey(UserSettingsKey.DrumSampleFile),
                        Audio.NotePlaybackStreams,
                        Audio.WASAPILatencyTarget,
                        userSettings.GetBoolForKey(UserSettingsKey.PanDrumSounds),
                        float.Parse(userSettings.GetValueForKey(Const.UserSettingsKey.DefaultNoteVolume))
                    );
                } catch (FileNotFoundException ex) {
                    Trace.TraceError(ex.Message);
                    MessageBox.Show(this, $"{ex.Message}. Make sure Edda.exe is next to Resources folder and you're starting Edda from the directory where Edda.exe is stored.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
                drummer.ChangeVolume(sliderDrumVol.Value);
                noteScanner?.SetAudioPlayer(drummer);
            } else {
                drummer = null;
                noteScanner?.SetAudioPlayer(null);
            }
        }
        private int UpdateMedalDistance(int medal, string strDist) {
            if (strDist.Trim() == "") {
                mapEditor.SetMedalDistance((RagnarockScoreMedals)medal, 0, RagnarockMapDifficulties.Current);
                return 0;
            }
            int prevDist = mapEditor.GetMedalDistance((RagnarockScoreMedals)medal, RagnarockMapDifficulties.Current);
            int dist;
            if (int.TryParse(strDist, out dist) && dist >= 0) {
                mapEditor.SetMedalDistance((RagnarockScoreMedals)medal, dist, RagnarockMapDifficulties.Current);
            } else {
                MessageBox.Show(this, $"The distance must be a non-negative integer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                dist = prevDist;
            }
            return dist;
        }

        // return value is whether or not to cancel the operation
        private bool PromptBeatmapSave() {
            if (!mapIsLoaded) {
                return true;
            }
            MessageBoxResult? res = null;
            if (mapEditor.saveIsNeeded && (res = MessageBox.Show(this, "There are some unsaved changes in the currently opened map. Do you want to save them?", "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning)) == MessageBoxResult.Yes) {
                BackupAndSaveBeatmap();
            }
            return !(res.HasValue && res.Value == MessageBoxResult.Cancel);
        }

        internal void SetDiscordRPC(bool enable) {
            if (enable) {
                discordClient.Enable();
                RefreshDiscordPresence();
            } else {
                discordClient.Disable();
            }
        }

        internal void RefreshDiscordPresence() {
            if (mapEditor != null && gridController != null) {
                discordClient.SetPresence((string)mapEditor.GetMapValue("_songName"), gridController.currentMapDifficultyNotes?.Count() ?? 0);
            }
        }

        internal void RefreshBPMChanges() {
            var win = Helper.GetFirstWindow<ChangeBPMWindow>();
            if (win != null) {
                ((ChangeBPMWindow)win).RefreshBPMChanges();
            }
        }
        private void ShowUniqueWindow<T>(Func<T> windowMaker) where T : Window {
            var win = Helper.GetFirstWindow<T>();

            if (win == null) {
                win = windowMaker();
                win.Topmost = true;
                win.Owner = this;
                win.Show();
            } else {
                win.Focus();
            }
        }

        public void SetMapStats(MapStats stats) {
            SetNotesStats(stats);
            SetColumnStats(stats);
            SetNPSStats(stats);
            UpdateDifficultyPrediction();
        }

        public void UpdateDifficultyPrediction() {
            var supportedFeatures = difficultyPredictor.GetSupportedFeatures();
            if (showDifficultyPrediction && mapEditor.currentMapDifficulty != null && supportedFeatures.HasFlag(IDifficultyPredictor.Features.RealTime)) {
                var difficulty = difficultyPredictor.PredictDifficulty(mapEditor, mapEditor.currentDifficultyIndex);
                if (difficulty.HasValue) {
                    difficultyPrediction.Foreground = new SolidColorBrush(DifficultyPrediction.Colour);
                    var showPreciseValue = userSettings.GetBoolForKey(UserSettingsKey.DifficultyPredictorShowPrecise) && supportedFeatures.HasFlag(IDifficultyPredictor.Features.PreciseFloat);
                    var predictionDisplay = Math.Round(difficulty.Value, showPreciseValue ? 2 : 0);
                    difficultyPrediction.Content = $"Difficulty: {predictionDisplay.ToString(showPreciseValue ? "#0.00" : null)}";
                } else if (!supportedFeatures.HasFlag(IDifficultyPredictor.Features.AlwaysPredict)) {
                    difficultyPrediction.Foreground = new SolidColorBrush(DifficultyPrediction.WarningColour);
                    difficultyPrediction.Content = "Difficulty: ???";
                } else {
                    difficultyPrediction.Foreground = new SolidColorBrush(DifficultyPrediction.Colour);
                    difficultyPrediction.Content = "Difficulty: 0";
                }
            }
        }

        private void SetNotesStats(MapStats stats) {
            notesStatsAll.Text = stats.allNotes.ToString();
            notesStatsSelected.Text = stats.selectedNotes.ToString();
            notesStatsSingle.Text = stats.singleNotes.ToString();
            notesStatsDouble.Text = stats.doubleNotes.ToString();
            var triplePlusNotes = stats.tripleNotes + stats.quadrupleNotes;
            notesStatsTriplePlus.Text = triplePlusNotes.ToString();
            var triplePlusFontWeight = triplePlusNotes > 0 ? Editor.Stats.WarningFontWeight : Editor.Stats.FontWeight;
            notesStatsTriplePlusLabel.FontWeight = triplePlusFontWeight;
            notesStatsTriplePlus.FontWeight = triplePlusFontWeight;
            var triplePlusColor = new SolidColorBrush(
                triplePlusNotes > 0 ? Editor.Stats.WarningColour : Editor.Stats.Colour
            );
            notesStatsTriplePlusLabel.Foreground = triplePlusColor;
            notesStatsTriplePlus.Foreground = triplePlusColor;
        }

        private void SetColumnStats(MapStats stats) {
            // values
            columnStatsValue1.Text = stats.columnCounts[0].ToString();
            columnStatsValue2.Text = stats.columnCounts[1].ToString();
            columnStatsValue3.Text = stats.columnCounts[2].ToString();
            columnStatsValue4.Text = stats.columnCounts[3].ToString();
            // percentages
            columnStatsPercentage1.Text = $"{stats.columnPercentages[0]}%";
            columnStatsPercentage2.Text = $"{stats.columnPercentages[1]}%";
            columnStatsPercentage3.Text = $"{stats.columnPercentages[2]}%";
            columnStatsPercentage4.Text = $"{stats.columnPercentages[3]}%";
        }

        private void SetNPSStats(MapStats stats) {
            npsStatsSong.Text = stats.npsSong.ToString();
            npsStatsMapped.Text = stats.npsMapped.ToString();
            npsStats16Beat.Text = stats.nps16Beat.ToString();
            npsStats8Beat.Text = stats.nps8Beat.ToString();
            npsStats4Beat.Text = stats.nps4Beat.ToString();
        }
    }
}
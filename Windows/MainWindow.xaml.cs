using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;
using System.Reactive.Linq;
using System.Threading;
using System.IO.Compression;
using Path = System.IO.Path;
using SoundTouch.Net.NAudioSupport;
using Edda.Const;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
/// 

namespace Edda
{
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
        public double songTotalTimeInSeconds {
            get {
                return songStream.TotalTime.TotalSeconds;
            }
        }

        // STATE VARIABLES
        public RagnarockMap beatMap;
        public EditorUI editorUI;
        System.Timers.Timer autosaveTimer;
        UserSettings userSettings;
        bool shiftKeyDown;
        bool ctrlKeyDown;
        bool returnToStartMenuOnClose = false;

        // store info about the currently selected difficulty
        public MapEditor mapEditor;
        internal List<Note> editorClipboard = new();

        DoubleAnimation songPlayAnim;            // used for animating scroll when playing a song
        double prevScrollPercent = 0;       // percentage of scroll progress before the scroll viewport was changed

        // Editor variables
        Point editorDragSelectStart;
        internal bool navMouseDown = false;
        bool editorMouseDown = false;
        double editorDrawRangeLower = 0;
        double editorDrawRangeHigher = 0;

        // -- audio playback
        CancellationTokenSource songPlaybackCancellationTokenSource;
        int editorAudioLatency; // ms
        SampleChannel songChannel;
        public VorbisWaveReader songStream;
        SoundTouchWaveStream songTempoStream;
        public WasapiOut songPlayer;
        internal ParallelAudioPlayer drummer;
        ParallelAudioPlayer metronome;
        NoteScanner noteScanner;
        BeatScanner beatScanner;

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
            //imgSaved.Opacity = 0;
            imgWaveformVertical.Opacity = Editor.NavWaveformOpacity;
            imgWaveformVertical.Stretch = Stretch.Fill;
            lineSongMouseover.Opacity = 0;
            DisableUI();

            autosaveTimer = new System.Timers.Timer(1000 * Editor.AutosaveInterval);
            autosaveTimer.Enabled = false;
            autosaveTimer.Elapsed += (source, e) => {
                try {
                    SaveBeatmap();
                } catch {
                    Trace.WriteLine("INFO: Unable to autosave beatmap");
                }
            };
            

            InitSettings();
            LoadSettingsFile();

            metronome = new ParallelAudioPlayer(
                Audio.MetronomeFilename, 
                Audio.MetronomeStreams, 
                Audio.WASAPILatencyTarget, 
                checkMetronome.IsChecked == true, 
                false,
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume))
            );

            // load editor UI
            editorUI = new EditorUI(this, null, EditorGrid, scrollEditor, DrumCol, DrumRow, borderNavWaveform, colWaveformVertical, imgWaveformVertical, EditorMarginGrid, canvasNavInputBox, canvasBookmarks, canvasBookmarkLabels, lineSongMouseover);

            // load editor preview note
            InitNavMouseoverLine();

            // init environment combobox
            InitComboEnvironment();
            songPlaybackCancellationTokenSource = new();
            //debounce grid redrawing on resize
            Observable
            .FromEventPattern<SizeChangedEventArgs>(scrollEditor, nameof(SizeChanged))
            .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(eventPattern => 
                AppMainWindow.Dispatcher.Invoke(() =>
                    ScrollEditor_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                )
            );

            Observable
            .FromEventPattern<SizeChangedEventArgs>(borderNavWaveform, nameof(SizeChanged))
            .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(eventPattern =>
                AppMainWindow.Dispatcher.Invoke(() =>
                    BorderNavWaveform_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                )
            );
        }

        // UI bindings
        private void AppMainWindow_Loaded(object sender, RoutedEventArgs e) {
            // disable hardware acceleration - for debugging
            //System.Windows.Interop.HwndSource hwndSource = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
            //System.Windows.Interop.HwndTarget hwndTarget = hwndSource.CompositionTarget;
            //hwndTarget.RenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        private void AppMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (PromptBeatmapSave()) {
                if (returnToStartMenuOnClose) {
                    new StartWindow().Show();
                }
                discordClient.SetPresence();
            } else {
                returnToStartMenuOnClose = false;
                e.Cancel = true;
            }
        }
        private void AppMainWindow_Closed(object sender, EventArgs e) {
            Trace.WriteLine("INFO: Closing main window...");
            songPlayer?.Stop();
            songPlayer?.Dispose();
            songStream?.Dispose();
            noteScanner?.Stop();
            beatScanner?.Stop();
            drummer?.Dispose();
            metronome?.Dispose();
            Trace.WriteLine("INFO: Audio resources disposed...");

            // TODO find other stuff to dispose so we don't cause a memory leak
            // Application.Current.Shutdown();
        }
        private void AppMainWindow_KeyDown(object sender, KeyEventArgs e) {

            /*=====================*
             |  GENERAL SHORTCUTS  |
             *=====================*/

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) {
                ctrlKeyDown = true;
            }
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift) {
                shiftKeyDown = true;
            }

            // ctrl shortcuts
            if (ctrlKeyDown) {
                // new map (Ctrl-N)
                if (e.Key == Key.N) {
                    CreateNewMap();
                }
                // open map (Ctrl-O)
                if (e.Key == Key.O) {
                    OpenMap();
                }
                // save map (Ctrl-S)
                if (e.Key == Key.S) {
                    BackupAndSaveBeatmap();
                }

                // toggle left dock (Ctrl-[)
                if (e.Key == Key.OemOpenBrackets) {
                    ToggleLeftDock();
                }

                // toggle right dock (Ctrl-])
                if (e.Key == Key.OemCloseBrackets) {
                    ToggleRightDock();
                }
            }

            /*====================*
             |  EDITOR SHORTCUTS  |
             *====================*/

            if (mapEditor?.currentMapDifficulty == null) {
                return;
            }

            // ctrl shortcuts
            if (ctrlKeyDown) {
                // select all (Ctrl-A)
                if (e.Key == Key.A && !songIsPlaying) {
                    mapEditor.SelectNewNotes(mapEditor.currentMapDifficulty.notes);
                }

                // copy (Ctrl-C)
                if (e.Key == Key.C && !songIsPlaying) {
                    mapEditor.CopySelection();
                }
                // cut (Ctrl-X)
                if (e.Key == Key.X && !songIsPlaying) {
                    mapEditor.CutSelection();
                }
                // paste (Ctrl-V)
                if (e.Key == Key.V && !songIsPlaying) {
                    editorUI.PasteClipboardWithOffset();
                }

                // undo (Ctrl-Z)
                if (e.Key == Key.Z && !songIsPlaying) {
                    mapEditor.Undo();
                }
                // redo (Ctrl-Y, Ctrl-Shift-Z)
                if (((e.Key == Key.Y) ||
                    (e.Key == Key.Z && shiftKeyDown)) && !songIsPlaying) {
                    mapEditor.Redo();
                }

                // mirror selected notes (Ctrl-M)
                if (e.Key == Key.M && !songIsPlaying) {
                    mapEditor.TransformSelection(NoteTransforms.Mirror());
                }

                // add bookmark (Ctrl-B)
                if (e.Key == Key.B && !songIsPlaying) {
                    editorUI.CreateBookmark();
                }

                // add timing change (Ctrl-T)
                if (e.Key == Key.T && !songIsPlaying) {
                    editorUI.CreateBPMChange(shiftKeyDown);
                }

                // toggle grid snap (Ctrl-G)
                if (e.Key == Key.G) {
                    checkGridSnap.IsChecked = !(checkGridSnap.IsChecked == true);
                    CheckGridSnap_Click(null, null);
                }
            }

            if ((e.Key == Key.D1 || e.Key == Key.D2 || e.Key == Key.D3 || e.Key == Key.D4) &&
                (songIsPlaying || editorUI.isMouseOnEditingGrid) &&
                    !(FocusManager.GetFocusedElement(this) is TextBox)){
                
                int col = e.Key - Key.D1;
                editorUI.CreateNote(col, !songIsPlaying);
                drummer.Play(col);
            }
            
            // delete selected notes
            if (e.Key == Key.Delete) {
                mapEditor.RemoveSelectedNotes();
            }
            // unselect all notes
            if (e.Key == Key.Escape) {
                mapEditor.UnselectAllNotes();
            }                                           

        }
        private void AppMainWindow_KeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) {
                ctrlKeyDown = false;
            }
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift) {
                shiftKeyDown = false;
            }
        }
        private void AppMainWindow_PreviewKeyDown(object sender, KeyEventArgs e) {

            // these need to be handled by tunnelling because some UIElements intercept these keys

            /*====================*
             |  EDITOR SHORTCUTS  |
             *====================*/

            if (mapEditor == null) {
                return;
            }

            var keyStr = e.Key.ToString();
            if (shiftKeyDown) {
                if (keyStr == "Up") {
                    mapEditor.TransformSelection(NoteTransforms.RowShift(BeatForRow(1)));
                    e.Handled = true;
                }
                if (keyStr == "Down") {
                    mapEditor.TransformSelection(NoteTransforms.RowShift(BeatForRow(-1)));
                    e.Handled = true;
                }
            }
            if (ctrlKeyDown) {
                if (keyStr == "Up") {
                    mapEditor.TransformSelection(NoteTransforms.RowShift(BeatForRow(editorUI.gridDivision)));
                    e.Handled = true;
                }
                if (keyStr == "Down") {
                    mapEditor.TransformSelection(NoteTransforms.RowShift(BeatForRow(-editorUI.gridDivision)));
                    e.Handled = true;
                }
            }
            if (shiftKeyDown || ctrlKeyDown) {
                if (keyStr == "Left") {
                    mapEditor.TransformSelection(NoteTransforms.ColShift(-1));
                    e.Handled = true;
                }
                if (keyStr == "Right") {
                    mapEditor.TransformSelection(NoteTransforms.ColShift(1));
                    e.Handled = true;
                }
            }

            // play/pause song
            if (keyStr == "Space" && !(FocusManager.GetFocusedElement(this) is TextBox)) {
                if (btnSongPlayer.IsEnabled) {
                    BtnSongPlayer_Click(null, null);
                }
                e.Handled = true;
            }
        }
       
        // map file I/O functions
        private void CreateNewMap() {
            // check if map already open
            if (beatMap != null) {
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
        private void OpenMap() {
            // check if map already open
            if (beatMap != null) {
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
            var oldBeatMap = beatMap;
            try {

                InitOpenMap(openMapFolder);


            } catch (Exception ex) {
                MessageBox.Show($"An error occured while opening the map:\n{ex.Message}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // reload previous beatmap
                if (oldBeatMap != null) {
                    beatMap = oldBeatMap;
                    LoadSong();
                    LoadCoverImage();
                    InitUI();
                }
                return;
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

            string songArtist = Helper.ValidFilenameFrom((string)beatMap.GetValue("_songAuthorName"));
            string songName = Helper.ValidFilenameFrom((string)beatMap.GetValue("_songName"));
            string baseFolder = beatMap.GetPath();
            string zipName = Helper.ValidMapFolderNameFrom(songArtist + songName);
            // make the temp dir for zip
            string zipFolder = Path.Combine(baseFolder, zipName + "_tempDir");
            string zipPath = Path.Combine(d.FileName, zipName + ".zip");

            try {
                if (File.Exists(zipPath)) {
                    File.Delete(zipPath);
                }

                var copyFiles = new List<string>();
                foreach (var file in Directory.GetFiles(baseFolder)) {
                    copyFiles.Add(file);
                }

                if (Directory.Exists(zipFolder)) {
                    Directory.Delete(zipFolder, true);
                }
                Directory.CreateDirectory(zipFolder);

                // need to use cmd to copy files; .NET Filesystem API throws an exception because "the file is being used"
                //Helper.CmdCopyFiles(copyFiles, zipFolder);
                foreach (var file in copyFiles) {
                    File.Copy(file, System.IO.Path.Combine(zipFolder, System.IO.Path.GetFileName(file)));
                }
                ZipFile.CreateFromDirectory(zipFolder, zipPath);

            } catch (Exception) {
                MessageBox.Show($"An error occured while creating the zip file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally {
                if (Directory.Exists(zipFolder)) {
                    Directory.Delete(zipFolder, true);
                }
            }
        }

        // initialisation
        internal void InitNewMap(string newMapFolder) {
            if (beatMap != null) {
                mapEditor.ClearSelectedDifficulty();
                ClearCoverImage();
            }
            
            // select an audio file
            string file = SelectSongDialog();
            if (file == null) {
                return;
            }

            beatMap = new RagnarockMap(newMapFolder, true);

            // load audio file
            LoadSong(file);

            recentMaps.AddRecentlyOpened((string)beatMap.GetValue("_songName"), newMapFolder);
            recentMaps.Write();
        }

        internal void InitOpenMap(string mapFolder) {
            beatMap = new RagnarockMap(mapFolder, false);
            LoadSong(); // song file
            LoadCoverImage();
            InitUI(); // cover image file

            recentMaps.AddRecentlyOpened((string)beatMap.GetValue("_songName"), mapFolder);
            recentMaps.Write();

            // bandaid fix to prevent WPF from committing unnecessarily large amounts of memory
            new Thread(new ThreadStart(delegate {
                Thread.Sleep(500);
                this.Dispatcher.Invoke(() => DrawEditorGrid());
            })).Start();

            discordClient.SetPresence((string)beatMap?.GetValue("_songName"), mapEditor?.currentMapDifficulty?.notes?.Count ?? 0);
        }
        // UI initialisation
        private void InitUI() {
            // reset variables
            prevScrollPercent = 0;

            lineSongProgress.Y1 = borderNavWaveform.ActualHeight;
            lineSongProgress.Y2 = borderNavWaveform.ActualHeight;

            sliderSongVol.Value = float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultSongVolume));
            sliderDrumVol.Value = float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume));

            // map settings
            txtSongName.Text   = (string)beatMap.GetValue("_songName");
            txtArtistName.Text = (string)beatMap.GetValue("_songAuthorName");
            txtMapperName.Text = (string)beatMap.GetValue("_levelAuthorName");
            txtSongBPM.Text    = (string)beatMap.GetValue("_beatsPerMinute");
            txtSongOffset.Text = (string)beatMap.GetValue("_songTimeOffset");
            checkExplicitContent.IsChecked = (string)beatMap.GetValue("_explicit") == "true";

            comboEnvironment.SelectedIndex = BeatmapDefaults.EnvironmentNames.IndexOf((string)beatMap.GetValue("_environmentName"));
            MenuItemSnapToGrid.IsChecked = (checkGridSnap.IsChecked == true);
            mapEditor = new MapEditor(this);
            mapEditor.globalBPM = Helper.DoubleParseInvariant((string)beatMap.GetValue("_beatsPerMinute"));
            editorUI.mapEditor = mapEditor;
            editorUI.showWaveform = (checkWaveform.IsChecked == true);
            songTempoStream.Tempo = sliderSongTempo.Value;
            var songPath = beatMap.PathOf((string)beatMap.GetValue("_songFilename"));
            editorUI.InitWaveforms(songPath);

            // enable UI parts
            EnableUI();

            // init difficulty-specific UI 
            SwitchDifficultyMap(0);
            sliderSongTempo.Value = 1.0;
            UpdateDifficultyButtons();
            DrawEditorGrid();
            scrollEditor.ScrollToBottom();
            editorUI.DrawNavWaveform();
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
            checkMetronome.IsEnabled = true;
            checkGridSnap.IsEnabled = true;
            txtDifficultyNumber.IsEnabled = true;
            txtNoteSpeed.IsEnabled = true;
            txtDistMedal0.IsEnabled = true;
            txtDistMedal1.IsEnabled = true;
            txtDistMedal2.IsEnabled = true;
            txtGridDivision.IsEnabled = true;
            txtGridOffset.IsEnabled = true;
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
            checkMetronome.IsEnabled = false;
            checkGridSnap.IsEnabled = false;
            txtDifficultyNumber.IsEnabled = false;
            txtNoteSpeed.IsEnabled = false;
            txtDistMedal0.IsEnabled = false;
            txtDistMedal1.IsEnabled = false;
            txtDistMedal2.IsEnabled = false;
            txtGridDivision.IsEnabled = false;
            txtGridOffset.IsEnabled = false;
            txtGridSpacing.IsEnabled = false;
            checkWaveform.IsEnabled = false;
            btnDeleteDifficulty.IsEnabled = false;
            btnSongPlayer.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;
            scrollEditor.IsEnabled = false;
            borderNavWaveform.IsEnabled = false;
        }
        private void SaveBeatmap() {
            if (beatMap == null) {
                return;
            }
            mapEditor.SaveMap();
            beatMap.SaveToFile();
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
            string backupFolder = beatMap.PathOf(Program.BackupPath);
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
                string recentSavePath = beatMap.PathOf($"{diffName}.dat");
                if (File.Exists(recentSavePath)) {
                    File.Copy(recentSavePath, System.IO.Path.Combine(newBackupPath, $"{diffName}.dat"));
                }
            }

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
        }
        internal void LoadSettingsFile() {
            userSettings = new UserSettings(Program.SettingsFile);

            if (!int.TryParse(userSettings.GetValueForKey(Const.UserSettings.EditorAudioLatency), out editorAudioLatency)) {
                userSettings.SetValueForKey(Const.UserSettings.EditorAudioLatency, DefaultUserSettings.AudioLatency);
                editorAudioLatency = DefaultUserSettings.AudioLatency;
            }

            if (userSettings.GetValueForKey(Const.UserSettings.PanDrumSounds) == null) {
                userSettings.SetValueForKey(Const.UserSettings.PanDrumSounds, DefaultUserSettings.PanDrumSounds);
            }
            bool isPanned = userSettings.GetBoolForKey(Const.UserSettings.PanDrumSounds);

            try {
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume));
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume);
            }

            try {
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultSongVolume));
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.DefaultSongVolume, DefaultUserSettings.DefaultSongVolume);
            }

            try {
                InitDrummer(userSettings.GetValueForKey(Const.UserSettings.DrumSampleFile), isPanned);
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.DrumSampleFile, DefaultUserSettings.DrumSampleFile);
                InitDrummer(DefaultUserSettings.DrumSampleFile, isPanned);
            }

            if (userSettings.GetValueForKey(Const.UserSettings.DefaultSongVolume) == null) {
                userSettings.SetValueForKey(Const.UserSettings.DefaultSongVolume, DefaultUserSettings.DefaultSongVolume);
            }

            if (userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume) == null) {
                userSettings.SetValueForKey(Const.UserSettings.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume);
            }

            if (userSettings.GetValueForKey(Const.UserSettings.EnableAutosave) == null) {
                userSettings.SetValueForKey(Const.UserSettings.EnableAutosave, DefaultUserSettings.EnableAutosave);
            }
            autosaveTimer.Enabled = userSettings.GetBoolForKey(Const.UserSettings.EnableAutosave);

            if (userSettings.GetValueForKey(Const.UserSettings.CheckForUpdates) == null) {
                userSettings.SetValueForKey(Const.UserSettings.CheckForUpdates, DefaultUserSettings.CheckForUpdates);
            }

            try {
                var index = int.Parse(userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationIndex));
                // game install directory chosen
                var gameInstallPath = userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationPath);
                if (index == 1 && !Directory.Exists(gameInstallPath)) {
                    throw new Exception();
                }
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.MapSaveLocationIndex, DefaultUserSettings.MapSaveLocationIndex);
                userSettings.SetValueForKey(Const.UserSettings.MapSaveLocationPath, DefaultUserSettings.MapSaveLocationPath);
            }

            try {
                int.Parse(userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationIndex));
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.MapSaveLocationIndex, DefaultUserSettings.MapSaveLocationIndex);
            }

            userSettings.Write();
        }

        // manage cover image
        private void SelectNewCoverImage() {
            var d = new Microsoft.Win32.OpenFileDialog() { Filter = "JPEG Files|*.jpg;*.jpeg" };
            d.Title = "Select a song to map";

            if (d.ShowDialog() != true) {
                return;
            }

            imgCover.Source = null;

            string prevPath = beatMap.PathOf((string)beatMap.GetValue("_coverImageFilename"));
            string newFile = System.IO.Path.GetFileName(d.FileName);
            string newPath = beatMap.PathOf(newFile);

            // load new cover image, if necessary
            if (prevPath != newPath) {
                // remove the previous cover image
                if (File.Exists(prevPath)) {
                    File.Delete(prevPath);
                }
                // copy over the image file if it's not in the same folder already
                if (!d.FileName.StartsWith(beatMap.GetPath())) {
                    // delete any existing files in the map folder with conflicting names
                    if (File.Exists(newPath)) {
                        File.Delete(newPath);
                    }
                    // copy image file over
                    File.Copy(d.FileName, newPath);
                }
                
                beatMap.SetValue("_coverImageFilename", newFile);
                SaveBeatmap();
            }
            LoadCoverImage();
        }
        private void LoadCoverImage() {
            var fileName = (string)beatMap.GetValue("_coverImageFilename");
            if (fileName == "") {
                ClearCoverImage();
            } else {
                BitmapImage b = Helper.BitmapGenerator(new Uri(beatMap.PathOf(fileName)));
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

        // manage difficulties
        public void UpdateDifficultyButtons() {
            var numDiff = beatMap.numDifficulties;

            // update visible state
            for (var i = 0; i < numDiff; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Visible;
            }
            for (var i = numDiff; i < 3; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Hidden;
            }

            // update enabled state
            foreach (Button b in DifficultyChangePanel.Children) {
                if (b.Name == ((Button)DifficultyChangePanel.Children[mapEditor.currentDifficultyIndex]).Name) {
                    b.IsEnabled = false;
                } else {
                    b.IsEnabled = true;
                }
            }

            // update button labels
            var difficultyLabels = new List<Label>() { lblDifficultyRank1, lblDifficultyRank2, lblDifficultyRank3 };
            for (int i = 0; i < 3; i++) {
                try {
                    difficultyLabels[i].Content = beatMap.GetValueForMap(i, "_difficultyRank");
                } catch {
                    Trace.WriteLine($"INFO: difficulty index {i} not found");
                    difficultyLabels[i].Content = "";
                }
            }

            // update states of add/delete buttons
            btnDeleteDifficulty.IsEnabled = (beatMap.numDifficulties > 1);
            btnAddDifficulty.IsEnabled = (beatMap.numDifficulties < 3);
        }
        private void SwitchDifficultyMap(int indx) {
            PauseSong();

            mapEditor.SelectDifficulty(indx);

            noteScanner = new NoteScanner(this, drummer);
            beatScanner = new BeatScanner(metronome);

            txtDifficultyNumber.Text = (string)beatMap.GetValueForMap(indx, "_difficultyRank");
            txtNoteSpeed.Text = (string)beatMap.GetValueForMap(indx, "_noteJumpMovementSpeed");

            int dist0 = beatMap.GetMedalDistanceForMap(indx, 0);
            int dist1 = beatMap.GetMedalDistanceForMap(indx, 1);
            int dist2 = beatMap.GetMedalDistanceForMap(indx, 2);
            txtDistMedal0.Text = (dist0 == 0) ? "Auto" : dist0.ToString();
            txtDistMedal1.Text = (dist1 == 0) ? "Auto" : dist1.ToString();
            txtDistMedal2.Text = (dist2 == 0) ? "Auto" : dist2.ToString();

            txtGridOffset.Text = (string)beatMap.GetCustomValueForMap(indx, "_editorOffset");
            txtGridSpacing.Text = (string)beatMap.GetCustomValueForMap(indx, "_editorGridSpacing");
            txtGridDivision.Text = (string)beatMap.GetCustomValueForMap(indx, "_editorGridDivision");
            
            // set internal values
            editorUI.gridDivision = int.Parse(txtGridDivision.Text);
            editorUI.gridSpacing = Helper.DoubleParseInvariant(txtGridSpacing.Text);
            editorUI.gridOffset = Helper.DoubleParseInvariant(txtGridOffset.Text);

            UpdateDifficultyButtons();
            editorUI.DrawNavBookmarks();
            DrawEditorGrid();
        }
        // song/note playback
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
        private bool LoadSong(string file) {
            VorbisWaveReader vorbisStream;
            try {
                vorbisStream = new VorbisWaveReader(file);
            } catch (Exception) {
                MessageBox.Show("The .ogg file is corrupted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (vorbisStream.TotalTime.TotalHours >= 1) {
                MessageBox.Show("Songs over 1 hour in duration are not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // check for same file
            var songFile = System.IO.Path.GetFileName(Helper.SanitiseSongFileName(file));
            var songFilePath = beatMap.PathOf(songFile);
            var prevSongFile = beatMap.PathOf((string)beatMap.GetValue("_songFilename"));

            if (file == prevSongFile) {
                return false;
            }

            // update beatmap data
            UnloadSong();
            beatMap.SetValue("_songApproximativeDuration", (int)vorbisStream.TotalTime.TotalSeconds + 1);
            beatMap.SetValue("_songFilename", songFile);
            vorbisStream.Dispose();

            // do file I/O
            if (File.Exists(prevSongFile)) {
                File.Delete(prevSongFile);
            }

            // can't copy over an existing file
            if (File.Exists(songFilePath)) {
                File.Delete(songFilePath);
            }
            File.Copy(file, songFilePath);

            LoadSong();
            
            // redraw waveforms
            if (editorUI.showWaveform) {
                editorUI.UndrawMainWaveform();
                editorUI.DrawMainWaveform();
            }
            editorUI.DrawNavWaveform();

            // reload map and editor
            InitUI();

            // save map to lock in the new song
            SaveBeatmap();

            return true;
        }
        private void LoadSong() {
            var songPath = beatMap.PathOf((string)beatMap.GetValue("_songFilename"));
            songStream = new VorbisWaveReader(songPath);
            songTempoStream = new SoundTouchWaveStream(songStream);
            songChannel = new SampleChannel(songTempoStream);
            songChannel.Volume = (float)sliderSongVol.Value;
            songPlayer = new WasapiOut(AudioClientShareMode.Shared, Audio.WASAPILatencyTarget);
            songPlayer.Init(songChannel);
            if ((int)beatMap.GetValue("_songApproximativeDuration") != (int)songStream.TotalTime.TotalSeconds + 1) {
               beatMap.SetValue("_songApproximativeDuration", (int)songStream.TotalTime.TotalSeconds + 1);
            }

            // subscribe to playbackstopped
            songPlayer.PlaybackStopped += (sender, args) => { PauseSong(); };

            // load UI
            sliderSongProgress.Minimum = 0;
            sliderSongProgress.Maximum = songStream.TotalTime.TotalSeconds * 1000;
            sliderSongProgress.Value = 0;
            txtSongDuration.Text = Helper.TimeFormat((int)songStream.TotalTime.TotalSeconds);
            txtSongFileName.Text = (string)beatMap.GetValue("_songFilename");

            if (editorUI != null) {
                editorUI.InitWaveforms(songPath);
            }
            //awd = new AudioVisualiser_Float32(new VorbisWaveReader(songPath));
        }
        private void UnloadSong() {
            if (songStream != null) {
                songStream.Dispose();
            }
            if (songPlayer != null) {
                songPlayer.Dispose();
            }
        }
        private void PlaySong() {
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
            txtGridOffset.IsEnabled = false;
            btnChangeDifficulty0.IsEnabled = false;
            btnChangeDifficulty1.IsEnabled = false;
            btnChangeDifficulty2.IsEnabled = false;
            scrollEditor.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;
            borderNavWaveform.IsEnabled = false;
            sliderSongTempo.IsEnabled = false; 

            // hide editor
            editorUI.SetPreviewNoteVisibility(Visibility.Hidden);

            // animate for smooth scrolling 
            var remainingTimeSpan = songStream.TotalTime - songStream.CurrentTime;

            // note: the DoubleAnimation induces a desync of around 0.1 seconds
            songPlayAnim = new DoubleAnimation();
            songPlayAnim.From = sliderSongProgress.Value;
            songPlayAnim.To = sliderSongProgress.Maximum;
            songPlayAnim.Duration = new Duration(remainingTimeSpan/songTempoStream.Tempo);
            //Timeline.SetDesiredFrameRate(songPlayAnim, animationFramerate);
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, songPlayAnim);

            noteScanner.Start((int)(sliderSongProgress.Value - editorAudioLatency), new List<Note>(mapEditor.currentMapDifficulty.notes), mapEditor.globalBPM);
            beatScanner.Start((int)(sliderSongProgress.Value - editorAudioLatency), editorUI.GetBeats(), mapEditor.globalBPM);

            if (songTempoStream.CurrentTime > new TimeSpan(0, 0, 0, 0, editorAudioLatency)) {
                songTempoStream.CurrentTime = songTempoStream.CurrentTime - new TimeSpan(0, 0, 0, 0, editorAudioLatency);
                songPlayer.Play();
            } else {
                songPlaybackCancellationTokenSource.Dispose();
                songPlaybackCancellationTokenSource = new();
                Task.Delay(new TimeSpan(0, 0, 0, 0, editorAudioLatency)).ContinueWith(o => {
                    if (!songPlaybackCancellationTokenSource.IsCancellationRequested) {
                        songPlayer.Play();
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
            txtGridOffset.IsEnabled = true;
            UpdateDifficultyButtons();
            scrollEditor.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            borderNavWaveform.IsEnabled = true;
            sliderSongTempo.IsEnabled = true;

            // reset scroll animation
            songPlayAnim.BeginTime = null;
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, null);

            // show editor
            editorUI.SetPreviewNoteVisibility(Visibility.Visible);

            //Trace.WriteLine($"Slider is late by {Math.Round(songStream.CurrentTime.TotalMilliseconds - sliderSongProgress.Value, 2)}ms");
            songPlayer.Pause();
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
        private void CalculateDrawRange() {
            if (scrollEditor.ScrollableHeight == 0) {
                return;
            }
            // calculate new drawn ranges for pagination, if we need it...
            var scrollPos = scrollEditor.ScrollableHeight - scrollEditor.VerticalOffset;
            if (scrollPos <= editorDrawRangeLower || editorDrawRangeHigher <= scrollPos) {
                editorDrawRangeLower = Math.Max(scrollPos - (Editor.GridDrawRange * scrollEditor.ActualHeight), 0);
                editorDrawRangeHigher = Math.Min(scrollPos + ((1 + Editor.GridDrawRange) * scrollEditor.ActualHeight), EditorGrid.ActualHeight);
                //Trace.WriteLine($"draw range: {editorDrawRangeLower} - {editorDrawRangeHigher}");
                // redraw
                //drawEditorWaveform(editorDrawRangeLower, editorDrawRangeHigher, EditorGrid.Height - scrollEditor.ActualHeight);
            }
        }
        internal void DrawEditorGrid(bool redrawWaveform = true) {
            editorUI.DrawGrid(redrawWaveform);
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
        private void InitDrummer(string basePath, bool isPanned) {
            drummer = new ParallelAudioPlayer(
                basePath, 
                Audio.NotePlaybackStreams, 
                Audio.WASAPILatencyTarget,
                isPanned,
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume))
            );
            drummer.ChangeVolume(sliderDrumVol.Value);
            noteScanner?.SetAudioPlayer(drummer);
        }
        
        private double BeatForRow(double row) {
            double userOffsetBeat = mapEditor.globalBPM * editorUI.gridOffset / 60;
            return row / (double)editorUI.gridDivision + userOffsetBeat;
        }
        private void ResnapAllNotes(double newOffset) {
            var offsetDelta = newOffset - editorUI.gridOffset;
            var beatOffset = mapEditor.globalBPM / 60 * offsetDelta;
            for (int i = 0; i < mapEditor.currentMapDifficulty.notes.Count; i++) {
                Note n = new Note();
                n.beat = mapEditor.currentMapDifficulty.notes[i].beat + beatOffset;
                n.col = mapEditor.currentMapDifficulty.notes[i].col;
                mapEditor.currentMapDifficulty.notes[i] = n;
            }
            // invalidate selections
            mapEditor.UnselectAllNotes();
        }
        private int UpdateMedalDistance(int medal, string strDist) {
            if (strDist.Trim() == "") {
                beatMap.SetMedalDistanceForMap(mapEditor.currentDifficultyIndex, medal, 0);
                return 0;
            }
            int prevDist = (int)beatMap.GetMedalDistanceForMap(mapEditor.currentDifficultyIndex, medal);
            int dist;
            if (int.TryParse(strDist, out dist) && dist >= 0) {
                beatMap.SetMedalDistanceForMap(mapEditor.currentDifficultyIndex, medal, dist);
            } else {
                MessageBox.Show($"The distance must be a non-negative integer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                dist = prevDist;
            }
            return dist;
        }
        // return value: whether or not to cancel the operation
        private bool PromptBeatmapSave() {
            if (beatMap == null) {
                return true;
            }
            var res = MessageBox.Show("Save the currently opened map?", "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes) {
                BackupAndSaveBeatmap();
            }
            return !(res == MessageBoxResult.Cancel);
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
    }
}

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
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;
using System.Reactive.Linq;
using System.Threading;
using System.IO.Compression;
using Path = System.IO.Path;
using System.Windows.Data;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
/// 

namespace Edda {
    public partial class MainWindow : Window {

        // COMPUTED PROPERTIES
        double unitLength {
            get { return DrumCol.ActualWidth * editorGridSpacing; }
        }
        double unitLengthUnscaled {
            get { return DrumCol.ActualWidth; }
        }
        double unitSubLength {
            get { return DrumCol.ActualWidth / 3; }
        }
        double unitHeight {
            get { return DrumRow.ActualHeight; }
        }
        bool songIsPlaying {
            set { btnSongPlayer.Tag = (value == false) ? 0 : 1; }
            get { return btnSongPlayer.Tag != null && (int)btnSongPlayer.Tag == 1; }
        }
        public string mapName {
            get {
                return (string)beatMap?.GetValue("_songName");
            }
        }
        public int mapNoteCount {
            get {
                if (mapEditor == null || mapEditor.notes == null) {
                    return 0;
                }
                return mapEditor.notes.Count;
            }
        }
        
        // STATE VARIABLES

        public RagnarockMap beatMap;
        public double globalBPM;
        System.Timers.Timer autosaveTimer;
        UserSettings userSettings;
        List<double> gridlines = new();
        List<double> majorGridlines = new();
        bool shiftKeyDown;
        bool ctrlKeyDown;

        // store info about the currently selected difficulty
        public int currentDifficulty;
        MapEditor[] mapEditors = new MapEditor[3];
        MapEditor mapEditor;
        List<Note> editorClipboard = new();

        DoubleAnimation songPlayAnim;            // used for animating scroll when playing a song
        double prevScrollPercent = 0;       // percentage of scroll progress before the scroll viewport was changed

        // Discord RPC
        DiscordClient discordClient;

        // variables used in the map editor

        // -- for waveform drawing
        Image imgAudioWaveform = new();
        Line lineGridMouseover = new();
        Canvas currentlyDraggingMarker;
        bool isEditingMarker = false;
        double markerDragOffset = 0;
        Bookmark currentlyDraggingBookmark;
        BPMChange currentlyDraggingBPMChange;
        VorbisWaveformVisualiser audioWaveform;
        VorbisWaveformVisualiser navWaveform;
        bool editorShowWaveform {
            get { return checkWaveform.IsChecked == true; }
        }

        // -- for note placement
        Canvas EditorGridNoteCanvas = new();
        Image imgPreviewNote = new();
        int editorMouseGridCol;
        double editorMouseBeatUnsnapped;
        double editorMouseBeatSnapped;

        // -- for drag select
        Border editorDragSelectBorder = new();
        Point editorDragSelectStart;
        double editorSelBeatStart;
        int editorSelColStart;
        bool editorIsDragging = false;
        bool editorMouseDown = false;
        bool navMouseDown = false;

        // -- for grid drawing
        bool editorSnapToGrid = true;
        public int editorGridDivision;
        double editorGridSpacing;
        double editorGridOffset;
        double editorDrawRangeLower = 0;
        double editorDrawRangeHigher = 0;

        // -- audio playback
        CancellationTokenSource songPlaybackCancellationTokenSource;
        int editorAudioLatency; // ms
        SampleChannel songChannel;
        public VorbisWaveReader songStream;
        public WasapiOut songPlayer;
        ParallelAudioPlayer drummer;
        ParallelAudioPlayer metronome;
        NoteScanner noteScanner;
        BeatScanner beatScanner;

        public MainWindow() {

            InitializeComponent();

            // disable parts of UI, as no map is loaded
            imgSaved.Opacity = 0;
            imgWaveformVertical.Opacity = Const.Editor.NavWaveformOpacity;
            imgWaveformVertical.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(imgAudioWaveform, BitmapScalingMode.NearestNeighbor);
            lineSongMouseover.Opacity = 0;
            DisableUI();

            autosaveTimer = new System.Timers.Timer(1000 * Const.Editor.AutosaveInterval);
            autosaveTimer.Enabled = false;
            autosaveTimer.Elapsed += (source, e) => {
                try {
                    SaveBeatmap();
                } catch {
                    Trace.WriteLine("INFO: Unable to autosave beatmap");
                }
            };
            discordClient = new DiscordClient(this);

            InitSettings();
            LoadSettingsFile();

            metronome = new ParallelAudioPlayer(
                Const.Audio.MetronomeFilename, 
                Const.Audio.MetronomeStreams, 
                Const.Audio.WASAPILatencyTarget, 
                checkMetronome.IsChecked == true, 
                false,
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume))
            );

            // init border
            InitDragSelectBorder();

            // load editor preview note
            InitPreviewNote();
            InitGridMouseoverLine();
            InitNavMouseoverLine();
            InitEditorGridNoteCanvas();

            // init environment combobox
            InitComboEnvironment();
            songPlaybackCancellationTokenSource = new();
            //debounce grid redrawing on resize
            Observable
            .FromEventPattern<SizeChangedEventArgs>(scrollEditor, nameof(SizeChanged))
            .Throttle(TimeSpan.FromMilliseconds(Const.Editor.DrawDebounceInterval))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(eventPattern => 
                AppMainWindow.Dispatcher.Invoke(() =>
                    ScrollEditor_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                )
            );

            Observable
            .FromEventPattern<SizeChangedEventArgs>(borderNavWaveform, nameof(SizeChanged))
            .Throttle(TimeSpan.FromMilliseconds(Const.Editor.DrawDebounceInterval))
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
        private void AppMainWindow_ContentRendered(object sender, EventArgs e) {
            try {
                if (userSettings.GetValueForKey(Const.UserSettings.CheckForUpdates) == true.ToString()) {
                    //#if !DEBUG
                        Helper.CheckForUpdates();
                    //#endif
                }
            } catch {
                Trace.WriteLine("INFO: Could not check for updates.");
            }
        }
        private void AppMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (PromptBeatmapSave()) {
                AppMainWindow_Closed(this, null);
                Environment.Exit(0);
            } else {
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
            Environment.Exit(0);
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
                    if (btnNewMap.IsEnabled) {
                        BtnNewMap_Click(null, null);
                    }
                }
                // open map (Ctrl-O)
                if (e.Key == Key.O) {
                    if (btnOpenMap.IsEnabled) {
                        BtnOpenMap_Click(null, null);
                    }
                }
                // save map (Ctrl-S)
                if (e.Key == Key.S) {
                    if (btnSaveMap.IsEnabled) {
                        BtnSaveMap_Click(null, null);
                    }
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

            if (mapEditor == null) {
                return;
            }

            // ctrl shortcuts
            if (ctrlKeyDown) {
                // select all (Ctrl-A)
                if (e.Key == Key.A) {
                    mapEditor.SelectNewNotes(mapEditor.notes);
                }

                // copy (Ctrl-C)
                if (e.Key == Key.C) {
                    mapEditor.CopySelection();
                }
                // cut (Ctrl-X)
                if (e.Key == Key.X) {
                    mapEditor.CutSelection();
                }
                // paste (Ctrl-V)
                if (e.Key == Key.V) {
                    mapEditor.PasteClipboard(editorMouseBeatSnapped);
                }

                // undo (Ctrl-Z)
                if (e.Key == Key.Z) {
                    mapEditor.Undo();
                }
                // redo (Ctrl-Y, Ctrl-Shift-Z)
                if ((e.Key == Key.Y) ||
                    (e.Key == Key.Z && shiftKeyDown)) {
                    mapEditor.Redo();
                }

                // mirror selected notes (Ctrl-M)
                if (e.Key == Key.M) {
                    mapEditor.TransformSelection(NoteTransforms.Mirror());
                }

                // add bookmark (Ctrl-B)
                if (e.Key == Key.B && !songIsPlaying) {
                    double beat = BeatForPosition(scrollEditor.VerticalOffset + scrollEditor.ActualHeight - unitLengthUnscaled / 2, editorSnapToGrid);
                    if (imgPreviewNote.Opacity > 0) {
                        beat = editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
                    } else if (lineSongMouseover.Opacity > 0) {
                        beat = globalBPM * sliderSongProgress.Maximum / 60000 * (1 - lineSongMouseover.Y1/borderNavWaveform.ActualHeight);
                    }
                    mapEditor.AddBookmark(new Bookmark(beat, Const.Editor.NavBookmark.DefaultName));
                }

                // add timing change (Ctrl-T)
                if (e.Key == Key.T && !songIsPlaying) {
                    double beat = (shiftKeyDown) ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
                    BPMChange previous = new BPMChange(0, globalBPM, editorGridDivision);
                    foreach (var b in mapEditor.bpmChanges) {
                        if (b.globalBeat < beat) {
                            previous = b;
                        }
                    }
                    mapEditor.AddBPMChange(new BPMChange(beat, previous.BPM, previous.gridDivision));
                }

                // toggle grid snap (Ctrl-G)
                if (e.Key == Key.G) {
                    checkGridSnap.IsChecked = !(checkGridSnap.IsChecked == true);
                    CheckGridSnap_Click(null, null);
                }
            }

            if ((e.Key == Key.D1 || e.Key == Key.D2 || e.Key == Key.D3 || e.Key == Key.D4) &&
                (songIsPlaying || imgPreviewNote.Opacity > 0) &&
                    !(FocusManager.GetFocusedElement(this) is TextBox)){
                
                int col = e.Key - Key.D1;
                double mouseInput = editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
                double defaultInput = BeatForPosition(scrollEditor.VerticalOffset + scrollEditor.ActualHeight - unitLengthUnscaled / 2, editorSnapToGrid);
                Note n = new Note(songIsPlaying ? defaultInput : mouseInput, col);
                mapEditor.AddNotes(n);
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
                    mapEditor.TransformSelection(NoteTransforms.RowShift(BeatForRow(editorGridDivision)));
                    e.Handled = true;
                }
                if (keyStr == "Down") {
                    mapEditor.TransformSelection(NoteTransforms.RowShift(BeatForRow(-editorGridDivision)));
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
        private void BtnNewMap_Click(object sender, RoutedEventArgs e) {

            // check if map already open
            if (beatMap != null) {
                if (!PromptBeatmapSave()) {
                    return;
                }

                PauseSong();
            }

            // select folder for map
            var d2 = new CommonOpenFileDialog();
            d2.Title = "Select an empty folder to store your map";
            d2.IsFolderPicker = true;
            d2.InitialDirectory = RagnarockMapFolder();
            if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
                return;
            }

            // check folder name is appropriate
            var folderName = new FileInfo(d2.FileName).Name;
            if (!Regex.IsMatch(folderName, @"^[a-zA-Z]+$")) {
                MessageBox.Show("The folder name cannot contain spaces or non-alphabetic characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // check folder is empty
            if (Directory.GetFiles(d2.FileName).Length > 0) {
                if (MessageBoxResult.No == MessageBox.Show("The specified folder is not empty. Continue anyway?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning)) {
                    return;
                }
            }

            if (beatMap != null) {
                mapEditor.notes.Clear();
                mapEditor.bookmarks.Clear();
                mapEditor.bpmChanges.Clear();
                ClearCoverImage();
            }
            beatMap = new RagnarockMap(d2.FileName, true);

            // select and load an audio file
            if (!SelectNewSong()) {
                return;
            }
            
            // save the map
            SaveBeatmap();

            // open the newly created map
            InitUI();
        }
        private void BtnOpenMap_Click(object sender, RoutedEventArgs e) {

            // check if map already open
            if (beatMap != null) {
                if (!PromptBeatmapSave()) {
                    return;
                }

                PauseSong();
            }

            // select folder for map
            // TODO: this dialog is sometimes hangs, is there a better way to select a folder?
            var d2 = new CommonOpenFileDialog();
            d2.Title = "Select your map's containing folder";
            d2.IsFolderPicker = true;
            d2.InitialDirectory = RagnarockMapFolder();
            if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
                return;
            }

            // try to load info
            var oldBeatMap = beatMap;
            try {
                beatMap = new RagnarockMap(d2.FileName, false);
                LoadSong(); // song file
                LoadCoverImage();
                InitUI(); // cover image file

                // bandaid fix to prevent WPF from committing unnecessarily large amounts of memory
                new Thread(new ThreadStart(delegate {
                    Thread.Sleep(500);
                    this.Dispatcher.Invoke(() => DrawEditorGrid());
                })).Start();

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
            discordClient.SetPresence();
        }
        private void BtnSaveMap_Click(object sender, RoutedEventArgs e) {
            BackupAndSaveBeatmap();
            //SaveBeatmap();
        }
        private void BtnExportMap_Click(object sender, RoutedEventArgs e) {
            var d = new CommonOpenFileDialog();
            d.Title = "Select a folder to export the map to";
            d.IsFolderPicker = true;
            d.InitialDirectory = RagnarockMapFolder();
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
                
            } catch (Exception ex) {
                MessageBox.Show($"An error occured while creating the zip file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally {
                if (Directory.Exists(zipFolder)) {
                    Directory.Delete(zipFolder, true);
                }
            }
        }
        private void BtnBPMFinder_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<BPMCalcWindow>();
            if (win == null) {
                win = new BPMCalcWindow();
                win.Topmost = true;
                win.Owner = this;
                win.Show();
            } else {
                win.Focus();
            }
        }
        private void BtnSettings_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<SettingsWindow>();
            
            if (win == null) {
                win = new SettingsWindow(this, userSettings);
                win.Topmost = true;
                win.Owner = this;
                win.ShowDialog();
            } else {
                win.Focus();
            }
        }
        private void BtnPickSong_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            SelectNewSong();
        }
        private void BtnMakePreview_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<SongPreviewWindow>();
            if (win == null) {
                int selectedTime = (int)(sliderSongProgress.Value / 1000.0);
                win = new SongPreviewWindow(beatMap.GetPath(), beatMap.PathOf((string)beatMap.GetValue("_songFilename")), selectedTime / 60, selectedTime % 60);
                win.Topmost = true;
                win.Owner = this;
                win.Show();
            } else {
                win.Focus();
            }
        }
        private void BtnPickCover_Click(object sender, RoutedEventArgs e) {
            SelectNewCoverImage();
        }
        private void BtnSongPlayer_Click(object sender, RoutedEventArgs e) {
            if (!songIsPlaying) {
                PlaySong();
            } else {
                PauseSong();
            }
        }
        private void BtnAddDifficulty_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            beatMap.AddMap();
            int newMap = beatMap.numDifficulties - 1;
            var res = MessageBox.Show("Copy over bookmarks and BPM changes from the currently selected map?", "Copy Existing Map Data?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes) {
                beatMap.SetBookmarksForMap(newMap, beatMap.GetBookmarksForMap(currentDifficulty));
                beatMap.SetBPMChangesForMap(newMap, beatMap.GetBPMChangesForMap(currentDifficulty));
            }
            SwitchDifficultyMap(newMap);
            UpdateDifficultyButtonVisibility();
            SortDifficultyMaps();
            UpdateDifficultyLabels();
        }
        private void BtnDeleteDifficulty_Click(object sender, RoutedEventArgs e) {
            var res = MessageBox.Show("Are you sure you want to delete this difficulty? This cannot be undone.", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) {
                return;
            }
            PauseSong();
            
            for (int i = currentDifficulty; i < mapEditors.Length - 1; i++) {
                mapEditors[i] = mapEditors[i + 1];
            }
            mapEditors[mapEditors.Length - 1] = null;

            beatMap.DeleteMap(currentDifficulty);
            SwitchDifficultyMap(Math.Min(currentDifficulty, beatMap.numDifficulties - 1), false);

            UpdateDifficultyButtonVisibility();
            UpdateDifficultyLabels();
        }
        private void BtnChangeDifficulty0_Click(object sender, RoutedEventArgs e) {
            SwitchDifficultyMap(0);
        }
        private void BtnChangeDifficulty1_Click(object sender, RoutedEventArgs e) {
            SwitchDifficultyMap(1);
        }
        private void BtnChangeDifficulty2_Click(object sender, RoutedEventArgs e) {
            SwitchDifficultyMap(2);
        }
        private void SliderSongVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            songChannel.Volume = (float)sliderSongVol.Value;
            txtSongVol.Text = $"{(int)(sliderSongVol.Value * 100)}%";
        }
        private void SliderDrumVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            drummer.ChangeVolume(sliderDrumVol.Value);
            txtDrumVol.Text = $"{(int)(sliderDrumVol.Value * 100)}%";
        }
        private void CheckMetronome_Click(object sender, RoutedEventArgs e) {
            metronome.isEnabled = (checkMetronome.IsChecked == true);
        }
        private void SliderSongProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {

            // update song seek time text box
            txtSongPosition.Text = Helper.TimeFormat((int)(sliderSongProgress.Value / 1000.0));

            // update vertical scrollbar
            var percentage = sliderSongProgress.Value / sliderSongProgress.Maximum;
            var offset = (1 - percentage) * scrollEditor.ScrollableHeight;
            scrollEditor.ScrollToVerticalOffset(offset);

            var newLineY = borderNavWaveform.ActualHeight * (1 - percentage);
            lineSongProgress.Y1 = newLineY;
            lineSongProgress.Y2 = newLineY;

            // play drum hits
            //if (songIsPlaying) {
            //    //Trace.WriteLine($"Slider: {sliderSongProgress.Value}ms");
            //    scanForNotes();
            //}
        }
        private void TxtSongBPM_LostFocus(object sender, RoutedEventArgs e) {
            double BPM;
            double prevBPM = globalBPM;
            if (double.TryParse(txtSongBPM.Text, out BPM) && BPM > 0) {
                if (BPM != prevBPM) {
                    var result = MessageBox.Show("Would you like to convert all BPM changes and notes so that they remain at the same time?", "", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) {
                        txtSongBPM.Text = prevBPM.ToString();
                        return;
                    } else if (result == MessageBoxResult.Yes) {
                        foreach (var bc in mapEditor.bpmChanges) {
                            bc.globalBeat *= BPM / prevBPM;
                        }
                        foreach (var n in mapEditor.notes) {
                            n.beat *= BPM / prevBPM;
                        }
                        foreach (var b in mapEditor.bookmarks) {
                            b.beat *= BPM / prevBPM;
                        }
                    }
                    beatMap.SetValue("_beatsPerMinute", BPM);
                    globalBPM = BPM;
                    UpdateEditorGridHeight();
                }
            } else {
                MessageBox.Show($"The BPM must be a positive number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BPM = prevBPM;
            }
            txtSongBPM.Text = BPM.ToString();
        }
        private void BtnChangeBPM_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<ChangeBPMWindow>();
            if (win == null) {
                win = new ChangeBPMWindow(this, mapEditor.bpmChanges);
                win.Topmost = true;
                win.Owner = this;
                win.Show();
            } else {
                win.Focus();
            }
        }
        private void TxtSongOffset_LostFocus(object sender, RoutedEventArgs e) {
            double offset;
            double prevOffset = Helper.DoubleParseInvariant((string)beatMap.GetValue("_songTimeOffset"));
            if (double.TryParse(txtSongOffset.Text, out offset)) {
                beatMap.SetValue("_songTimeOffset", offset);
            } else {
                MessageBox.Show($"The song offset must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                offset = prevOffset;
            }
            txtSongOffset.Text = offset.ToString();
        }
        private void TxtSongName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.SetValue("_songName", txtSongName.Text);
        }
        private void TxtSongName_LostFocus(object sender, RoutedEventArgs e) {
            txtSongName.ScrollToHome();
        }
        private void TxtArtistName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.SetValue("_songAuthorName", txtArtistName.Text);
        }
        private void TxtArtistName_LostFocus(object sender, RoutedEventArgs e) {
            txtArtistName.ScrollToHome();
        }
        private void TxtMapperName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.SetValue("_levelAuthorName", txtMapperName.Text);
        }
        private void TxtMapperName_LostFocus(object sender, RoutedEventArgs e) {
            txtMapperName.ScrollToHome();
        }
        private void checkExplicitContent_Click(object sender, RoutedEventArgs e) {
            beatMap.SetValue("_explicit", (checkExplicitContent.IsChecked == true).ToString().ToLower());
        }
        private void TxtDifficultyNumber_LostFocus(object sender, RoutedEventArgs e) {
            int prevLevel = (int)beatMap.GetValueForMap(currentDifficulty, "_difficultyRank");
            int level;
            if (int.TryParse(txtDifficultyNumber.Text, out level) && Helper.DoubleRangeCheck(level, Const.Editor.DifficultyLevelMin, Const.Editor.DifficultyLevelMax)) {
                if (level != prevLevel) {
                    beatMap.SetValueForMap(currentDifficulty, "_difficultyRank", level);
                    SortDifficultyMaps();
                    UpdateDifficultyLabels();
                }
            } else {
                MessageBox.Show($"The difficulty level must be an integer between {Const.Editor.DifficultyLevelMin} and {Const.Editor.DifficultyLevelMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                level = prevLevel;
            }
            txtDifficultyNumber.Text = level.ToString();
        }
        private void TxtNoteSpeed_LostFocus(object sender, RoutedEventArgs e) {
            double prevSpeed = int.Parse((string)beatMap.GetValueForMap(currentDifficulty, "_noteJumpMovementSpeed"));
            double speed;
            if (double.TryParse(txtNoteSpeed.Text, out speed) && speed > 0) {
                beatMap.SetValueForMap(currentDifficulty, "_noteJumpMovementSpeed", speed);
            } else {
                MessageBox.Show($"The note speed must be a positive number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                speed = prevSpeed;
            }
            txtNoteSpeed.Text = speed.ToString();
        }
        private void TxtDistMedal0_GotFocus(object sender, RoutedEventArgs e) {
            if (txtDistMedal0.Text == "Auto") {
                txtDistMedal0.Text = "";
            }
        }
        private void TxtDistMedal1_GotFocus(object sender, RoutedEventArgs e) {
            if (txtDistMedal1.Text == "Auto") {
                txtDistMedal1.Text = "";
            }
        }
        private void TxtDistMedal2_GotFocus(object sender, RoutedEventArgs e) {
            if (txtDistMedal2.Text == "Auto") {
                txtDistMedal2.Text = "";
            }
        }
        private void TxtDistMedal0_LostFocus(object sender, RoutedEventArgs e) {
            if (txtDistMedal0.Text == "Auto" || txtDistMedal0.Text == "auto") {
                txtDistMedal0.Text = "";
            }
            int dist = UpdateMedalDistance(0, txtDistMedal0.Text);
            txtDistMedal0.Text = dist == 0 ? "Auto" : dist.ToString();
        }
        private void TxtDistMedal1_LostFocus(object sender, RoutedEventArgs e) {
            if (txtDistMedal1.Text == "Auto" || txtDistMedal1.Text == "auto") {
                txtDistMedal1.Text = "";
            }
            int dist = UpdateMedalDistance(1, txtDistMedal1.Text);
            txtDistMedal1.Text = dist == 0 ? "Auto" : dist.ToString();
        }
        private void TxtDistMedal2_LostFocus(object sender, RoutedEventArgs e) {
            if (txtDistMedal2.Text == "Auto" || txtDistMedal2.Text == "auto") {
                txtDistMedal2.Text = "";
            }
            int dist = UpdateMedalDistance(2, txtDistMedal2.Text);
            txtDistMedal2.Text = dist == 0 ? "Auto" : dist.ToString();
        }
        private void CheckGridSnap_Click(object sender, RoutedEventArgs e) {
            editorSnapToGrid = (checkGridSnap.IsChecked == true);
        }
        private void TxtGridOffset_LostFocus(object sender, RoutedEventArgs e) {
            double prevOffset = Helper.DoubleParseInvariant((string)beatMap.GetCustomValueForMap(currentDifficulty, "_editorOffset"));
            double offset;
            if (double.TryParse(txtGridOffset.Text, out offset)) {
                if (offset != prevOffset) {
                    // resnap all notes
                    var dialogResult = MessageBox.Show("Resnap all currently placed notes to align with the new grid?\nThis cannot be undone.", "", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dialogResult == MessageBoxResult.Yes) {
                        ResnapAllNotes(offset);
                    }

                    editorGridOffset = offset;
                    beatMap.SetCustomValueForMap(currentDifficulty, "_editorOffset", offset);
                    UpdateEditorGridHeight();
                }
            } else {
                MessageBox.Show($"The grid offset must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                offset = prevOffset;
            }
            txtGridOffset.Text = offset.ToString();
        }
        private void TxtGridSpacing_LostFocus(object sender, RoutedEventArgs e) {
            double prevSpacing = Helper.DoubleParseInvariant((string)beatMap.GetCustomValueForMap(currentDifficulty, "_editorGridSpacing"));
            double spacing;
            if (double.TryParse(txtGridSpacing.Text, out spacing)) {
                if (spacing != prevSpacing) {
                    editorGridSpacing = spacing;
                    beatMap.SetCustomValueForMap(currentDifficulty, "_editorGridSpacing", spacing);
                    UpdateEditorGridHeight();
                }
            } else {
                MessageBox.Show($"The grid spacing must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                spacing = prevSpacing;
            }
            txtGridSpacing.Text = spacing.ToString();
        }
        private void TxtGridDivision_LostFocus(object sender, RoutedEventArgs e) {
            int prevDiv = int.Parse((string)beatMap.GetCustomValueForMap(currentDifficulty, "_editorGridDivision"));
            int div;

            if (int.TryParse(txtGridDivision.Text, out div) && Helper.DoubleRangeCheck(div, 1, Const.Editor.GridDivisionMax)) {
                if (div != prevDiv) {
                    editorGridDivision = div;
                    beatMap.SetCustomValueForMap(currentDifficulty, "_editorGridDivision", div);
                    DrawEditorGrid(false);
                }
            } else {
                MessageBox.Show($"The grid division amount must be an integer from 1 to {Const.Editor.GridDivisionMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                div = prevDiv;
            }
            txtGridDivision.Text = div.ToString();
        }
        private void CheckWaveform_Click(object sender, RoutedEventArgs e) {
            if (editorShowWaveform) {
                if (!EditorGrid.Children.Contains(imgAudioWaveform)) { 
                    EditorGrid.Children.Add(imgAudioWaveform);
                }
                DrawEditorWaveform();
            } else {
                EditorGrid.Children.Remove(imgAudioWaveform);
            }
        }
        private void ComboEnvironment_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            //var env = (string)comboEnvironment.SelectedItem;
            //if (env == Constants.BeatmapDefaults.DefaultEnvironmentAlias) {
            //    env = "DefaultEnvironment";
            //}
            beatMap.SetValue("_environmentName", (string)comboEnvironment.SelectedItem);
        }
        private void BorderNavWaveform_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (beatMap != null) {
                DrawEditorNavWaveform();
                DrawNavBookmarks();
                var lineY = sliderSongProgress.Value/sliderSongProgress.Maximum * borderNavWaveform.ActualHeight;
                lineSongMouseover.Y1 = lineY;
                lineSongMouseover.Y2 = lineY;
            }
        }
        private void BorderNavWaveform_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            navMouseDown = true;
            sliderSongProgress.Value = sliderSongProgress.Maximum * (1 - lineSongMouseover.Y1 / borderNavWaveform.ActualHeight);
            Keyboard.ClearFocus();
            Keyboard.Focus(this);
        }
        private void BorderNavWaveform_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            navMouseDown = false;
        }
        private void BorderNavWaveform_MouseMove(object sender, MouseEventArgs e) {
            var mouseY = e.GetPosition(borderNavWaveform).Y;
            lineSongMouseover.Y1 = mouseY;
            lineSongMouseover.Y2 = mouseY;
            var mouseTime = sliderSongProgress.Maximum * (1 - mouseY / borderNavWaveform.ActualHeight);
            if (navMouseDown) {
                sliderSongProgress.Value = mouseTime;
            }
            lblSelectedBeat.Content = $"Time: {Helper.TimeFormat(mouseTime / 1000)}, Global Beat: {Math.Round(mouseTime / 60000 * globalBPM, 3)}";
        }
        private void BorderNavWaveform_MouseEnter(object sender, MouseEventArgs e) {
            lineSongMouseover.Opacity = 1;
        }
        private void BorderNavWaveform_MouseLeave(object sender, MouseEventArgs e) {
            lineSongMouseover.Opacity = 0;
            navMouseDown = false;
            lblSelectedBeat.Content = "";
        }
        private void ScrollEditor_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateEditorGridHeight(false);
            if (e.WidthChanged) {
                if (beatMap != null) {
                    DrawEditorGrid();
                }
            } else if (beatMap != null && editorShowWaveform) {
                DrawEditorWaveform();
            }
        }
        private void ScrollEditor_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            var curr = scrollEditor.VerticalOffset;
            var range = scrollEditor.ScrollableHeight;
            var value = (1 - curr / range) * (sliderSongProgress.Maximum - sliderSongProgress.Minimum);
            sliderSongProgress.Value = Double.IsNaN(value) ? 0 : value;

            // try to keep the scroller at the same percentage scroll that it was before
            if (e.ExtentHeightChange != 0) {
                scrollEditor.ScrollToVerticalOffset((1 - prevScrollPercent) * scrollEditor.ScrollableHeight);
                //Console.Write($"time: {txtSongPosition.Text} curr: {scrollEditor.VerticalOffset} max: {scrollEditor.ScrollableHeight} change: {e.ExtentHeightChange}\n");
            } else if (range != 0) {
                prevScrollPercent = (1 - curr / range);
            }
            CalculateDrawRange();
        }
        private void ScrollEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

        }
        private void EditorGrid_MouseMove(object sender, MouseEventArgs e) {

            // calculate beat
            try {
                editorMouseBeatSnapped = BeatForPosition(e.GetPosition(EditorGrid).Y, true);
                editorMouseBeatUnsnapped = BeatForPosition(e.GetPosition(EditorGrid).Y, false);
            } catch {
                editorMouseBeatSnapped = 0;
                editorMouseBeatUnsnapped = 0;
            }

            // calculate column
            editorMouseGridCol = ColFromPos(e.GetPosition(EditorGrid).X);
            if (editorMouseGridCol < 0) {
                imgPreviewNote.Opacity = 0;
            } else {
                imgPreviewNote.Opacity = Const.Editor.PreviewNoteOpacity; 
            }
            double noteX = (1 + 4 * editorMouseGridCol) * unitSubLength;
            // for some reason Canvas.SetLeft(0) doesn't correspond to the leftmost of the canvas, so we need to do some unknown adjustment to line it up
            var unknownNoteXAdjustment = (unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2;

            double userOffsetBeat = editorGridOffset * globalBPM / 60;
            double userOffset = userOffsetBeat * unitLength;
            var mousePos = EditorGrid.ActualHeight - e.GetPosition(EditorGrid).Y - unitHeight / 2;
            double gridLength = unitLength / editorGridDivision;

            // place preview note
            Canvas.SetBottom(imgPreviewNote, editorSnapToGrid ? (editorMouseBeatSnapped * gridLength * editorGridDivision + userOffset) : Math.Max(mousePos, userOffset));
            imgPreviewNote.Source = RuneForBeat(userOffsetBeat + (editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped));
            Canvas.SetLeft(imgPreviewNote, noteX - unknownNoteXAdjustment + EditorMarginGrid.Margin.Left);

            // place preview line
            lineGridMouseover.Y1 = e.GetPosition(EditorGrid).Y - markerDragOffset;
            lineGridMouseover.Y2 = e.GetPosition(EditorGrid).Y - markerDragOffset;

             // update beat display
            lblSelectedBeat.Content = $"Time: {Helper.TimeFormat(editorMouseBeatSnapped * 60 / globalBPM)}, Global Beat: {Math.Round(editorMouseBeatSnapped, 3)} ({Math.Round(editorMouseBeatUnsnapped, 3)})";

            // calculate drag stuff
            if (currentlyDraggingMarker != null && !isEditingMarker) { 
                double newBottom = unitLength * BeatForPosition(e.GetPosition(EditorGrid).Y - markerDragOffset, shiftKeyDown);
                Canvas.SetBottom(currentlyDraggingMarker, newBottom + unitHeight / 2);
                this.Cursor = Cursors.Hand;
                lineGridMouseover.Visibility = Visibility.Visible;
            } else if (editorIsDragging) {
                UpdateDragSelection(e.GetPosition(EditorGrid));
            } else if (editorMouseDown) {
                Vector delta = e.GetPosition(EditorGrid) - editorDragSelectStart;
                if (delta.Length > Const.Editor.DragInitThreshold) {
                    imgPreviewNote.Visibility = Visibility.Hidden;
                    editorIsDragging = true;
                    editorDragSelectBorder.Visibility = Visibility.Visible;
                    UpdateDragSelection(e.GetPosition(EditorGrid));
                }
            }
        }
        private void EditorGrid_MouseEnter(object sender, MouseEventArgs e) {
            imgPreviewNote.Opacity = Const.Editor.PreviewNoteOpacity;
            lineGridMouseover.Opacity = 1.0;
        }
        private void EditorGrid_MouseLeave(object sender, MouseEventArgs e) {
            imgPreviewNote.Opacity = 0;
            lineGridMouseover.Opacity = 0;
            lblSelectedBeat.Content = "";
        }
        private void EditorGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {

            editorMouseDown = true;
            editorDragSelectStart = e.GetPosition(EditorGrid);
            editorSelBeatStart = editorMouseBeatUnsnapped;
            editorSelColStart = editorMouseGridCol;
            EditorGrid.CaptureMouse();
        }
        private void EditorGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            imgPreviewNote.Visibility = Visibility.Visible;
            if (currentlyDraggingMarker != null && !isEditingMarker) {

                if (currentlyDraggingBPMChange == null) {
                    mapEditor.RemoveBookmark(currentlyDraggingBookmark);
                    currentlyDraggingBookmark.beat = BeatForPosition(e.GetPosition(EditorGrid).Y - markerDragOffset, shiftKeyDown);
                    mapEditor.AddBookmark(currentlyDraggingBookmark);
                    DrawEditorGrid(false);
                } else {
                    mapEditor.RemoveBPMChange(currentlyDraggingBPMChange, false);
                    currentlyDraggingBPMChange.globalBeat = BeatForPosition(e.GetPosition(EditorGrid).Y - markerDragOffset, shiftKeyDown);
                    mapEditor.AddBPMChange(currentlyDraggingBPMChange);
                    DrawEditorGrid(false);
                }
                this.Cursor = Cursors.Arrow;
                lineGridMouseover.Visibility = Visibility.Hidden;
                currentlyDraggingBPMChange = null;
                currentlyDraggingMarker = null;
                markerDragOffset = 0;
            } else if (editorIsDragging) {
                editorDragSelectBorder.Visibility = Visibility.Hidden;
                // calculate new selections
                List<Note> newSelection = new List<Note>();
                double startBeat = editorSelBeatStart;
                double endBeat = editorMouseBeatUnsnapped;
                int editorSelColEnd = editorMouseGridCol;
                if (editorSelColEnd == -1) {
                    editorSelColEnd = e.GetPosition(EditorGrid).X < EditorGrid.ActualWidth/2 ? 0 : 3;
                }
                foreach (Note n in mapEditor.notes) {
                    // minor optimisation
                    if (n.beat > Math.Max(startBeat, endBeat)) {
                        break;
                    }
                    // check range
                    if (Helper.DoubleRangeCheck(n.beat, startBeat, endBeat) && Helper.DoubleRangeCheck(n.col, editorSelColStart, editorSelColEnd)) {
                        newSelection.Add(n);
                    }
                }
                if (shiftKeyDown) {
                    mapEditor.SelectNotes(newSelection);
                } else {
                    mapEditor.SelectNewNotes(newSelection);
                }
            } else if (editorMouseDown && editorMouseGridCol >= 0) {
                //Trace.WriteLine($"Row: {editorMouseGridRow} ({Math.Round(editorMouseGridRowFractional, 2)}), Col: {editorMouseGridCol}, Beat: {beat} ({beatFractional})");

                // create the note
                double beat = editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
                Note n = new Note(beat, editorMouseGridCol);

                if (mapEditor.notes.Contains(n)) {
                    if (shiftKeyDown) {
                        mapEditor.ToggleSelection(n);
                    } else {
                        mapEditor.SelectNewNotes(n);
                    }
                } else {
                    mapEditor.AddNotes(n);
                    drummer.Play(n.col);
                }
            }

            EditorGrid.ReleaseMouseCapture();
            editorIsDragging = false;
            editorMouseDown = false;
        }
        private void EditorGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            // remove the note
            double beat = editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
            Note n = new Note(beat, editorMouseGridCol);
            if (mapEditor.notes.Contains(n)) {
                mapEditor.RemoveNotes(n);
            } else {
                mapEditor.UnselectAllNotes();
            }
        }

        // UI initialisation
        private void InitUI() {
            // reset variables
            currentDifficulty = 0;
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
            globalBPM = Helper.DoubleParseInvariant((string)beatMap.GetValue("_beatsPerMinute"));

            comboEnvironment.SelectedIndex = Const.BeatmapDefaults.EnvironmentNames.IndexOf((string)beatMap.GetValue("_environmentName"));

            for (int i = 0; i < mapEditors.Length; i++) {
                mapEditors[i] = null;
            }

            UpdateDifficultyLabels();

            // enable UI parts
            EnableUI();

            // init difficulty-specific UI 
            SwitchDifficultyMap(0, false, false);

            UpdateEditorGridHeight();
            scrollEditor.ScrollToBottom();
            DrawEditorNavWaveform();
        }
        private void EnableUI() {
            btnSaveMap.IsEnabled = true;
            btnExportMap.IsEnabled = true;
            btnChangeDifficulty0.IsEnabled = true;
            btnChangeDifficulty1.IsEnabled = true;
            btnChangeDifficulty2.IsEnabled = true;
            EnableDifficultyButtons();
            UpdateDifficultyButtonVisibility();
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
            btnSaveMap.IsEnabled = false;
            btnExportMap.IsEnabled = false;
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
            if (mapEditor != null) {
                beatMap.SetBPMChangesForMap(currentDifficulty, mapEditor.bpmChanges);
                beatMap.SetBookmarksForMap(currentDifficulty, mapEditor.bookmarks);
                beatMap.SetNotesForMap(currentDifficulty, mapEditor.notes);
            }
            beatMap.SaveToFile();
            this.Dispatcher.Invoke(() => {
                imgSaved.Opacity = 1;
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
            string backupFolder = beatMap.PathOf(Const.Program.BackupPath);
            if (!Directory.Exists(backupFolder)) {
                Directory.CreateDirectory(backupFolder);
            }

            // get names of files to backup
            List<string> files = new(Const.BeatmapDefaults.DifficultyNames);
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
            if (existingBackups.Count == Const.Program.MaxBackups) {
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
            if (!Directory.Exists(Const.Program.ProgramDataDir)) {
                Directory.CreateDirectory(Const.Program.ProgramDataDir);
            }
            // move old settings files to new centralised location
            if (File.Exists(Const.Program.OldSettingsFile)) {
                File.Move(Const.Program.OldSettingsFile, Const.Program.SettingsFile);
            }
        }
        internal void LoadSettingsFile() {
            userSettings = new UserSettings(Const.Program.SettingsFile);

            if (!int.TryParse(userSettings.GetValueForKey(Const.UserSettings.EditorAudioLatency), out editorAudioLatency)) {
                userSettings.SetValueForKey(Const.UserSettings.EditorAudioLatency, Const.DefaultUserSettings.AudioLatency);
                editorAudioLatency = Const.DefaultUserSettings.AudioLatency;
            }

            if (userSettings.GetValueForKey(Const.UserSettings.PanDrumSounds) == null) {
                userSettings.SetValueForKey(Const.UserSettings.PanDrumSounds, Const.DefaultUserSettings.PanDrumSounds);
            }
            bool isPanned = userSettings.GetBoolForKey(Const.UserSettings.PanDrumSounds);

            try {
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume));
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.DefaultNoteVolume, Const.DefaultUserSettings.DefaultNoteVolume);
            }

            try {
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultSongVolume));
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.DefaultSongVolume, Const.DefaultUserSettings.DefaultSongVolume);
            }

            try {
                InitDrummer(userSettings.GetValueForKey(Const.UserSettings.DrumSampleFile), isPanned);
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.DrumSampleFile, Const.DefaultUserSettings.DrumSampleFile);
                InitDrummer(Const.DefaultUserSettings.DrumSampleFile, isPanned);
            }

            if (userSettings.GetValueForKey(Const.UserSettings.DefaultSongVolume) == null) {
                userSettings.SetValueForKey(Const.UserSettings.DefaultSongVolume, Const.DefaultUserSettings.DefaultSongVolume);
            }

            if (userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume) == null) {
                userSettings.SetValueForKey(Const.UserSettings.DefaultNoteVolume, Const.DefaultUserSettings.DefaultNoteVolume);
            }

            if (userSettings.GetValueForKey(Const.UserSettings.EnableDiscordRPC) == null) {
                userSettings.SetValueForKey(Const.UserSettings.EnableDiscordRPC, Const.DefaultUserSettings.EnableDiscordRPC);
            }
            SetDiscordRPC(userSettings.GetBoolForKey(Const.UserSettings.EnableDiscordRPC));

            if (userSettings.GetValueForKey(Const.UserSettings.EnableAutosave) == null) {
                userSettings.SetValueForKey(Const.UserSettings.EnableAutosave, Const.DefaultUserSettings.EnableAutosave);
            }
            autosaveTimer.Enabled = userSettings.GetBoolForKey(Const.UserSettings.EnableAutosave);

            if (userSettings.GetValueForKey(Const.UserSettings.CheckForUpdates) == null) {
                userSettings.SetValueForKey(Const.UserSettings.CheckForUpdates, Const.DefaultUserSettings.CheckForUpdates);
            }

            try {
                var index = int.Parse(userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationIndex));
                // game install directory chosen
                var gameInstallPath = userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationPath);
                if (index == 1 && !Directory.Exists(gameInstallPath)) {
                    throw new Exception();
                }
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.MapSaveLocationIndex, Const.DefaultUserSettings.MapSaveLocationIndex);
                userSettings.SetValueForKey(Const.UserSettings.MapSaveLocationPath, Const.DefaultUserSettings.MapSaveLocationPath);
            }

            try {
                int.Parse(userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationIndex));
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.MapSaveLocationIndex, Const.DefaultUserSettings.MapSaveLocationIndex);
            }

            userSettings.Write();
        }

        // Discord RPC
        public void SetDiscordRPC(bool enable) {
            if (enable) {
                discordClient.Enable();
            } else {
                discordClient.Disable();
            }
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
        private void UpdateDifficultyButtonVisibility() {
            var numDiff = beatMap.numDifficulties;
            for (var i = 0; i < numDiff; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Visible;
            }
            for (var i = numDiff; i < 3; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Hidden;
            }
            btnDeleteDifficulty.IsEnabled = (numDiff == 1) ? false : true;
            btnAddDifficulty.Visibility = (numDiff == 3) ? Visibility.Hidden : Visibility.Visible;
        }
        private void EnableDifficultyButtons() {
            foreach (Button b in DifficultyChangePanel.Children) {
                if (b.Name == ((Button)DifficultyChangePanel.Children[currentDifficulty]).Name) {
                    b.IsEnabled = false;
                } else {
                    b.IsEnabled = true;
                }
            }
            btnDeleteDifficulty.IsEnabled = (beatMap.numDifficulties > 1);
            btnAddDifficulty.IsEnabled = (beatMap.numDifficulties < 3);
        }
        private void SwitchDifficultyMap(int indx, bool savePrevious = true, bool redrawGrid = true) {
            PauseSong();

            if (savePrevious) {
                beatMap.SetNotesForMap(currentDifficulty, mapEditor.notes);
                beatMap.SetBookmarksForMap(currentDifficulty, mapEditor.bookmarks);
                beatMap.SetBPMChangesForMap(currentDifficulty, mapEditor.bpmChanges);
            }

            currentDifficulty = indx;

            if (mapEditors[indx] == null) {
                mapEditors[indx] = new MapEditor(this, beatMap.GetNotesForMap(indx), beatMap.GetBPMChangesForMap(indx), beatMap.GetBookmarksForMap(indx), editorClipboard);
            }
            mapEditor = mapEditors[indx];
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
            editorGridDivision = int.Parse(txtGridDivision.Text);
            editorGridSpacing = Helper.DoubleParseInvariant(txtGridSpacing.Text);
            editorGridOffset = Helper.DoubleParseInvariant(txtGridOffset.Text);

            EnableDifficultyButtons();
            DrawNavBookmarks();
            if (redrawGrid) {
                UpdateEditorGridHeight();
            }
        }
        private void SortDifficultyMaps() {
            // bubble sort
            bool swap;
            do {
                swap = false;
                for (int i = 0; i < beatMap.numDifficulties - 1; i++) {
                    int lowDiff = (int)beatMap.GetValueForMap(i, "_difficultyRank");
                    int highDiff = (int)beatMap.GetValueForMap(i + 1, "_difficultyRank");
                    if (lowDiff > highDiff) {
                        SwapDifficultyMaps(i, i + 1);
                        if (currentDifficulty == i) {
                            currentDifficulty++;
                        } else if (currentDifficulty == i + 1) {
                            currentDifficulty--;
                        }
                        swap = true;
                    }
                }
            } while (swap);
            SwitchDifficultyMap(currentDifficulty);
        }
        private void SwapDifficultyMaps(int i, int j) {
            var temp = mapEditors[i];
            mapEditors[i] = mapEditors[j];
            mapEditors[j] = temp;

            beatMap.SwapMaps(i, j);
            //if (currentDifficulty == i) {
            //    SwitchDifficultyMap(j);
            //} else if (currentDifficulty == j) {
            //    SwitchDifficultyMap(i);
            //}
            EnableDifficultyButtons();
        }

        // song/note playback
        private bool SelectNewSong() {
            // select audio file
            var d = new Microsoft.Win32.OpenFileDialog();
            d.Title = "Select a song to map";
            d.DefaultExt = ".ogg";
            d.Filter = "OGG Vorbis (*.ogg)|*.ogg";

            if (d.ShowDialog() != true) {
                return false;
            }
            VorbisWaveReader vorbisStream;
            try {
                vorbisStream = new VorbisWaveReader(d.FileName);
            } catch (Exception) {
                MessageBox.Show("The .ogg file is corrupted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (vorbisStream.TotalTime.TotalHours >= 1) {
                MessageBox.Show("Songs over 1 hour in duration are not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // check for same file
            var songFile = System.IO.Path.GetFileName(Helper.SanitiseSongFileName(d.FileName));
            var songFilePath = beatMap.PathOf(songFile);
            var prevSongFile = beatMap.PathOf((string)beatMap.GetValue("_songFilename"));

            if (d.FileName == prevSongFile) {
                return false;
            }

            // update beatmap data
            UnloadSong();
            beatMap.SetValue("_songApproximativeDuration", (int)vorbisStream.TotalTime.TotalSeconds + 1);
            beatMap.SetValue("_songFilename", songFile);
            SaveBeatmap();
            vorbisStream.Dispose();

            // do file I/O
            if (File.Exists(prevSongFile)) {
                File.Delete(prevSongFile);
            }

            // can't copy over an existing file
            if (File.Exists(songFilePath)) {
                File.Delete(songFilePath);
            }
            File.Copy(d.FileName, songFilePath);

            LoadSong();

            // redraw waveforms
            if (editorShowWaveform) {
                DrawEditorWaveform();
            }
            DrawEditorNavWaveform();

            return true;
        }
        private void LoadSong() {

            var songPath = beatMap.PathOf((string)beatMap.GetValue("_songFilename"));
            songStream = new VorbisWaveReader(songPath);
            songChannel = new SampleChannel(songStream);
            songChannel.Volume = (float)sliderSongVol.Value;
            songPlayer = new WasapiOut(AudioClientShareMode.Shared, Const.Audio.WASAPILatencyTarget);
            songPlayer.Init(songChannel);
            beatMap.SetValue("_songApproximativeDuration", (int)songStream.TotalTime.TotalSeconds + 1);
            // subscribe to playbackstopped
            songPlayer.PlaybackStopped += (sender, args) => { PauseSong(); };

            // load UI
            sliderSongProgress.Minimum = 0;
            sliderSongProgress.Maximum = songStream.TotalTime.TotalSeconds * 1000;
            sliderSongProgress.Value = 0;
            txtSongDuration.Text = Helper.TimeFormat((int)songStream.TotalTime.TotalSeconds);
            txtSongFileName.Text = (string)beatMap.GetValue("_songFilename");

            audioWaveform = new VorbisWaveformVisualiser(songPath);
            navWaveform = new VorbisWaveformVisualiser(songPath);
            //awd = new AudioVisualiser_Float32(new VorbisWaveReader(songPath));

            imgAudioWaveform.Source = null;
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

            // hide editor
            imgPreviewNote.Opacity = 0;

            // animate for smooth scrolling 
            var remainingTimeSpan = songStream.TotalTime - songStream.CurrentTime;

            // note: the DoubleAnimation induces a desync of around 0.1 seconds
            songPlayAnim = new DoubleAnimation();
            songPlayAnim.From = sliderSongProgress.Value;
            songPlayAnim.To = sliderSongProgress.Maximum;
            songPlayAnim.Duration = new Duration(remainingTimeSpan);
            //Timeline.SetDesiredFrameRate(songPlayAnim, animationFramerate);
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, songPlayAnim);

            noteScanner.Start((int)(sliderSongProgress.Value - editorAudioLatency), new List<Note>(mapEditor.notes), globalBPM);
            beatScanner.Start((int)(sliderSongProgress.Value - editorAudioLatency), majorGridlines, globalBPM);

            if (songStream.CurrentTime > new TimeSpan(0, 0, 0, 0, editorAudioLatency)) {
                songStream.CurrentTime = songStream.CurrentTime - new TimeSpan(0, 0, 0, 0, editorAudioLatency);
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
            EnableDifficultyButtons();
            scrollEditor.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            borderNavWaveform.IsEnabled = true;

            // reset scroll animation
            songPlayAnim.BeginTime = null;
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, null);

            // show editor
            imgPreviewNote.Opacity = Const.Editor.PreviewNoteOpacity;

            //Trace.WriteLine($"Slider is late by {Math.Round(songStream.CurrentTime.TotalMilliseconds - sliderSongProgress.Value, 2)}ms");
            songPlayer.Pause();
            //if (noteScanner.playedLateNote) {
            //    drummer.InitAudioOut();
            //    noteScanner.playedLateNote = false;
            //}
            //bool isPanned = userSettings.GetBoolForKey(Const.UserSettings.PanDrumSounds);
            //InitDrummer(userSettings.GetValueForKey(Const.UserSettings.DrumSampleFile), isPanned);
        }
        private void AnimateDrum(int num) {
            if (!Helper.DoubleRangeCheck(num, 0, 3)) {
                return;
            }
            var duration = new Duration(new TimeSpan(0, 0, 0, 0, Const.Editor.DrumHitDuration));

            var heightAnim = new DoubleAnimation();
            heightAnim.From = DrumRow.ActualHeight * Const.Editor.DrumHitScaleFactor;
            heightAnim.To = DrumRow.ActualHeight;
            heightAnim.Duration = duration;
            Storyboard.SetTargetProperty(heightAnim, new PropertyPath("(Image.Height)"));
            Storyboard.SetTargetName(heightAnim, $"Drum{num}");

            var widthAnim = new DoubleAnimation();
            widthAnim.From = DrumCol.ActualWidth * Const.Editor.DrumHitScaleFactor;
            widthAnim.To = DrumCol.ActualWidth;
            widthAnim.Duration = duration;
            Storyboard.SetTargetProperty(widthAnim, new PropertyPath("(Image.Width)"));
            Storyboard.SetTargetName(widthAnim, $"Drum{num}");

            var heightStackAnim = new DoubleAnimation();
            heightStackAnim.From = DrumRow.ActualHeight * (1 - Const.Editor.DrumHitScaleFactor) / 2;
            heightStackAnim.To = 0;
            heightStackAnim.Duration = duration;
            Storyboard.SetTargetProperty(heightStackAnim, new PropertyPath("(StackPanel.Height)"));
            Storyboard.SetTargetName(heightStackAnim, $"DrumStack{num}");

            var st = new Storyboard();
            st.Children.Add(heightAnim);
            st.Children.Add(widthAnim);
            st.Children.Add(heightStackAnim);
            st.Begin(this, true);

        }
        internal void AnimateNote(Note n) {
            var duration = new Duration(new TimeSpan(0, 0, 0, 0, Const.Editor.NoteHitDuration));

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
                editorDrawRangeLower = Math.Max(scrollPos - (Const.Editor.GridDrawRange * scrollEditor.ActualHeight), 0);
                editorDrawRangeHigher = Math.Min(scrollPos + ((1 + Const.Editor.GridDrawRange) * scrollEditor.ActualHeight), EditorGrid.ActualHeight);
                //Trace.WriteLine($"draw range: {editorDrawRangeLower} - {editorDrawRangeHigher}");
                // redraw
                //drawEditorWaveform(editorDrawRangeLower, editorDrawRangeHigher, EditorGrid.Height - scrollEditor.ActualHeight);
            }
        }
        private void UpdateEditorGridHeight(bool redraw = true) {
            if (beatMap == null) {
                return;
            }

            // resize editor grid height to fit scrollEditor height
            double beats = globalBPM / 60 * songStream.TotalTime.TotalSeconds;
            EditorGrid.Height = beats * unitLength + scrollEditor.ActualHeight;

            if (redraw) {
                DrawEditorGrid();
            }
        }
        internal void DrawEditorGrid(bool redrawWaveform = true) {
            if (beatMap == null) {
                return;
            }

            EditorGrid.Children.Clear();
            
            DateTime start = DateTime.Now;

            // draw gridlines
            EditorGrid.Children.Add(lineGridMouseover);
            DrawEditorGridLines(EditorGrid.Height);

            // then draw the waveform
            if (redrawWaveform && editorShowWaveform && EditorGrid.Height - scrollEditor.ActualHeight > 0) {
                DrawEditorWaveform();
            }
            EditorGrid.Children.Add(imgAudioWaveform);

            // then draw the notes
            EditorGridNoteCanvas.Children.Clear();
            DrawEditorNotes(mapEditor.notes);

            // including the mouseover preview note
            imgPreviewNote.Width = unitLength;
            imgPreviewNote.Height = unitHeight;
            EditorGridNoteCanvas.Children.Add(imgPreviewNote);

            EditorGrid.Children.Add(EditorGridNoteCanvas);

            // then the drag selection rectangle
            EditorGrid.Children.Add(editorDragSelectBorder);

            // finally, draw the markers
            DrawEditorGridBookmarks();
            DrawEditorGridBPMChanges();

            Trace.WriteLine($"INFO: Redrew editor grid in {(DateTime.Now - start).TotalSeconds} seconds.");
        }
        private void DrawEditorWaveform() {
            ResizeEditorWaveform();
            double height = EditorGrid.Height - scrollEditor.ActualHeight;
            double width = EditorGrid.ActualWidth * Const.Editor.Waveform.Width;
            CreateEditorWaveform(height, width);
        }
        private void ResizeEditorWaveform() {
            
            //EditorGrid.Children.Remove(imgAudioWaveform);
            imgAudioWaveform.Height = EditorGrid.Height - scrollEditor.ActualHeight;
            imgAudioWaveform.Width = EditorGrid.ActualWidth;
            Canvas.SetBottom(imgAudioWaveform, unitHeight / 2);
            //EditorGrid.Children.Insert(0, imgAudioWaveform);
        }
        private void CreateEditorWaveform(double height, double width) {
            Task.Run(() => {
                DateTime before = DateTime.Now;
                ImageSource bmp = audioWaveform.Draw(height, width);
                Trace.WriteLine($"INFO: Drew big waveform in {(DateTime.Now - before).TotalSeconds} sec");
                    
                this.Dispatcher.Invoke(() => {
                    if (bmp != null && editorShowWaveform) {
                        imgAudioWaveform.Source = bmp;
                        ResizeEditorWaveform();
                    }
                });
            });
        }
        private void DrawEditorNavWaveform() {
            Task.Run(() => {
                DateTime before = DateTime.Now;
                ImageSource bmp = navWaveform.Draw(borderNavWaveform.ActualHeight, colWaveformVertical.ActualWidth);
                Trace.WriteLine($"INFO: Drew nav waveform in {(DateTime.Now - before).TotalSeconds} sec");
                if (bmp != null) {
                    this.Dispatcher.Invoke(() => {
                        imgWaveformVertical.Source = bmp;
                    });
                }
            });
        }
        private void DrawEditorGridLines(double gridHeight) {
            // helper function for creating gridlines
            Line makeGridLine(double offset, bool isMajor = false) {
                var l = makeLine(EditorGrid.ActualWidth, offset);
                l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(
                    isMajor ? Const.Editor.MajorGridlineColour : Const.Editor.MinorGridlineColour)
                ;
                l.StrokeThickness = isMajor ? Const.Editor.MajorGridlineThickness : Const.Editor.MinorGridlineThickness;
                Canvas.SetBottom(l, offset + unitHeight / 2);
                return l;
            }

            majorGridlines.Clear();
            gridlines.Clear();

            // calculate grid offset
            double userOffset = editorGridOffset * globalBPM / 60 * unitLength;

            // the position to place gridlines, starting at the user-specified grid offset
            var offset = userOffset;

            var localBPM = globalBPM;
            var localGridDiv = editorGridDivision;

            // draw gridlines
            int counter = 0;
            int bpmChangeCounter = 0;
            while (offset <= gridHeight) {

                // add new gridline
                bool isMajor = counter % localGridDiv == 0;
                var l = makeGridLine(offset, isMajor);
                EditorGrid.Children.Add(l);
                if (isMajor) {
                    majorGridlines.Add((offset - userOffset) / unitLength);
                }
                gridlines.Add((offset - userOffset) / unitLength);

                offset += globalBPM / localBPM * unitLength / localGridDiv;
                counter++;

                // check for BPM change
                if (bpmChangeCounter < mapEditor.bpmChanges.Count && Helper.DoubleApproxGreaterEqual((offset - userOffset) / unitLength, mapEditor.bpmChanges[bpmChangeCounter].globalBeat)) {
                    BPMChange next = mapEditor.bpmChanges[bpmChangeCounter];

                    offset = next.globalBeat * unitLength + userOffset;
                    localBPM = next.BPM;
                    localGridDiv = next.gridDivision;

                    bpmChangeCounter++;
                    counter = 0;
                }
            }
            //this.Dispatcher.Invoke(() => {
            //    foreach (var l in gridLinesUI) {
            //        EditorGrid.Children.Add(l);
            //    }
            //});
        }
        internal void DrawEditorNotes(List<Note> notes) {
            // draw drum notes
            // TODO: paginate these? they cause lag when resizing

            // init drum note image
            foreach (var n in notes) {
                var img = new Image();
                img.Width = unitLengthUnscaled;
                img.Height = unitHeight;

                var noteHeight = n.beat * unitLength;
                var noteXOffset = (1 + 4 * n.col) * unitSubLength;

                // find which beat fraction this note lies on
                img.Source = RuneForBeat(n.beat);
                
                // this assumes there are no duplicate notes given to us
                img.Uid = Helper.UidGenerator(n);
                var name = Helper.NameGenerator(n);

                if (FindName(name) != null) {
                    UnregisterName(name);
                }
                RegisterName(name, img);

                Canvas.SetLeft(img, noteXOffset + EditorMarginGrid.Margin.Left);
                Canvas.SetBottom(img, noteHeight);

                EditorGridNoteCanvas.Children.Add(img);
            }
        }
        internal void DrawEditorNotes(Note n) {
            DrawEditorNotes(new List<Note>() { n });
        }
        private void DrawEditorGridBookmarks() {
            foreach (Bookmark b in mapEditor.bookmarks) {
                Canvas bookmarkCanvas = new();
                Canvas.SetRight(bookmarkCanvas, 0);
                Canvas.SetBottom(bookmarkCanvas, unitLength * b.beat + unitHeight / 2);

                var l = makeLine(EditorGrid.ActualWidth / 2, unitLength * b.beat);
                l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.GridBookmark.Colour);
                l.StrokeThickness = Const.Editor.GridBookmark.Thickness;
                l.Opacity = Const.Editor.GridBookmark.Opacity;
                Canvas.SetRight(l, 0);
                Canvas.SetBottom(l, 0);
                bookmarkCanvas.Children.Add(l);

                var txtBlock = new Label();
                txtBlock.Foreground = Brushes.White;
                txtBlock.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.GridBookmark.NameColour);
                txtBlock.Background.Opacity = Const.Editor.GridBookmark.Opacity;
                txtBlock.Content = b.name;
                txtBlock.FontSize = Const.Editor.GridBookmark.NameSize;
                txtBlock.Padding = new Thickness(Const.Editor.GridBookmark.NamePadding);
                txtBlock.FontWeight = FontWeights.Bold;
                txtBlock.Opacity = 1.0;
                //txtBlock.IsReadOnly = true;
                txtBlock.Cursor = Cursors.Hand;
                Canvas.SetRight(txtBlock, 0);
                Canvas.SetBottom(txtBlock, 0.75 * Const.Editor.GridBookmark.Thickness);
                txtBlock.MouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                    e.Handled = true;
                });
                txtBlock.MouseLeftButtonUp += new MouseButtonEventHandler((src, e) => {
                    sliderSongProgress.Value = b.beat / globalBPM * 60000;
                    navMouseDown = false;
                    e.Handled = true;
                });
                txtBlock.PreviewMouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                    currentlyDraggingMarker = bookmarkCanvas;

                    currentlyDraggingBookmark = b;
                    currentlyDraggingBPMChange = null;
                    markerDragOffset = e.GetPosition(bookmarkCanvas).Y;
                    imgPreviewNote.Visibility = Visibility.Hidden;
                    EditorGrid.CaptureMouse();
                    e.Handled = true;
                });
                txtBlock.MouseDown += new MouseButtonEventHandler((src, e) => {
                    if (!(e.ChangedButton == MouseButton.Middle)) {
                        return;
                    }
                    var res = MessageBox.Show("Are you sure you want to delete this bookmark?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes) {
                        mapEditor.RemoveBookmark(b);
                    }
                });
                txtBlock.MouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                    var txtBox = new TextBox();
                    txtBox.Text = b.name;
                    txtBox.FontSize = Const.Editor.GridBookmark.NameSize;
                    Canvas.SetRight(txtBox, Const.Editor.GridBookmark.NamePadding);
                    Canvas.SetBottom(txtBox, Canvas.GetBottom(bookmarkCanvas) + Const.Editor.GridBookmark.NamePadding);
                    txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                        if (txtBox.Text != "") {
                            mapEditor.RenameBookmark(b, txtBox.Text);
                        }
                        EditorGrid.Children.Remove(txtBox);
                    });
                    txtBox.KeyDown += new KeyEventHandler((src, e) => {
                        if (e.Key == Key.Escape || e.Key == Key.Enter) {
                            Keyboard.ClearFocus();
                            Keyboard.Focus(this);
                        }
                    });

                    EditorGrid.Children.Add(txtBox);
                    txtBox.Focus();
                    txtBox.SelectAll();

                    e.Handled = true;
                });
                bookmarkCanvas.Children.Add(txtBlock);

                
                EditorGrid.Children.Add(bookmarkCanvas);
            }
        }
        private void DrawEditorGridBPMChanges() {
            string FormatRelativeBPMChange(BPMChange b, BPMChange prev) {
                if (b.BPM != prev.BPM && b.gridDivision != prev.gridDivision) {
                    return $"{b.BPM} BPM\n1/{b.gridDivision} beat";
                } else if (b.BPM != prev.BPM) {
                    return $"{b.BPM} BPM";
                } else if (b.gridDivision != prev.gridDivision) {
                    return $"1/{b.gridDivision} beat";
                } else {
                    return $"Timing\nOffset";
                }
            }
            Label makeBPMChangeLabel(string content) {
                var label = new Label();
                label.Foreground = Brushes.White;
                label.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.BPMChange.NameColour);
                label.Background.Opacity = Const.Editor.BPMChange.Opacity;
                label.Content = content;
                label.FontSize = Const.Editor.BPMChange.NameSize;
                label.Padding = new Thickness(Const.Editor.BPMChange.NamePadding);
                label.FontWeight = FontWeights.Bold;
                label.Opacity = 1.0;
                label.Cursor = Cursors.Hand;
                return label;
            }
            BPMChange prev = new BPMChange(0, globalBPM, editorGridDivision);
            foreach (BPMChange b in mapEditor.bpmChanges) {
                Canvas bpmChangeCanvas = new();
                Canvas bpmChangeFlagCanvas = new();

                var line = makeLine(EditorGrid.ActualWidth / 2, unitLength * b.globalBeat);
                line.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.BPMChange.Colour);
                line.StrokeThickness = Const.Editor.BPMChange.Thickness;
                line.Opacity = Const.Editor.BPMChange.Opacity;
                Canvas.SetBottom(line, 0);
                bpmChangeCanvas.Children.Add(line);

                var divLabel = makeBPMChangeLabel($"1/{b.gridDivision} beat");
                divLabel.PreviewMouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                    isEditingMarker = true;
                    var txtBox = new TextBox();
                    txtBox.Text = b.gridDivision.ToString();
                    txtBox.FontSize = Const.Editor.BPMChange.NameSize;
                    Canvas.SetLeft(txtBox, 12);
                    Canvas.SetBottom(txtBox, line.StrokeThickness + 2);
                    txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                        int div;
                        if (int.TryParse(txtBox.Text, out div) && Helper.DoubleRangeCheck(div, 1, Const.Editor.GridDivisionMax)) {
                            mapEditor.RemoveBPMChange(b, false);
                            b.gridDivision = div;
                            mapEditor.AddBPMChange(b);
                        }
                        isEditingMarker = false;
                        this.Cursor = Cursors.Arrow;
                        canvasNavInputBox.Children.Remove(txtBox);
                    });
                    txtBox.KeyDown += new KeyEventHandler((src, e) => {
                        if (e.Key == Key.Escape || e.Key == Key.Enter) {
                            Keyboard.ClearFocus();
                            Keyboard.Focus(this);
                        }
                    });

                    bpmChangeCanvas.Children.Add(txtBox);
                    txtBox.Focus();
                    txtBox.SelectAll();

                    e.Handled = true;
                });
                Canvas.SetBottom(divLabel, line.StrokeThickness);
                bpmChangeFlagCanvas.Children.Add(divLabel);

                var bpmLabel = makeBPMChangeLabel($"{b.BPM} BPM");
                bpmLabel.PreviewMouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                    isEditingMarker = true;
                    var txtBox = new TextBox();
                    txtBox.Text = b.BPM.ToString();
                    txtBox.FontSize = Const.Editor.BPMChange.NameSize;
                    Canvas.SetLeft(txtBox, 2);
                    Canvas.SetBottom(txtBox, line.StrokeThickness + 22);
                    txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                        double BPM;
                        if (double.TryParse(txtBox.Text, out BPM) && BPM > 0) {
                            mapEditor.RemoveBPMChange(b, false);
                            b.BPM = BPM;
                            mapEditor.AddBPMChange(b);
                        }
                        isEditingMarker = false;
                        this.Cursor = Cursors.Arrow;
                        canvasNavInputBox.Children.Remove(txtBox);
                    });
                    txtBox.KeyDown += new KeyEventHandler((src, e) => {
                        if (e.Key == Key.Escape || e.Key == Key.Enter) {
                            Keyboard.ClearFocus();
                            Keyboard.Focus(this);
                        }
                    });

                    bpmChangeCanvas.Children.Add(txtBox);
                    txtBox.Focus();
                    txtBox.SelectAll();

                    e.Handled = true;
                });
                Canvas.SetBottom(bpmLabel, line.StrokeThickness + 20);
                bpmChangeFlagCanvas.Children.Add(bpmLabel);

                bpmChangeFlagCanvas.PreviewMouseLeftButtonDown += new MouseButtonEventHandler((src, e) => { 
                    currentlyDraggingMarker = bpmChangeCanvas;
                    currentlyDraggingBPMChange = b;
                    currentlyDraggingBookmark = null;
                    markerDragOffset = e.GetPosition(bpmChangeCanvas).Y;
                    imgPreviewNote.Visibility = Visibility.Hidden;
                    EditorGrid.CaptureMouse();
                    e.Handled = true;
                });
                bpmChangeFlagCanvas.PreviewMouseDown += new MouseButtonEventHandler((src, e) => {
                    if (!(e.ChangedButton == MouseButton.Middle)) {
                        return;
                    }
                    var res = MessageBox.Show("Are you sure you want to delete this timing change?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes) {
                        mapEditor.RemoveBPMChange(b);
                    }
                    e.Handled = true;
                 });
                Canvas.SetBottom(bpmChangeFlagCanvas, 0);
                bpmChangeCanvas.Children.Add(bpmChangeFlagCanvas);

                Canvas.SetLeft(bpmChangeCanvas, 0);
                Canvas.SetBottom(bpmChangeCanvas, unitLength * b.globalBeat + unitHeight / 2);
                EditorGrid.Children.Add(bpmChangeCanvas);

                prev = b;
            }
        }
        internal void UndrawEditorNotes(List<Note> notes) {
            foreach (Note n in notes) {
                var nUid = Helper.UidGenerator(n);
                foreach (UIElement u in EditorGridNoteCanvas.Children) {
                    if (u.Uid == nUid) {
                        EditorGridNoteCanvas.Children.Remove(u);
                        break;
                    }
                }
            }
        }
        internal void UndrawEditorNotes(Note n) {
            UndrawEditorNotes(new List<Note>() { n });
        }
        internal void HighlightEditorNotes(List<Note> notes) {
            foreach (Note n in notes) {
                foreach (UIElement e in EditorGridNoteCanvas.Children) {
                    if (e.Uid == Helper.UidGenerator(n)) {
                        var img = (Image)e;
                        img.Source = RuneForBeat(n.beat, true);
                    }
                }
            }
        }
        internal void HighlightEditorNotes(Note n) {
            HighlightEditorNotes(new List<Note>() { n });
        }
        internal void UnhighlightEditorNotes(List<Note> notes) {
            foreach (Note n in notes) {
                foreach (UIElement e in EditorGridNoteCanvas.Children) {
                    if (e.Uid == Helper.UidGenerator(n)) {
                        var img = (Image)e;
                        img.Source = RuneForBeat(n.beat);
                    }
                }
            }
        }
        internal void UnhighlightEditorNotes(Note n) {
            UnhighlightEditorNotes(new List<Note>() { n });
        }
        internal void DrawNavBookmarks() {
            canvasBookmarks.Children.Clear();
            canvasBookmarkLabels.Children.Clear();
            foreach (Bookmark b in mapEditor.bookmarks) {
                var l = makeLine(borderNavWaveform.ActualWidth, borderNavWaveform.ActualHeight * (1 - 60000 * b.beat / (globalBPM * sliderSongProgress.Maximum)));
                l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.NavBookmark.Colour);
                l.StrokeThickness = Const.Editor.NavBookmark.Thickness;
                l.Opacity = Const.Editor.NavBookmark.Opacity;
                canvasBookmarks.Children.Add(l);

                var txtBlock = CreateBookmarkLabel(b);
                canvasBookmarkLabels.Children.Add(txtBlock);
            }
        }

        // helper functions
        private void InitComboEnvironment() {
            foreach (var name in Const.BeatmapDefaults.EnvironmentNames) {
                //if (name == "DefaultEnvironment") {
                //    comboEnvironment.Items.Add(Constants.BeatmapDefaults.DefaultEnvironmentAlias);
                //} else {
                comboEnvironment.Items.Add(name);
                //}
            }
        }
        private void InitDragSelectBorder() {
            editorDragSelectBorder.BorderBrush = Brushes.Black;
            editorDragSelectBorder.BorderThickness = new Thickness(2);
            editorDragSelectBorder.Background = Brushes.LightBlue;
            editorDragSelectBorder.Opacity = 0.5;
            editorDragSelectBorder.Visibility = Visibility.Hidden;
        }
        private void InitPreviewNote() {
            imgPreviewNote.Opacity = 0;
            imgPreviewNote.Width = unitLength;
            imgPreviewNote.Height = unitHeight;
            EditorGridNoteCanvas.Children.Add(imgPreviewNote);
        }
        private void InitNavMouseoverLine() {
            // already initialised in the XAML, for the most part
            lineSongMouseover.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.NavPreviewLine.Colour);
            lineSongMouseover.StrokeThickness = Const.Editor.NavPreviewLine.Thickness;
        }
        private void InitGridMouseoverLine() {
            lineGridMouseover.Opacity = 0;
            lineGridMouseover.X1 = 0;
            lineGridMouseover.SetBinding(Line.X2Property, new Binding("ActualWidth") { Source = EditorGrid });
            lineGridMouseover.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.GridPreviewLine.Colour);
            lineGridMouseover.StrokeThickness = Const.Editor.GridPreviewLine.Thickness;
            lineGridMouseover.Visibility = Visibility.Hidden;
            EditorGrid.Children.Add(lineGridMouseover);
        }
        private void InitEditorGridNoteCanvas() {
            EditorGridNoteCanvas.SetBinding(Canvas.WidthProperty, new Binding("ActualWidth") { Source = EditorGrid });
            EditorGridNoteCanvas.SetBinding(Canvas.HeightProperty, new Binding("ActualHeight") { Source = EditorGrid });
        }
        private void InitDrummer(string basePath, bool isPanned) {
            drummer = new ParallelAudioPlayer(
                basePath, 
                Const.Audio.NotePlaybackStreams, 
                Const.Audio.WASAPILatencyTarget,
                isPanned,
                float.Parse(userSettings.GetValueForKey(Const.UserSettings.DefaultNoteVolume))
            );
            drummer.ChangeVolume(sliderDrumVol.Value);
            noteScanner?.SetAudioPlayer(drummer);
        }
        private double BeatForPosition(double position, bool snap) {
            double userOffsetBeat = editorGridOffset * globalBPM / 60;
            double userOffset = userOffsetBeat * unitLength;
            var pos = EditorGrid.ActualHeight - position - unitHeight / 2;
            double gridLength = unitLength / editorGridDivision;
            // check if mouse position would correspond to a negative row index
            double snapped = 0;
            double unsnapped = 0;
            if (pos >= userOffset) {
                unsnapped = (pos - userOffset) / unitLength;
                int binarySearch = gridlines.BinarySearch(unsnapped);
                if (binarySearch > 0) {
                    return gridlines[binarySearch];
                }
                int indx1 = -binarySearch - 1;
                int indx2 = Math.Max(0, indx1 - 1);
                snapped = (gridlines[indx1] - unsnapped) < (unsnapped - gridlines[indx2]) ? gridlines[indx1] : gridlines[indx2];
            }
            return snap ? snapped : unsnapped;
        }
        private double BeatForRow(double row) {
            double userOffsetBeat = globalBPM * editorGridOffset / 60;
            return row / (double)editorGridDivision + userOffsetBeat;
        }
        private void ResnapAllNotes(double newOffset) {
            var offsetDelta = newOffset - editorGridOffset;
            var beatOffset = globalBPM / 60 * offsetDelta;
            for (int i = 0; i < mapEditor.notes.Count; i++) {
                Note n = new Note();
                n.beat = mapEditor.notes[i].beat + beatOffset;
                n.col = mapEditor.notes[i].col;
                mapEditor.notes[i] = n;
            }
            // invalidate selections
            mapEditor.UnselectAllNotes();
        }
        private int UpdateMedalDistance(int medal, string strDist) {
            if (strDist.Trim() == "") {
                beatMap.SetMedalDistanceForMap(currentDifficulty, medal, 0);
                return 0;
            }
            int prevDist = (int)beatMap.GetMedalDistanceForMap(currentDifficulty, medal);
            int dist;
            if (int.TryParse(strDist, out dist) && dist >= 0) {
                beatMap.SetMedalDistanceForMap(currentDifficulty, medal, dist);
            } else {
                MessageBox.Show($"The distance must be a non-negative integer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                dist = prevDist;
            }
            return dist;
        }
        private void UpdateDragSelection(Point newPoint) {
            Point p1;
            p1.X = Math.Min(newPoint.X, editorDragSelectStart.X);
            p1.Y = Math.Min(newPoint.Y, editorDragSelectStart.Y);
            Point p2;
            p2.X = Math.Max(newPoint.X, editorDragSelectStart.X);
            p2.Y = Math.Max(newPoint.Y, editorDragSelectStart.Y);
            Vector delta = p2 - p1;
            Canvas.SetLeft(editorDragSelectBorder, p1.X);
            Canvas.SetTop(editorDragSelectBorder, p1.Y);
            editorDragSelectBorder.Width = delta.X;
            editorDragSelectBorder.Height = delta.Y;
        }
        private int ColFromPos(double pos) {
            // calculate horizontal element
            var subLength = (pos - EditorMarginGrid.Margin.Left) / unitSubLength;
            int col = -1;
            if (0 <= subLength && subLength <= 4.5) {
                col = 0;
            } else if (4.5 <= subLength && subLength <= 8.5) {
                col = 1;
            } else if (8.5 <= subLength && subLength <= 12.5) {
                col = 2;
            } else if (12.5 <= subLength && subLength <= 17.0) {
                col = 3;
            }
            return col;
        }
        private BitmapImage RuneForBeat(double beat, bool highlight = false) {
            // find most recent BPM change
            double recentBPMChange = 0;
            double recentBPM = globalBPM;
            foreach (var bc in mapEditor.bpmChanges) {
                if (Helper.DoubleApproxGreaterEqual(beat, bc.globalBeat)) {
                    recentBPMChange = bc.globalBeat;
                    recentBPM = bc.BPM;
                } else {
                    break;
                }
            }
            double beatNormalised = beat - recentBPMChange;
            beatNormalised /= globalBPM / recentBPM;
            beatNormalised -= (int)beatNormalised;
            return Helper.BitmapImageForBeat(beatNormalised, highlight);
        }
        private Label CreateBookmarkLabel(Bookmark b) {
            var offset = borderNavWaveform.ActualHeight * (1 - 60000 * b.beat / (globalBPM * sliderSongProgress.Maximum));
            var txtBlock = new Label();
            txtBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.NavBookmark.NameColour);

            txtBlock.Content = b.name;
            txtBlock.FontSize = Const.Editor.NavBookmark.NameSize;
            txtBlock.Padding = new Thickness(Const.Editor.NavBookmark.NamePadding);
            txtBlock.FontWeight = FontWeights.Bold;
            txtBlock.Opacity = Const.Editor.NavBookmark.Opacity;
            //txtBlock.IsReadOnly = true;
            txtBlock.Cursor = Cursors.Hand;
            Canvas.SetBottom(txtBlock, borderNavWaveform.ActualHeight - offset);
            txtBlock.MouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                e.Handled = true;
            });
            txtBlock.MouseLeftButtonUp += new MouseButtonEventHandler((src, e) => {
                sliderSongProgress.Value = b.beat / globalBPM * 60000;
                navMouseDown = false;
                e.Handled = true;
            });
            txtBlock.MouseDown += new MouseButtonEventHandler((src, e) => {
                if (!(e.ChangedButton == MouseButton.Middle)) {
                    return;
                }
                var res = MessageBox.Show("Are you sure you want to delete this bookmark?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes) {
                    mapEditor.RemoveBookmark(b);
                }
            });
            txtBlock.MouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                var txtBox = new TextBox();
                txtBox.Text = b.name;
                txtBox.FontSize = Const.Editor.NavBookmark.NameSize;
                Canvas.SetBottom(txtBox, borderNavWaveform.ActualHeight - offset);
                txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                    if (txtBox.Text != "") {
                        mapEditor.RenameBookmark(b, txtBox.Text);
                    }
                    canvasNavInputBox.Children.Remove(txtBox);
                });
                txtBox.KeyDown += new KeyEventHandler((src, e) => {
                    if (e.Key == Key.Escape || e.Key == Key.Enter) {
                        Keyboard.ClearFocus();
                        Keyboard.Focus(this);
                    }
                });
                
                canvasNavInputBox.Children.Add(txtBox);
                txtBox.Focus();
                txtBox.SelectAll();

                e.Handled = true;
            });
            return txtBlock;
        }
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
        private void UpdateDifficultyLabels() {
            var difficultyLabels = new List<Label>() { lblDifficultyRank1, lblDifficultyRank2, lblDifficultyRank3 };
            for (int i = 0; i < 3; i++) {
                try {
                    difficultyLabels[i].Content = beatMap.GetValueForMap(i, "_difficultyRank");
                } catch {
                    Trace.WriteLine($"INFO: difficulty index {i} not found");
                    difficultyLabels[i].Content = "";
                }
            }
        }
        private string RagnarockMapFolder() {
            var index = int.Parse(userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationIndex));
            if (index == 0) {
                return Helper.DefaultRagnarockMapPath();
            } else {
                return Path.Combine(userSettings.GetValueForKey(Const.UserSettings.MapSaveLocationPath), Const.Program.GameInstallRelativeMapFolder);
            }
        }
        private Line makeLine(double width, double offset) {
            var l = new Line();
            l.X1 = 0;
            l.X2 = width;
            l.Y1 = offset;
            l.Y2 = offset;
            return l;
        }
        internal void RefreshBPMChanges() {
            var win = Helper.GetFirstWindow<ChangeBPMWindow>();
            if (win != null) {
                ((ChangeBPMWindow)win).RefreshBPMChanges();
            }
        }

    }
}
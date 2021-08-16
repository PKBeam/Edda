using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
using System.Drawing.Imaging;
using System.Reactive.Linq;
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
        List<double> gridLines = new();
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
        VorbisWaveformVisualiser audioWaveform;
        VorbisWaveformVisualiser navWaveform;
        bool editorShowWaveform = false;

        // -- for note placement
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
        int editorGridDivision;
        double editorGridSpacing;
        double editorGridOffset;
        double editorDrawRangeLower = 0;
        double editorDrawRangeHigher = 0;

        // variables used to handle drum hits on a separate thread
        NoteScanner noteScanner;

        // -- audio playback
        int editorAudioLatency; // ms
        SampleChannel songChannel;
        VorbisWaveReader songStream;
        WasapiOut songPlayer;
        DrumPlayer drummer;

        public MainWindow() {

            InitializeComponent();

            // disable parts of UI, as no map is loaded
            imgSaved.Opacity = 0;
            imgWaveformVertical.Opacity = Const.Editor.NavWaveformOpacity;
            RenderOptions.SetBitmapScalingMode(imgAudioWaveform, BitmapScalingMode.NearestNeighbor);
            lineSongMouseover.Opacity = 0;
            DisableUI();

            autosaveTimer = new System.Timers.Timer(1000 * Const.Editor.AutosaveInterval);
            autosaveTimer.Enabled = false;
            autosaveTimer.Elapsed += (source, e) => {
                SaveBeatmap();
            };
            discordClient = new DiscordClient(this);
            LoadSettingsFile();

            // init border
            InitDragSelectBorder();

            // load editor preview note
            InitPreviewNote();

            // init environment combobox
            InitComboEnvironment();

            //debounce grid redrawing on resize
            Observable
            .FromEventPattern<SizeChangedEventArgs>(scrollEditor, nameof(SizeChanged))
            .Throttle(TimeSpan.FromMilliseconds(Const.Editor.DrawDebounceInterval))
            .Subscribe(eventPattern => 
                AppMainWindow.Dispatcher.Invoke(() =>
                    ScrollEditor_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                )
            );

            Observable
            .FromEventPattern<SizeChangedEventArgs>(borderNavWaveform, nameof(SizeChanged))
            .Throttle(TimeSpan.FromMilliseconds(Const.Editor.DrawDebounceInterval))
            .Subscribe(eventPattern =>
                AppMainWindow.Dispatcher.Invoke(() =>
                    BorderNavWaveform_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
                )
            );
        }

        // UI bindings
        private void AppMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            PromptBeatmapSave();
        }
        private void AppMainWindow_Closed(object sender, EventArgs e) {
            Trace.WriteLine("Closing window...");
            noteScanner?.Stop();
            songPlayer?.Stop();
            songPlayer?.Dispose();
            songStream?.Dispose();
            drummer?.Dispose();
            Application.Current.Shutdown();
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
                if (e.Key == Key.B) {
                    double beat = BeatForPosition(scrollEditor.VerticalOffset + scrollEditor.ActualHeight - unitLengthUnscaled / 2, editorSnapToGrid);
                    if (imgPreviewNote.Opacity > 0) {
                        beat = editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
                    } else if (lineSongMouseover.Opacity > 0) {
                        beat = globalBPM * sliderSongProgress.Maximum / 60000 * (1 - lineSongMouseover.Y1/borderNavWaveform.ActualHeight);
                    }
                    mapEditor.AddBookmark(new Bookmark(beat, Const.Editor.Bookmark.DefaultName));
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
                drummer.PlayDrum(1);
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
                PromptBeatmapSave();

                // clear some stuff
                PauseSong();
                mapEditor.notes.Clear();
            }

            // select folder for map
            var d2 = new CommonOpenFileDialog();
            d2.Title = "Select an empty folder to store your map";
            d2.IsFolderPicker = true;

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
                MessageBox.Show("The specified folder is not empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
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
                PromptBeatmapSave();
                // clear some stuff
                PauseSong();
                mapEditor.notes.Clear();
            }

            // select folder for map
            // TODO: this dialog is sometimes hangs, is there a better way to select a folder?
            var d2 = new CommonOpenFileDialog();
            d2.Title = "Select your map's containing folder";
            d2.IsFolderPicker = true;

            if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
                return;
            }

            // try to load info
            try {
                beatMap = new RagnarockMap(d2.FileName, false);
                LoadSong(); // song file
                LoadCoverImage();
                InitUI(); // cover image file
            } catch (Exception ex) {
                beatMap = null;
                txtSongFileName.Text = "N/A";
                txtCoverFileName.Text = "N/A";

                MessageBox.Show($"An error occured while opening the map:\n{ex.Message}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            discordClient.SetPresence();
        }
        private void BtnSaveMap_Click(object sender, RoutedEventArgs e) {
            BackupAndSaveBeatmap();
            //SaveBeatmap();
        }
        private void BtnBPMFinder_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<BPMCalcWindow>();
            if (win == null) {
                new BPMCalcWindow().Show();
            } else {
                win.Focus();
            }
        }
        private void BtnSettings_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<SettingsWindow>();
            if (win == null) {
                new SettingsWindow(this, userSettings).Show();
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
                new SongPreviewWindow(beatMap.GetPath(), beatMap.PathOf((string)beatMap.GetValue("_songFilename"))).Show();
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
            SwitchDifficultyMap(beatMap.numDifficulties - 1);
            UpdateDifficultyButtonVisibility();
            SortDifficultyMaps();
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
            if (double.TryParse(txtSongBPM.Text, out BPM)) {
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
                    }
                    beatMap.SetValue("_beatsPerMinute", BPM);
                    globalBPM = BPM;
                    noteScanner.bpm = BPM;
                    
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
                new ChangeBPMWindow(this, mapEditor.bpmChanges).Show();
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
        private void TxtArtistName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.SetValue("_songAuthorName", txtArtistName.Text);
        }
        private void TxtMapperName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.SetValue("_levelAuthorName", txtMapperName.Text);
        }
        private void TxtDifficultyNumber_LostFocus(object sender, RoutedEventArgs e) {
            int prevLevel = (int)beatMap.GetValueForMap(currentDifficulty, "_difficultyRank");
            int level;
            if (int.TryParse(txtDifficultyNumber.Text, out level) && Helper.DoubleRangeCheck(level, 1, 10)) {
                beatMap.SetValueForMap(currentDifficulty, "_difficultyRank", level);
                txtDifficultyNumber.Text = level.ToString();
                SortDifficultyMaps();
            } else {
                MessageBox.Show($"The difficulty level must be an integer between 1 and 10.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                level = prevLevel;
                txtDifficultyNumber.Text = level.ToString();
            }
            
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
        private void TxtDistMedal0_LostFocus(object sender, RoutedEventArgs e) {
            txtDistMedal0.Text = UpdateMedalDistance(0, txtDistMedal0.Text).ToString();
        }
        private void TxtDistMedal1_LostFocus(object sender, RoutedEventArgs e) {
            txtDistMedal1.Text = UpdateMedalDistance(1, txtDistMedal1.Text).ToString();
        }
        private void TxtDistMedal2_LostFocus(object sender, RoutedEventArgs e) {
            txtDistMedal2.Text = UpdateMedalDistance(2, txtDistMedal2.Text).ToString();
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
                    DrawEditorGrid();
                }
            } else {
                MessageBox.Show($"The grid division amount must be an integer from 1 to {Const.Editor.GridDivisionMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                div = prevDiv;
            }
            txtGridDivision.Text = div.ToString();
        }
        private void CheckWaveform_Click(object sender, RoutedEventArgs e) {
            editorShowWaveform = (checkWaveform.IsChecked == true);
            if (editorShowWaveform) {
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
                DrawBookmarks();
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
            var mouseTime = sliderSongProgress.Maximum * (1 - mouseY / borderNavWaveform.ActualHeight);
            lineSongMouseover.Y1 = mouseY;
            lineSongMouseover.Y2 = mouseY;
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
        private void ScrollEditor_MouseMove(object sender, MouseEventArgs e) {

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
            double noteX = (1 + 4 * editorMouseGridCol) * unitSubLength;
            // for some reason Canvas.SetLeft(0) doesn't correspond to the leftmost of the canvas, so we need to do some unknown adjustment to line it up
            var unknownNoteXAdjustment = (unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2;

            double userOffsetBeat = editorGridOffset * globalBPM / 60;
            double userOffset = userOffsetBeat * unitLength;
            var mousePos = EditorGrid.ActualHeight - e.GetPosition(EditorGrid).Y - unitHeight / 2;
            double gridLength = unitLength / editorGridDivision;

            // place preview note
            Canvas.SetBottom(imgPreviewNote, editorSnapToGrid ? (editorMouseBeatSnapped * gridLength * editorGridDivision + userOffset) : Math.Max(mousePos, userOffset));
            
            // TODO: what runes should be used with variable BPM?
            imgPreviewNote.Source = RuneForBeat(userOffsetBeat + (editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped));
            Canvas.SetLeft(imgPreviewNote, noteX - unknownNoteXAdjustment);
            
             // update beat display
            lblSelectedBeat.Content = $"Time: {Helper.TimeFormat(editorMouseBeatSnapped * 60 / globalBPM)}, Global Beat: {Math.Round(editorMouseBeatSnapped, 3)} ({Math.Round(editorMouseBeatUnsnapped, 3)})";

            // calculate drag stuff
            if (editorIsDragging) {
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
        private void ScrollEditor_MouseEnter(object sender, MouseEventArgs e) {
                imgPreviewNote.Opacity = Const.Editor.PreviewNoteOpacity;
        }
        private void ScrollEditor_MouseLeave(object sender, MouseEventArgs e) {
            imgPreviewNote.Opacity = 0;
            lblSelectedBeat.Content = "";
        }
        private void ScrollEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            editorMouseDown = true;
            editorDragSelectStart = e.GetPosition(EditorGrid);
            editorSelBeatStart = editorMouseBeatUnsnapped;
            editorSelColStart = editorMouseGridCol;
            EditorGrid.CaptureMouse();
        }
        private void ScrollEditor_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { 
            if (editorIsDragging) {
                editorDragSelectBorder.Visibility = Visibility.Hidden;
                imgPreviewNote.Visibility = Visibility.Visible;
                // calculate new selections
                List<Note> newSelection = new List<Note>();
                double startBeat = editorSelBeatStart;
                double endBeat = editorMouseBeatUnsnapped;
                foreach (Note n in mapEditor.notes) {
                    // minor optimisation
                    if (n.beat > Math.Max(startBeat, endBeat)) {
                        break;
                    }
                    // check range
                    if (Helper.DoubleRangeCheck(n.beat, startBeat, endBeat) && Helper.DoubleRangeCheck(n.col, editorSelColStart, editorMouseGridCol)) {
                        newSelection.Add(n);
                    }
                }
                mapEditor.SelectNewNotes(newSelection);
            } else if (editorMouseDown) {
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
                    drummer.PlayDrum(1);
                }
            }
            EditorGrid.ReleaseMouseCapture();
            editorIsDragging = false;
            editorMouseDown = false;
        }
        private void ScrollEditor_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {

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

            sliderSongVol.Value = Const.Audio.DefaultSongVolume;
            sliderDrumVol.Value = Const.Audio.DefaultNoteVolume;

            // map settings
            txtSongName.Text   = (string)beatMap.GetValue("_songName");
            txtArtistName.Text = (string)beatMap.GetValue("_songAuthorName");
            txtMapperName.Text = (string)beatMap.GetValue("_levelAuthorName");
            txtSongBPM.Text    = (string)beatMap.GetValue("_beatsPerMinute");
            txtSongOffset.Text = (string)beatMap.GetValue("_songTimeOffset");
            
            globalBPM = Helper.DoubleParseInvariant((string)beatMap.GetValue("_beatsPerMinute"));

            comboEnvironment.SelectedIndex = Const.BeatmapDefaults.EnvironmentNames.IndexOf((string)beatMap.GetValue("_environmentName"));

            // init difficulty-specific UI 
            SwitchDifficultyMap(0, false, false);

            // enable UI parts
            EnableUI();

            UpdateEditorGridHeight();
            scrollEditor.ScrollToBottom();
            DrawEditorNavWaveform();
        }
        private void EnableUI() {
            btnSaveMap.IsEnabled = true;
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
            comboEnvironment.IsEnabled = true;
            btnPickSong.IsEnabled = true;
            btnMakePreview.IsEnabled = true;
            btnPickCover.IsEnabled = true;
            sliderSongVol.IsEnabled = true;
            sliderDrumVol.IsEnabled = true;
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
            comboEnvironment.IsEnabled = false;
            btnPickSong.IsEnabled = false;
            btnMakePreview.IsEnabled = false;
            btnPickCover.IsEnabled = false;
            sliderSongVol.IsEnabled = false;
            sliderDrumVol.IsEnabled = false;
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

            string GetBackupPath(int i, string fileName, bool create=true) {
                string backupSubFolder = System.IO.Path.Combine(beatMap.PathOf(Const.Program.BackupPath), $"backup{i}");
                if (!Directory.Exists(backupSubFolder) && create) {
                    Directory.CreateDirectory(backupSubFolder);
                }
                return System.IO.Path.Combine(backupSubFolder, $"{fileName}.dat");
            }

            // create backup directory if it doesnt exist
            if (!Directory.Exists(beatMap.PathOf(Const.Program.BackupPath))) {
                Directory.CreateDirectory(beatMap.PathOf(Const.Program.BackupPath));
            }

            List<string> files = new(Const.BeatmapDefaults.DifficultyNames);
            files.Add("info");

            // shift backup files
            for (int i = Const.Program.MaxBackups - 1; i > 0; i--) {
                foreach (var diffName in files) {
                    string oldSavePath = GetBackupPath(i, diffName, false);
                    if (File.Exists(oldSavePath)) {
                        string newSavePath = GetBackupPath(i + 1, diffName);
                        File.Move(oldSavePath, newSavePath, true);
                    }
                }
            }

            // save beatmap
            SaveBeatmap();

            // make new backup file
            foreach (var diffName in files) {
                string recentSavePath = beatMap.PathOf($"{diffName}.dat");
                string backupPath = GetBackupPath(1, diffName);
                if (File.Exists(recentSavePath)) {
                    File.Copy(recentSavePath, backupPath);
                }
            }

            // delete unused backup folders
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
        internal void LoadSettingsFile() {
            userSettings = new UserSettings(Const.Program.SettingsFile);

            if (!int.TryParse(userSettings.GetValueForKey(Const.UserSettings.EditorAudioLatency), out editorAudioLatency)) {
                userSettings.SetValueForKey(Const.UserSettings.EditorAudioLatency, Const.DefaultUserSettings.AudioLatency);
                editorAudioLatency = Const.DefaultUserSettings.AudioLatency;
            }

            try {
                InitDrummer(userSettings.GetValueForKey(Const.UserSettings.DrumSampleFile));
            } catch {
                userSettings.SetValueForKey(Const.UserSettings.DrumSampleFile, Const.DefaultUserSettings.DrumSampleFile);
                InitDrummer(Const.DefaultUserSettings.DrumSampleFile);
            }

            if (userSettings.GetValueForKey(Const.UserSettings.EnableDiscordRPC) == null) {
                userSettings.SetValueForKey(Const.UserSettings.EnableDiscordRPC, Const.DefaultUserSettings.EnableDiscordRPC);
            }
            SetDiscordRPC(userSettings.GetBoolForKey(Const.UserSettings.EnableDiscordRPC));

            if (userSettings.GetValueForKey(Const.UserSettings.EnableAutosave) == null) {
                userSettings.SetValueForKey(Const.UserSettings.EnableAutosave, Const.DefaultUserSettings.EnableAutosave);
            }
            autosaveTimer.Enabled = userSettings.GetBoolForKey(Const.UserSettings.EnableAutosave);

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

            if (prevPath != beatMap.PathOf(newFile)) {
                if (File.Exists(prevPath)) {
                    File.Delete(prevPath);
                }

                File.Copy(d.FileName, beatMap.PathOf(newFile));
                beatMap.SetValue("_coverImageFilename", newFile);
                SaveBeatmap();
            }
            LoadCoverImage();
        }
        private void LoadCoverImage() {
            var fileName = (string)beatMap.GetValue("_coverImageFilename");
            if (fileName == "") {
                imgCover.Source = null;
                txtCoverFileName.Text = "N/A";
                borderImgCover.BorderThickness = new(0);
            } else {
                BitmapImage b = Helper.BitmapGenerator(new Uri(beatMap.PathOf(fileName)));
                imgCover.Source = b;
                txtCoverFileName.Text = fileName;
                borderImgCover.BorderThickness = new(2);
            }
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
            noteScanner = new NoteScanner(this, globalBPM, drummer);

            txtDifficultyNumber.Text = (string)beatMap.GetValueForMap(indx, "_difficultyRank");
            txtNoteSpeed.Text = (string)beatMap.GetValueForMap(indx, "_noteJumpMovementSpeed");

            txtDistMedal0.Text = beatMap.GetMedalDistanceForMap(indx, 0).ToString();
            txtDistMedal1.Text = beatMap.GetMedalDistanceForMap(indx, 1).ToString();
            txtDistMedal2.Text = beatMap.GetMedalDistanceForMap(indx, 2).ToString();

            txtGridOffset.Text = (string)beatMap.GetCustomValueForMap(indx, "_editorOffset");
            txtGridSpacing.Text = (string)beatMap.GetCustomValueForMap(indx, "_editorGridSpacing");
            txtGridDivision.Text = (string)beatMap.GetCustomValueForMap(indx, "_editorGridDivision");

            // set internal values
            editorGridDivision = int.Parse(txtGridDivision.Text);
            editorGridSpacing = Helper.DoubleParseInvariant(txtGridSpacing.Text);
            editorGridOffset = Helper.DoubleParseInvariant(txtGridOffset.Text);

            EnableDifficultyButtons();
            DrawBookmarks();
            if (redrawGrid) {
                DrawEditorGrid();
            }
        }
        private void SortDifficultyMaps() {
            // bubble sort
            bool swap;
            int diff = currentDifficulty;
            do {
                swap = false;
                for (int i = 0; i < beatMap.numDifficulties - 1; i++) {
                    int lowDiff = (int)beatMap.GetValueForMap(i, "_difficultyRank");
                    int highDiff = (int)beatMap.GetValueForMap(i + 1, "_difficultyRank");
                    if (lowDiff > highDiff) {
                        SwapDifficultyMaps(i, i + 1);
                        if (diff == i) {
                            diff++;
                        } else if (diff == i + 1) {
                            diff--;
                        }
                        swap = true;
                    }
                }
            } while (swap);
            SwitchDifficultyMap(diff);
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
            var songFile = System.IO.Path.GetFileName(d.FileName);
            var prevSongFile = (string)beatMap.GetValue("_songFilename");
            if (d.FileName == beatMap.PathOf(prevSongFile)) {
                return false;
            }

            // update beatmap data
            UnloadSong();
            beatMap.SetValue("_songApproximativeDuration", (int)vorbisStream.TotalTime.TotalSeconds + 1);
            beatMap.SetValue("_songFilename", songFile);
            SaveBeatmap();
            vorbisStream.Dispose();

            // do file I/O
            File.Delete(beatMap.PathOf(prevSongFile));
            File.Copy(d.FileName, beatMap.PathOf(songFile));

            LoadSong();

            return true;
        }
        private void LoadSong() {

            var songPath = beatMap.PathOf((string)beatMap.GetValue("_songFilename"));
            songStream = new VorbisWaveReader(songPath);
            songChannel = new SampleChannel(songStream);
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

            // redraw waveforms
            if (editorShowWaveform) { 
                DrawEditorWaveform();
            }
            DrawEditorNavWaveform();

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
            } catch {
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

            noteScanner.Start((int)(sliderSongProgress.Value - editorAudioLatency), new List<Note>(mapEditor.notes));

            // play song
            songPlayer.Play();
        }
        internal void PauseSong() {
            if (!songIsPlaying) {
                return;
            }
            songIsPlaying = false;
            imgPlayerButton.Source = Helper.BitmapGenerator("playButton.png");

            // stop note scaning
            noteScanner.Stop();

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
        internal void DrawEditorGrid() {

            

            if (beatMap == null) {
                return;
            }

            EditorGrid.Children.Clear();
            
            DateTime start = DateTime.Now;

            if (editorShowWaveform && EditorGrid.Height - scrollEditor.ActualHeight > 0) {
                DrawEditorWaveform();
            }

            DateTime beforeLines = DateTime.Now;

            DrawEditorGridLines();

            TimeSpan lineDraw = DateTime.Now - beforeLines;
            DateTime beforeNotes = DateTime.Now;

            DrawEditorNotes(mapEditor.notes);

            TimeSpan noteDraw = DateTime.Now - beforeNotes;

            // change editor preview note size
            imgPreviewNote.Width = unitLength;
            imgPreviewNote.Height = unitHeight;
            EditorGrid.Children.Add(imgPreviewNote);

            EditorGrid.Children.Add(editorDragSelectBorder);

            Trace.WriteLine($"INFO: Redrew editor grid in {(DateTime.Now - start).TotalSeconds} seconds. (lines: {lineDraw.TotalSeconds}, notes: {noteDraw.TotalSeconds})");
        }
        private void DrawEditorWaveform() {
            ResizeEditorWaveform();
            double height = EditorGrid.Height - scrollEditor.ActualHeight;
            double width = EditorGrid.ActualWidth * Const.Editor.Waveform.Width;
            CreateEditorWaveform(height, width);
        }
        private void ResizeEditorWaveform() {
            
            EditorGrid.Children.Remove(imgAudioWaveform);
            imgAudioWaveform.Height = EditorGrid.Height - scrollEditor.ActualHeight;
            imgAudioWaveform.Width = EditorGrid.ActualWidth;
            Canvas.SetBottom(imgAudioWaveform, unitHeight / 2);
            EditorGrid.Children.Insert(0, imgAudioWaveform);
        }
        private void CreateEditorWaveform(double height, double width) {
            Task.Run(() => {
                DateTime before = DateTime.Now;
                ImageSource bmp = audioWaveform.Draw(height, width);
                Trace.WriteLine($"INFO: Drew big waveform in {(DateTime.Now - before).TotalSeconds} sec");
                if (bmp != null && editorShowWaveform) {
                    this.Dispatcher.Invoke(() => {
                        imgAudioWaveform.Source = bmp;
                        ResizeEditorWaveform();
                    });
                }
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
        private void DrawEditorGridLines() {
            // helper function for creating gridlines
            Line makeGridLine(double offset, bool isMajor = false) {
                var l = new Line();
                l.X1 = 0;
                l.X2 = EditorGrid.ActualWidth;
                l.Y1 = offset;
                l.Y2 = offset;
                l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(
                    isMajor ? Const.Editor.MajorGridlineColour : Const.Editor.MinorGridlineColour)
                ;
                l.StrokeThickness = isMajor ? Const.Editor.MajorGridlineThickness : Const.Editor.MinorGridlineThickness;
                Canvas.SetBottom(l, offset + unitHeight / 2);
                return l;
            }

            gridLines.Clear();

            // calculate grid offset
            double userOffset = editorGridOffset * globalBPM / 60 * unitLength;

            // the position to place gridlines, starting at the user-specified grid offset
            var offset = userOffset;

            var localBPM = globalBPM;
            var localGridDiv = editorGridDivision;

            // draw gridlines
            int counter = 0;
            int bpmChangeCounter = 0;
            while (offset <= EditorGrid.Height) {

                // add new gridline
                var l = makeGridLine(offset, counter % localGridDiv == 0);
                EditorGrid.Children.Add(l);
                gridLines.Add((offset - userOffset)/unitLength);

                offset += globalBPM/localBPM * unitLength / localGridDiv;
                counter++;

                // check for BPM change
                if (bpmChangeCounter < mapEditor.bpmChanges.Count && Helper.DoubleApproxGreaterEqual((offset - userOffset)/ unitLength, mapEditor.bpmChanges[bpmChangeCounter].globalBeat)) {
                    BPMChange next = mapEditor.bpmChanges[bpmChangeCounter];

                    offset = next.globalBeat * unitLength + userOffset;
                    localBPM = next.BPM;
                    localGridDiv = next.gridDivision;

                    bpmChangeCounter++;
                    counter = 0;
                }           
            }
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
                if (FindName(Helper.NameGenerator(n)) != null) {
                    UnregisterName(Helper.NameGenerator(n));
                }
                RegisterName(Helper.NameGenerator(n), img);

                Canvas.SetBottom(img, noteHeight);
                Canvas.SetLeft(img, noteXOffset);
                EditorGrid.Children.Add(img);
            }
        }
        internal void DrawEditorNotes(Note n) {
            DrawEditorNotes(new List<Note>() { n });
        }
        internal void UndrawEditorNotes(List<Note> notes) {
            foreach (Note n in notes) {
                var nUid = Helper.UidGenerator(n);
                foreach (UIElement u in EditorGrid.Children) {
                    if (u.Uid == nUid) {
                        EditorGrid.Children.Remove(u);
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
                foreach (UIElement e in EditorGrid.Children) {
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
                foreach (UIElement e in EditorGrid.Children) {
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
        internal void DrawBookmarks() {
            canvasBookmarks.Children.Clear();
            canvasBookmarkLabels.Children.Clear();
            foreach (Bookmark b in mapEditor.bookmarks) {
                var l = new Line();
                l.X1 = 0;
                l.X2 = borderNavWaveform.ActualWidth;
                var offset = borderNavWaveform.ActualHeight * (1 - 60000 * b.beat/(globalBPM * sliderSongProgress.Maximum));
                l.Y1 = offset;
                l.Y2 = offset;
                l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.Bookmark.Colour);
                l.StrokeThickness = Const.Editor.Bookmark.Thickness;

                var txtBlock = new Label();
                txtBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.Bookmark.NameColour);
                
                txtBlock.Content = b.name;
                txtBlock.FontSize = Const.Editor.Bookmark.NameSize;
                txtBlock.Padding = new Thickness(Const.Editor.Bookmark.NamePadding);
                txtBlock.FontWeight = FontWeights.Bold;
                txtBlock.Opacity = Const.Editor.Bookmark.Opacity;
                //txtBlock.IsReadOnly = true;
                txtBlock.Cursor = Cursors.Arrow;
                Canvas.SetBottom(txtBlock, borderNavWaveform.ActualHeight - offset);
                txtBlock.MouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                    e.Handled = true;
                });
                txtBlock.MouseLeftButtonUp += new MouseButtonEventHandler((src, e) => {
                    sliderSongProgress.Value = b.beat / globalBPM * 60000;
                    navMouseDown = false;
                    e.Handled = true;
                });
                txtBlock.MouseRightButtonDown += new MouseButtonEventHandler((src, e) => {
                    var res = MessageBox.Show("Are you sure you want to delete this bookmark?", "Confirm Bookmark Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes) {
                        mapEditor.RemoveBookmark(b);
                    }
                });
                txtBlock.MouseDoubleClick += new MouseButtonEventHandler((src, e) => {
                    var txtBox = new TextBox();  
                    txtBox.Text = b.name;
                    txtBox.FontSize = Const.Editor.Bookmark.NameSize;
                    Canvas.SetBottom(txtBox, borderNavWaveform.ActualHeight - offset);
                    txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                        mapEditor.RenameBookmark(b, txtBox.Text);
                        canvasNavInputBox.Children.Remove(txtBox);
                    });
                    txtBox.KeyDown += new KeyEventHandler((src, e) => {
                        if (e.Key == Key.Escape || e.Key == Key.Enter) {
                            Keyboard.ClearFocus();
                        }
                    });

                    canvasNavInputBox.Children.Add(txtBox);
                    txtBox.Focus();
                    txtBox.SelectAll();
                    
                    e.Handled = true;
                });
                canvasBookmarks.Children.Add(l);
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
            EditorGrid.Children.Add(imgPreviewNote);
        }
        private void InitDrummer(string basePath) {
            drummer = new DrumPlayer(basePath, Const.Audio.NotePlaybackStreams, Const.Audio.WASAPILatencyTarget);
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
                int indx1 = -gridLines.BinarySearch(unsnapped) - 1;
                int indx2 = Math.Max(0, indx1 - 1);
                snapped = (gridLines[indx1] - unsnapped) < (unsnapped - gridLines[indx2]) ? gridLines[indx1] : gridLines[indx2];
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
            var subLength = pos / unitSubLength;
            int res = -1;
            if (0 <= subLength && subLength <= 4.5) {
                res = 0;
            } else if (4.5 <= subLength && subLength <= 8.5) {
                res = 1;
            } else if (8.5 <= subLength && subLength <= 12.5) {
                res = 2;
            } else if (12.5 <= subLength && subLength <= 17.0) {
                res = 3;
            }
            return res;
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
        private void PromptBeatmapSave() {
            if (beatMap == null) {
                return;
            }
            var res = MessageBox.Show("Save the currently opened map?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes) {
                BackupAndSaveBeatmap();
            }
        }
    }
}
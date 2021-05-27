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
using System.Reactive.Linq;
using System.Globalization;

namespace Edda {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    using Note = ValueTuple<double, int>;
    using Pen = System.Drawing.Pen;

    public partial class MainWindow : Window {

        // CONSTANTS
        readonly string eddaVersionNumber    = "0.2.0";
        readonly string defaultSongName      = "song.ogg";
        readonly string settingsFileName     = "settings.txt";
        readonly string gridColourMajor      = "#333333";
        readonly string gridColourMinor      = "#666666";
        readonly double gridThicknessMajor   = 2;
        readonly double gridThicknessMinor   = 1.5;
        readonly int    gridDivisionMax      = 12;
        readonly double waveformWidthPercent = 1.00;
        readonly int    notePlaybackStreams  = 16; 
        readonly int    desiredWASAPILatency = 100; // ms
        readonly int    notePollRate         = 15;  // ms
        readonly double noteDetectionDelta   = 15;  // ms
        readonly double initDragThreshold    = 10;
        readonly float  defaultSongVolume    = 0.25f;
        readonly float  defaultDrumVolume    = 1.0f;
        readonly int    defaultAudioLatency  = -20; // ms
        readonly int    editHistorySize      = 128;
        readonly List<string> environmentNames = new List<string> { "DefaultEnvironment", "Alfheim", "Nidavellir", "Asgard" };
        //readonly int      gridRedrawInterval   = 100; // ms
        readonly double   gridDrawRange        = 1;

        // COMPUTED PROPERTIES
        double unitLength {
            get { return Drum1.ActualWidth * editorGridSpacing; }
        }
        double unitLengthUnscaled {
            get { return Drum1.ActualWidth; }
        }
        double unitSubLength {
            get { return Drum1.ActualWidth/3; }
        }
        double unitHeight {
            get { return Drum1.ActualHeight; }
        }
        bool songIsPlaying {
            set { btnSongPlayer.Tag = (value == false) ? 0 : 1; }
            get { return (int)btnSongPlayer.Tag == 1; }
        }
        double currentBPM {
            get { return doubleParseInvariant((string)beatMap.getValue("_beatsPerMinute")); }
        }

        // store images for drawing runes
        BitmapImage rune1;
        BitmapImage rune12;
        BitmapImage rune13;
        BitmapImage rune14;
        BitmapImage rune23;
        BitmapImage rune34;
        BitmapImage runeX;

        BitmapImage rune1Highlight;
        BitmapImage rune12Highlight;
        BitmapImage rune13Highlight;
        BitmapImage rune14Highlight;
        BitmapImage rune23Highlight;
        BitmapImage rune34Highlight;
        BitmapImage runeXHighlight;

        // STATE VARIABLES

        RagnarockMap    beatMap;

        // store info about the currently selected difficulty
        int        currentDifficulty;
        List<Note> currentDifficultyNotes;

        DoubleAnimation songPlayAnim;            // used for animating scroll when playing a song
        bool            songWasChanged;          // used for resetting scroll position on a new song load
        double          prevScrollPercent;       // percentage of scroll progress before the scroll viewport was changed

        // variables used in the map editor
        Image             imgPreviewNote;
        List<Note>        editorClipboard;
        EditHistory<Note> editorHistory;

        bool shiftKeyDown;
        bool ctrlKeyDown;
        // -- for waveform drawing
        Image imgAudioWaveform;
        AudioWaveformDrawerF32 awd;

        // -- for note placement
        int    editorMouseGridRow;
        int    editorMouseGridCol;
        double editorMouseGridRowFractional;

        // -- for drag select
        List<Note> editorSelectedNotes;
        Border     editorDragSelectBorder;
        Point      editorDragSelectStart;
        double     editorRowStart;
        int        editorColStart;
        bool       editorIsDragging = false;
        bool       editorMouseDown = false;

        // -- for grid drawing
        bool       editorSnapToGrid = true;
        int        editorGridDivision;
        double     editorGridSpacing;
        double     editorGridOffset;
        double     editorDrawRangeLower = 0;
        double     editorDrawRangeHigher = 0;

        // variables used to handle drum hits on a separate thread
        int noteScanIndex;
        int noteScanStopwatchOffset = 0;
        Stopwatch noteScanStopwatch;
        CancellationTokenSource noteScanTokenSource;
        CancellationToken noteScanToken;

        // -- audio playback
        int editorAudioLatency; // ms
        SampleChannel    songChannel;
        VorbisWaveReader songStream;
        WasapiOut        songPlayer;
        Drummer          drummer;

        public MainWindow() {
            InitializeComponent();
            songIsPlaying = false;
            sliderSongProgress.Tag = 0;
            scrollEditor.Tag = 0;

            string[] drumSounds = { "Resources/drum1.wav", "Resources/drum2.wav", "Resources/drum3.wav", "Resources/drum4.wav" };
            drummer = new Drummer(drumSounds, notePlaybackStreams, desiredWASAPILatency);

            // disable parts of UI, as no map is loaded
            btnSaveMap.IsEnabled = false;
            btnChangeDifficulty0.IsEnabled = false;
            btnChangeDifficulty1.IsEnabled = false;
            btnChangeDifficulty2.IsEnabled = false;
            btnAddDifficulty.IsEnabled = false;
            txtSongName.IsEnabled = false;
            txtArtistName.IsEnabled = false;
            txtMapperName.IsEnabled = false;
            txtSongBPM.IsEnabled = false;
            txtSongOffset.IsEnabled = false;
            comboEnvironment.IsEnabled = false;
            btnPickSong.IsEnabled = false;
            btnPickCover.IsEnabled = false;
            sliderSongVol.IsEnabled = false;
            sliderDrumVol.IsEnabled = false;
            //checkGridSnap.IsEnabled = false;
            txtDifficultyNumber.IsEnabled = false;
            txtNoteSpeed.IsEnabled = false;
            txtGridDivision.IsEnabled = false;
            txtGridOffset.IsEnabled = false;
            txtGridSpacing.IsEnabled = false;
            btnDeleteDifficulty.IsEnabled = false;
            btnSongPlayer.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;
            scrollEditor.IsEnabled = false;

            // load config file
            if (File.Exists(settingsFileName)) {
                string[] lines = File.ReadAllLines(settingsFileName);
                foreach (var line in lines) {
                    // load editorAudioLatency
                    if (line.StartsWith("editorAudioLatency")) {
                        int latency;
                        if (!int.TryParse(line.Split("=")[1], out latency)) {
                            Trace.WriteLine("INFO: using default editor audio latency");
                            editorAudioLatency = defaultAudioLatency;
                        } else {
                            Trace.WriteLine($"INFO: using user editor audio latency ({latency}ms)");
                            editorAudioLatency = latency;
                        }
                    }
                }
            } else {
                createConfigFile();
                editorAudioLatency = defaultAudioLatency;
            }

            // init border
            editorDragSelectBorder = new Border();
            editorDragSelectBorder.BorderBrush = Brushes.Black;
            editorDragSelectBorder.BorderThickness = new Thickness(2);
            editorDragSelectBorder.Background = Brushes.LightBlue;
            editorDragSelectBorder.Opacity = 0.5;
            editorDragSelectBorder.Visibility = Visibility.Hidden;

            // load bitmaps
            rune1  = bitmapGenerator("rune1.png");
            rune12 = bitmapGenerator("rune12.png");
            rune13 = bitmapGenerator("rune13.png");
            rune14 = bitmapGenerator("rune14.png");
            rune23 = bitmapGenerator("rune23.png");
            rune34 = bitmapGenerator("rune34.png");
            runeX  = bitmapGenerator("runeX.png");

            rune1Highlight  = bitmapGenerator("rune1highlight.png");
            rune12Highlight = bitmapGenerator("rune12highlight.png");
            rune13Highlight = bitmapGenerator("rune13highlight.png");
            rune14Highlight = bitmapGenerator("rune14highlight.png");
            rune23Highlight = bitmapGenerator("rune23highlight.png");
            rune34Highlight = bitmapGenerator("rune34highlight.png");
            runeXHighlight  = bitmapGenerator("runeXhighlight.png");

            // load editor preview
            imgPreviewNote = new Image();
            imgPreviewNote.Source = rune1;
            imgPreviewNote.Opacity = 0.25;
            imgPreviewNote.Width = unitLength;
            imgPreviewNote.Height = unitHeight;
            EditorGrid.Children.Add(imgPreviewNote);

            imgAudioWaveform = new Image();

            // init editor stuff
            editorHistory = new EditHistory<Note>(editHistorySize);
            editorClipboard = new List<Note>();
            editorSelectedNotes = new List<Note>();

            // TODO: properly debounce grid redrawing on resize
            //Observable
            //.FromEventPattern<SizeChangedEventArgs>(EditorGrid, nameof(Canvas.SizeChanged))
            //.Throttle(TimeSpan.FromMilliseconds(gridRedrawInterval))
            //.Subscribe(eventPattern => 
            //    AppMainWindow.Dispatcher.Invoke(() => 
            //        EditorGrid_SizeChanged(eventPattern.Sender, eventPattern.EventArgs)
            //    )
            //);

        }

        // UI bindings
        private void AppMainWindow_Closed(object sender, EventArgs e) {
            Trace.WriteLine("Closing window...");
            if (noteScanTokenSource != null) {
                noteScanTokenSource.Cancel();
            }
            if (songPlayer != null) {
                songPlayer.Stop();
                songPlayer.Dispose();
            }
            if (songStream != null) {
                songStream.Dispose();
            }
            if (drummer != null) {
                drummer.Dispose();
            }
        }
        private void AppMainWindow_KeyDown(object sender, KeyEventArgs e) {
            var keyStr = e.Key.ToString();
            if (keyStr.EndsWith("Ctrl")) {
                ctrlKeyDown = true;
            }
            if (keyStr.EndsWith("Shift")) {
                shiftKeyDown = true;
            }

            // ctrl shortcuts
            if (ctrlKeyDown) {
                // new map (Ctrl-N)
                if (keyStr == "N") {
                    if (btnNewMap.IsEnabled) {
                        btnNewMap_Click(null, null);
                    }
                }
                // open map (Ctrl-O)
                if (keyStr == "O") {
                    if (btnOpenMap.IsEnabled) {
                        btnOpenMap_Click(null, null);
                    }
                }
                // save map (Ctrl-S)
                if (keyStr == "S") {
                    if (btnSaveMap.IsEnabled) {
                        btnSaveMap_Click(null, null);
                    }
                }

                // copy (Ctrl-C)
                if (keyStr == "C") {
                    copyNotes();
                }
                // paste (Ctrl-V)
                if (keyStr == "V") {
                    pasteNotes(beatForRow(editorMouseGridRow));
                }

                // undo (Ctrl-Z)
                if (keyStr == "Z") {
                    Edit<Note> edit = editorHistory.undo();
                    applyEdit(edit);
                }
                // redo (Ctrl-Y, Ctrl-Shift-Z)
                if ((keyStr == "Y") ||
                    (keyStr == "Z" && shiftKeyDown)) {
                    Edit<Note> edit = editorHistory.redo();
                    applyEdit(edit);
                }

                // mirror selected notes (Ctrl-M)
                if (keyStr == "M") {
                    mirrorSelection();
                }
            }
            // toggle media player
            if (keyStr == "Space") {
                if (btnSongPlayer.IsEnabled) {
                    btnSongPlayer_Click(null, null);
                }
            }
            // delete selected notes
            if (keyStr == "Delete") {
                removeNotes(editorSelectedNotes);
            }
            // unselect all notes
            if (keyStr == "Escape") {
                unselectAllNotes();
            }
            //Trace.WriteLine(keyStr);
            //Trace.WriteLine($"Row: {editorMouseGridRow} ({Math.Round(editorMouseGridRowFractional, 2)}), Col: {editorMouseGridCol}");
        }
        private void AppMainWindow_KeyUp(object sender, KeyEventArgs e) {
            var keyStr = e.Key.ToString();
            if (keyStr.EndsWith("Ctrl")) {
                ctrlKeyDown = false;
            }
            if (keyStr.EndsWith("Shift")) {
                shiftKeyDown = false;
            }
        }
        private void btnNewMap_Click(object sender, RoutedEventArgs e) {

            // check if map already open
            if (beatMap != null) {
                var res = MessageBox.Show("A map is already open. Creating a new map will close the existing map. Are you sure you want to continue?", "Warning", MessageBoxButton.YesNo);
                if (res != MessageBoxResult.Yes) {
                    return;
                }
                // save existing work before making a new map
                beatMap.writeInfo();
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

            beatMap = new RagnarockMap(d2.FileName, true, eddaVersionNumber);

            // select and load an audio file
            if (!selectNewSong()) {
                return;
            }

            // open the newly created map
            initUI();
        }
        private void btnOpenMap_Click(object sender, RoutedEventArgs e) {

            // select folder for map
            // TODO: this dialog is sometimes hangs, is there a better way to select a folder?
            var d2 = new CommonOpenFileDialog();
            d2.Title = "Select your map's containing folder";
            d2.IsFolderPicker = true;

            if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
                return;
            }

            // TODO: check folder has a valid map

            // load info
            beatMap = new RagnarockMap(d2.FileName, false, eddaVersionNumber);

            loadSong();
            initUI();
        }
        private void btnSaveMap_Click(object sender, RoutedEventArgs e) {
            // TODO: update _lastEditedBy field 
            beatMap.writeInfo();
            beatMap.setNotesForMap(currentDifficultyNotes, currentDifficulty);
            for (int i = 0; i < beatMap.numDifficulties; i++) {
                beatMap.writeDifficultyMap(i);
            }
        }
        private void btnPickSong_Click(object sender, RoutedEventArgs e) {
            if (selectNewSong()) {
                // TODO: clear generated preview?
                initUI();
            }
        }
        private void btnPickCover_Click(object sender, RoutedEventArgs e) {
            var d = new Microsoft.Win32.OpenFileDialog() { Filter = "JPEG Files|*.jpg;*.jpeg" };
            d.Title = "Select a song to map";

            if (d.ShowDialog() != true) {
                return;
            }

            imgCover.Source = null;

            if (File.Exists(absPath("cover.jpg"))) {
                File.Delete(absPath("cover.jpg"));
            }
            
            File.Copy(d.FileName, absPath("cover.jpg"));
            beatMap.setValue("_coverImageFilename", "cover.jpg");
            loadCoverImage();
        }
        private void btnSongPlayer_Click(object sender, RoutedEventArgs e) {
            if (!songIsPlaying) {
                playSong();
            } else {
                pauseSong();
            }          
        }
        private void btnAddDifficulty_Click(object sender, RoutedEventArgs e) {
            beatMap.addDifficultyMap(beatMap.numDifficulties == 1 ? "Normal" : "Hard");
            updateDifficultyButtonVisibility();
            switchDifficultyMap(beatMap.numDifficulties - 1);
        }
        private void btnDeleteDifficulty_Click(object sender, RoutedEventArgs e) {
            var res = MessageBox.Show("Are you sure you want to delete this difficulty? This cannot be undone.", "Warning", MessageBoxButton.YesNo);
            if (res != MessageBoxResult.Yes) {
                return;
            }
            beatMap.deleteDifficultyMap(currentDifficulty);
            switchDifficultyMap(Math.Min(currentDifficulty, beatMap.numDifficulties - 1));
            updateDifficultyButtonVisibility();
        }
        private void btnChangeDifficulty0_Click(object sender, RoutedEventArgs e) {
            // save previous work to buffer before switching
            // note: this does NOT save to file
            beatMap.setNotesForMap(currentDifficultyNotes, currentDifficulty); 
            switchDifficultyMap(0);
        }
        private void btnChangeDifficulty1_Click(object sender, RoutedEventArgs e) {
            beatMap.setNotesForMap(currentDifficultyNotes, currentDifficulty);
            switchDifficultyMap(1);
        }
        private void btnChangeDifficulty2_Click(object sender, RoutedEventArgs e) {
            beatMap.setNotesForMap(currentDifficultyNotes, currentDifficulty);
            switchDifficultyMap(2);
        }
        private void sliderSongVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            songChannel.Volume = (float)sliderSongVol.Value; 
            txtSongVol.Text = $"{(int)(sliderSongVol.Value * 100)}%";
        }
        private void sliderDrumVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            drummer.changeVolume(sliderDrumVol.Value);
            txtDrumVol.Text = $"{(int) (sliderDrumVol.Value * 100)}%";
        }
        private void sliderSongProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {

            // update song seek time text box
            var seek = (int)(sliderSongProgress.Value / 1000.0);
            int min = seek / 60;
            int sec = seek % 60;

            txtSongPosition.Text = $"{min}:{sec.ToString("D2")}";

            // update vertical scrollbar
            var percentage = sliderSongProgress.Value / sliderSongProgress.Maximum;
            var offset = (1 - percentage) * scrollEditor.ScrollableHeight;
            scrollEditor.ScrollToVerticalOffset(offset);

            // play drum hits
            //if (songIsPlaying) {
            //    //Trace.WriteLine($"Slider: {sliderSongProgress.Value}ms");
            //    scanForNotes();
            //}
        }
        private void txtSongBPM_LostFocus(object sender, RoutedEventArgs e) {
            double BPM;
            double prevBPM = doubleParseInvariant((string)beatMap.getValue("_beatsPerMinute"));
            if (double.TryParse(txtSongBPM.Text, out BPM)) {
                if (BPM != prevBPM) {
                    beatMap.setValue("_beatsPerMinute", BPM);
                    updateEditorGridHeight();
                }
            } else {
                MessageBox.Show($"The BPM must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BPM = prevBPM;
            }
            txtSongBPM.Text = BPM.ToString();

        }
        private void txtSongOffset_LostFocus(object sender, RoutedEventArgs e) {
            double offset;
            double prevOffset = doubleParseInvariant((string)beatMap.getValue("_songTimeOffset"));
            if (double.TryParse(txtSongOffset.Text, out offset)) {
                beatMap.setValue("_songTimeOffset", offset);
            } else {
                MessageBox.Show($"The song offset must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                offset = prevOffset;
            }
            txtSongOffset.Text = offset.ToString();
        }
        private void txtSongName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.setValue("_songName", txtSongName.Text);
        }
        private void txtArtistName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.setValue("_songAuthorName", txtArtistName.Text);
        }
        private void txtMapperName_TextChanged(object sender, TextChangedEventArgs e) {
            beatMap.setValue("_levelAuthorName", txtMapperName.Text);
        }
        private void txtDifficultyNumber_LostFocus(object sender, RoutedEventArgs e) {
            int prevLevel = (int)beatMap.getValueForDifficultyMap("_difficultyRank", currentDifficulty);
            int level;
            if (int.TryParse(txtDifficultyNumber.Text, out level) && rangeCheck(level, 1, 10)) {
                beatMap.setValueForDifficultyMap("_difficultyRank", level, currentDifficulty);
            } else {
                MessageBox.Show($"The difficulty level must be an integer between 1 and 10.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                level = prevLevel;
            }
            txtDifficultyNumber.Text = level.ToString();
        }
        private void txtNoteSpeed_LostFocus(object sender, RoutedEventArgs e) {
            double prevSpeed = int.Parse((string)beatMap.getValueForDifficultyMap("_noteJumpMovementSpeed", currentDifficulty));
            double speed;
            if (double.TryParse(txtNoteSpeed.Text, out speed) && speed > 0) {
                beatMap.setValueForDifficultyMap("_noteJumpMovementSpeed", speed, currentDifficulty);
            } else {
                MessageBox.Show($"The note speed must be a positive number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                speed = prevSpeed;
            }
            txtNoteSpeed.Text = speed.ToString();
        }
        private void txtGridOffset_LostFocus(object sender, RoutedEventArgs e) {
            double prevOffset = doubleParseInvariant((string)beatMap.getCustomValueForDifficultyMap("_editorOffset", currentDifficulty));
            double offset;
            if (double.TryParse(txtGridOffset.Text, out offset)) {
                if (offset != prevOffset) {
                    // resnap all notes
                    var offsetDelta = offset - editorGridOffset;
                    var beatOffset = currentBPM / 60 * offsetDelta;
                    for (int i = 0; i < currentDifficultyNotes.Count; i++) {
                        Note n = new Note();
                        n.Item1 = currentDifficultyNotes[i].Item1 + beatOffset;
                        n.Item2 = currentDifficultyNotes[i].Item2;
                        currentDifficultyNotes[i] = n;
                    }

                    editorGridOffset = offset;
                    beatMap.setCustomValueForDifficultyMap("_editorOffset", offset, currentDifficulty);
                    updateEditorGridHeight();
                }
            } else {
                MessageBox.Show($"The grid offset must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                offset = prevOffset;
            }
            txtGridOffset.Text = offset.ToString();
        }
        private void txtGridSpacing_LostFocus(object sender, RoutedEventArgs e) {
            double prevSpacing = doubleParseInvariant((string)beatMap.getCustomValueForDifficultyMap("_editorGridSpacing", currentDifficulty));
            double spacing;
            if (double.TryParse(txtGridSpacing.Text, out spacing)) {
                if (spacing != prevSpacing) {
                    editorGridSpacing = spacing;
                    beatMap.setCustomValueForDifficultyMap("_editorGridSpacing", spacing, currentDifficulty);
                    updateEditorGridHeight();
                }
            } else {
                MessageBox.Show($"The grid spacing must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                spacing = prevSpacing;
            }
            txtGridSpacing.Text = spacing.ToString();
        }
        private void txtGridDivision_LostFocus(object sender, RoutedEventArgs e) {
            int prevDiv = int.Parse((string)beatMap.getCustomValueForDifficultyMap("_editorGridDivision", currentDifficulty));
            int div;

            if (int.TryParse(txtGridDivision.Text, out div) && rangeCheck(div, 1, gridDivisionMax)) {
                if (div != prevDiv) {
                    editorGridDivision = div;
                    beatMap.setCustomValueForDifficultyMap("_editorGridDivision", div, currentDifficulty);
                    drawEditorGrid();
                }
            } else {
                MessageBox.Show($"The grid division amount must be an integer from 1 to {gridDivisionMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                div = prevDiv;
            }
            txtGridDivision.Text = div.ToString();
        }
        private void comboEnvironment_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            string env = "DefaultEnvironment";
            switch (comboEnvironment.SelectedIndex) {
                case 0:
                    env = "DefaultEnvironment"; break;   
                case 1:
                    env = "Alfheim"; break;
                case 2:
                    env = "Nidavellir"; break;
                case 3:
                    env = "Asgard"; break;
            }
            beatMap.setValue("_environmentName", env);
        }
        private void EditorGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (songIsPlaying) {
                pauseSong();
            }
            if (beatMap != null && e.WidthChanged) {
                drawEditorGrid();
            }
        }
        private void scrollEditor_SizeChanged(object sender, SizeChangedEventArgs e) {
            updateEditorGridHeight(false);
        }
        private void scrollEditor_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            var curr = scrollEditor.VerticalOffset;
            var range = scrollEditor.ScrollableHeight;
            var value = (1 - curr / range) * (sliderSongProgress.Maximum - sliderSongProgress.Minimum);
            if (!songIsPlaying) {
                sliderSongProgress.Value = Double.IsNaN(value) ? 0 : value;
            }

            // try to keep the scroller at the same percentage scroll that it was before
            if (e.ExtentHeightChange != 0) {
                if (songWasChanged) {
                    songWasChanged = false;
                } else {
                    scrollEditor.ScrollToVerticalOffset((1 - prevScrollPercent) * scrollEditor.ScrollableHeight);
                }
                //Trace.Write($"time: {txtSongPosition.Text} curr: {scrollEditor.VerticalOffset} max: {scrollEditor.ScrollableHeight} change: {e.ExtentHeightChange}\n");
            } else if (range != 0) {
                prevScrollPercent = (1 - curr / range);
            }

        }
        private void scrollEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

        }
        private void scrollEditor_MouseMove(object sender, MouseEventArgs e) {

            // calculate vertical element
            double userOffsetBeat = currentBPM * editorGridOffset / 60;
            double userOffset = userOffsetBeat * unitLength;
            var mousePos = EditorGrid.ActualHeight - e.GetPosition(EditorGrid).Y - unitHeight / 2;
            double gridLength = unitLength / (double)editorGridDivision;
            // check if mouse position would correspond to a negative beat index
            if (mousePos < 0) {
                editorMouseGridRowFractional = - userOffset / gridLength;
                editorMouseGridRow = (int)(editorMouseGridRowFractional); // round towards infinity; otherwise this lands on a negative beat
            } else {
                editorMouseGridRowFractional = (mousePos - userOffset) / gridLength;
                editorMouseGridRow = (int)Math.Round(editorMouseGridRowFractional, MidpointRounding.AwayFromZero);
            }

            // calculate horizontal element
            var mouseX = e.GetPosition(EditorGrid).X / unitSubLength;
            if (0 <= mouseX && mouseX <= 4.5) {
                editorMouseGridCol = 0;
            } else if (4.5 <= mouseX && mouseX <= 8.5) {
                editorMouseGridCol = 1;
            } else if (8.5 <= mouseX && mouseX <= 12.5) {
                editorMouseGridCol = 2;
            } else if (12.5 <= mouseX && mouseX <= 17.0) {
                editorMouseGridCol = 3;
            }

            // place preview note
            if (editorSnapToGrid) {
                Canvas.SetBottom(imgPreviewNote, gridLength * editorMouseGridRow + userOffset);
            } else {
                Canvas.SetBottom(imgPreviewNote, Math.Max(mousePos, 0));
            }
            double noteX = (1 + 4 * (editorMouseGridCol)) * unitSubLength;

            // for some reason Canvas.SetLeft(0) doesn't correspond to the leftmost of the canvas, so we need to do some unknown adjustment to line it up
            var unknownNoteXAdjustment = ((unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2);

            double beat = editorMouseGridRow / (double)editorGridDivision + userOffsetBeat;
            imgPreviewNote.Source = bitmapImageForBeat(beat);
            Canvas.SetLeft(imgPreviewNote, noteX - unknownNoteXAdjustment);

            // calculate drag stuff
            if (editorIsDragging) {
                updateDragSelection(e.GetPosition(EditorGrid));
            } else if (editorMouseDown) {
                Vector delta = e.GetPosition(EditorGrid) - editorDragSelectStart;
                if (delta.Length > initDragThreshold) {
                    imgPreviewNote.Visibility = Visibility.Hidden;
                    editorIsDragging = true;
                    editorDragSelectBorder.Visibility = Visibility.Visible;
                    updateDragSelection(e.GetPosition(EditorGrid));
                }
            
            }  
        }
        private void scrollEditor_MouseEnter(object sender, MouseEventArgs e) {
            imgPreviewNote.Opacity = 0.5;
        }
        private void scrollEditor_MouseLeave(object sender, MouseEventArgs e) {
            imgPreviewNote.Opacity = 0;
        }
        private void scrollEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            editorMouseDown = true;
            editorDragSelectStart = e.GetPosition(EditorGrid);
            editorRowStart = editorMouseGridRowFractional;
            editorColStart = editorMouseGridCol;
            EditorGrid.CaptureMouse();
        }
        private void scrollEditor_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (editorIsDragging) {
                editorDragSelectBorder.Visibility = Visibility.Hidden;
                imgPreviewNote.Visibility = Visibility.Visible;
                // calculate new selections
                List<Note> newSelection = new List<Note>();
                double startBeat = beatForRow(editorRowStart);
                double endBeat = beatForRow(editorMouseGridRowFractional);
                foreach (Note n in currentDifficultyNotes) {
                    // minor optimisation
                    if (n.Item1 > Math.Max(startBeat, endBeat)) {
                        break;
                    }
                    // check range
                    if (rangeCheck(n.Item1, startBeat, endBeat) && rangeCheck(n.Item2, editorColStart, editorMouseGridCol)) {
                        newSelection.Add(n);
                    }
                }
                selectNewNotes(newSelection);
            } else if (editorMouseDown) {
                //Trace.WriteLine($"Row: {editorMouseGridRow} ({Math.Round(editorMouseGridRowFractional, 2)}), Col: {editorMouseGridCol}, Beat: {beat} ({beatFractional})");

                // create the note
                double row = (editorSnapToGrid) ? (editorMouseGridRow) : (editorMouseGridRowFractional);
                Note n = new Note(beatForRow(row), editorMouseGridCol);

                if (currentDifficultyNotes.Contains(n)) {
                    if (shiftKeyDown) {
                        if (editorSelectedNotes.Contains(n)) {
                            unselectNote(n);
                        } else {
                            selectNote(n);
                        }
                    } else {
                        selectNewNotes(n);
                    }      
                } else {
                    addNotes(n);
                }
            }
            EditorGrid.ReleaseMouseCapture();
            editorIsDragging = false;
            editorMouseDown = false;
        }
        private void scrollEditor_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            // remove the note
            double row = (editorSnapToGrid) ? (editorMouseGridRow) : (editorMouseGridRowFractional);
            Note n = new Note(beatForRow(row), editorMouseGridCol);
            if (currentDifficultyNotes.Contains(n)) {
                removeNotes(n);
            } else {
                unselectAllNotes();
            }
        }

        //private void checkGridSnap_Click(object sender, RoutedEventArgs e) {
        //    editorSnapToGrid = (checkGridSnap.IsChecked == true);
        //}


        // UI initialisation
        private void initUI() {

            // reset variables
            currentDifficulty = 0;
            prevScrollPercent = 0;

            // map settings
            txtSongName.Text   = (string)beatMap.getValue("_songName");
            txtArtistName.Text = (string)beatMap.getValue("_songAuthorName");
            txtMapperName.Text = (string)beatMap.getValue("_levelAuthorName");
            txtSongBPM.Text    = (string)beatMap.getValue("_beatsPerMinute");
            txtSongOffset.Text = (string)beatMap.getValue("_songTimeOffset");

            comboEnvironment.SelectedIndex = environmentNames.IndexOf((string)beatMap.getValue("_environmentName"));
            
            // file info
            txtSongFileName.Text = (string)beatMap.getValue("_songFilename");

            if ((string)beatMap.getValue("_coverImageFilename") != "") {
                loadCoverImage();
            } else {
                clearCoverImage();
            }

            // song player
            var duration = (int) songStream.TotalTime.TotalSeconds;
            txtSongDuration.Text = $"{duration / 60}:{(duration % 60).ToString("D2")}";

            //checkGridSnap.IsChecked = editorSnapToGrid;

            sliderSongVol.Value = defaultSongVolume;
            sliderDrumVol.Value = defaultDrumVolume;

            // enable UI parts
            btnSaveMap.IsEnabled = true;
            btnChangeDifficulty0.IsEnabled = true;
            btnChangeDifficulty1.IsEnabled = true;
            btnChangeDifficulty2.IsEnabled = true;
            enableDifficultyButtons();
            updateDifficultyButtonVisibility();
            txtSongName.IsEnabled = true;
            txtArtistName.IsEnabled = true;
            txtMapperName.IsEnabled = true;
            txtSongBPM.IsEnabled = true;
            txtSongOffset.IsEnabled = true;
            comboEnvironment.IsEnabled = true;
            btnPickSong.IsEnabled = true;
            btnPickCover.IsEnabled = true;
            sliderSongVol.IsEnabled = true;
            sliderDrumVol.IsEnabled = true;
            //checkGridSnap.IsEnabled = true;
            txtDifficultyNumber.IsEnabled = true;
            txtNoteSpeed.IsEnabled = true;
            txtGridDivision.IsEnabled = true;
            txtGridOffset.IsEnabled = true;
            txtGridSpacing.IsEnabled = true;
            btnDeleteDifficulty.IsEnabled = true;
            btnSongPlayer.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            scrollEditor.IsEnabled = true;

            // init difficulty-specific UI 
            switchDifficultyMap(currentDifficulty, false);

            updateEditorGridHeight();

            scrollEditor.ScrollToBottom();
        }

        // manage cover image
        private void loadCoverImage() {
            var fileName = (string)beatMap.getValue("_coverImageFilename");
            BitmapImage b = bitmapGenerator(new Uri(absPath(fileName)));
            imgCover.Source = b;
            txtCoverFileName.Text = fileName;
        }
        private void clearCoverImage() {
            imgCover.Source = null;
            txtCoverFileName.Text = "N/A";
        }

        // manage difficulties
        private void updateDifficultyButtonVisibility() {
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
        private void enableDifficultyButtons() {
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
        private void switchDifficultyMap(int indx, bool redraw = true) {
            currentDifficulty = indx;
            currentDifficultyNotes = beatMap.getNotesForMap(indx);

            txtDifficultyNumber.Text = (string)beatMap.getValueForDifficultyMap("_difficultyRank", indx);
            txtNoteSpeed.Text        = (string)beatMap.getValueForDifficultyMap("_noteJumpMovementSpeed", indx);
            txtGridOffset.Text       = (string)beatMap.getCustomValueForDifficultyMap("_editorOffset", indx);
            txtGridSpacing.Text      = (string)beatMap.getCustomValueForDifficultyMap("_editorGridSpacing", indx);
            txtGridDivision.Text     = (string)beatMap.getCustomValueForDifficultyMap("_editorGridDivision", indx);

            // set internal values
            editorGridDivision = int.Parse(txtGridDivision.Text);
            editorGridSpacing = doubleParseInvariant(txtGridSpacing.Text);
            editorGridOffset = doubleParseInvariant(txtGridOffset.Text);

            enableDifficultyButtons();
            if (redraw) {
                drawEditorGrid();
            }
        }

        // file creation
        private void createConfigFile() {
            string[] fields = { 
                "editorAudioLatency=" 
            };
            File.WriteAllLines("settings.txt", fields);
        }

        // song/note playback
        private bool selectNewSong() {
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
                vorbisStream = new NAudio.Vorbis.VorbisWaveReader(d.FileName);
            } catch (Exception) {
                MessageBox.Show("The .ogg file is corrupted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (vorbisStream.TotalTime.TotalHours >= 1) {
                MessageBox.Show("Songs over 1 hour in duration are not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            var songPath = System.IO.Path.Combine(beatMap.folderPath, defaultSongName);

            unloadSong();
            File.Delete(songPath);
            beatMap.setValue("_songApproximativeDuration", (int)vorbisStream.TotalTime.TotalSeconds + 1);
            File.Copy(d.FileName, songPath);
            loadSong();
            return true;
        }
        // TODO: draw a song waveform (https://stackoverflow.com/questions/2042155/high-quality-graph-waveform-display-component-in-c-sharp)
        private void loadSong() {

            // cleanup old players
            unloadSong();

            var songPath = System.IO.Path.Combine(beatMap.folderPath, (string)beatMap.getValue("_songFilename"));
            songStream = new VorbisWaveReader(songPath);
            songPlayer = new WasapiOut(AudioClientShareMode.Shared, desiredWASAPILatency);

            songChannel = new SampleChannel(songStream);
            songChannel.Volume = (float)sliderSongVol.Value;
            songPlayer.Init(songChannel);

            // subscribe to playbackstopped
            songPlayer.PlaybackStopped += (sender, args) => { pauseSong(); };
            sliderSongProgress.Minimum = 0;
            sliderSongProgress.Maximum = songStream.TotalTime.TotalSeconds * 1000;
            sliderSongProgress.Value = 0;
            songWasChanged = true;

            awd = new AudioWaveformDrawerF32(songStream);
        }
        private void unloadSong() {
            if (songStream != null) {
                songStream.Dispose();
            }
            if (songPlayer != null) {
                songPlayer.Dispose();
            }
        }
        private void playSong() {
            songIsPlaying = true;
            imgPlayerButton.Source = bitmapGenerator("pauseButton.png");
            // disable some UI elements for performance reasons
            // song/note playback gets desynced if these are changed during playback
            // TODO: fix this?
            //checkGridSnap.IsEnabled = false;
            txtSongBPM.IsEnabled = false;
            txtGridDivision.IsEnabled = false;
            txtGridOffset.IsEnabled = false;
            txtGridSpacing.IsEnabled = false;
            btnDeleteDifficulty.IsEnabled = false;
            btnChangeDifficulty0.IsEnabled = false;
            btnChangeDifficulty1.IsEnabled = false;
            btnChangeDifficulty2.IsEnabled = false;
            btnAddDifficulty.IsEnabled = false;

            songStream.CurrentTime = TimeSpan.FromMilliseconds(sliderSongProgress.Value);

            // disable scrolling while playing
            scrollEditor.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;

            // disable editor features
            EditorGrid.Children.Remove(imgPreviewNote);

            // animate for smooth scrolling 
            var remainingTimeSpan = songStream.TotalTime - songStream.CurrentTime;

            // note: the DoubleAnimation induces a desync of around 0.1 seconds
            songPlayAnim = new DoubleAnimation();
            songPlayAnim.From = sliderSongProgress.Value;
            songPlayAnim.To = sliderSongProgress.Maximum;
            songPlayAnim.Duration = new Duration(remainingTimeSpan);
            //Timeline.SetDesiredFrameRate(songPlayAnim, animationFramerate);
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, songPlayAnim);

            // init stopwatch
            noteScanStopwatch = new Stopwatch();
            noteScanStopwatchOffset = (int)(sliderSongProgress.Value - editorAudioLatency); // set user audio delay
            scanNoteIndex();

            // start scanning for notes
            noteScanTokenSource = new CancellationTokenSource();
            noteScanToken = noteScanTokenSource.Token;
            Task.Run(() => scanForNotes(noteScanStopwatchOffset, noteScanToken), noteScanToken);

            noteScanStopwatch.Start();

            // play song
            songPlayer.Play();
        }
        private void pauseSong() {
            songIsPlaying = false;
            imgPlayerButton.Source = bitmapGenerator("playButton.png");

            // reset note scan
            noteScanTokenSource.Cancel();
            noteScanStopwatch.Reset();

            // re-enable UI elements
            //checkGridSnap.IsEnabled = true;
            txtSongBPM.IsEnabled = true;
            txtGridDivision.IsEnabled = true;
            txtGridOffset.IsEnabled = true;
            txtGridSpacing.IsEnabled = true;
            btnDeleteDifficulty.IsEnabled = true;
            enableDifficultyButtons();
            btnAddDifficulty.IsEnabled = true;

            // enable scrolling while paused
            scrollEditor.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            songPlayAnim.BeginTime = null;
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, null);
            var curr = scrollEditor.VerticalOffset;
            var range = scrollEditor.ScrollableHeight;
            var value = (1 - curr / range) * (sliderSongProgress.Maximum - sliderSongProgress.Minimum);
            sliderSongProgress.Value = value;

            // enable editor features
            if (!EditorGrid.Children.Contains(imgPreviewNote)) {
                EditorGrid.Children.Add(imgPreviewNote);
            }

            //Trace.WriteLine($"Slider is late by {Math.Round(songStream.CurrentTime.TotalMilliseconds - sliderSongProgress.Value, 2)}ms");

            songPlayer.Pause();
        }
        private void scanNoteIndex() {
            // calculate scan index for playing drum hits
            var seekBeat = (noteScanStopwatchOffset / 1000.0) * (currentBPM / 60.0);
            noteScanIndex = 0;
            foreach (var n in currentDifficultyNotes) {
                if (n.Item1 >= seekBeat) {
                    break;
                }
                noteScanIndex++;
            }
        }
        private void scanForNotes(int startFrom, CancellationToken ct) {
            // NOTE: this function is called on a separate thread

            // scan notes while song is still playing
            var nextPollTime = notePollRate;
            while (!ct.IsCancellationRequested) {
                if (noteScanStopwatch.ElapsedMilliseconds + startFrom >= nextPollTime) {
                    playNotes();
                    nextPollTime += notePollRate;
                }
            }
        }
        private void playNotes() {
            var currentTime = noteScanStopwatch.ElapsedMilliseconds + noteScanStopwatchOffset;
            // check if we started past the last note in the song
            if (noteScanIndex < currentDifficultyNotes.Count) {
                var noteTime = 60000 * currentDifficultyNotes[noteScanIndex].Item1 / currentBPM;
                var drumHits = 0;

                // check if any notes were missed
                while (currentTime - noteTime >= noteDetectionDelta && noteScanIndex < currentDifficultyNotes.Count - 1) {
                    Trace.WriteLine($"WARNING: A note was played late during playback. (Delta: {Math.Round(currentTime - noteTime, 2)})");
                    drumHits++;
                    noteScanIndex++;
                    noteTime = 60000 * currentDifficultyNotes[noteScanIndex].Item1 / currentBPM;
                }

                // check if we need to play any notes
                while (Math.Abs(currentTime - noteTime) < noteDetectionDelta) {
                    //Trace.WriteLine($"Played note at beat {selectedDifficultyNotes[noteScanIndex].Item1}");
                      
                    drumHits++;
                    noteScanIndex++;
                    if (noteScanIndex >= currentDifficultyNotes.Count) {
                        break;
                    }
                    noteTime = 60000 * currentDifficultyNotes[noteScanIndex].Item1 / currentBPM;
                }

                // play all pending drum hits
                if (drummer.playDrum(drumHits) == false) {
                    Trace.WriteLine("WARNING: Drummer skipped a drum hit");
                }
            }
        }

        // editor functions
        private void addNotes(List<Note> notes, bool updateHistory = true) {
            foreach (Note n in notes) {
                insertSortedUnique(currentDifficultyNotes, n);
            }
            // draw the added notes
            // note: by drawing this note out of order, it is inconsistently layered with other notes.
            //       should we take the performance hit of redrawing the entire grid for visual consistency?
            drawEditorGridNotes(notes);

            if (updateHistory) {
                editorHistory.add(true, notes);
            }
            //editorHistory.print();
        }
        private void addNotes(Note n, bool updateHistory = true) {
            addNotes(new List<Note>() { n }, updateHistory);
        }
        private void removeNotes(List<Note> notes, bool updateHistory = true) {
            foreach (Note n in notes) {
                currentDifficultyNotes.Remove(n);
            }
            // undraw the added notes
            undrawEditorGridNotes(notes);

            if (updateHistory) {
                editorHistory.add(false, notes);
            }
            //editorHistory.print();
        }
        private void removeNotes(Note n, bool updateHistory = true) {
            removeNotes(new List<Note>() { n }, updateHistory);
        }
        private void selectNote(Note n) {
            insertSortedUnique(editorSelectedNotes, n);

            // draw highlighted note
            foreach (UIElement e in EditorGrid.Children) {
                if (e.Uid == uidGenerator(n)) {
                    var img = (Image)e;
                    img.Source = bitmapImageForBeat(n.Item1, true);
                }
            }
        }
        private void selectNewNotes(List<Note> notes) {
            unselectAllNotes();
            foreach (Note n in notes) {
                selectNote(n);
            }
        }
        private void selectNewNotes(Note n) {
            selectNewNotes(new List<Note>() { n });
        }
        private void unselectNote(Note n) {
            if (editorSelectedNotes == null) {
                return;
            }
            editorSelectedNotes.Remove(n);
            foreach (UIElement e in EditorGrid.Children) {
                if (e.Uid == uidGenerator(n)) {
                    var img = (Image)e;
                    img.Source = bitmapImageForBeat(n.Item1);
                }
            }
        }
        private void unselectAllNotes() {
            if (editorSelectedNotes == null) {
                return;
            }
            foreach (Note n in editorSelectedNotes) {
                foreach (UIElement e in EditorGrid.Children) {
                    if (e.Uid == uidGenerator(n)) {
                        var img = (Image)e;
                        img.Source = bitmapImageForBeat(n.Item1);
                    }
                }
            }
            editorSelectedNotes.Clear();
        }
        private void copyNotes() {
            editorClipboard = new List<Note>(editorSelectedNotes);
            editorClipboard.Sort(compareNotes);

        }
        private void pasteNotes(double beatOffset) {
            // paste notes so that the first note lands on the given beat offset
            double offset = beatOffset - editorClipboard[0].Item1;
            List<Note> notes = new List<Note>();
            for (int i = 0; i < editorClipboard.Count; i++) {
                Note n = new Note(editorClipboard[i].Item1 + offset, editorClipboard[i].Item2);
                notes.Add(n);
            }
            addNotes(notes);
        }
        private void applyEdit(Edit<Note> e) {
            if (e.isAdding) {
                addNotes(e.items, false);
            } else {
                removeNotes(e.items, false);
            }
        }
        private void updateDragSelection(Point newPoint) {
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
        private void mirrorSelection() {
            // prepare new selection
            List<Note> mirroredSelection = new List<Note>();
            for (int i = 0; i < editorSelectedNotes.Count; i++) {
                Note mirrored = new Note(editorSelectedNotes[i].Item1, 3 - editorSelectedNotes[i].Item2);
                mirroredSelection.Add(mirrored);
            }
            // TODO: make this undoable
            removeNotes(editorSelectedNotes, false);
            addNotes(mirroredSelection, false);
            selectNewNotes(mirroredSelection);
        }

        // drawing functions for the editor grid
        private void updateEditorGridHeight(bool redraw = true) {
            if (beatMap == null) {
                return;
            }

            // resize editor grid height to fit scrollEditor height
            double beats = (currentBPM / 60) * songStream.TotalTime.TotalSeconds;
            EditorGrid.Height = beats * unitLength + scrollEditor.ActualHeight;

            if (redraw) {
                drawEditorGrid();
            }
        }
        private void drawEditorGrid() {

            if (beatMap == null) {
                return;
            }

            Trace.WriteLine("INFO: Redrawing editor grid...");

            EditorGrid.Children.Clear();

            // calculate new drawn ranges for pagination, if we need it...
            editorDrawRangeLower  = Math.Max(scrollEditor.VerticalOffset -     (gridDrawRange) * scrollEditor.ActualHeight, 0                      );
            editorDrawRangeHigher = Math.Min(scrollEditor.VerticalOffset + (1 + gridDrawRange) * scrollEditor.ActualHeight, EditorGrid.ActualHeight);
            Trace.WriteLine($"draw range: {editorDrawRangeLower} - {editorDrawRangeHigher}");

            updateEditorWaveform();

            drawEditorGridLines();

            drawEditorGridNotes(currentDifficultyNotes);

            // change editor preview note size
            imgPreviewNote.Width = unitLength;
            imgPreviewNote.Height = unitHeight;
            EditorGrid.Children.Add(imgPreviewNote);

            EditorGrid.Children.Add(editorDragSelectBorder);

            // rescan notes after drawing
            scanNoteIndex();

            Trace.WriteLine("INFO: Finished drawing editor grid");
        }
        private void updateEditorWaveform() {
            if (EditorGrid.Height - scrollEditor.ActualHeight > 0) {
                resizeEditorWaveform();
                double height = EditorGrid.Height - scrollEditor.ActualHeight;
                double width = EditorGrid.ActualWidth * waveformWidthPercent;
                Task.Run(() => {
                    drawEditorWaveform(height, width);
                });
                
            }
        }
        private void resizeEditorWaveform() {
            EditorGrid.Children.Remove(imgAudioWaveform);
            imgAudioWaveform.Height = EditorGrid.Height - scrollEditor.ActualHeight;
            imgAudioWaveform.Width = EditorGrid.ActualWidth;
            Canvas.SetBottom(imgAudioWaveform, unitLength / 2);
            EditorGrid.Children.Insert(0, imgAudioWaveform);
        }
        private void drawEditorWaveform(double height, double width) {
            BitmapSource bmp = awd.draw(height, width);
            if (bmp == null) {
                return;
            }
            this.Dispatcher.Invoke(() => {
                imgAudioWaveform.Source = bmp;
                resizeEditorWaveform();
            });
        }
        private void drawEditorGridLines() {
            // calculate grid offset: default is 
            double offsetBeats = currentBPM * editorGridOffset / 60;

            //            default                  user specified
            var offset = (unitHeight / 2) + (offsetBeats * unitLength);

            // draw gridlines
            int counter = 0;
            while (offset <= EditorGrid.Height) {
                var l = new Line();
                l.X1 = 0;
                l.X2 = EditorGrid.ActualWidth;
                l.Y1 = offset;
                l.Y2 = offset;
                l.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(
                    (counter % editorGridDivision == 0) ? gridColourMajor : gridColourMinor)
                );
                l.StrokeThickness = (counter % editorGridDivision == 0) ? gridThicknessMajor : gridThicknessMinor;
                Canvas.SetBottom(l, offset);
                EditorGrid.Children.Add(l);
                offset += unitLength / editorGridDivision;
                counter++;
            }
        }
        private void drawEditorGridNotes(List<Note> notes) {
            // draw drum notes
            // TODO: paginate these? they cause lag when resizing

            // init drum note image

            foreach (var n in notes) {
                var img = new Image();
                img.Width = unitLengthUnscaled;
                img.Height = unitHeight;

                var noteHeight = n.Item1 * unitLength;
                var noteXOffset = (1 + 4 * n.Item2) * unitSubLength;

                // find which beat fraction this note lies on
                img.Source = bitmapImageForBeat(n.Item1);

                // this assumes there are no duplicate notes given to us
                img.Uid = uidGenerator(n);

                Canvas.SetBottom(img, noteHeight);
                Canvas.SetLeft(img, noteXOffset);
                EditorGrid.Children.Add(img);
            }
        }
        private void drawEditorGridNotes(Note n) {
            drawEditorGridNotes(new List<Note>() { n });
        }
        private void undrawEditorGridNotes(List<Note> notes) {
            foreach (Note n in notes) {
                var nUid = uidGenerator(n);
                foreach (UIElement u in EditorGrid.Children) {
                    if (u.Uid == nUid) {
                        EditorGrid.Children.Remove(u);
                        break;
                    }
                }
            }
        }
        private void undrawEditorGridNotes(Note n) {
            undrawEditorGridNotes(new List<Note>() { n });
        }

        // helper functions
        private double doubleParseInvariant(string s) {
            return double.Parse(s, CultureInfo.InvariantCulture);
        }
        private double beatForRow(double row) {
            double userOffsetBeat = currentBPM * editorGridOffset / 60;
            return row / (double)editorGridDivision + userOffsetBeat;
        }
        private void insertSortedUnique(List<Note> notes, Note note) {
            // check which index to insert the new note at (keep everything in sorted order)
            var i = 0;
            foreach (var thisNote in notes) {
                if (compareNotes(thisNote, note) == 0) {
                    return;
                }
                if (compareNotes(thisNote, note) > 0) {
                    notes.Insert(i, note);
                    return;
                }
                i++;
            }
            notes.Add(note);
        }
        private int compareNotes(Note m, Note n) {
            if (m == n) {
                return 0;
            }
            if (m.Item1 > n.Item1) {
                return 1;
            }
            if (m.Item1 == n.Item1 && m.Item2 > n.Item2) {
                return 1;
            }
            return -1;
        }
        private string absPath(string f) {
            return System.IO.Path.Combine(beatMap.folderPath, f);
        }
        private bool rangeCheck(double a, double x, double y) {
            double lower = Math.Min(x, y);
            double higher = Math.Max(x, y);
            return (lower <= a && a <= higher);
        }
        private string uidGenerator(Note n) {
            return $"Note({n.Item1},{n.Item2})";
        }
        private Uri packUriGenerator(string fileName) {
            return new Uri($"pack://application:,,,/resources/{fileName}");
        }
        private BitmapImage bitmapImageForBeat(double beat, bool isHighlighted) {
            var fracBeat = beat - (int)beat;
            switch (Math.Round(fracBeat, 5)) {
                case 0.00000: return (isHighlighted) ? rune1Highlight  : rune1 ; 
                case 0.25000: return (isHighlighted) ? rune14Highlight : rune14; 
                case 0.33333: return (isHighlighted) ? rune13Highlight : rune13; 
                case 0.50000: return (isHighlighted) ? rune12Highlight : rune12; 
                case 0.66667: return (isHighlighted) ? rune23Highlight : rune23; 
                case 0.75000: return (isHighlighted) ? rune34Highlight : rune34; 
                default:      return (isHighlighted) ? runeXHighlight  : runeX ;
            }
        }
        private BitmapImage bitmapGenerator(Uri u) {
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = u;
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.EndInit();
            b.Freeze();
            return b;
        }
        private BitmapImage bitmapGenerator(string file) {
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = packUriGenerator(file);
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.EndInit();
            b.Freeze();
            return b;
        }
        private BitmapImage bitmapImageForBeat(double beat) {
            return bitmapImageForBeat(beat, false);
        }
    }
}
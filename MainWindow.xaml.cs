using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NAudio.Vorbis;
using NAudio.Wave;
using NVorbis;
using System.Windows.Media.Animation;
using System.Reflection;
using NAudio.Wave.SampleProviders;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using NAudio.CoreAudioApi;

namespace RagnarockEditor {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    using Note = ValueTuple<double, int>;
    public partial class MainWindow : Window {

        // constants
        string   gridColourMajor      = "#333333";
        string   gridColourMinor      = "#666666";
        double   gridThicknessMajor   = 2;
        double   gridThicknessMinor   = 1.5;
        int      gridDivisionMax      = 24;
        string[] difficultyNames      = {"Easy", "Normal", "Hard"};
        int      notePlaybackStreams  = 16; 
        int      desiredWasapiLatency = 100; // ms
        int      notePollRate         = 10; // ms
        double   noteDetectionDelta   = 20; // ms
        int      defaultGridDivision  = 4;
        int defaultEditorAudioLatency = 0; // ms
        // int gridRedrawInterval = 200; // ms
        // double   gridDrawRange = 1;

        // readonly values

        double unitLength {
            get { return Drum.ActualWidth * editorGridSpacing; }
        }
        double unitLengthUnscaled {
            get { return Drum.ActualWidth; }
        }

        double unitSubLength {
            get { return Drum.ActualWidth/3; }
        }
        double unitHeight {
            get { return Drum.ActualHeight; }
        }

        double editorScrollPosition {
            get { return Math.Max(EditorGrid.ActualHeight - scrollEditor.VerticalOffset - scrollEditor.ActualHeight, 0); }
        }
        string songFilePath {
            get { return absPath((string)getValInfoDat("_songFilename")); }
        }

        // state variables
        int _selectedDifficulty;
        int selectedDifficulty {  // 0, 1, 2
            get { return _selectedDifficulty; }
            set {
                _selectedDifficulty = value;
                switchDifficultyMap(_selectedDifficulty);
            }
        }
        string[] mapsStr = {"", "", ""};
        Note[] selectedDifficultyNotes;
        DoubleAnimation songPlayAnim;

        bool songIsPlaying {
            set { btnSongPlayer.Tag = (value == false) ? 0 : 1; }
            get { return (int)btnSongPlayer.Tag == 1; }
        }
        double currentBPM {
            get { return double.Parse((string)getValInfoDat("_beatsPerMinute")); }
        }

        int numDifficulties {
           get {
                var obj = JObject.Parse(infoStr);
                var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
                return res.Count();
           }
        }
        string infoStr;
        string saveFolder;
        double songOffset;
        double prevScrollPercent = 0; // percentage of scroll progress before the scroll viewport was changed

        // variables used in the map editor
        Image  imgPreviewNote;
        bool   editorSnapToGrid;
        int    editorGridDivision;
        double editorGridSpacing;
        double editorGridOffset;
        int    editorAudioDelay; // in ms
        double editorDrawRangeLower = 0;
        double editorDrawRangeHigher = 0;
        int    editorMouseGridRow;
        int    editorMouseGridCol;
        double editorMouseGridRowFractional;

        // variables used to handle drum hits on a separate thread
        int noteScanIndex;
        Stopwatch noteScanStopwatch;
        int noteScanStopwatchOffset = 0;
        CancellationTokenSource noteScanTokenSource;
        CancellationToken noteScanToken;

        // variables used to play audio
        SampleChannel songChannel;
        VorbisWaveReader songStream;
        WasapiOut songPlayer;
        Drummer drummer;

        public MainWindow() {
            InitializeComponent();
            songIsPlaying = false;
            sliderSongProgress.Tag = 0;
            scrollEditor.Tag = 0;

            drummer = new Drummer(new String[] { "Resources/drum1.wav", "Resources/drum2.wav", "Resources/drum3.wav", "Resources/drum4.wav" }, notePlaybackStreams, desiredWasapiLatency);

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
            txtGridDivision.IsEnabled = false;
            txtGridOffset.IsEnabled = false;
            txtGridSpacing.IsEnabled = false;
            btnDeleteDifficulty.IsEnabled = false;
            btnSongPlayer.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;
            scrollEditor.IsEnabled = false;

 


            // TODO: properly debounce grid redrawing on resize
            //Observable
            //.FromEventPattern<SizeChangedEventArgs>(EditorGrid, nameof(Canvas.SizeChanged))
            //.Throttle(TimeSpan.FromMilliseconds(gridRedrawInterval))
            //.Subscribe(eventPattern => _EditorGrid_SizeChanged(eventPattern.Sender, eventPattern.EventArgs));
        }

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

        private void btnNewMap_Click(object sender, RoutedEventArgs e) {

            // check if map already open
            if (saveFolder != null) {
                var res = MessageBox.Show("A map is already open. Creating a new map will close the existing map. Are you sure you want to continue?", "Warning", MessageBoxButton.YesNo);
                if (res != MessageBoxResult.Yes) {
                    return;
                }
                // save existing work before making a new map
                writeInfoStr();
            }

            // select folder for map
            var d2 = new CommonOpenFileDialog();
            d2.Title = "Select an empty folder to store your map";
            d2.IsFolderPicker = true;

            if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
                return;
            }

            saveFolder = d2.FileName;

            if (Directory.GetFiles(saveFolder).Length > 0) {
                MessageBox.Show("The specified folder is not empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // init info.dat json
            initialiseInfoDat();

            // select audio file
            if (!changeSong()) {
                return;
            }

            // init first difficulty map
            addDifficulty(difficultyNames[0]);
            writeMapStr(0);

            // save to file
            writeInfoStr();

            // load the selected song
            loadSong();

            // open the newly created map
            initUI();

        }
         
        private void btnOpenMap_Click(object sender, RoutedEventArgs e) {

            // select folder for map

            // TODO: this dialog is really slow and sometimes hangs... is there another way to select a folder?
            var d2 = new CommonOpenFileDialog();
            d2.Title = "Select your map's containing folder";
            d2.IsFolderPicker = true;

            if (d2.ShowDialog() != CommonFileDialogResult.Ok) {
                return;
            }

            // check folder is OK

            // load info
            saveFolder = d2.FileName;

            readInfoStr();

            for (int i = 0; i < numDifficulties; i++) {
                readMapStr(i);
            }
            loadSong();
            initUI();
        }

        private void btnSaveMap_Click(object sender, RoutedEventArgs e) {
            writeInfoStr();
            for (int i = 0; i < numDifficulties; i++) {
                writeMapStr(i);
            }
        }

        private void btnPickSong_Click(object sender, RoutedEventArgs e) {
            if (changeSong()) {
                setValInfoDat("_songFilename", "song.ogg");
                setValInfoDat("_songName", "");
                setValInfoDat("_songAuthorName", "");
                setValInfoDat("_beatsPerMinute", 120);
                setValInfoDat("_coverImageFilename", "");
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
            setValInfoDat("_coverImageFilename", "cover.jpg");
            loadCoverImage();
        }

        private void btnSongPlayer_Click(object sender, RoutedEventArgs e) {
            if (!songIsPlaying) {
                beginSongPlayback();
            } else {
                endSongPlayback();
            }          
        }

        private void btnAddDifficulty_Click(object sender, RoutedEventArgs e) {
            addDifficulty(numDifficulties == 1 ? "Normal" : "Hard");
        }

        private void btnDeleteDifficulty_Click(object sender, RoutedEventArgs e) {
            var res = MessageBox.Show("Are you sure you want to delete this difficulty?", "Warning", MessageBoxButton.YesNo);
            if (res != MessageBoxResult.Yes) {
                return;
            }
            deleteDifficultyMap(selectedDifficulty);
        }

        private void btnChangeDifficulty0_Click(object sender, RoutedEventArgs e) {
            selectedDifficulty = 0;
        }

        private void btnChangeDifficulty1_Click(object sender, RoutedEventArgs e) {
            selectedDifficulty = 1;
        }

        private void btnChangeDifficulty2_Click(object sender, RoutedEventArgs e) {
            selectedDifficulty = 2;
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

        private void scrollEditor_SizeChanged(object sender, SizeChangedEventArgs e) {
            updateEditorGridHeight();
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
                scrollEditor.ScrollToVerticalOffset((1 - prevScrollPercent) * scrollEditor.ScrollableHeight);
                //Trace.Write($"time: {txtSongPosition.Text} curr: {scrollEditor.VerticalOffset} max: {scrollEditor.ScrollableHeight} change: {e.ExtentHeightChange}\n");
            } else if (range != 0) {
                prevScrollPercent = (1 - curr / range);
            }
            
        }
        
        private void scrollEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

        }

        private void txtSongBPM_LostFocus(object sender, RoutedEventArgs e) {
            double BPM;
            if (!double.TryParse(txtSongBPM.Text, out BPM)) {
                BPM = currentBPM;
            } else {
                setValInfoDat("_beatsPerMinute", BPM);
            }
            txtSongBPM.Text = BPM.ToString();
            updateEditorGridHeight();
            drawEditorGrid();
        }

        private void txtSongOffset_LostFocus(object sender, RoutedEventArgs e) {
            double offset;
            if (!double.TryParse(txtSongOffset.Text, out offset)) {
                offset = songOffset;
            } else {
                setValInfoDat("_songTimeOffset", offset);
            }
            txtSongBPM.Text = offset.ToString();
        }

        private void txtSongName_TextChanged(object sender, TextChangedEventArgs e) {
            setValInfoDat("_songName", txtSongName.Text);
        }

        private void txtArtistName_TextChanged(object sender, TextChangedEventArgs e) {
            setValInfoDat("_songAuthorName", txtArtistName.Text);
        }

        private void txtMapperName_TextChanged(object sender, TextChangedEventArgs e) {
            setValInfoDat("_levelAuthorName", txtMapperName.Text);
        }

        private void txtGridOffset_LostFocus(object sender, RoutedEventArgs e) {
            double offset;
            if (double.TryParse(txtGridOffset.Text, out offset) && offset != editorGridOffset) {
                setCustomMapValInfoDat("_editorOffset", offset);
                txtGridOffset.Text = offset.ToString();
                editorGridOffset = offset;
                updateEditorGridHeight();
                drawEditorGrid();
            }
        }

        private void txtGridSpacing_LostFocus(object sender, RoutedEventArgs e) {
            double spacing;
            if (double.TryParse(txtGridSpacing.Text, out spacing) && spacing != editorGridSpacing) {
                setCustomMapValInfoDat("_editorGridSpacing", spacing);
                txtGridSpacing.Text = spacing.ToString();
                editorGridSpacing = spacing;
                updateEditorGridHeight();
            }
        }

        private void txtGridDivision_LostFocus(object sender, RoutedEventArgs e) {
            int div;
            if (!int.TryParse(txtGridDivision.Text, out div) || div < 1) {
                div = 1;
            }
            if (div > gridDivisionMax) {
                div = gridDivisionMax;
                MessageBox.Show($"The maximum grid division amount is {gridDivisionMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (div != editorGridDivision) {
                txtGridDivision.Text = div.ToString();
                editorGridDivision = div;
                drawEditorGrid();
            }
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
            setValInfoDat("_environmentName", env);
        }

        private void EditorGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (songIsPlaying) {
                endSongPlayback();
            }
            if (infoStr != null) {
                rescanNoteIndex();
                //updateEditorGridHeight();
                drawEditorGrid();
            }
        }

        private void scrollEditor_MouseEnter(object sender, MouseEventArgs e) {
            imgPreviewNote.Opacity = 0.5;
        }

        private void scrollEditor_MouseMove(object sender, MouseEventArgs e) {

            // set vertical element
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

            if (editorSnapToGrid) {
                Canvas.SetBottom(imgPreviewNote, gridLength * editorMouseGridRow + userOffset);
            } else {
                Canvas.SetBottom(imgPreviewNote, Math.Max(mousePos, 0));
            }

            // set horizontal element
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
            var unknownNoteXAdjustment = ((unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2);
            var unitSubLengthOffset = 1 + 4 * (editorMouseGridCol);
            Canvas.SetLeft(imgPreviewNote, (unitSubLengthOffset * unitSubLength) - unknownNoteXAdjustment);
        }

        private void scrollEditor_MouseLeave(object sender, MouseEventArgs e) {
            imgPreviewNote.Opacity = 0;
        }

        private void scrollEditor_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            double userOffsetBeat = currentBPM * editorGridOffset / 60;
            double beat = editorMouseGridRow / (double)editorGridDivision + userOffsetBeat;
            double beatFractional = editorMouseGridRowFractional / (double)editorGridDivision + userOffsetBeat;
            //Trace.WriteLine($"Row: {editorMouseGridRow} ({Math.Round(editorMouseGridRowFractional, 2)}), Col: {editorMouseGridCol}, Beat: {beat} ({beatFractional})");

            // add the note
            Note n;
            n.Item1 = (editorSnapToGrid) ? beat : beatFractional;
            n.Item2 = editorMouseGridCol;
            addNote(n);

            // draw the added notes
            Note[] notesToDraw = {n};
            drawEditorGridNotes(notesToDraw);

            // save new note data
            setMapStrNotes(selectedDifficulty);

            //printNotes();
        }

        private void scrollEditor_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            double userOffsetBeat = currentBPM * editorGridOffset / 60;
            double beat = editorMouseGridRow / (double)editorGridDivision + userOffsetBeat;
            double beatFractional = editorMouseGridRowFractional / (double)editorGridDivision + userOffsetBeat;
            //Trace.WriteLine($"Row: {editorMouseGridRow} ({Math.Round(editorMouseGridRowFractional, 2)}), Col: {editorMouseGridCol}, Beat: {beat} ({beatFractional})");

            // remove the note
            Note n;
            n.Item1 = (editorSnapToGrid) ? beat : beatFractional;
            n.Item2 = editorMouseGridCol;
            removeNote(n);

            // undraw the added notes
            undrawEditorGridNote(uidGenerator(n));

            // save new note data
            setMapStrNotes(selectedDifficulty);

            printNotes();
        }

        private void checkGridSnap_Click(object sender, RoutedEventArgs e) {
            //editorSnapToGrid = (checkGridSnap.IsChecked == true);
        }

        // (re)initialise UI
        private void initUI() {
            txtSongName.Text   = (string) getValInfoDat("_songName");
            txtArtistName.Text = (string) getValInfoDat("_songAuthorName");
            txtMapperName.Text = (string) getValInfoDat("_levelAuthorName");
            txtSongBPM.Text    = (string) getValInfoDat("_beatsPerMinute");
 
            switch ((string)getValInfoDat("_environmentName")) {
                case "DefaultEnvironment":
                    comboEnvironment.SelectedIndex = 0; break;
                case "Alfheim":
                    comboEnvironment.SelectedIndex = 1; break;
                case "Nidavellir":
                    comboEnvironment.SelectedIndex = 2; break;
                case "Asgard":
                    comboEnvironment.SelectedIndex = 3; break;
                default:
                    comboEnvironment.SelectedIndex = 0; break;
            }
            
            txtSongFileName.Text  = (string)getValInfoDat("_songFilename")       == "" ? "N/A" : (string)getValInfoDat("_songFilename");
            txtCoverFileName.Text = (string)getValInfoDat("_coverImageFilename") == "" ? "N/A" : (string)getValInfoDat("_coverImageFilename");
            if (txtCoverFileName.Text != "N/A") {
                loadCoverImage();
            }
            var duration = (int) songStream.TotalTime.TotalSeconds;
            txtSongDuration.Text = $"{duration / 60}:{(duration % 60).ToString("D2")}";

            songOffset = double.Parse((string)getValInfoDat("_songTimeOffset"));
            txtSongOffset.Text = songOffset.ToString();

            editorGridDivision = defaultGridDivision;
            txtGridDivision.Text = editorGridDivision.ToString();

            editorGridSpacing = double.Parse((string)getCustomMapValInfoDat("_editorGridSpacing"));
            txtGridSpacing.Text = editorGridSpacing.ToString();

            editorGridOffset = double.Parse((string)getCustomMapValInfoDat("_editorOffset"));
            txtGridOffset.Text = editorGridOffset.ToString();

            editorSnapToGrid = true;
            //checkGridSnap.IsChecked = true;

            editorAudioDelay = defaultEditorAudioLatency;

            sliderSongVol.Value = 0.25;
            sliderDrumVol.Value = 1.0;

            // enable UI parts
            btnSaveMap.IsEnabled = true;
            btnChangeDifficulty0.IsEnabled = true;
            btnChangeDifficulty1.IsEnabled = true;
            btnChangeDifficulty2.IsEnabled = true;
            btnAddDifficulty.IsEnabled = true;
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
            txtGridDivision.IsEnabled = true;
            txtGridOffset.IsEnabled = true;
            txtGridSpacing.IsEnabled = true;
            btnDeleteDifficulty.IsEnabled = true;
            btnSongPlayer.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            scrollEditor.IsEnabled = true;

            // load editor resources
            BitmapImage b = imageGenerator(packUriGenerator("rune1.png"));
            imgPreviewNote = new Image();
            imgPreviewNote.Source = b;
            imgPreviewNote.Opacity = 0.5;
            imgPreviewNote.Width = unitLength;
            imgPreviewNote.Height = unitHeight;
            EditorGrid.Children.Add(imgPreviewNote);

            updateDifficultyButtonVisibility();
            updateEditorGridHeight();
            selectedDifficulty = 0;
            scrollEditor.ScrollToBottom();
        }

        private void initialiseInfoDat() {
            // init info.dat json
            var infoDat = new {
                _version = "1",
                _songName = "",
                _songSubName = "",                              // dummy
                _songAuthorName = "",
                _levelAuthorName = "",
                _beatsPerMinute = 120,
                _shuffle = 0,                                   // dummy?
                _shufflePeriod = 0.5,                           // dummy?
                _previewStartTime = 0,                          // dummy?
                _previewDuration = 0,                           // dummy?
                _songApproximativeDuration = 0,
                _songFilename = "song.ogg",
                _coverImageFilename = "",
                _environmentName = "DefaultEnvironment",
                _songTimeOffset = 0,
                _customData = new {
                    _contributors = new List<string>(),
                    _editors = new {
                        RagnarockEditor = new {
                            version = "0.0.1",
                        },
                        _lastEditedBy = "RagnarockEditor"
                    },
                },
                _difficultyBeatmapSets = new [] {
                    new {
                        _beatmapCharacteristicName = "Standard",
                        _difficultyBeatmaps = new List<object> {},
                    },
                },
            };
            infoStr = JsonConvert.SerializeObject(infoDat, Formatting.Indented);
        }

        private void addDifficulty(string difficulty) {
            var obj = JObject.Parse(infoStr);
            var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
            var beatmapDat = new {
                _difficulty = difficulty,
                _difficultyRank = 1,
                _beatmapFilename = $"{difficulty}.dat",
                _noteJumpMovementSpeed = 10,
                _noteJumpStartBeatOffset = 0,
                _customData = new {
                    _editorOffset = 0,
                    _editorOldOffset = 0,
                    _editorGridSpacing = 1,
                    _warnings = new List<string>(),
                    _information = new List<string>(),
                    _suggestions = new List<string>(),
                    _requirements = new List<string>(),
                },
            };
            beatmaps.Add(JToken.FromObject(beatmapDat));
            infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
            var mapDat = new {
                _version = "1",
                _customData = new {
                    _time = 0,
                    _BPMChanges = new List<object>(),
                    _bookmarks = new List<object>(),
                },
                _events = new List<object>(),
                _notes = new List<object>(),
                _obstacles = new List<object>(),
            };
            mapsStr[numDifficulties - 1] = JsonConvert.SerializeObject(mapDat, Formatting.Indented);
            updateDifficultyButtonVisibility();
        }

        private void updateDifficultyButtonVisibility() {
            for (var i = 0; i < numDifficulties; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Visible;
            }
            for (var i = numDifficulties; i < 3; i++) {
                ((Button)DifficultyChangePanel.Children[i]).Visibility = Visibility.Hidden;
            }
            btnDeleteDifficulty.IsEnabled = (numDifficulties == 1) ? false : true;
            btnAddDifficulty.Visibility = (numDifficulties == 3) ? Visibility.Hidden : Visibility.Visible;
        }

        private void enableDifficultyButtons(int indx) {
            foreach (Button b in DifficultyChangePanel.Children) {
                if (b.Name == ((Button)DifficultyChangePanel.Children[indx]).Name) {
                    b.IsEnabled = false;
                } else {
                    b.IsEnabled = true;
                }
            }
        }

        private void switchDifficultyMap(int indx) {
            enableDifficultyButtons(indx);
            selectedDifficultyNotes = getMapStrNotes(_selectedDifficulty);
            drawEditorGrid();
        }

        private void deleteDifficultyMap(int indx) {
            if (numDifficulties == 1) {
                return;
            }
            deleteMapStr(indx);
            var obj = JObject.Parse(infoStr);
            var beatmaps = (JArray) obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
            beatmaps.RemoveAt(indx);
            infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
            selectedDifficulty = Math.Min(selectedDifficulty, numDifficulties - 1);
            renameMapStr();
            writeInfoStr();
            writeMapStr(indx);
            updateDifficultyButtonVisibility();
        }

        // == read/write values to .dat files ==

        private string absPath(string f) {
            return System.IO.Path.Combine(saveFolder, f);
        }

        private void setValInfoDat(string key, object value) {
            var obj = JObject.Parse(infoStr);
            obj[key] = JToken.FromObject(value);
            infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        private JToken getValInfoDat(string key) {
            var obj = JObject.Parse(infoStr);
            var res = obj[key];
            return res;
        }

        private void setMapValInfoDat(string key, object value, int indx) {
            var obj = JObject.Parse(infoStr);
            obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key] = JToken.FromObject(value);
            infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        private JToken getMapValInfoDat(string key, int indx) {
            var obj = JObject.Parse(infoStr);
            var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key];
            return res;
        }

        private void setCustomMapValInfoDat(string key, object value) {
            var obj = JObject.Parse(infoStr);
            obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][selectedDifficulty]["_customData"][key] = JToken.FromObject(value);
            infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        } 

        private JToken getCustomMapValInfoDat(string key) {
            var obj = JObject.Parse(infoStr);
            var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][selectedDifficulty]["_customData"][key];
            return res;
        }

        private void readInfoStr() {
            infoStr = File.ReadAllText(absPath("info.dat"));
        }

        private void writeInfoStr() {
            File.WriteAllText(absPath("info.dat"), infoStr);
        }

        private void readMapStr(int indx) {
            var filename = (string) getMapValInfoDat("_beatmapFilename", indx);
            mapsStr[indx] = File.ReadAllText(absPath(filename));
        }

        private void writeMapStr(int indx) {
            var filename = (string) getMapValInfoDat("_beatmapFilename", indx);
            File.WriteAllText(absPath(filename), mapsStr[indx]);
        }

        private void deleteMapStr(int indx) {
            var filename = (string)getMapValInfoDat("_beatmapFilename", indx);
            File.Delete(absPath(filename));
            mapsStr[indx] = "";
        }

        private void renameMapStr() {
            for (int i = 0; i < numDifficulties; i++) {
                setMapValInfoDat("_difficulty", difficultyNames[i], i);
                var oldFile = (string) getMapValInfoDat("_beatmapFilename", i);
                File.Move(absPath(oldFile), absPath($"{difficultyNames[i]}_temp.dat"));
                setMapValInfoDat("_beatmapFilename", $"{difficultyNames[i]}.dat", i);
            }
            for (int i = 0; i < numDifficulties; i++) {
                File.Move(absPath($"{difficultyNames[i]}_temp.dat"), absPath($"{difficultyNames[i]}.dat"));
            }
        }

        private Note[] getMapStrNotes(int indx) {
            var obj = JObject.Parse(mapsStr[indx]);
            var res = obj["_notes"];
            Note[] output = new Note[res.Count()];
            var i = 0;
            foreach (JToken n in res) {
                double time = double.Parse((string)n["_time"]);
                int colIndex = int.Parse((string)n["_lineIndex"]);
                output[i] = (time, colIndex);
                i++;
            }
            return output;
        }

        private void setMapStrNotes(int indx) {
            var numNotes = selectedDifficultyNotes.Length;
            var notes = new Object[numNotes];
            for (int i = 0; i < numNotes; i++) {
                var thisNote = selectedDifficultyNotes[i];
                var thisNoteObj = new {
                    _time = thisNote.Item1,
                    _lineIndex = thisNote.Item2,
                    _lineLayer = 1,
                    _type = 0,
                    _cutDirection = 1
                };
                notes[i] = thisNoteObj;
            }
            var thisMapStr = JObject.Parse(mapsStr[selectedDifficulty]);
            thisMapStr["_notes"] = JToken.FromObject(notes);
            mapsStr[selectedDifficulty] = JsonConvert.SerializeObject(thisMapStr, Formatting.Indented);
            //mapsStr[selectedDifficulty]["_notes"] = jObj;
        }

        // ===================================

        private bool changeSong() {
            // select audio file
            var d = new Microsoft.Win32.OpenFileDialog();
            d.Title = "Select a song to map";
            d.DefaultExt = ".ogg";
            d.Filter = "OGG Vorbis (*.ogg)|*.ogg";

            if (d.ShowDialog() != true) {
                return false;
            }

            if (d.FileName == absPath("song.ogg")) {
                MessageBox.Show("This song is already being used.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            VorbisWaveReader vorbisStream;
            try {
                vorbisStream = new NAudio.Vorbis.VorbisWaveReader(d.FileName);
            } catch (Exception) {
                MessageBox.Show("The .ogg file is corrupted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            var time = vorbisStream.TotalTime;
            if (time.TotalHours >= 1) {
                MessageBox.Show("Songs over 1 hour in duration are not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            setValInfoDat("_songApproximativeDuration", (int) time.TotalSeconds + 1);

            File.Delete(absPath("song.ogg"));
            File.Copy(d.FileName, absPath("song.ogg"));
            loadSong();

            return true;
        }

        private void loadSong() {

            // cleanup old players
            if (songStream != null) {
                songStream.Dispose();
            }
            if (songPlayer != null) {
                songPlayer.Dispose();
            }

            songStream = new NAudio.Vorbis.VorbisWaveReader(songFilePath);
            songPlayer = new WasapiOut(AudioClientShareMode.Shared, desiredWasapiLatency);

            songChannel = new SampleChannel(songStream);
            songPlayer.Init(songChannel);

            // subscribe to playbackstopped
            songPlayer.PlaybackStopped += (sender, args) => { endSongPlayback(); };
            sliderSongProgress.Minimum = 0;
            sliderSongProgress.Maximum = songStream.TotalTime.TotalSeconds * 1000;

        }

        private void beginSongPlayback() {
            songIsPlaying = true;
            imgPlayerButton.Source = imageGenerator(packUriGenerator("pauseButton.png"));
            // disable some UI elements for performance reasons
            // song/note playback gets desynced if these are changed during playback
            // TODO: fix this?
            //checkGridSnap.IsEnabled = false;
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
            noteScanStopwatchOffset = (int)(sliderSongProgress.Value - editorAudioDelay); // set user audio delay
            rescanNoteIndex();
            noteScanTokenSource = new CancellationTokenSource();
            noteScanToken = noteScanTokenSource.Token;

            // start scanning for notes
            Task.Run(() => beginNoteScanning(noteScanStopwatchOffset, noteScanToken), noteScanToken);

            noteScanStopwatch.Start();

            // play song
            songPlayer.Play();
        }

        private void endSongPlayback() {
            songIsPlaying = false;
            imgPlayerButton.Source = imageGenerator(packUriGenerator("playButton.png"));

            // reset note scan
            noteScanTokenSource.Cancel();
            noteScanStopwatch.Reset();

            // re-enable UI elements
            //checkGridSnap.IsEnabled = true;
            txtGridDivision.IsEnabled = true;
            txtGridOffset.IsEnabled = true;
            txtGridSpacing.IsEnabled = true;
            btnDeleteDifficulty.IsEnabled = true;
            enableDifficultyButtons(selectedDifficulty);
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

            songPlayer.Stop();
        }

        private void playDrumHit(int hits) {
            if (drummer.playDrum(hits) == false) {
                Trace.WriteLine("WARNING: drummer skipped a drum hit");
            }
        }

        private void rescanNoteIndex() {
            // calculate scan index for playing drum hits
            var seekBeat = (noteScanStopwatchOffset / 1000.0) * (currentBPM / 60.0);
            noteScanIndex = 0;
            foreach (var n in selectedDifficultyNotes) {
                if (n.Item1 >= seekBeat) {
                    break;
                }
                noteScanIndex++;
            }
        }

        // this function is called on a separate thread
        private void beginNoteScanning(int startFrom, CancellationToken ct) {
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
            //var currentTime = songStream.CurrentTime.TotalMilliseconds;
            // check if we started past the last note in the song
            if (noteScanIndex < selectedDifficultyNotes.Length) {
                var noteTime = 60000 * selectedDifficultyNotes[noteScanIndex].Item1 / currentBPM;
                var drumHits = 0;

                // check if any notes were missed
                while (currentTime - noteTime >= noteDetectionDelta && noteScanIndex < selectedDifficultyNotes.Length - 1) {
                    Trace.WriteLine($"WARNING: A note was played late during playback. (Delta: {Math.Round(currentTime - noteTime, 2)})");
                    drumHits++;
                    noteScanIndex++;
                    noteTime = 60000 * selectedDifficultyNotes[noteScanIndex].Item1 / currentBPM;
                }

                // check if we need to play any notes
                while (approximatelyEqual(currentTime, noteTime, noteDetectionDelta)) {
                    //Trace.WriteLine($"Played note at beat {selectedDifficultyNotes[noteScanIndex].Item1}");
                      
                    drumHits++;
                    noteScanIndex++;
                    if (noteScanIndex >= selectedDifficultyNotes.Length) {
                        break;
                    }
                    noteTime = 60000 * selectedDifficultyNotes[noteScanIndex].Item1 / currentBPM;
                }

                // play pending drum hits
                playDrumHit(drumHits);
                //if (drumHits > 0) {
                //    this.Dispatcher.Invoke(() => {
                //        Trace.WriteLine($"Played note - UI: {Math.Round(sliderSongProgress.Value, 2)}ms, stream: {Math.Round(songStream.CurrentTime.TotalMilliseconds, 2)}, timer: {currentTime}");
                //    });
                //}
                
            }
        }

        // =======================================

        private bool addNote(Note n) {
            var insertIndx = 0;

            // check which index to insert the new note at (keep everything in sorted order)
            foreach (var thisNote in selectedDifficultyNotes) {

                // no duplicates of the same note
                if (n.Item1 == thisNote.Item1 && n.Item2 == thisNote.Item2) {
                    return false;
                }

                if (n.Item1 <= thisNote.Item1 || (n.Item1 == thisNote.Item1 && n.Item2 <= thisNote.Item2)) {
                    break;
                }

                insertIndx++;
            }

            // do the inserting
            selectedDifficultyNotes = selectedDifficultyNotes.Append((0, 0)).ToArray();
            // shift notes across
            for (var i = selectedDifficultyNotes.Length - 1; i > insertIndx; i--) {
                selectedDifficultyNotes[i] = selectedDifficultyNotes[i - 1];
            }
            selectedDifficultyNotes[insertIndx] = n;
            return true;
        }

        private bool removeNote(Note n) {
            var removeIndx = 0;

            // check which index to insert the new note at (keep everything in sorted order)
            foreach (var thisNote in selectedDifficultyNotes) {
                // no duplicates of the same note
                if (n.Item1 == thisNote.Item1 && n.Item2 == thisNote.Item2) {
                    break;
                }
                removeIndx++;
            }

            // note not found
            if (removeIndx == selectedDifficultyNotes.Length) {
                return false;
            }

            // do the removal
            // shift notes across
            for (var i = removeIndx; i < selectedDifficultyNotes.Length - 1; i++) {
                selectedDifficultyNotes[i] = selectedDifficultyNotes[i + 1];
            }
            // remove the last element
            selectedDifficultyNotes = (Note[]) selectedDifficultyNotes.Take(selectedDifficultyNotes.Length - 1).ToArray();
            return true;
        }

        // ======================================

        private void loadCoverImage() {
            var fileName = (string)getValInfoDat("_coverImageFilename");
            BitmapImage b = imageGenerator(new Uri(absPath(fileName)));
            imgCover.Source = b;
            txtCoverFileName.Text = fileName;
        }

        private void updateEditorGridHeight() {
            if (infoStr == null) {
                return;
            }

            // set editor grid height
            double beats = (currentBPM / 60) * songStream.TotalTime.TotalSeconds;

            // this triggers a grid redraw
            EditorGrid.Height = beats * unitLength + scrollEditor.ActualHeight;

            // change editor preview note size
            imgPreviewNote.Width = unitLength;
            imgPreviewNote.Height = unitHeight;
        }

        private void drawEditorGrid() {

            if (infoStr == null) {
                return;
            }

            Trace.WriteLine("INFO: Redrawing editor grid...");

            EditorGrid.Children.Clear();
            EditorGrid.Children.Add(imgPreviewNote);

            // calculate new drawn ranges for pagination, if we need it...
            //editorDrawRangeLower  = Math.Max(editorScrollPosition -     (gridDrawRange) * scrollEditor.ActualHeight, 0                      );
            //editorDrawRangeHigher = Math.Min(editorScrollPosition + (1 + gridDrawRange) * scrollEditor.ActualHeight, EditorGrid.ActualHeight);

            drawEditorGridLines();

            drawEditorGridNotes(selectedDifficultyNotes);

            // rescan notes after drawing
            rescanNoteIndex();
        }

        private void drawEditorGridLines() {
            // calculate grid offset: default is 
            double offsetBeats = currentBPM * editorGridOffset / 60;

            //            default                  user specified
            var offset = (unitHeight / 2) + (offsetBeats * unitLength);

            // draw gridlines
            int counter = 0;
            while (offset <= EditorGrid.ActualHeight) {
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

        private void drawEditorGridNotes(Note[] notes) {
            // draw drum notes
            // TODO: paginate these? they cause lag when resizing

            // init drum note image
            BitmapImage b = imageGenerator(packUriGenerator("rune1.png"));

            // for some reason, WPF does not display notes in the correct x-position with a Grid Scaling multiplier not equal to 1.
            // e.g. Canvas.SetLeft(img, 0) leaves a small gap between the left side of the Canvas and the img
            var unknownNoteXAdjustment = ((unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2);

            foreach (var n in notes) {
                var img = new Image();
                img.Source = b;
                img.Width = unitLength;
                img.Height = unitHeight;

                var noteHeight = n.Item1 * unitLength;
                var noteXOffset = (1 + 4 * n.Item2) * unitLengthUnscaled / 3;

                // this assumes there are no duplicate notes given to us
                img.Uid = uidGenerator(n);

                Canvas.SetBottom(img, noteHeight);
                Canvas.SetLeft(img, noteXOffset - unknownNoteXAdjustment);
                EditorGrid.Children.Add(img);
            }
        }

        private void undrawEditorGridNote(string Uid) {
            foreach (UIElement u in EditorGrid.Children) {
                if (u.Uid == Uid) {
                    EditorGrid.Children.Remove(u);
                    break;
                }
            }
        }

        //=======================

        private bool approximatelyEqual(double x, double y, double delta) {
            return Math.Abs(x - y) < delta;
        }

        private string uidGenerator(Note n) {
            return $"Note({n.Item1},{n.Item2})";
        }

        private Uri packUriGenerator(string fileName) {
            return new Uri($"pack://application:,,,/resources/{fileName}");
        }

        private BitmapImage imageGenerator(Uri u) {
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = u;
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.EndInit();
            b.Freeze();
            return b;
        }

        private void printNotes() {
            string output = "Notes: ";
            foreach (Note n in selectedDifficultyNotes) {
                output += $"({n.Item1}, {n.Item2}) ";
            }
            Trace.WriteLine(output);
        }

    }
}







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

namespace RagnarockEditor {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        // constants
        string gridColourMajor = "#333333";
        string gridColourMinor = "#666666";
        int gridThicknessMajor = 3;
        int gridThicknessMinor = 2;
        int gridDivisionMax = 24;
        double gridDrawRange = 1;
        string[] difficultyNames = { "Easy", "Normal", "Hard"};
        double notePlaybackDelta = 0.02;
        int simultaneousNotePlaybackStreams = 16;
        int gridRedrawInterval = 100; // ms
        // readonly values

        double unitLength {
            get { return Drum.ActualWidth * double.Parse((string)getForMapCustomInfoDat("_editorGridSpacing")); }
        }
        double unitLengthUnscaled {
            get { return Drum.ActualWidth; }
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
        (double, int)[] selectedDifficultyNotes;
        int noteScanIndex;
        bool songIsPlaying {
            set { btnSongPlayer.Tag = (value == false) ? 0 : 1; }
            get { return (int)btnSongPlayer.Tag == 1; }
        }
        double currentBPM {
            get { return double.Parse((string)getValInfoDat("_beatsPerMinute")); }
        }
        double prevScrollPercent = 0; // percentage of scroll progress before the scroll viewport was changed

        int numDifficulties {
           get {
                var obj = JObject.Parse(infoStr);
                var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
                return res.Count();
           }
        }
        string infoStr;
        string saveFolder;

        SampleChannel songChannel;
        VorbisWaveReader songStream;
        WaveOut songPlayer;
        Drummer drummer;


        int gridDivision; 
        double editorDrawRangeLower = 0;
        double editorDrawRangeHigher = 0;

        public MainWindow() {
            InitializeComponent();
            songIsPlaying = false;
            sliderSongProgress.Tag = 0;
            scrollEditor.Tag = 0;

            drummer = new Drummer(new String[] { "Resources/drum1.wav", "Resources/drum2.wav", "Resources/drum3.wav", "Resources/drum4.wav" }, simultaneousNotePlaybackStreams);

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
            comboEnvironment.IsEnabled = false;
            btnPickSong.IsEnabled = false;
            btnPickCover.IsEnabled = false;
            sliderSongVol.IsEnabled = false;
            sliderDrumVol.IsEnabled = false;
            txtGridDivision.IsEnabled = false;
            txtGridOffset.IsEnabled = false;
            txtGridSpacing.IsEnabled = false;
            btnDeleteDifficulty.IsEnabled = false;
            btnSongPlayer.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;
            scrollEditor.IsEnabled = false;

            // debounce grid redrawing on resize
            Observable
            .FromEventPattern<SizeChangedEventArgs>(EditorGrid, nameof(Canvas.SizeChanged))
            .Throttle(TimeSpan.FromMilliseconds(gridRedrawInterval))
            .Subscribe(eventPattern => _EditorGrid_SizeChanged(eventPattern.Sender, eventPattern.EventArgs));
        }

        void MainWindow_Closed(object sender, EventArgs e) {
            try {
                songStream.Dispose();
                songPlayer.Dispose();
                drummer.Dispose();
            } catch {
                return;
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
            txtSongVol.Text = Math.Round(sliderSongVol.Value, 2).ToString();
        }

        private void sliderDrumVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            drummer.changeVolume(sliderDrumVol.Value);
            txtDrumVol.Text = Math.Round(sliderDrumVol.Value, 2).ToString();
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
            if (songIsPlaying) {
                var currentBeat = currentBPM / 60 * sliderSongProgress.Value / 1000;
                var noteBeat = selectedDifficultyNotes[noteScanIndex].Item1;
                //Trace.WriteLine($"Current beat: {currentBeat}, target beat: {noteBeat}");
                // we are scanning too late, probably due to a large skip in animation
                while (currentBeat - noteBeat > notePlaybackDelta && noteScanIndex < selectedDifficultyNotes.Length - 1) {
                    Trace.WriteLine("WARNING: A note was skipped during playback.");
                    noteScanIndex++;
                    noteBeat = selectedDifficultyNotes[noteScanIndex].Item1;
                }
                while (approximatelyEqual(currentBeat, noteBeat, notePlaybackDelta)) {
                    //Trace.WriteLine($"Played note at beat {Math.Round(currentBeat, 2)}; scrollBar is {Math.Round((scrollEditor.ScrollableHeight - scrollEditor.VerticalOffset)/unitLength, 2)}");
                    //Trace.WriteLine($"Played note at second {Math.Round(currentBeat / (currentBPM / 60), 2)}; song is {Math.Round(songStream.CurrentTime.TotalSeconds, 2)}");
                    playDrumHit();
                    if (noteScanIndex >= selectedDifficultyNotes.Length - 1) {
                        break;
                    }
                    noteScanIndex++;
                    noteBeat = selectedDifficultyNotes[noteScanIndex].Item1;
                }
                //Trace.WriteLine(sliderSongProgress.Value/1000);
            }
        }

        private void scrollEditor_SizeChanged(object sender, SizeChangedEventArgs e) {
            updateEditorGridHeight();
        }

        private void scrollEditor_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            //if (!songIsPlaying) {
                var curr = scrollEditor.VerticalOffset;
                var range = scrollEditor.ScrollableHeight;
                var value = (1 - curr / range) * (sliderSongProgress.Maximum - sliderSongProgress.Minimum);
                sliderSongProgress.Value = Double.IsNaN(value) ? 0 : value;
            
            if (e.ExtentHeightChange != 0) {
                scrollEditor.ScrollToVerticalOffset((1 - prevScrollPercent) * scrollEditor.ScrollableHeight);
                //Trace.Write($"time: {txtSongPosition.Text} curr: {scrollEditor.VerticalOffset} max: {scrollEditor.ScrollableHeight} change: {e.ExtentHeightChange}\n");
            } else if (range != 0) {
                prevScrollPercent = (1 - curr / range);
            }
            
            //}
            //Trace.WriteLine($"{scrollEditor.VerticalOffset}/{scrollEditor.ScrollableHeight}");
        }
        
        private void scrollEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

        }

        private void txtSongBPM_LostFocus(object sender, RoutedEventArgs e) {
            double BPM;
            if (!double.TryParse(txtSongBPM.Text, out BPM)) {
                BPM = currentBPM;
            }
            setValInfoDat("_beatsPerMinute", BPM);
            txtSongBPM.Text = BPM.ToString();
            updateEditorGridHeight();
            drawEditorGrid();
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
            double prev = double.Parse((string)getForMapCustomInfoDat("_editorOffset"));
            double offset;
            if (double.TryParse(txtGridOffset.Text, out offset) && offset != prev) {
                setForMapCustomInfoDat("_editorOffset", offset);
                txtGridOffset.Text = offset.ToString();
                updateEditorGridHeight();
            }
        }

        private void txtGridSpacing_LostFocus(object sender, RoutedEventArgs e) {
            double spacing;
            double prev = double.Parse((string)getForMapCustomInfoDat("_editorGridSpacing"));
            if (double.TryParse(txtGridSpacing.Text, out spacing) && spacing != prev) {
                setForMapCustomInfoDat("_editorGridSpacing", spacing);
                txtGridSpacing.Text = spacing.ToString();
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
            if (div != gridDivision) {
                gridDivision = div;
                txtGridDivision.Text = div.ToString();
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
            }
            setValInfoDat("_environmentName", env);
        }

        private void EditorGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
        }
        private void _EditorGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            this.Dispatcher.Invoke(() => {
                if (songIsPlaying) {
                    endSongPlayback();
                }
                if (infoStr != null) {
                    rescanNotes();
                    updateEditorGridHeight();
                    drawEditorGrid();
                    //if (e.PreviousSize.Height <= EditorPanel.ActualHeight) {
                    //    scrollEditor.ScrollToBottom();
                    //} else {
                    //    // try to maintain scroll position at the same time in the song
                    //    //scrollEditor.ScrollToVerticalOffset(scrollEditor.ScrollableHeight * (1 - songStream.CurrentTime/songStream.TotalTime));
                    //    sliderSongProgress.Value = songStream.CurrentTime.TotalMilliseconds;
                    //}
                }
            });
        }

        // (re)initialise UI
        private void initUI() {
            txtSongName.Text = (string) getValInfoDat("_songName");
            txtArtistName.Text = (string) getValInfoDat("_songAuthorName");
            txtMapperName.Text = (string) getValInfoDat("_levelAuthorName");
            txtSongBPM.Text = (string) getValInfoDat("_beatsPerMinute");
 
            switch ((string)getValInfoDat("_environmentName")) {
                case "DefaultEnvironment":
                    comboEnvironment.SelectedIndex = 0; break;
                case "Alfheim":
                    comboEnvironment.SelectedIndex = 1; break;
                case "Nidavellir":
                    comboEnvironment.SelectedIndex = 2; break;
                default:
                    comboEnvironment.SelectedIndex = 0; break;
            }
            
            txtSongFileName.Text = (string)getValInfoDat("_songFilename") == "" ? "N/A" : (string)getValInfoDat("_songFilename");
            txtCoverFileName.Text = (string)getValInfoDat("_coverImageFilename") == "" ? "N/A" : (string)getValInfoDat("_coverImageFilename");
            if (txtCoverFileName.Text != "N/A") {
                loadCoverImage();
            }
            var duration = (int) songStream.TotalTime.TotalSeconds;
            txtSongDuration.Text = $"{duration / 60}:{(duration % 60).ToString("D2")}";
         
            gridDivision = 4;
            txtGridDivision.Text = "4";
            txtGridOffset.Text = (string)getForMapCustomInfoDat("_editorOffset");
            txtGridSpacing.Text = (string)getForMapCustomInfoDat("_editorGridSpacing");

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
            comboEnvironment.IsEnabled = true;
            btnPickSong.IsEnabled = true;
            btnPickCover.IsEnabled = true;
            sliderSongVol.IsEnabled = true;
            sliderDrumVol.IsEnabled = true;
            txtGridDivision.IsEnabled = true;
            txtGridOffset.IsEnabled = true;
            txtGridSpacing.IsEnabled = true;
            btnDeleteDifficulty.IsEnabled = true;
            btnSongPlayer.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;
            scrollEditor.IsEnabled = true;

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

        private void setForMapInfoDat(string key, object value, int indx) {
            var obj = JObject.Parse(infoStr);
            obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key] = JToken.FromObject(value);
            infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        private JToken getForMapInfoDat(string key, int indx) {
            var obj = JObject.Parse(infoStr);
            var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key];
            return res;
        }

        private void setForMapCustomInfoDat(string key, object value) {
            var obj = JObject.Parse(infoStr);
            obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][selectedDifficulty]["_customData"][key] = JToken.FromObject(value);
            infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        } 

        private JToken getForMapCustomInfoDat(string key) {
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
            var filename = (string) getForMapInfoDat("_beatmapFilename", indx);
            mapsStr[indx] = File.ReadAllText(absPath(filename));
        }

        private void writeMapStr(int indx) {
            var filename = (string) getForMapInfoDat("_beatmapFilename", indx);
            File.WriteAllText(absPath(filename), mapsStr[indx]);
        }

        private void deleteMapStr(int indx) {
            var filename = (string)getForMapInfoDat("_beatmapFilename", indx);
            File.Delete(absPath(filename));
            mapsStr[indx] = "";
        }

        private void renameMapStr() {
            for (int i = 0; i < numDifficulties; i++) {
                setForMapInfoDat("_difficulty", difficultyNames[i], i);
                var oldFile = (string) getForMapInfoDat("_beatmapFilename", i);
                File.Move(absPath(oldFile), absPath($"{difficultyNames[i]}_temp.dat"));
                setForMapInfoDat("_beatmapFilename", $"{difficultyNames[i]}.dat", i);
            }
            for (int i = 0; i < numDifficulties; i++) {
                File.Move(absPath($"{difficultyNames[i]}_temp.dat"), absPath($"{difficultyNames[i]}.dat"));
            }
        }

        private (double, int)[] getMapStrNotes(int indx) {
            var obj = JObject.Parse(mapsStr[indx]);
            var res = obj["_notes"];
            (double, int)[] output = new (double, int)[res.Count()];
            var i = 0;
            foreach (JToken n in res) {
                double time = double.Parse((string)n["_time"]);
                int colIndex = int.Parse((string)n["_lineIndex"]);
                output[i] = (time, colIndex);
                i++;
            }
            return output;
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
            songPlayer = new WaveOut();
            songChannel = new SampleChannel(songStream);
            songPlayer.Init(songChannel);
            sliderSongProgress.Minimum = 0;
            sliderSongProgress.Maximum = songStream.TotalTime.TotalSeconds * 1000;
            
        }

        private void loadCoverImage() {
            var fileName = (string)getValInfoDat("_coverImageFilename");
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = new Uri(absPath(fileName));
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.EndInit();
            imgCover.Source = b;

            txtCoverFileName.Text = fileName;
        }

        private void updateEditorGridHeight() {
            if (infoStr == null) {
                return;
            }

            // set editor grid height
            double userOffset = double.Parse((string)getForMapCustomInfoDat("_editorOffset"));
            double beats = (currentBPM / 60) * songStream.TotalTime.TotalSeconds;

            // this triggers a grid redraw
            EditorGrid.Height = beats * unitLength + scrollEditor.ActualHeight;
        }

        private void drawEditorGrid() {

            if (infoStr == null) {
                return;
            }

            Trace.WriteLine("INFO: Redrawing editor grid...");

            EditorGrid.Children.Clear();

            // calculate new drawn ranges for pagination, if we need it...
            //editorDrawRangeLower  = Math.Max(editorScrollPosition -     (gridDrawRange) * scrollEditor.ActualHeight, 0                      );
            //editorDrawRangeHigher = Math.Min(editorScrollPosition + (1 + gridDrawRange) * scrollEditor.ActualHeight, EditorGrid.ActualHeight);


            // calculate grid offset: default is 
            double userOffset = double.Parse((string)getForMapCustomInfoDat("_editorOffset"));
            double beats = currentBPM * userOffset/60;

            //            default                  user specified
            var offset = (unitHeight / 2 - 2.5) + (beats * unitLength);

            // draw gridlines
            int counter = 0;
            while (offset <= EditorGrid.ActualHeight) { 
                var l = new Line();
                l.X1 = 0;
                l.X2 = EditorGrid.ActualWidth;
                l.Y1 = offset;
                l.Y2 = offset;
                l.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(
                    (counter % gridDivision == 0) ? gridColourMajor : gridColourMinor)
                );
                l.StrokeThickness = (counter % gridDivision == 0) ? gridThicknessMajor : gridThicknessMinor;
                Canvas.SetBottom(l, offset);
                EditorGrid.Children.Add(l);
                offset += unitLength/gridDivision;
                counter++;
            }
            // init drum note image
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = new Uri("pack://application:,,,/resources/placeholder.png");
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.EndInit();

            // draw drum notes
            // todo: paginate these, they cause lag when resizing
            offset = 0;
            foreach (var n in selectedDifficultyNotes) {
                var img = new Image();          
                img.Source = b;
                img.Width = unitLength;
                img.Height = unitHeight;

                var noteHeight = offset + n.Item1 * unitLength;
                var noteXOffset = (1 + 4 * n.Item2) * unitLengthUnscaled / 3;
                // for some reason, WPF does not display notes in the correct x-position with a Grid Scaling multiplier not equal to 1.
                // e.g. Canvas.SetLeft(img, 0) leaves a small gap between the left side of the Canvas and the img
                var unknownNoteXAdjustment = ((unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2);

                Canvas.SetBottom(img, noteHeight);
                Canvas.SetLeft(img, noteXOffset - unknownNoteXAdjustment);
                EditorGrid.Children.Add(img);
            }

            // rescan notes after drawing
            rescanNotes();
        }

        private void playDrumHit() {
            var res = drummer.playDrum();
            if (res == false) {
                Trace.WriteLine("WARNING: skipped a drum hit");
            }
        }

        private void rescanNotes() {
            // calculate scan index for playing drum hits
            var seekBeat = (sliderSongProgress.Value / 1000.0) * (currentBPM / 60.0);
            noteScanIndex = 0;
            foreach (var n in selectedDifficultyNotes) {
                if (n.Item1 >= seekBeat) {
                    break;
                }
                noteScanIndex++;
            }
        }


        private void beginSongPlayback() {
            songIsPlaying = true;

            // disable some UI elements for performance reasons
            txtGridDivision.IsEnabled = false;
            txtGridOffset.IsEnabled = false;
            txtGridSpacing.IsEnabled = false;
            btnDeleteDifficulty.IsEnabled = false;
            btnChangeDifficulty0.IsEnabled = false;
            btnChangeDifficulty1.IsEnabled = false;
            btnChangeDifficulty2.IsEnabled = false;
            btnAddDifficulty.IsEnabled = false;

            // update seek time from slider
            var seek = (int)sliderSongProgress.Value;
            var seekTime = new TimeSpan(0, 0, (seek / 1000) / 60, (seek / 1000) % 60, seek % 1000);
            songStream.CurrentTime = seekTime;

            // calculate scan index for playing drum hits
            rescanNotes();

            // disable scrolling while playing
            scrollEditor.IsEnabled = false;
            sliderSongProgress.IsEnabled = false;

            // animate for smooth scrolling 
            var remainingTime = songStream.TotalTime.TotalMilliseconds - songStream.CurrentTime.TotalMilliseconds;
            var remainingTimeSpan = new TimeSpan(0, 0, 0, 0, (int)(remainingTime));

            DoubleAnimation anim = new DoubleAnimation();
            anim.From = sliderSongProgress.Value;
            anim.To = sliderSongProgress.Maximum;
            anim.Duration = new Duration(remainingTimeSpan);
            sliderSongProgress.BeginAnimation(Slider.ValueProperty, anim);

            // play song
            songPlayer.Play();
        }

        private void endSongPlayback() {
            songIsPlaying = false;
            // disable some UI elements for performance reasons
            // song/note playback gets desynced if these are changed during playback
            // TODO: fix this?
            txtGridDivision.IsEnabled = true;
            txtGridOffset.IsEnabled = true;
            txtGridSpacing.IsEnabled = true;
            btnDeleteDifficulty.IsEnabled = true;
            enableDifficultyButtons(selectedDifficulty);
            btnAddDifficulty.IsEnabled = true;

            // enable scrolling while paused
            scrollEditor.IsEnabled = true;
            sliderSongProgress.IsEnabled = true;

            sliderSongProgress.BeginAnimation(Slider.ValueProperty, null);

            songPlayer.Stop();
        }

        //=======================

        private bool approximatelyEqual(double x, double y, double delta) {
            return Math.Abs(x - y) < delta;
        }
    }
}

using Edda.Const;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;

namespace Edda {
    public partial class MainWindow : Window {
        // main window controls
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
            if (deviceEnumerator != null) {
                if (deviceChangeListener != null) {
                    deviceEnumerator.UnregisterEndpointNotificationCallback(deviceChangeListener);
                }
                deviceEnumerator.Dispose();
            }
            var oldSongPlayer = songPlayer;
            songPlayer = null;
            oldSongPlayer?.Stop();
            oldSongPlayer?.Dispose();

            var oldSongStream = songStream;
            songStream = null;
            oldSongStream?.Dispose();

            noteScanner?.Stop();
            beatScanner?.Stop();

            var oldDrummer = drummer;
            drummer = null;
            oldDrummer?.Dispose();

            var oldMetronome = metronome;
            metronome = null;
            oldMetronome?.Dispose();
            Trace.WriteLine("INFO: Audio resources disposed...");

            // TODO find other stuff to dispose so we don't cause a memory leak
            // Application.Current.Shutdown();
        }
        private void AppMainWindow_KeyDown(object sender, KeyEventArgs e) {

        #if DEBUG
            if (e.Key == Key.D) {
                Trace.WriteLine(colSpectrogram.Width.Value);
            }
        #endif

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
                    PauseSong();
                    CreateNewMap();
                }
                // open map (Ctrl-O)
                if (e.Key == Key.O) {
                    PauseSong();
                    OpenMap();
                }
                // save map (Ctrl-S)
                if (e.Key == Key.S) {
                    BackupAndSaveBeatmap();
                }
                // import map (Ctrl-I)
                if (e.Key == Key.I) {
                    PauseSong();
                    ImportMap();
                }
                // export map (Ctrl-E)
                if (e.Key == Key.E) {
                    PauseSong();
                    ExportMap();
                }
                // close map (Ctrl-W)
                if (e.Key == Key.W) {
                    PauseSong();
                    returnToStartMenuOnClose = true;
                    this.Close();
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

            if (!gridController.isMapDifficultySelected) {
                return;
            }

            // ctrl shortcuts
            if (ctrlKeyDown) {
                // select all (Ctrl-A)
                if (e.Key == Key.A && !songIsPlaying) {
                    mapEditor.SelectAllNotes();
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
                    gridController.PasteClipboardWithOffset(shiftKeyDown);
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
                    mapEditor.MirrorSelection();
                }

                // add bookmark (Ctrl-B)
                if (e.Key == Key.B && !songIsPlaying) {
                    gridController.CreateBookmark();
                }

                // add timing change (Ctrl-T)
                if (e.Key == Key.T && !songIsPlaying) {
                    gridController.CreateBPMChange(shiftKeyDown);
                }

                // toggle grid snap (Ctrl-G)
                if (e.Key == Key.G) {
                    checkGridSnap.IsChecked = !(checkGridSnap.IsChecked == true);
                    CheckGridSnap_Click(null, null);
                }

                // quantize selection (Ctrl-Q)
                if (e.Key == Key.Q && !songIsPlaying) {
                    mapEditor.QuantizeSelection();
                }
            }

            if ((e.Key == Key.D1 || e.Key == Key.D2 || e.Key == Key.D3 || e.Key == Key.D4) &&
                (songIsPlaying || gridController.isMouseOnEditingGrid) &&
                    !(FocusManager.GetFocusedElement(this) is TextBox)) {

                int col = e.Key - Key.D1;
                gridController.AddNoteAt(col, !songIsPlaying);
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
                TxtGridDivision_LostFocus(sender, null);
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

            if (!gridController.isMapDifficultySelected) {
                return;
            }

            var keyStr = e.Key.ToString();
            if (shiftKeyDown) {
                if (keyStr == "Up") {
                    gridController.ShiftSelectionByRow(MoveNote.MOVE_BEAT_UP);
                    e.Handled = true;
                }
                if (keyStr == "Down") {
                    gridController.ShiftSelectionByRow(MoveNote.MOVE_BEAT_DOWN);
                    e.Handled = true;
                }
            }
            if (ctrlKeyDown) {
                if (keyStr == "Up") {
                    gridController.ShiftSelectionByRow(MoveNote.MOVE_GRID_UP);
                    e.Handled = true;
                }
                if (keyStr == "Down") {
                    gridController.ShiftSelectionByRow(MoveNote.MOVE_GRID_DOWN);
                    e.Handled = true;
                }
            }
            if (shiftKeyDown || ctrlKeyDown) {
                if (keyStr == "Left") {
                    mapEditor.ShiftSelectionByCol(-1);
                    e.Handled = true;
                }
                if (keyStr == "Right") {
                    mapEditor.ShiftSelectionByCol(1);
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

        private void AppMainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!ctrlKeyDown) {
                return;
            }

            var delta = (int) Helper.DoubleRangeTruncate(e.Delta, -1, 1);

            double pos = gridController.mouseBeat;
            BPMChange lastChange = mapEditor.GetLastBeatChange(pos);

            if (lastChange.globalBeat == 0.0) {
                int currentBeatDivision;
                int.TryParse(txtGridDivision.Text, out currentBeatDivision);
                txtGridDivision.Text = ((int)Helper.DoubleRangeTruncate(currentBeatDivision + delta, 1, Editor.GridDivisionMax)).ToString();
            } else {    
                var currentBpmChange = mapEditor.currentMapDifficulty.bpmChanges
                    .Where(bpmChange => bpmChange.globalBeat < pos)
                    .OrderByDescending(bpmChange => bpmChange.globalBeat)
                    .FirstOrDefault();

                if (currentBpmChange != null) {
                    currentBpmChange.gridDivision = (int) Helper.DoubleRangeTruncate(currentBpmChange.gridDivision + delta, 1, Editor.GridDivisionMax);
                    gridController.DrawGrid(false);
                }
            }
            e.Handled = true; // Mark tunneling event as handled to prevent scrolling on the grid while changing the division.
        }

        // other UI elements
        private void BtnPickSong_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            string file = SelectSongDialog();
            if (file == null) {
                return;
            }
            ClearSongCache();
            LoadSongFile(file);
        }
        private void BtnMakePreview_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<SongPreviewWindow>();
            if (win == null) {
                int selectedTime = (int)(sliderSongProgress.Value / 1000.0);
                var songFile = (string)mapEditor.GetMapValue("_songFilename");
                win = new SongPreviewWindow(mapEditor.mapFolder, Path.Combine(mapEditor.mapFolder, songFile), selectedTime / 60, selectedTime % 60);
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
            var res = MessageBox.Show("Copy over bookmarks and BPM changes from the currently selected map?", "Copy Existing Map Data?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            mapEditor.CreateDifficulty(res == MessageBoxResult.Yes);
            SwitchDifficultyMap(mapEditor.numDifficulties - 1);
            mapEditor.SortDifficulties();
            DrawEditorGrid();
            UpdateDifficultyButtons();
        }
        private void BtnDeleteDifficulty_Click(object sender, RoutedEventArgs e) {
            var res = MessageBox.Show("Are you sure you want to delete this difficulty? This cannot be undone.", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) {
                return;
            }
            PauseSong();
            var selectedDifficultyIsStillValid = mapEditor.DeleteDifficulty();
            if (!selectedDifficultyIsStillValid) {
                SwitchDifficultyMap(mapEditor.numDifficulties - 1);
            }
            DrawEditorGrid();
            UpdateDifficultyButtons();
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
        private void sliderSongTempo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!mapIsLoaded) {
                return;
            }
            double newTempo = sliderSongTempo.Value;
            songTempoStream.Tempo = newTempo;
            noteScanner.SetTempo(newTempo);
            beatScanner.SetTempo(newTempo);
            txtSongTempo.Text = $"{Math.Round(newTempo, 2).ToString("0.00")}x";
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
                    var result = MessageBox.Show("Would you like to convert all notes and markers so that they remain at the same time?", "", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) {
                        txtSongBPM.Text = prevBPM.ToString();
                        return;
                    } else if (result == MessageBoxResult.Yes) {
                        mapEditor.RetimeNotesAndMarkers(BPM, prevBPM);
                    }
                    mapEditor.SetMapValue("_beatsPerMinute", BPM);
                    globalBPM = BPM;
                    DrawEditorGrid();
                }
            } else {
                MessageBox.Show($"The BPM must be a positive number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BPM = prevBPM;
            }
            txtSongBPM.Text = BPM.ToString();
        }
        private void TxtSongBPM_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) {
                TxtSongBPM_LostFocus(sender, e);
            }
        }
        private void BtnChangeBPM_Click(object sender, RoutedEventArgs e) {
            var win = Helper.GetFirstWindow<ChangeBPMWindow>();
            if (win == null) {
                win = new ChangeBPMWindow(this, gridController.currentMapDifficultyBpmChanges);
                win.Topmost = true;
                win.Owner = this;
                win.Show();
            } else {
                win.Focus();
            }
        }
        private void TxtSongOffset_LostFocus(object sender, RoutedEventArgs e) {
            double offset;
            double prevOffset = Helper.DoubleParseInvariant((string)mapEditor.GetMapValue("_songTimeOffset"));
            if (double.TryParse(txtSongOffset.Text, out offset)) {
                mapEditor.SetMapValue("_songTimeOffset", offset);
            } else {
                MessageBox.Show($"The song offset must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                offset = prevOffset;
            }
            txtSongOffset.Text = offset.ToString();
        }
        private void TxtSongName_TextChanged(object sender, TextChangedEventArgs e) {
            mapEditor.SetMapValue("_songName", txtSongName.Text);

            // update the name of the map in recently opened folders
            recentMaps.RemoveRecentlyOpened(mapEditor.mapFolder);
            recentMaps.AddRecentlyOpened((string)mapEditor.GetMapValue("_songName"), mapEditor.mapFolder);
            recentMaps.Write();

        }
        private void TxtSongName_LostFocus(object sender, RoutedEventArgs e) {
            txtSongName.ScrollToHome();
        }
        private void TxtArtistName_TextChanged(object sender, TextChangedEventArgs e) {
            mapEditor.SetMapValue("_songAuthorName", txtArtistName.Text);
        }
        private void TxtArtistName_LostFocus(object sender, RoutedEventArgs e) {
            txtArtistName.ScrollToHome();
        }
        private void TxtMapperName_TextChanged(object sender, TextChangedEventArgs e) {
            mapEditor.SetMapValue("_levelAuthorName", txtMapperName.Text);
        }
        private void TxtMapperName_LostFocus(object sender, RoutedEventArgs e) {
            txtMapperName.ScrollToHome();
        }
        private void checkExplicitContent_Click(object sender, RoutedEventArgs e) {
            mapEditor.SetMapValue("_explicit", (checkExplicitContent.IsChecked == true).ToString().ToLower());
        }
        private void TxtDifficultyNumber_LostFocus(object sender, RoutedEventArgs e) {
            int prevLevel = (int)mapEditor.GetMapValue("_difficultyRank", RagnarockMapDifficulties.Current);
            int level;
            if (int.TryParse(txtDifficultyNumber.Text, out level) && Helper.DoubleRangeCheck(level, Editor.Difficulty.LevelMin, Editor.Difficulty.LevelMax)) {
                if (level != prevLevel) {
                    mapEditor.SetMapValue("_difficultyRank", level, RagnarockMapDifficulties.Current);
                    mapEditor.SortDifficulties();
                    UpdateDifficultyButtons();
                }
            } else {
                MessageBox.Show($"The difficulty level must be an integer between {Editor.Difficulty.LevelMin} and {Editor.Difficulty.LevelMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                level = prevLevel;
            }
            txtDifficultyNumber.Text = level.ToString();
        }
        private void TxtDifficultyNumber_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) {
                TxtDifficultyNumber_LostFocus(sender, null);
            }
        }
        private void TxtNoteSpeed_LostFocus(object sender, RoutedEventArgs e) {
            double prevSpeed = int.Parse((string)mapEditor.GetMapValue("_noteJumpMovementSpeed", RagnarockMapDifficulties.Current));
            double speed;
            if (double.TryParse(txtNoteSpeed.Text, out speed) && speed > 0) {
                mapEditor.SetMapValue("_noteJumpMovementSpeed", speed, RagnarockMapDifficulties.Current);
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
            bool newVal = checkGridSnap.IsChecked == true;
            gridController.snapToGrid = newVal;
            MenuItemSnapToGrid.IsChecked = newVal;
        }
        private void TxtGridSpacing_LostFocus(object sender, RoutedEventArgs e) {
            double prevSpacing = Helper.DoubleParseInvariant((string)mapEditor.GetMapValue("_editorGridSpacing", RagnarockMapDifficulties.Current, custom: true));
            double spacing;
            if (double.TryParse(txtGridSpacing.Text, out spacing)) {
                if (spacing != prevSpacing) {
                    gridController.gridSpacing = spacing;
                    mapEditor.SetMapValue("_editorGridSpacing", spacing, RagnarockMapDifficulties.Current, custom: true);
                    DrawEditorGrid();
                }
            } else {
                MessageBox.Show($"The grid spacing must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                spacing = prevSpacing;
            }
            txtGridSpacing.Text = spacing.ToString();
        }
        private void TxtGridSpacing_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) {
                TxtGridSpacing_LostFocus(sender, null);
            }
        }
        private void TxtGridDivision_LostFocus(object sender, RoutedEventArgs e) {
            int prevDiv = int.Parse((string)mapEditor.GetMapValue("_editorGridDivision", RagnarockMapDifficulties.Current, custom: true));
            int div;

            if (int.TryParse(txtGridDivision.Text, out div) && Helper.DoubleRangeCheck(div, 1, Editor.GridDivisionMax)) {
                if (div != prevDiv) {
                    gridController.gridDivision = div;
                    mapEditor.SetMapValue("_editorGridDivision", div, RagnarockMapDifficulties.Current, custom: true);
                    defaultGridDivision = div;
                    DrawEditorGrid(false);
                }
            } else {
                MessageBox.Show($"The grid division amount must be an integer from 1 to {Editor.GridDivisionMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                div = prevDiv;
            }
            txtGridDivision.Text = div.ToString();
        }
        private void TxtGridDivision_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) {
                TxtGridDivision_LostFocus(sender, null);
            }
        }
        private void CheckWaveform_Click(object sender, RoutedEventArgs e) {
            if (checkWaveform.IsChecked == true) {
                gridController.showWaveform = true;
                gridController.DrawMainWaveform();
            } else {
                gridController.showWaveform = false;
                gridController.UndrawMainWaveform();
            }
        }
        private void ComboEnvironment_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            //var env = (string)comboEnvironment.SelectedItem;
            //if (env == Constants.BeatmapDefaults.DefaultEnvironmentAlias) {
            //    env = "DefaultEnvironment";
            //}
            mapEditor.SetMapValue("_environmentName", (string)comboEnvironment.SelectedItem);
        }
    }
}

using Edda.Const;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Edda {
    public partial class MainWindow : Window {
        private void BtnPickSong_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            string file = SelectSongDialog();
            if (file == null) {
                return;
            }
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
            if (int.TryParse(txtDifficultyNumber.Text, out level) && Helper.DoubleRangeCheck(level, Editor.DifficultyLevelMin, Editor.DifficultyLevelMax)) {
                if (level != prevLevel) {
                    mapEditor.SetMapValue("_difficultyRank", level, RagnarockMapDifficulties.Current);
                    mapEditor.SortDifficulties();
                    UpdateDifficultyButtons();
                }
            } else {
                MessageBox.Show($"The difficulty level must be an integer between {Editor.DifficultyLevelMin} and {Editor.DifficultyLevelMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                level = prevLevel;
            }
            txtDifficultyNumber.Text = level.ToString();
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
        private void TxtGridDivision_LostFocus(object sender, RoutedEventArgs e) {
            int prevDiv = int.Parse((string)mapEditor.GetMapValue("_editorGridDivision", RagnarockMapDifficulties.Current, custom: true));
            int div;

            if (int.TryParse(txtGridDivision.Text, out div) && Helper.DoubleRangeCheck(div, 1, Editor.GridDivisionMax)) {
                if (div != prevDiv) {
                    gridController.gridDivision = div;
                    mapEditor.SetMapValue("_editorGridDivision", div, RagnarockMapDifficulties.Current, custom: true);
                    DrawEditorGrid(false);
                }
            } else {
                MessageBox.Show($"The grid division amount must be an integer from 1 to {Editor.GridDivisionMax}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                div = prevDiv;
            }
            txtGridDivision.Text = div.ToString();
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

using Edda.Const;
using System;
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
            LoadSong(file);
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
            var res = MessageBox.Show("Copy over bookmarks and BPM changes from the currently selected map?", "Copy Existing Map Data?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            mapEditor.CreateDifficulty(res == MessageBoxResult.Yes);
            SwitchDifficultyMap(beatMap.numDifficulties - 1);
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
            mapEditor.DeleteDifficulty();
            if (mapEditor.currentDifficultyIndex >= beatMap.numDifficulties - 1) {
                SwitchDifficultyMap(beatMap.numDifficulties - 1);
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
            if (beatMap == null) {
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
            double prevBPM = mapEditor.globalBPM;
            if (double.TryParse(txtSongBPM.Text, out BPM) && BPM > 0) {
                if (BPM != prevBPM) {
                    var result = MessageBox.Show("Would you like to convert all BPM changes and notes so that they remain at the same time?", "", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) {
                        txtSongBPM.Text = prevBPM.ToString();
                        return;
                    } else if (result == MessageBoxResult.Yes) {
                        foreach (var bc in mapEditor.currentMapDifficulty.bpmChanges) {
                            bc.globalBeat *= BPM / prevBPM;
                        }
                        foreach (var n in mapEditor.currentMapDifficulty.notes) {
                            n.beat *= BPM / prevBPM;
                        }
                        foreach (var b in mapEditor.currentMapDifficulty.bookmarks) {
                            b.beat *= BPM / prevBPM;
                        }
                    }
                    beatMap.SetValue("_beatsPerMinute", BPM);
                    mapEditor.globalBPM = BPM;
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
                win = new ChangeBPMWindow(this, mapEditor.currentMapDifficulty.bpmChanges);
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
            int prevLevel = (int)beatMap.GetValueForMap(mapEditor.currentDifficultyIndex, "_difficultyRank");
            int level;
            if (int.TryParse(txtDifficultyNumber.Text, out level) && Helper.DoubleRangeCheck(level, Editor.DifficultyLevelMin, Editor.DifficultyLevelMax)) {
                if (level != prevLevel) {
                    beatMap.SetValueForMap(mapEditor.currentDifficultyIndex, "_difficultyRank", level);
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
            double prevSpeed = int.Parse((string)beatMap.GetValueForMap(mapEditor.currentDifficultyIndex, "_noteJumpMovementSpeed"));
            double speed;
            if (double.TryParse(txtNoteSpeed.Text, out speed) && speed > 0) {
                beatMap.SetValueForMap(mapEditor.currentDifficultyIndex, "_noteJumpMovementSpeed", speed);
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
            editorUI.snapToGrid = newVal;
            MenuItemSnapToGrid.IsChecked = newVal;
        }
        private void TxtGridOffset_LostFocus(object sender, RoutedEventArgs e) {
            double prevOffset = Helper.DoubleParseInvariant((string)beatMap.GetCustomValueForMap(mapEditor.currentDifficultyIndex, "_editorOffset"));
            double offset;
            if (double.TryParse(txtGridOffset.Text, out offset)) {
                if (offset != prevOffset) {
                    // resnap all notes
                    var dialogResult = MessageBox.Show("Resnap all currently placed notes to align with the new grid?\nThis cannot be undone.", "", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dialogResult == MessageBoxResult.Yes) {
                        ResnapAllNotes(offset);
                    }

                    editorUI.gridOffset = offset;
                    beatMap.SetCustomValueForMap(mapEditor.currentDifficultyIndex, "_editorOffset", offset);
                    DrawEditorGrid();
                }
            } else {
                MessageBox.Show($"The grid offset must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                offset = prevOffset;
            }
            txtGridOffset.Text = offset.ToString();
        }
        private void TxtGridSpacing_LostFocus(object sender, RoutedEventArgs e) {
            double prevSpacing = Helper.DoubleParseInvariant((string)beatMap.GetCustomValueForMap(mapEditor.currentDifficultyIndex, "_editorGridSpacing"));
            double spacing;
            if (double.TryParse(txtGridSpacing.Text, out spacing)) {
                if (spacing != prevSpacing) {
                    editorUI.gridSpacing = spacing;
                    beatMap.SetCustomValueForMap(mapEditor.currentDifficultyIndex, "_editorGridSpacing", spacing);
                    DrawEditorGrid();
                }
            } else {
                MessageBox.Show($"The grid spacing must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                spacing = prevSpacing;
            }
            txtGridSpacing.Text = spacing.ToString();
        }
        private void TxtGridDivision_LostFocus(object sender, RoutedEventArgs e) {
            int prevDiv = int.Parse((string)beatMap.GetCustomValueForMap(mapEditor.currentDifficultyIndex, "_editorGridDivision"));
            int div;

            if (int.TryParse(txtGridDivision.Text, out div) && Helper.DoubleRangeCheck(div, 1, Editor.GridDivisionMax)) {
                if (div != prevDiv) {
                    editorUI.gridDivision = div;
                    beatMap.SetCustomValueForMap(mapEditor.currentDifficultyIndex, "_editorGridDivision", div);
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
                editorUI.showWaveform = true;
                editorUI.DrawMainWaveform();
            } else {
                editorUI.showWaveform = false;
                editorUI.UndrawMainWaveform();
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
                var lineY = sliderSongProgress.Value / sliderSongProgress.Maximum * borderNavWaveform.ActualHeight;
                editorUI.DrawNavWaveform();
                editorUI.DrawNavBookmarks();
                editorUI.SetSongMouseoverLinePosition(lineY);
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
            editorUI.SetSongMouseoverLinePosition(mouseY);
            var mouseTime = sliderSongProgress.Maximum * (1 - mouseY / borderNavWaveform.ActualHeight);
            if (navMouseDown) {
                sliderSongProgress.Value = mouseTime;
            }
            lblSelectedBeat.Content = $"Time: {Helper.TimeFormat(mouseTime / 1000)}, Global Beat: {Math.Round(mouseTime / 60000 * mapEditor.globalBPM, 3)}";
        }
        private void BorderNavWaveform_MouseEnter(object sender, MouseEventArgs e) {
            lineSongMouseover.Opacity = 1;
        }
        private void BorderNavWaveform_MouseLeave(object sender, MouseEventArgs e) {
            navMouseDown = false;
            lineSongMouseover.Opacity = 0;
            lblSelectedBeat.Content = "";
        }
        private void ScrollEditor_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (beatMap != null) {
                editorUI.UpdateGridHeight();
            }
            if (e.WidthChanged) {
                if (beatMap != null) {
                    DrawEditorGrid();
                }
            } else if (beatMap != null && editorUI.showWaveform) {
                editorUI.DrawMainWaveform();
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
        private void scrollEditor_MouseMove(object sender, MouseEventArgs e) {
            Point mousePos = e.GetPosition(EditorGrid);
            editorUI.GridMouseMove(mousePos, shiftKeyDown);

            // update beat display
            lblSelectedBeat.Content = $"Time: {Helper.TimeFormat(editorUI.snappedBeat * 60 / mapEditor.globalBPM)}, Global Beat: {Math.Round(editorUI.snappedBeat, 3)} ({Math.Round(editorUI.unsnappedBeat, 3)})";

            // initiate drag selection
            if (editorMouseDown) {
                Vector delta = mousePos - editorDragSelectStart;
                if (delta.Length > Editor.DragInitThreshold) {
                    editorUI.BeginDragSelection(mousePos);
                }
            }
        }
        private void scrollEditor_MouseEnter(object sender, MouseEventArgs e) {
            editorUI.SetPreviewNoteVisibility(Visibility.Visible);
            editorUI.SetMouseoverLineVisibility(Visibility.Visible);
        }
        private void scrollEditor_MouseLeave(object sender, MouseEventArgs e) {
            editorUI.SetPreviewNoteVisibility(Visibility.Hidden);
            editorUI.SetMouseoverLineVisibility(Visibility.Hidden);
            lblSelectedBeat.Content = "";
        }
        private void scrollEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            Point mousePos = e.GetPosition(EditorGrid);
            editorDragSelectStart = mousePos;
            editorUI.GridMouseDown(mousePos);
            editorMouseDown = true;
        }
        private void scrollEditor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            Point mousePos = e.GetPosition(EditorGrid);
            editorUI.GridMouseUp(mousePos, shiftKeyDown);
            editorMouseDown = false;
        }
        private void scrollEditor_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            // remove the note
            Note n = editorUI.mouseNote;
            if (mapEditor.currentMapDifficulty.notes.Contains(n)) {
                mapEditor.RemoveNotes(n);
            } else {
                mapEditor.UnselectAllNotes();
            }
        }
    }
}

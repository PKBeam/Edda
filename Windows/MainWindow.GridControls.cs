using Edda.Const;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Edda {
    public partial class MainWindow : Window {
        
        // grid variables
        Point editorDragSelectStart;
        internal bool navMouseDown = false;
        bool editorMouseDown = false;

        private void BorderNavWaveform_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (mapIsLoaded) {
                var lineY = sliderSongProgress.Value / sliderSongProgress.Maximum * borderNavWaveform.ActualHeight;
                gridController.DrawNavWaveform();
                gridController.DrawNavBookmarks();
                gridController.SetSongMouseoverLinePosition(lineY);
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
            gridController.SetSongMouseoverLinePosition(mouseY);
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
            navMouseDown = false;
            lineSongMouseover.Opacity = 0;
            lblSelectedBeat.Content = "";
        }
        private void ScrollEditor_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (mapIsLoaded) {
                gridController.UpdateGridHeight();
            }
            if (e.WidthChanged) {
                if (mapIsLoaded) {
                    DrawEditorGrid();
                }
            } else if (mapIsLoaded) {
                gridController.DrawScrollingWaveforms();
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

            scrollSpectrogram.ScrollToVerticalOffset(e.VerticalOffset);
        }
        private void ScrollEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

        }
        private void scrollEditor_MouseMove(object sender, MouseEventArgs e) {

            Point mousePos = e.GetPosition(EditorGrid);
            gridController.GridMouseMove(mousePos, shiftKeyDown);

            // update beat display
            lblSelectedBeat.Content = $"Time: {Helper.TimeFormat(gridController.snappedBeat * 60 / globalBPM)}, Global Beat: {Math.Round(gridController.snappedBeat, 3)} ({Math.Round(gridController.unsnappedBeat, 3)})";

            // initiate drag selection
            if (editorMouseDown) {
                Vector delta = mousePos - editorDragSelectStart;
                if (delta.Length > Editor.DragInitThreshold) {
                    gridController.BeginDragSelection(mousePos);
                }
            }
        }
        private void scrollEditor_MouseEnter(object sender, MouseEventArgs e) {
            gridController.SetPreviewNoteVisibility(Visibility.Visible);
            gridController.SetMouseoverLineVisibility(Visibility.Visible);
        }
        private void scrollEditor_MouseLeave(object sender, MouseEventArgs e) {
            gridController.SetPreviewNoteVisibility(Visibility.Hidden);
            gridController.SetMouseoverLineVisibility(Visibility.Hidden);
            lblSelectedBeat.Content = "";
        }
        private void scrollEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (Keyboard.FocusedElement is TextBox) {
                return;
            }
            Point mousePos = e.GetPosition(EditorGrid);
            editorDragSelectStart = mousePos;
            gridController.GridMouseDown(mousePos);
            editorMouseDown = true;
        }
        private void scrollEditor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            Point mousePos = e.GetPosition(EditorGrid);
            gridController.GridMouseUp(mousePos, shiftKeyDown);
            editorMouseDown = false;
        }
        private void scrollEditor_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            gridController.GridRightMouseUp();
        }
    }
}

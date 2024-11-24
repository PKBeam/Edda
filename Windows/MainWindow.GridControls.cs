﻿using Edda.Const;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Edda {
    public partial class MainWindow : Window {

        // grid variables
        Point? editorDragSelectStart = null;
        internal bool navMouseDown = false;
        bool editorMouseDown = false;
        bool editorIsLoaded = false;

        // grid hold scroll
        bool editorIsHoldScrolling = false;
        bool editorIsDeferredHoldScrollingStarted = false;
        Point? editorScrollStart = null;

        private void BorderNavWaveform_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (mapIsLoaded) {
                var lineY = sliderSongProgress.Value / sliderSongProgress.Maximum * borderNavWaveform.ActualHeight;
                gridController.DrawNavWaveform();
                gridController.DrawNavBookmarks();
                gridController.DrawNavBPMChanges();
                canvasNavNotes.Children.Clear();
                gridController.DrawNavNotes(mapEditor.currentMapDifficulty.notes);
                gridController.HighlightNavNotes(mapEditor.currentMapDifficulty.selectedNotes);
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
        private void BorderSpectrogram_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (e.PreviousSize == new Size()) {
                return;
            }
            if (mapIsLoaded) {
                gridController.DrawSpectrogram();
            }
        }
        private void ScrollSpectrogram_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            scrollEditor.ScrollToVerticalOffset(scrollSpectrogram.VerticalOffset);
            ScrollEditor_ScrollChanged(null, e);
        }
        private void ScrollSpectrogram_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

        }
        private void ScrollEditor_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (e.PreviousSize == new Size()) {
                return;
            }
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
            if (e != null && e.ExtentHeightChange != 0) {
                scrollEditor.ScrollToVerticalOffset((1 - prevScrollPercent) * scrollEditor.ScrollableHeight);
                //Console.Write($"time: {txtSongPosition.Text} curr: {scrollEditor.VerticalOffset} max: {scrollEditor.ScrollableHeight} change: {e.ExtentHeightChange}\n");
            } else if (range != 0) {
                prevScrollPercent = (1 - curr / range);
            }

            scrollSpectrogram.ScrollToVerticalOffset(scrollEditor.VerticalOffset);
        }
        private void ScrollEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

        }
        private void scrollEditor_MouseMove(object sender, MouseEventArgs e) {
            if (!editorIsLoaded) {
                return;
            }
            if (editorIsHoldScrolling) {
                var currentPosition = e.GetPosition(scrollEditor);
                var offset = currentPosition - editorScrollStart.Value;
                offset.Y /= Editor.HoldScroll.Slowdown;

                if (Math.Abs(offset.Y) > Editor.HoldScroll.DeadZone / Editor.HoldScroll.Slowdown) {
                    editorIsDeferredHoldScrollingStarted = false; //standard scrolling (Mouse down -> Move)
                    scrollEditor.ScrollToVerticalOffset(scrollEditor.VerticalOffset + offset.Y);
                    ScrollEditor_ScrollChanged(null, null);
                }
            }
            Point mousePos = e.GetPosition(EditorGrid);
            gridController.GridMouseMove(mousePos);

            // update beat display
            lblSelectedBeat.Content = $"Time: {Helper.TimeFormat(gridController.snappedBeat * 60 / globalBPM)}, Global Beat: {Math.Round(gridController.snappedBeat, 3)} ({Math.Round(gridController.unsnappedBeat, 3)})";

            // initiate drag selection
            if (editorMouseDown && editorDragSelectStart.HasValue) {
                Vector delta = mousePos - editorDragSelectStart.Value;
                if (delta.Length > Editor.DragInitThreshold) {
                    gridController.BeginDragSelection(mousePos);
                }
            }
        }
        private void scrollEditor_MouseDown(object sender, MouseButtonEventArgs e) {
            if (!editorIsLoaded) {
                return;
            }
            if (editorIsHoldScrolling) //Moving with a released wheel and pressing a button
                scrollEditor_CancelHoldScrolling();
            else if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed) {
                if (!editorIsHoldScrolling) //Pressing a wheel the first time
                {
                    EditorGrid.CaptureMouse();
                    editorIsHoldScrolling = true;
                    editorScrollStart = e.GetPosition(sender as IInputElement);
                    editorIsDeferredHoldScrollingStarted = true; //the default value is true until the opposite value is set

                    scrollEditor_AddHoldScrollSign(e.GetPosition(scrollEditorHoldScrollLayer).X, e.GetPosition(scrollEditorHoldScrollLayer).Y);
                }
            }
        }
        private void scrollEditor_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released && editorIsDeferredHoldScrollingStarted != true) {
                scrollEditor_CancelHoldScrolling();
            }
        }
        private void scrollEditor_AddHoldScrollSign(double x, double y) {
            Canvas.SetLeft(scrollEditorHoldIcon, x - scrollEditorHoldIcon.Width / 2);
            Canvas.SetTop(scrollEditorHoldIcon, y - scrollEditorHoldIcon.Height / 2);
            scrollEditorHoldScrollLayer.Visibility = Visibility.Visible;
        }

        private void scrollEditor_RemoveHoldScrollSign() {
            scrollEditorHoldScrollLayer.Visibility = Visibility.Hidden;
        }
        private void scrollEditor_CancelHoldScrolling() {
            EditorGrid.ReleaseMouseCapture();
            editorIsHoldScrolling = false;
            editorScrollStart = null;
            editorIsDeferredHoldScrollingStarted = false;
            scrollEditor_RemoveHoldScrollSign();
        }
        private void scrollEditor_MouseEnter(object sender, MouseEventArgs e) {
            if (!editorIsLoaded) {
                return;
            }
            gridController.SetPreviewNoteVisibility(Visibility.Visible);
            gridController.SetMouseoverLineVisibility(Visibility.Visible);
        }
        private void scrollEditor_MouseLeave(object sender, MouseEventArgs e) {
            if (!editorIsLoaded) {
                return;
            }
            gridController.SetPreviewNoteVisibility(Visibility.Hidden);
            gridController.SetMouseoverLineVisibility(Visibility.Hidden);
            lblSelectedBeat.Content = "";
        }
        private void scrollEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (Keyboard.FocusedElement is TextBox || !editorIsLoaded) {
                return;
            }
            if (editorIsHoldScrolling) { //Moving with a released wheel and pressing a button
                scrollEditor_CancelHoldScrolling();
            } else {
                Point mousePos = e.GetPosition(EditorGrid);
                editorDragSelectStart = mousePos;
                gridController.GridMouseDown(mousePos);
                editorMouseDown = true;
            }
        }
        private void scrollEditor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!editorIsLoaded) {
                return;
            }
            if (editorIsHoldScrolling) { //Moving with a released wheel and pressing a button
                scrollEditor_CancelHoldScrolling();
            }
            if (editorDragSelectStart.HasValue) {
                Point mousePos = e.GetPosition(EditorGrid);
                gridController.GridMouseUp(mousePos);
                editorDragSelectStart = null;
                editorMouseDown = false;
            }
        }
        private void scrollEditor_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            if (!editorIsLoaded) {
                return;
            }
            if (editorIsHoldScrolling) { //Moving with a released wheel and pressing a button
                scrollEditor_CancelHoldScrolling();
            } else {
                gridController.GridRightMouseUp();
            }
        }
        private void scrollEditor_Loaded(object sender, RoutedEventArgs e) {
            // Wait for all the background operations to finish before accepting input for the grid.
            // This is a fix for an annoying bug, where mouse clicks were registered while the window or grid is still loading, which resulted in unintended notes being placed right after opening the map.
            Dispatcher.BeginInvoke(new Action(() => {
                editorIsLoaded = true;
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void EditorPanel_SizeChanged(object sender, SizeChangedEventArgs e) {
            /**
             * FIXME: the ratio of the spectrogram column is still not the same after resizing down in a way that makes the EditorPanel wider than the slot in the DockPanel.
             *        In this situation, this event doesn't trigger unless height is changed as well.
             *        If the height changes as well, this even handler will resize the width down, which triggers the event again (this time for width change) and the resulting ratio is not the same.
             * Replication steps: increase window width, drag spectrogram column as far right as possible and then reduce the window width.
             **/
            if (editorIsLoaded) {
                var previousSpectrogramRatio = (gridSpectrogram.ActualWidth - spectrogramResize.Width) / e.PreviousSize.Width;
                var newSpectrogramWidth = Math.Min(EditorPanel.ActualWidth, EditorPanel.MaxWidth) * previousSpectrogramRatio;
                gridSpectrogram.ColumnDefinitions[0].Width = new GridLength(newSpectrogramWidth);
            }
        }
    }

    public class DoubleOffsetConverter : IValueConverter {
        public double Offset {
            get; set;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return (double)value - Offset;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return (double)value + Offset;
        }
    }
}
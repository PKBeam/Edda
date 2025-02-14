using ColorPicker;
using Edda.Const;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace Edda {
    /// <summary>
    /// Interaction logic for CustomizeNavBarWindow.xaml
    /// </summary>
    public partial class CustomizeNavBarWindow : Window {
        readonly MainWindow caller;
        readonly UserSettingsManager userSettings;
        readonly bool doneInit = false;

        IDisposable ColorWaveformColorChangedDebounce;
        IDisposable ColorBookmarkColorChangedDebounce;
        IDisposable ColorBPMChangeColorChangedDebounce;
        IDisposable ColorNoteColorChangedDebounce;

        public CustomizeNavBarWindow(MainWindow caller, UserSettingsManager userSettings) {
            InitializeComponent();
            this.caller = caller;
            this.userSettings = userSettings;

            CheckWaveform.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavWaveform);
            var waveformColor = userSettings.GetValueForKey(UserSettingsKey.NavWaveformColor) ?? Editor.Waveform.ColourWPF.ToString();
            ColorWaveform.SelectedColor = (Color)ColorConverter.ConvertFromString(waveformColor);
            ToggleWaveformColorIsEnabled();
            ColorWaveformColorChangedDebounce = Observable
                .FromEventPattern<RoutedEventArgs>(ColorWaveform, nameof(PortableColorPicker.ColorChanged))
                .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(eventPattern =>
                    Dispatcher.Invoke(() =>
                        ColorWaveform_ColorChanged(eventPattern.Sender, eventPattern.EventArgs)
                    )
                );

            CheckBookmark.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavBookmarks);
            ColorBookmark.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBookmarkColor) ?? Editor.NavBookmark.Colour);
            ColorBookmark.SecondaryColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBookmarkNameColor) ?? Editor.NavBookmark.NameColour);
            SliderBookmarkShadowOpacity.Value = double.Parse(userSettings.GetValueForKey(UserSettingsKey.NavBookmarkShadowOpacity));
            ToggleBookmarkColorIsEnabled();
            ColorBookmarkColorChangedDebounce = Observable
                .FromEventPattern<RoutedEventArgs>(ColorBookmark, nameof(PortableColorPicker.ColorChanged))
                .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(eventPattern =>
                    Dispatcher.Invoke(() =>
                        ColorBookmark_ColorChanged(eventPattern.Sender, eventPattern.EventArgs)
                    )
                );

            CheckBPMChange.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavBPMChanges);
            ColorBPMChange.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeColor) ?? Editor.NavBPMChange.Colour);
            ColorBPMChange.SecondaryColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeLabelColor) ?? Editor.NavBPMChange.LabelColour);
            SliderBPMChangeShadowOpacity.Value = double.Parse(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeShadowOpacity));
            ToggleBPMChangeColorIsEnabled();
            ColorBPMChangeColorChangedDebounce = Observable
                .FromEventPattern<RoutedEventArgs>(ColorBPMChange, nameof(PortableColorPicker.ColorChanged))
                .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(eventPattern =>
                    Dispatcher.Invoke(() =>
                        ColorBPMChange_ColorChanged(eventPattern.Sender, eventPattern.EventArgs)
                    )
                );

            CheckNote.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavNotes);
            ColorNote.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavNoteColor) ?? Editor.NavNote.Colour);
            ColorNote.SecondaryColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavSelectedNoteColor) ?? Editor.NavNote.HighlightColour);
            ToggleNoteColorIsEnabled();
            ColorNoteColorChangedDebounce = Observable
                .FromEventPattern<RoutedEventArgs>(ColorNote, nameof(PortableColorPicker.ColorChanged))
                .Throttle(TimeSpan.FromMilliseconds(Editor.DrawDebounceInterval))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(eventPattern =>
                    Dispatcher.Invoke(() =>
                        ColorNote_ColorChanged(eventPattern.Sender, eventPattern.EventArgs)
                    )
                );

            doneInit = true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void UpdateSettings() {
            userSettings.Write();
            caller.LoadSettingsFile();
        }

        private void UpdateWaveform() {
            UpdateSettings();
            caller.gridController.DrawNavWaveform();
        }

        private void UpdateBookmarks() {
            UpdateSettings();
            caller.gridController.DrawNavBookmarks();
        }

        private void UpdateBPMChanges() {
            UpdateSettings();
            caller.gridController.DrawNavBPMChanges();
        }

        private void UpdateNotes() {
            UpdateSettings();
            caller.canvasNavNotes.Children.Clear();
            caller.gridController.DrawNavNotes(caller.mapEditor.currentMapDifficulty.notes);
            caller.gridController.HighlightNavNotes(caller.mapEditor.currentMapDifficulty.selectedNotes);
        }

        private void CheckWaveform_Click(object sender, RoutedEventArgs e) {
            ToggleWaveformColorIsEnabled();
            userSettings.SetValueForKey(UserSettingsKey.EnableNavWaveform, CheckWaveform.IsChecked ?? false);
            UpdateWaveform();
        }
        private void ColorWaveform_ColorChanged(object sender, RoutedEventArgs e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavWaveformColor, ColorWaveform.SelectedColor.ToString() ?? Editor.Waveform.ColourWPF.ToString());
                UpdateWaveform();
            }
        }
        private void ToggleWaveformColorIsEnabled() {
            ColorWaveform.IsEnabled = CheckWaveform.IsChecked ?? false;
        }

        private void CheckBookmark_Click(object sender, RoutedEventArgs e) {
            ToggleBookmarkColorIsEnabled();
            userSettings.SetValueForKey(UserSettingsKey.EnableNavBookmarks, CheckBookmark.IsChecked ?? false);
            UpdateBookmarks();
        }
        private void ColorBookmark_ColorChanged(object sender, RoutedEventArgs e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBookmarkColor, ColorBookmark.SelectedColor.ToString() ?? Editor.NavBookmark.Colour);
                userSettings.SetValueForKey(UserSettingsKey.NavBookmarkNameColor, ColorBookmark.SecondaryColor.ToString() ?? Editor.NavBookmark.NameColour);
                UpdateBookmarks();
            }
        }
        private void SliderBookmarkShadowOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBookmarkShadowOpacity, SliderBookmarkShadowOpacity.Value.ToString());
                UpdateBookmarks();
            }
        }
        private void SliderBookmarkShadowOpacity_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            SliderBookmarkShadowOpacity.Value = Editor.NavBookmark.ShadowOpacity;
        }
        private void ToggleBookmarkColorIsEnabled() {
            var status = CheckBookmark.IsChecked ?? false;
            ColorBookmark.IsEnabled = status;
            SliderBookmarkShadowOpacity.IsEnabled = status;
        }

        private void CheckBPMChange_Click(object sender, RoutedEventArgs e) {
            ToggleBPMChangeColorIsEnabled();
            userSettings.SetValueForKey(UserSettingsKey.EnableNavBPMChanges, CheckBPMChange.IsChecked ?? false);
            UpdateBPMChanges();
        }
        private void ColorBPMChange_ColorChanged(object sender, RoutedEventArgs e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBPMChangeColor, ColorBPMChange.SelectedColor.ToString() ?? Editor.NavBPMChange.Colour);
                userSettings.SetValueForKey(UserSettingsKey.NavBPMChangeLabelColor, ColorBPMChange.SecondaryColor.ToString() ?? Editor.NavBPMChange.LabelColour);
                UpdateBPMChanges();
            }
        }
        private void SliderBPMChangeShadowOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBPMChangeShadowOpacity, SliderBPMChangeShadowOpacity.Value.ToString());
                UpdateBPMChanges();
            }
        }
        private void SliderBPMChangeShadowOpacity_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            SliderBPMChangeShadowOpacity.Value = Editor.NavBPMChange.ShadowOpacity;
        }
        private void ToggleBPMChangeColorIsEnabled() {
            var status = CheckBPMChange.IsChecked ?? false;
            ColorBPMChange.IsEnabled = status;
            SliderBPMChangeShadowOpacity.IsEnabled = status;
        }

        private void CheckNote_Click(object sender, RoutedEventArgs e) {
            ToggleNoteColorIsEnabled();
            userSettings.SetValueForKey(UserSettingsKey.EnableNavNotes, CheckNote.IsChecked ?? false);
            UpdateNotes();
        }
        private void ColorNote_ColorChanged(object sender, RoutedEventArgs e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavNoteColor, ColorNote.SelectedColor.ToString() ?? Editor.NavNote.Colour);
                userSettings.SetValueForKey(UserSettingsKey.NavSelectedNoteColor, ColorNote.SecondaryColor.ToString() ?? Editor.NavNote.HighlightColour);
                UpdateNotes();
            }
        }
        private void ToggleNoteColorIsEnabled() {
            ColorNote.IsEnabled = CheckNote.IsChecked ?? false;
        }

        private void ButtonResetWaveform_Click(object sender, RoutedEventArgs e) {
            ColorWaveform.SelectedColor = Editor.Waveform.ColourWPF;
            UpdateWaveform();
        }

        private void ButtonResetBookmark_Click(object sender, RoutedEventArgs e) {
            ColorBookmark.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavBookmark.Colour);
            ColorBookmark.SecondaryColor = (Color)ColorConverter.ConvertFromString(Editor.NavBookmark.NameColour);
            SliderBookmarkShadowOpacity.Value = Editor.NavBookmark.ShadowOpacity;
        }

        private void ButtonResetBPMChange_Click(object sender, RoutedEventArgs e) {
            ColorBPMChange.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavBPMChange.Colour);
            ColorBPMChange.SecondaryColor = (Color)ColorConverter.ConvertFromString(Editor.NavBPMChange.LabelColour);
            SliderBPMChangeShadowOpacity.Value = Editor.NavBPMChange.ShadowOpacity;
        }

        private void ButtonResetNote_Click(object sender, RoutedEventArgs e) {
            ColorNote.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavNote.Colour);
            ColorNote.SecondaryColor = (Color)ColorConverter.ConvertFromString(Editor.NavNote.HighlightColour);
        }
    }
}
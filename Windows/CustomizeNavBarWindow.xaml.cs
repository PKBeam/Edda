using Edda.Const;
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
        public CustomizeNavBarWindow(MainWindow caller, UserSettingsManager userSettings) {
            InitializeComponent();
            this.caller = caller;
            this.userSettings = userSettings;

            CheckWaveform.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavWaveform);
            var waveformColor = userSettings.GetValueForKey(UserSettingsKey.NavWaveformColor) ?? Editor.Waveform.ColourWPF.ToString();
            ColorWaveform.SelectedColor = (Color)ColorConverter.ConvertFromString(waveformColor);
            ToggleWaveformColorIsEnabled();

            CheckBookmark.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavBookmarks);
            ColorBookmark.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBookmarkColor) ?? Editor.NavBookmark.Colour);
            ColorBookmarkName.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBookmarkNameColor) ?? Editor.NavBookmark.NameColour);
            SliderBookmarkShadowOpacity.Value = double.Parse(userSettings.GetValueForKey(UserSettingsKey.NavBookmarkShadowOpacity));
            ToggleBookmarkColorIsEnabled();

            CheckBPMChange.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavBPMChanges);
            ColorBPMChange.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeColor) ?? Editor.NavBPMChange.Colour);
            ColorBPMChangeLabel.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeLabelColor) ?? Editor.NavBPMChange.LabelColour);
            SliderBPMChangeShadowOpacity.Value = double.Parse(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeShadowOpacity));
            ToggleBPMChangeColorIsEnabled();

            CheckNote.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableNavNotes);
            ColorNote.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavNoteColor) ?? Editor.NavNote.Colour);
            ColorSelectedNote.SelectedColor = (Color)ColorConverter.ConvertFromString(userSettings.GetValueForKey(UserSettingsKey.NavSelectedNoteColor) ?? Editor.NavNote.HighlightColour);
            ToggleNoteColorIsEnabled();

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
        private void ColorWaveform_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavWaveformColor, (ColorWaveform.SelectedColor ?? Editor.Waveform.ColourWPF).ToString());
            }
        }
        private void ColorWaveform_LostFocus(object sender, RoutedEventArgs e) {
            if (doneInit) {
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
        private void ColorBookmark_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBookmarkColor, ColorBookmark.SelectedColor.ToString() ?? Editor.NavBookmark.Colour);
                UpdateBookmarks();
            }
        }
        private void ColorBookmarkName_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBookmarkNameColor, ColorBookmarkName.SelectedColor.ToString() ?? Editor.NavBookmark.NameColour);
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
            ColorBookmarkName.IsEnabled = status;
            SliderBookmarkShadowOpacity.IsEnabled = status;
        }

        private void CheckBPMChange_Click(object sender, RoutedEventArgs e) {
            ToggleBPMChangeColorIsEnabled();
            userSettings.SetValueForKey(UserSettingsKey.EnableNavBPMChanges, CheckBPMChange.IsChecked ?? false);
            UpdateBPMChanges();
        }
        private void ColorBPMChange_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBPMChangeColor, ColorBPMChange.SelectedColor.ToString() ?? Editor.NavBPMChange.Colour);
                UpdateBPMChanges();
            }
        }
        private void ColorBPMChangeLabel_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavBPMChangeLabelColor, ColorBPMChangeLabel.SelectedColor.ToString() ?? Editor.NavBPMChange.LabelColour);
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
            ColorBPMChangeLabel.IsEnabled = status;
            SliderBPMChangeShadowOpacity.IsEnabled = status;
        }

        private void CheckNote_Click(object sender, RoutedEventArgs e) {
            ToggleNoteColorIsEnabled();
            userSettings.SetValueForKey(UserSettingsKey.EnableNavNotes, CheckNote.IsChecked ?? false);
            UpdateNotes();
        }
        private void ColorNote_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavNoteColor, ColorNote.SelectedColor.ToString() ?? Editor.NavNote.Colour);
                UpdateNotes();
            }
        }
        private void ColorSelectedNote_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            if (doneInit) {
                userSettings.SetValueForKey(UserSettingsKey.NavSelectedNoteColor, ColorSelectedNote.SelectedColor.ToString() ?? Editor.NavNote.HighlightColour);
                UpdateNotes();
            }
        }
        private void ToggleNoteColorIsEnabled() {
            ColorNote.IsEnabled = CheckNote.IsChecked ?? false;
            ColorSelectedNote.IsEnabled = CheckNote.IsChecked ?? false;
        }

        private void ButtonResetWaveform_Click(object sender, RoutedEventArgs e) {
            ColorWaveform.SelectedColor = Editor.Waveform.ColourWPF;
            UpdateWaveform();
        }

        private void ButtonResetBookmark_Click(object sender, RoutedEventArgs e) {
            ColorBookmark.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavBookmark.Colour);
            ColorBookmarkName.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavBookmark.NameColour);
            SliderBookmarkShadowOpacity.Value = Editor.NavBookmark.ShadowOpacity;
        }

        private void ButtonResetBPMChange_Click(object sender, RoutedEventArgs e) {
            ColorBPMChange.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavBPMChange.Colour);
            ColorBPMChangeLabel.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavBPMChange.LabelColour);
            SliderBPMChangeShadowOpacity.Value = Editor.NavBPMChange.ShadowOpacity;
        }

        private void ButtonResetNote_Click(object sender, RoutedEventArgs e) {
            ColorNote.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavNote.Colour);
            ColorSelectedNote.SelectedColor = (Color)ColorConverter.ConvertFromString(Editor.NavNote.HighlightColour);
        }
    }
}
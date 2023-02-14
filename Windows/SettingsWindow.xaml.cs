using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;
using System.Windows.Controls.Primitives;
using Edda.Const;
using NAudio.CoreAudioApi;

namespace Edda
{
    /// <summary>
    /// Interaction logic for WindowSettings.xaml
    /// </summary>
    public partial class SettingsWindow : Window {
        MainWindow caller;
        UserSettingsManager userSettings;
        bool doneInit = false;
        public SettingsWindow(MainWindow caller, UserSettingsManager userSettings) {
            InitializeComponent();
            this.caller = caller;
            this.userSettings = userSettings;
            InitComboPlaybackDevices();
            InitComboDrumSample();
            txtDefaultNoteSpeed.Text = userSettings.GetValueForKey(UserSettingsKey.DefaultNoteSpeed);
            txtAudioLatency.Text = userSettings.GetValueForKey(UserSettingsKey.EditorAudioLatency);
            checkPanNotes.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.PanDrumSounds);
            sliderSongVol.Value = float.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultSongVolume));
            sliderDrumVol.Value = float.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultNoteVolume));
            checkDiscord.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableDiscordRPC);
            CheckAutosave.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableAutosave);
            CheckShowSpectrogram.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableSpectrogram);
            checkStartupUpdate.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.CheckForUpdates);
            comboMapSaveFolder.SelectedIndex = int.Parse(userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationIndex));
            txtMapSaveFolderPath.Text = (comboMapSaveFolder.SelectedIndex == 0) ? Program.DocumentsMapFolder : userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationPath);
            txtMapSaveFolderPath.TextTrimming = TextTrimming.CharacterEllipsis;
            txtMapSaveFolderPath.Cursor = Cursors.Hand;
            ToggleMapPathVisibility();
            doneInit = true;
        }

        private void CheckShowSpectrogram_Click(object sender, RoutedEventArgs e) {
            bool newStatus = CheckShowSpectrogram.IsChecked ?? false;
            userSettings.SetValueForKey(UserSettingsKey.EnableSpectrogram, newStatus);
            UpdateSettings();
        }

        private void TxtDefaultNoteSpeed_LostFocus(object sender, RoutedEventArgs e) {
            double noteSpeed;
            double prevNoteSpeed = double.Parse(userSettings.GetValueForKey(UserSettingsKey.DefaultNoteSpeed));
            if (double.TryParse(txtDefaultNoteSpeed.Text, out noteSpeed)) {
                userSettings.SetValueForKey(UserSettingsKey.DefaultNoteSpeed, noteSpeed);
                UpdateSettings();
            } else {
                MessageBox.Show($"The note speed must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                noteSpeed = prevNoteSpeed;
            }
            txtAudioLatency.Text = noteSpeed.ToString();
        }
        private void ComboPlaybackDevice_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (doneInit) {
                var newPlaybackDeviceID = ((PlaybackDevice)comboPlaybackDevice.SelectedItem).ID;
                caller.UpdatePlaybackDevice(newPlaybackDeviceID, string.IsNullOrEmpty(newPlaybackDeviceID));
                userSettings.SetValueForKey(UserSettingsKey.PlaybackDeviceID, newPlaybackDeviceID);
                UpdateSettings();
            }
        }
        private void InitComboPlaybackDevices() {
            int i = comboPlaybackDevice.Items.Add(new PlaybackDevice(null, "Default"));
            comboPlaybackDevice.SelectedIndex = i;
            foreach (var device in caller.availablePlaybackDevices) {
                // Having MMDevice as Item lags the ComboBox quite a bit, so we use a simple data class instead.
                i = comboPlaybackDevice.Items.Add(new PlaybackDevice(device));
                if (!caller.playingOnDefaultDevice && device.ID == caller.playbackDeviceID) {
                    comboPlaybackDevice.SelectedIndex = i;
                }
            }
        }
        private void TxtAudioLatency_LostFocus(object sender, RoutedEventArgs e) {
            double latency;
            double prevLatency = double.Parse(userSettings.GetValueForKey(UserSettingsKey.EditorAudioLatency));
            if (double.TryParse(txtAudioLatency.Text, out latency)) {
                userSettings.SetValueForKey(UserSettingsKey.EditorAudioLatency, latency);
                UpdateSettings();
                caller.PauseSong();
            } else {
                MessageBox.Show($"The latency must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                latency = prevLatency;
            }
            txtAudioLatency.Text = latency.ToString();
        }
        private void ComboDrumSample_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            userSettings.SetValueForKey(UserSettingsKey.DrumSampleFile, comboDrumSample.SelectedItem.ToString());
            if (doneInit) {
                UpdateSettings();
                caller.PauseSong();
                caller.RestartDrummer();
            }
        }
        private void InitComboDrumSample() {
            string selectedSampleFile = userSettings.GetValueForKey(UserSettingsKey.DrumSampleFile);
            var files = Directory.GetFiles(Program.ResourcesPath);
            foreach (var file in files) {
                if (file.EndsWith("1.wav") || file.EndsWith("1.mp3")) {
                    var localFile = file.Split(Program.ResourcesPath)[1];
                    var strippedLocalFile = localFile.Substring(0, localFile.Length - 5);
                    int i = comboDrumSample.Items.Add(strippedLocalFile);
                    
                    if (strippedLocalFile == selectedSampleFile) {
                        comboDrumSample.SelectedIndex = i;
                    }
                }
            }
        }
        private void checkPanNotes_Click(object sender, RoutedEventArgs e) {
            bool newStatus = checkPanNotes.IsChecked ?? false;
            userSettings.SetValueForKey(UserSettingsKey.PanDrumSounds, newStatus);
            UpdateSettings();
            caller.PauseSong();
            caller.RestartDrummer();
        }

        private void SliderSongVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {   
            txtSongVol.Text = $"{(int)(sliderSongVol.Value * 100)}%";          
        }

        private void sliderSongVol_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            userSettings.SetValueForKey(UserSettingsKey.DefaultSongVolume, sliderSongVol.Value);
            UpdateSettings();
        }

        private void sliderSongVol_DragCompleted(object sender, DragCompletedEventArgs e) {
            userSettings.SetValueForKey(UserSettingsKey.DefaultSongVolume, sliderSongVol.Value);
            UpdateSettings();
        }

        private void SliderDrumVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            txtDrumVol.Text = $"{(int)(sliderDrumVol.Value * 100)}%";
            UpdateSettings();
        }
        private void sliderDrumVol_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            userSettings.SetValueForKey(UserSettingsKey.DefaultNoteVolume, sliderDrumVol.Value);
            UpdateSettings();
        }

        private void sliderDrumVol_DragCompleted(object sender, DragCompletedEventArgs e) {
            userSettings.SetValueForKey(UserSettingsKey.DefaultNoteVolume, sliderDrumVol.Value);
            UpdateSettings();
        }
        
        private void CheckDiscord_Click(object sender, RoutedEventArgs e) {
            bool newStatus = checkDiscord.IsChecked ?? false;
            userSettings.SetValueForKey(UserSettingsKey.EnableDiscordRPC, newStatus);
            UpdateSettings();
        }
        private void CheckAutosave_Click(object sender, RoutedEventArgs e) {
            bool newStatus = CheckAutosave.IsChecked ?? false;
            userSettings.SetValueForKey(UserSettingsKey.EnableAutosave, newStatus);
            UpdateSettings();
        }
        private void CheckStartupUpdate_Click(object sender, RoutedEventArgs e) {
            bool newStatus = checkStartupUpdate.IsChecked ?? false;
            userSettings.SetValueForKey(UserSettingsKey.CheckForUpdates, newStatus);
            UpdateSettings();
        }

        private void comboMapSaveFolder_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            
            if (comboMapSaveFolder.SelectedIndex == 1) {
                string gameInstall = PickGameFolder();
                if (gameInstall == null) {
                    comboMapSaveFolder.SelectedIndex = 0;
                    userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, DefaultUserSettings.MapSaveLocationPath);
                } else {
                    txtMapSaveFolderPath.Text = gameInstall; 
                    userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, gameInstall);
                }
            }

            ToggleMapPathVisibility();
            userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationIndex, comboMapSaveFolder.SelectedIndex.ToString());
            UpdateSettings();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void UpdateSettings() {
            userSettings.Write();
            caller.LoadSettingsFile();
        }

        private void txtMapSaveFolderPath_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            string gameInstall = PickGameFolder();
            if (gameInstall == null) {
                return;
            } else {
                userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, gameInstall);
            }
            UpdateSettings();
        }

        private string PickGameFolder() {
            var d = new CommonOpenFileDialog();
            d.Title = "Select the folder that Ragnarock is installed in";
            d.IsFolderPicker = true;
            var prevGamePath = userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationPath);
            if (Directory.Exists(prevGamePath)) {
                d.InitialDirectory = prevGamePath;
            }
            if (d.ShowDialog() != CommonFileDialogResult.Ok) {
                return null;
            }

            // make custom song game folder if it doesnt exist
            var songFolder = Path.Combine(d.FileName, Program.GameInstallRelativeMapFolder);
            if (!Directory.Exists(songFolder)) { 
                Directory.CreateDirectory(songFolder);
            }

            return d.FileName;
        }

        private void ToggleMapPathVisibility() {
            if (comboMapSaveFolder.SelectedIndex == 0) {
                txtMapSaveFolderPath.Visibility = Visibility.Collapsed;
            } else {
                txtMapSaveFolderPath.Visibility = Visibility.Visible;
            }
        }

        class PlaybackDevice {
            public string ID {get; private set;}
            public string Name {get; private set;}
            public PlaybackDevice(string ID, string Name) {
                this.ID = ID;
                this.Name = Name;
            }
            public PlaybackDevice(MMDevice device) {
                this.ID = device.ID;
                this.Name = device.FriendlyName;
            }
        }
    }
}

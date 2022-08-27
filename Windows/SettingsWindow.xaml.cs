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
using Edda.Class;

namespace Edda
{
    /// <summary>
    /// Interaction logic for WindowSettings.xaml
    /// </summary>
    public partial class SettingsWindow : Window {
        MainWindow caller;
        UserSettings userSettings;
        bool doneInit = false;
        public SettingsWindow(MainWindow caller, UserSettings userSettings) {
            InitializeComponent();
            this.caller = caller;
            this.userSettings = userSettings;
            InitComboDrumSample();  
            lblProgramName.Content = "Edda v" + Program.DisplayVersionString;
            txtAudioLatency.Text = userSettings.GetValueForKey(Class.UserSettings.EditorAudioLatency);
            checkPanNotes.IsChecked = userSettings.GetBoolForKey(Class.UserSettings.PanDrumSounds);
            sliderSongVol.Value = float.Parse(userSettings.GetValueForKey(Class.UserSettings.DefaultSongVolume));
            sliderDrumVol.Value = float.Parse(userSettings.GetValueForKey(Class.UserSettings.DefaultNoteVolume));
            checkDiscord.IsChecked = userSettings.GetBoolForKey(Class.UserSettings.EnableDiscordRPC);
            CheckAutosave.IsChecked = userSettings.GetBoolForKey(Class.UserSettings.EnableAutosave);
            checkStartupUpdate.IsChecked = userSettings.GetBoolForKey(Class.UserSettings.CheckForUpdates);
            comboMapSaveFolder.SelectedIndex = int.Parse(userSettings.GetValueForKey(Class.UserSettings.MapSaveLocationIndex));
            txtMapSaveFolderPath.Text = (comboMapSaveFolder.SelectedIndex == 0) ? Program.DocumentsMapFolder : userSettings.GetValueForKey(Class.UserSettings.MapSaveLocationPath);
            txtMapSaveFolderPath.TextTrimming = TextTrimming.CharacterEllipsis;
            txtMapSaveFolderPath.Cursor = Cursors.Hand;
            ToggleMapPathVisibility();
            doneInit = true;
        }

        private void TxtAudioLatency_LostFocus(object sender, RoutedEventArgs e) {
            double latency;
            double prevLatency = double.Parse(userSettings.GetValueForKey(Class.UserSettings.EditorAudioLatency));
            if (double.TryParse(txtAudioLatency.Text, out latency)) {
                userSettings.SetValueForKey(Class.UserSettings.EditorAudioLatency, latency);
                UpdateSettings();
                caller.PauseSong();
            } else {
                MessageBox.Show($"The latency must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                latency = prevLatency;
            }
            txtAudioLatency.Text = latency.ToString();
        }
        private void ComboDrumSample_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            userSettings.SetValueForKey(Class.UserSettings.DrumSampleFile, comboDrumSample.SelectedItem.ToString());
            if (doneInit) {
                UpdateSettings();
            }
        }
        private void InitComboDrumSample() {
            string selectedSampleFile = userSettings.GetValueForKey(Class.UserSettings.DrumSampleFile);
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
            userSettings.SetValueForKey(Class.UserSettings.PanDrumSounds, newStatus);
            UpdateSettings();
        }

        private void SliderSongVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {   
            txtSongVol.Text = $"{(int)(sliderSongVol.Value * 100)}%";          
        }

        private void sliderSongVol_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            userSettings.SetValueForKey(Class.UserSettings.DefaultSongVolume, sliderSongVol.Value);
            UpdateSettings();
        }

        private void sliderSongVol_DragCompleted(object sender, DragCompletedEventArgs e) {
            userSettings.SetValueForKey(Class.UserSettings.DefaultSongVolume, sliderSongVol.Value);
            UpdateSettings();
        }

        private void SliderDrumVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            txtDrumVol.Text = $"{(int)(sliderDrumVol.Value * 100)}%";
            
        }
        private void sliderDrumVol_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            userSettings.SetValueForKey(Class.UserSettings.DefaultNoteVolume, sliderDrumVol.Value);
            UpdateSettings();
        }

        private void sliderDrumVol_DragCompleted(object sender, DragCompletedEventArgs e) {
            userSettings.SetValueForKey(Class.UserSettings.DefaultNoteVolume, sliderDrumVol.Value);
            UpdateSettings();
        }

        private void LblRepoLink_MouseDown(object sender, MouseButtonEventArgs e) {
            Helper.OpenWebUrl(Program.RepositoryURL);
        }
        private void CheckDiscord_Click(object sender, RoutedEventArgs e) {
            bool newStatus = checkDiscord.IsChecked ?? false;
            userSettings.SetValueForKey(Class.UserSettings.EnableDiscordRPC, newStatus);
            UpdateSettings();
        }
        private void CheckAutosave_Click(object sender, RoutedEventArgs e) {
            bool newStatus = CheckAutosave.IsChecked ?? false;
            userSettings.SetValueForKey(Class.UserSettings.EnableAutosave, newStatus);
            UpdateSettings();
        }
        private void CheckStartupUpdate_Click(object sender, RoutedEventArgs e) {
            bool newStatus = checkStartupUpdate.IsChecked ?? false;
            userSettings.SetValueForKey(Class.UserSettings.CheckForUpdates, newStatus);
            UpdateSettings();
        }

        private void comboMapSaveFolder_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            
            if (comboMapSaveFolder.SelectedIndex == 1) {
                string gameInstall = PickGameFolder();
                if (gameInstall == null) {
                    comboMapSaveFolder.SelectedIndex = 0;
                    userSettings.SetValueForKey(Class.UserSettings.MapSaveLocationPath, DefaultUserSettings.MapSaveLocationPath);
                } else {
                    txtMapSaveFolderPath.Text = gameInstall; 
                    userSettings.SetValueForKey(Class.UserSettings.MapSaveLocationPath, gameInstall);
                }
            }

            ToggleMapPathVisibility();
            userSettings.SetValueForKey(Class.UserSettings.MapSaveLocationIndex, comboMapSaveFolder.SelectedIndex.ToString());
            UpdateSettings();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void UpdateSettings() {
            userSettings.Write();
            caller.LoadSettingsFile();
        }

        private void lblRepoLink_MouseEnter(object sender, MouseEventArgs e) {
            Mouse.OverrideCursor = Cursors.Hand;
        }

        private void lblRepoLink_MouseLeave(object sender, MouseEventArgs e) {
            Mouse.OverrideCursor = null;
        }

        private void txtMapSaveFolderPath_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            string gameInstall = PickGameFolder();
            if (gameInstall == null) {
                return;
            } else {
                userSettings.SetValueForKey(Class.UserSettings.MapSaveLocationPath, gameInstall);
            }
            UpdateSettings();
        }

        private string PickGameFolder() {
            var d = new CommonOpenFileDialog();
            d.Title = "Select the folder that Ragnarock is installed in";
            d.IsFolderPicker = true;
            var prevGamePath = userSettings.GetValueForKey(Class.UserSettings.MapSaveLocationPath);
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
    }
}

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

namespace Edda {
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
            lblProgramName.Content = "Edda " + Const.Program.VersionDisplayString;
            txtAudioLatency.Text = userSettings.GetValueForKey(Const.UserSettings.EditorAudioLatency);
            checkDiscord.IsChecked = userSettings.GetBoolForKey(Const.UserSettings.EnableDiscordRPC);
            CheckAutosave.IsChecked = userSettings.GetBoolForKey(Const.UserSettings.EnableAutosave);
            checkStartupUpdate.IsChecked = userSettings.GetBoolForKey(Const.UserSettings.CheckForUpdates);
            doneInit = true;
        }

        private void TxtAudioLatency_LostFocus(object sender, RoutedEventArgs e) {
            double latency;
            double prevLatency = double.Parse(userSettings.GetValueForKey(Const.UserSettings.EditorAudioLatency));
            if (double.TryParse(txtAudioLatency.Text, out latency)) {
                userSettings.SetValueForKey(Const.UserSettings.EditorAudioLatency, latency);
                UpdateSettings();
                caller.PauseSong();
            } else {
                MessageBox.Show($"The latency must be numerical.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                latency = prevLatency;
            }
            txtAudioLatency.Text = latency.ToString();
        }
        private void ComboDrumSample_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            userSettings.SetValueForKey(Const.UserSettings.DrumSampleFile, comboDrumSample.SelectedItem.ToString());
            if (doneInit) {
                UpdateSettings();
            }
        }
        private void InitComboDrumSample() {
            string selectedSampleFile = userSettings.GetValueForKey(Const.UserSettings.DrumSampleFile);
            var files = Directory.GetFiles(Const.Program.ResourcesPath);
            foreach (var file in files) {
                if (file.EndsWith("1.wav") || file.EndsWith("1.mp3")) {
                    var localFile = file.Split(Const.Program.ResourcesPath)[1];
                    var strippedLocalFile = localFile.Substring(0, localFile.Length - 5);
                    int i = comboDrumSample.Items.Add(strippedLocalFile);
                    
                    if (strippedLocalFile == selectedSampleFile) {
                        comboDrumSample.SelectedIndex = i;
                    }
                }
            }
        }
        private void LblRepoLink_MouseDown(object sender, MouseButtonEventArgs e) {
            Process proc = new Process();
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.FileName = Const.Program.RepositoryURL;
            proc.Start();
        }
        private void CheckDiscord_Click(object sender, RoutedEventArgs e) {
            bool newStatus = checkDiscord.IsChecked ?? false;
            userSettings.SetValueForKey(Const.UserSettings.EnableDiscordRPC, newStatus);
            UpdateSettings();
        }
        private void CheckAutosave_Click(object sender, RoutedEventArgs e) {
            bool newStatus = CheckAutosave.IsChecked ?? false;
            userSettings.SetValueForKey(Const.UserSettings.EnableAutosave, newStatus);
            UpdateSettings();
        }
        private void CheckStartupUpdate_Click(object sender, RoutedEventArgs e) {
            bool newStatus = checkStartupUpdate.IsChecked ?? false;
            userSettings.SetValueForKey(Const.UserSettings.CheckForUpdates, newStatus);
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
    }
}

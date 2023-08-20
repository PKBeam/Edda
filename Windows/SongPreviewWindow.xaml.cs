using Edda.Const;
using NAudio.Vorbis;
using System.IO;
using System.Windows;

namespace Edda {
    /// <summary>
    /// Interaction logic for SongPreviewWindow.xaml
    /// </summary>
    public partial class SongPreviewWindow : Window {
        int startMin;
        int startSec;
        int endMin;
        int endSec;
        int fadeInDur;
        int fadeOutDur;

        int songEndMin;
        int songEndSec;

        string songFolder;
        string songURL;
        public SongPreviewWindow(string songFolder, string songURL, int startMin, int startSec) {
            InitializeComponent();
            this.songFolder = songFolder;
            this.songURL = songURL;
            var songStream = new VorbisWaveReader(songURL);

            songEndMin = (int)(songStream.TotalTime.TotalSeconds / 60);
            songEndSec = (int)songStream.TotalTime.TotalSeconds % 60;

            this.startMin = startMin;
            this.startSec = startSec;
            endMin = (startMin * 60 + startSec + Audio.MaxPreviewLength) / 60;
            endSec = (startMin * 60 + startSec + Audio.MaxPreviewLength) % 60;
            fadeInDur = Audio.DefaultPreviewFadeIn;
            fadeOutDur = Audio.DefaultPreviewFadeOut;

            UpdateTextFields();
        }

        private void TxtStartTimeMin_GotFocus(object sender, RoutedEventArgs e) {
            TxtStartTimeMin.SelectAll();
        }
        private void TxtStartTimeSec_GotFocus(object sender, RoutedEventArgs e) {
            TxtStartTimeSec.SelectAll();
        }
        private void TxtEndTimeMin_GotFocus(object sender, RoutedEventArgs e) {
            TxtEndTimeMin.SelectAll();
        }
        private void TxtEndTimeSec_GotFocus(object sender, RoutedEventArgs e) {
            TxtEndTimeSec.SelectAll();
        }
        private void TxtFadeInDuration_GotFocus(object sender, RoutedEventArgs e) {
            TxtFadeInDuration.SelectAll();
        }
        private void TxtFadeInDuration_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtFadeInDuration.Text, out temp) && temp >= 0) {
                if (temp > TotalSec(songEndMin, songEndSec)) {
                    temp = TotalSec(songEndMin, songEndSec);
                }
                fadeInDur = temp;
            } else {
                MessageBox.Show(this, $"The duration must be a positive integer", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateTextFields();
        }
        private void TxtFadeOutDuration_GotFocus(object sender, RoutedEventArgs e) {
            TxtFadeOutDuration.SelectAll();
        }
        private void TxtFadeOutDuration_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtFadeOutDuration.Text, out temp) && temp >= 0) {
                if (temp > TotalSec(songEndMin, songEndSec)) {
                    temp = TotalSec(songEndMin, songEndSec);
                }
                fadeOutDur = temp;
            } else {
                MessageBox.Show(this, $"The duration must be a positive integer", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateTextFields();
        }
        private void TxtStartTimeMin_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtStartTimeMin.Text, out temp) && Helper.DoubleRangeCheck(startMin, 0, 59)) {
                startMin = temp;
                if (!TimeRangeCheck()) {
                    endMin = startMin;
                    endSec = startSec;
                }
            } else {
                ShowRangeError();
            }
            UpdateTextFields();
        }
        private void TxtStartTimeSec_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtStartTimeSec.Text, out temp) && Helper.DoubleRangeCheck(startSec, 0, 59)) {
                startSec = temp;
                if (!TimeRangeCheck()) {
                    endMin = startMin;
                    endSec = startSec;
                }
            } else {
                ShowRangeError();
            }
            UpdateTextFields();
        }
        private void TxtEndTimeMin_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtEndTimeMin.Text, out temp) && Helper.DoubleRangeCheck(endMin, 0, 59)) {
                endMin = temp;
                if (!TimeRangeCheck()) {
                    startMin = endMin;
                    startSec = endSec;
                }
            } else {
                ShowRangeError();
            }

            UpdateTextFields();
        }
        private void TxtEndTimeSec_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtEndTimeSec.Text, out temp) && Helper.DoubleRangeCheck(endSec, 0, 59)) {
                endSec = temp;
                if (!TimeRangeCheck()) {
                    startMin = endMin;
                    startSec = endSec;
                }
            } else {
                ShowRangeError();
            }
            UpdateTextFields();
        }
        private void BtnGenerate_Click(object sender, RoutedEventArgs e) {
            if (TimeRangeDurationCheck() || ShowDurationError()) {
                string saveURL = Path.Combine(songFolder, "preview.ogg");
                btnGenerate.IsEnabled = false;
                int exitCode = Helper.FFmpeg(songFolder, $"-i \"{songURL}\" -y -ss 00:{startMin:D2}:{startSec:D2} -to 00:{endMin:D2}:{endSec:D2} -vn -af afade=t=out:st={TotalSec(endMin, endSec) - fadeOutDur}:d={fadeOutDur},afade=t=in:st={TotalSec(startMin, startSec)}:d={fadeInDur} \"{saveURL}\"");
                btnGenerate.IsEnabled = true;
                if (exitCode == 0) {
                    MessageBox.Show(this, $"Song preview created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                } else {
                    MessageBox.Show(this, $"There was an issue creating song preview.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }
        }

        private void UpdateTextFields() {
            TxtStartTimeMin.Text = startMin.ToString();
            TxtStartTimeSec.Text = startSec.ToString();
            TxtEndTimeMin.Text = endMin.ToString();
            TxtEndTimeSec.Text = endSec.ToString();
            TxtFadeInDuration.Text = fadeInDur.ToString();
            TxtFadeOutDuration.Text = fadeOutDur.ToString();
        }
        private int TotalSec(int min, int sec) {
            return 60 * min + sec;
        }
        private bool TimeRangeCheck() {
            return TotalSec(startMin, startSec) <= TotalSec(endMin, endSec);
        }
        private bool TimeRangeDurationCheck() {
            return TotalSec(endMin, endSec) - TotalSec(startMin, startSec) <= Audio.MaxPreviewLength;
        }
        private void ShowRangeError() {
            MessageBox.Show(this, $"The input must be an integer from 0 to 59.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private bool ShowDurationError() {
            return MessageBox.Show(this, $"The preview duration should be less than {Audio.MaxPreviewLength} seconds.\nContinue anyway?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
    }
}

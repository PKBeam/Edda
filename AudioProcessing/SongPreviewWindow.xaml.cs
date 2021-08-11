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
        int fadeDur;

        int songEndMin;
        int songEndSec;

        string songFolder;
        string songURL;
        public SongPreviewWindow(string songFolder, string songURL) {
            InitializeComponent();
            this.songFolder = songFolder;
            this.songURL = songURL;
            var songStream = new VorbisWaveReader(songURL);

            songEndMin = (int)(songStream.TotalTime.TotalSeconds / 60);
            songEndSec = (int)songStream.TotalTime.TotalSeconds % 60;

            startMin = 0;
            startSec = 0;
            endMin   = Const.Audio.MaxPreviewLength / 60;
            endSec   = Const.Audio.MaxPreviewLength % 60;
            fadeDur  = Const.Audio.DefaultPreviewFade;

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
        private void TxtFadeDuration_GotFocus(object sender, RoutedEventArgs e) {
            TxtFadeDuration.SelectAll();
        }
        private void TxtFadeDuration_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtFadeDuration.Text, out temp) && temp >= 0) {
                if (temp > TotalSec(songEndMin, songEndSec)) {
                    temp = TotalSec(songEndMin, songEndSec);
                }
                fadeDur = temp;
            } else {
                MessageBox.Show($"The duration must be a positive integer", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateTextFields();
        }
        private void TxtStartTimeMin_LostFocus(object sender, RoutedEventArgs e) {
            int temp;
            if (int.TryParse(TxtStartTimeMin.Text, out temp) && Helper.DoubleRangeCheck(startMin, 0, 59)) {
                startMin = temp;
                if (!TimeRangeCheck()) {
                    startMin = endMin;
                    startSec = endSec;
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
                    startMin = endMin;
                    startSec = endSec;
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
                    endMin = startMin;
                    endSec = startSec;
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
                    endMin = startMin;
                    endSec = startSec;
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
                Helper.FFmpeg(songFolder, $"-i \"{songURL}\" -y -ss 00:{startMin:D2}:{startSec:D2} -to 00:{endMin:D2}:{endSec:D2} -af afade=t=out:st={TotalSec(endMin, endSec) - fadeDur}:d={fadeDur} \"{saveURL}\"");
                btnGenerate.IsEnabled = true;
            }
        }

        private void UpdateTextFields() {
            TxtStartTimeMin.Text = startMin.ToString();
            TxtStartTimeSec.Text = startSec.ToString();
            TxtEndTimeMin.Text = endMin.ToString();
            TxtEndTimeSec.Text = endSec.ToString();
            TxtFadeDuration.Text = fadeDur.ToString();
        }
        private int TotalSec(int min, int sec) {
            return 60 * min + sec;
        }
        private bool TimeRangeCheck() {
            return TotalSec(startMin, startSec) <= TotalSec(endMin, endSec);
        }
        private bool TimeRangeDurationCheck() {
            return TotalSec(endMin, endSec) - TotalSec(startMin, startSec) <= Const.Audio.MaxPreviewLength;
        }
        private void ShowRangeError() {
            MessageBox.Show($"The input must be an integer from 0 to 59.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private bool ShowDurationError() {
            return MessageBox.Show($"The preview duration should be less than {Const.Audio.MaxPreviewLength} seconds.\nContinue anyway?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
    }
}

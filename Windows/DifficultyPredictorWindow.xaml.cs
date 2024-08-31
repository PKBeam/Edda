using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edda.Windows {
    /// <summary>
    /// Interaction logic for DifficultyPredictorWindow.xaml
    /// </summary>
    public partial class DifficultyPredictorWindow : Window {
        readonly MainWindow mainWindow;
        readonly UserSettingsManager userSettings;

        private readonly bool windowLoaded = false;

        public DifficultyPredictorWindow(MainWindow mainWindow, UserSettingsManager userSettings) {
            this.mainWindow = mainWindow;
            this.userSettings = userSettings;
            InitializeComponent();
            CheckShowPreciseValues.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.DifficultyPredictorShowPrecise);
            CheckShowInMapStats.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.DifficultyPredictorShowInMapStats);

            var selectedAlgorithm = userSettings.GetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm);

            PKBeamAlgoRadioButton.IsChecked = selectedAlgorithm == DifficultyPrediction.SupportedAlgorithms.PKBeam;
            NytildeAlgoRadioButton.IsChecked = selectedAlgorithm == DifficultyPrediction.SupportedAlgorithms.Nytilde;
            MelchiorAlgoRadioButton.IsChecked = selectedAlgorithm == DifficultyPrediction.SupportedAlgorithms.Melchior;
            windowLoaded = true;
        }

        private void BtnPredict_Click(object sender, RoutedEventArgs e) {
            btnDifficulty0.IsEnabled = false;
            btnDifficulty1.IsEnabled = false;
            btnDifficulty2.IsEnabled = false;
            PanelPredictionResults.Visibility = Visibility.Hidden;
            PanelPredictionWarning.Visibility = Visibility.Hidden;
            var mapEditor = mainWindow.mapEditor;
            for (int i = 0; i < mapEditor.numDifficulties; i++) {
                var supportedFeatures = mainWindow.difficultyPredictor.GetSupportedFeatures();
                var diffPrediction = mainWindow.difficultyPredictor.PredictDifficulty(mapEditor, i);
                Label diffLabel;
                Button diffBtn;
                switch (i) {
                    case 0: diffLabel = lblDifficultyRank1; diffBtn = btnDifficulty0; break;
                    case 1: diffLabel = lblDifficultyRank2; diffBtn = btnDifficulty1; break;
                    case 2: diffLabel = lblDifficultyRank3; diffBtn = btnDifficulty2; break;
                    default: diffLabel = null; diffBtn = null; break;
                }
                diffBtn.IsEnabled = true;
                if (diffPrediction.HasValue) {
                    diffLabel.Foreground = new SolidColorBrush(DifficultyPrediction.Colour);
                    var showPreciseValue = CheckShowPreciseValues.IsChecked == true && supportedFeatures.HasFlag(Classes.MapEditorNS.Stats.IDifficultyPredictor.Features.PreciseFloat);
                    var predictionDisplay = Math.Round(diffPrediction.Value, showPreciseValue ? 2 : 0);
                    diffLabel.Content = $"{predictionDisplay.ToString(showPreciseValue ? "#0.00" : null)}";
                } else if (!supportedFeatures.HasFlag(Classes.MapEditorNS.Stats.IDifficultyPredictor.Features.AlwaysPredict)) {
                    diffLabel.Foreground = new SolidColorBrush(DifficultyPrediction.WarningColour);
                    PanelPredictionWarning.Visibility = Visibility.Visible;
                    diffLabel.Content = "???";
                } else {
                    diffLabel.Foreground = new SolidColorBrush(DifficultyPrediction.Colour);
                    diffLabel.Content = "0";
                }
            }
            PanelPredictionResults.Visibility = Visibility.Visible;
        }

        private void CheckShowPreciseValues_Click(object sender, RoutedEventArgs e) {
            bool newStatus = CheckShowPreciseValues.IsChecked ?? false;
            userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorShowPrecise, newStatus);
            UpdateSettings();
            mainWindow.UpdateDifficultyPrediction();
        }

        private void CheckShowInMapStats_Click(object sender, RoutedEventArgs e) {
            bool newStatus = CheckShowInMapStats.IsChecked ?? false;
            userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorShowInMapStats, newStatus);
            UpdateSettings();
            mainWindow.UpdateDifficultyPrediction();
        }

        private void UpdateSettings() {
            userSettings.Write();
            mainWindow.LoadSettingsFile(true);
        }

        private void PKBeamAlgoRadioButton_Checked(object sender, RoutedEventArgs e) {
            CheckShowPreciseValues.IsEnabled = DifficultyPredictorPKBeam.SINGLETON.GetSupportedFeatures().HasFlag(IDifficultyPredictor.Features.RealTime);
            if (windowLoaded) {
                userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm, DifficultyPrediction.SupportedAlgorithms.PKBeam);
                UpdateSettings();
                mainWindow.UpdateDifficultyPrediction();
            }
        }

        private void NytildeAlgoRadioButton_Checked(object sender, RoutedEventArgs e) {
            CheckShowPreciseValues.IsEnabled = DifficultyPredictorNytilde.SINGLETON.GetSupportedFeatures().HasFlag(IDifficultyPredictor.Features.RealTime);
            if (windowLoaded) {
                userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm, DifficultyPrediction.SupportedAlgorithms.Nytilde);
                UpdateSettings();
                mainWindow.UpdateDifficultyPrediction();
            }
        }

        private void MelchiorAlgoRadioButton_Checked(object sender, RoutedEventArgs e) {
            CheckShowPreciseValues.IsEnabled = DifficultyPredictorMelchior.SINGLETON.GetSupportedFeatures().HasFlag(IDifficultyPredictor.Features.RealTime);
            if (windowLoaded) {
                userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm, DifficultyPrediction.SupportedAlgorithms.Melchior);
                UpdateSettings();
                mainWindow.UpdateDifficultyPrediction();
            }
        }
    }
}
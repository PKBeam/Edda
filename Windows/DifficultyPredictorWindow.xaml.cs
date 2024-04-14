using Edda.Const;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edda.Windows {
    /// <summary>
    /// Interaction logic for DifficultyPredictorWindow.xaml
    /// </summary>
    public partial class DifficultyPredictorWindow : Window {
        MainWindow mainWindow;

        public DifficultyPredictorWindow(MainWindow mainWindow) {
            this.mainWindow = mainWindow;
            InitializeComponent();
        }

        private void BtnPredict_Click(object sender, RoutedEventArgs e) {
            btnDifficulty0.IsEnabled = false;
            btnDifficulty1.IsEnabled = false;
            btnDifficulty2.IsEnabled = false;
            PanelPredictionResults.Visibility = Visibility.Hidden;
            PanelPredictionWarning.Visibility = Visibility.Hidden;
            var mapEditor = mainWindow.mapEditor;
            var globalBpm = mapEditor.GlobalBPM;
            var globalSongDuration = mapEditor.SongDuration;
            for (int i = 0; i < mapEditor.numDifficulties; i++) {
                var diff = mapEditor.GetDifficulty(i);
                var timeSeries = diff.notes.Select(note => 60 / globalBpm * note.beat).ToList();
                var timeDifferences = timeSeries.Zip(timeSeries.Skip(1), (a, b) => b - a).ToList();
                var timeDifferencesNoZero = timeDifferences.Where(timeDiff => !Helper.DoubleApproxEqual(timeDiff, 0)).ToList();
                var maxTime = timeSeries.Last() - timeSeries.First();

                var noteDensity = GetNoteDensity(timeSeries, maxTime);
                var averageTimeDifference = timeDifferencesNoZero.Average();
                var countNoteDensityPerWindow = GetCountNoteDensityPerWindow(timeDifferencesNoZero);
                var localNoteDensity = GetNoteDensitiesPerWindow(timeSeries, 2);
                var highLocalNoteDensity = Helper.GetQuantile(localNoteDensity, 0.95);
                var typicalTimeDifference = Helper.GetQuantile(timeDifferencesNoZero, 0.4);

                var parametersInRange = CheckParameterRanges(noteDensity, averageTimeDifference, countNoteDensityPerWindow, highLocalNoteDensity, typicalTimeDifference);

                Label diffLabel;
                Button diffBtn;
                switch (i) {
                    case 0: diffLabel = lblDifficultyRank1; diffBtn = btnDifficulty0; break;
                    case 1: diffLabel = lblDifficultyRank2; diffBtn = btnDifficulty1; break;
                    case 2: diffLabel = lblDifficultyRank3; diffBtn = btnDifficulty2; break;
                    default: diffLabel = null; diffBtn = null; break;
                }
                diffBtn.IsEnabled = true;
                if (parametersInRange) {
                    diffLabel.Foreground = new SolidColorBrush(DifficultyPrediction.Colour);
                    var predictedDiff = EvaluateModel((float)noteDensity, (float)averageTimeDifference, (float)countNoteDensityPerWindow, (float)highLocalNoteDensity, (float)typicalTimeDifference);
                    var showPreciseValue = CheckShowPreciseValues.IsChecked == true;
                    var predictionDisplay = Math.Round(predictedDiff, showPreciseValue ? 2 : 0);
                    diffLabel.Content = $"{predictionDisplay.ToString(showPreciseValue ? "##.00" : null)}";
                } else {
                    diffLabel.Foreground = new SolidColorBrush(DifficultyPrediction.WarningColour);
                    PanelPredictionWarning.Visibility = Visibility.Visible;
                    diffLabel.Content = ">10";
                }
            }
            PanelPredictionResults.Visibility = Visibility.Visible;
        }

        private static double GetNoteDensity(IEnumerable<double> noteTimes, double songDuration) {
            return noteTimes.Count() / songDuration;
        }

        private static List<double> GetNoteDensitiesPerWindow(List<double> timeSeries, double windowLength = 2.75) {
            var startTimes = timeSeries
                .Select(t => t - windowLength)
                .ToList();
            var endTimes = timeSeries
                .Select(t => t + windowLength)
                .ToList();
            var startIndices = startTimes
                .Select(timeSeries.BinarySearch)
                .Select(idx => idx >= 0 ? idx : ~idx)
                .ToList();
            var endIndices = endTimes
                .Select(timeSeries.BinarySearch)
                .Select(idx => idx >= 0 ? idx : ~idx)
                .ToList();
            return startIndices
                .Zip(endIndices, (start, end) => (end - start) / windowLength)
                .ToList();
        }

        private static int GetCountNoteDensityPerWindow(List<double> timeDiffs, double windowLength = 2) {
            return timeDiffs
                .Where(timeDiff => Helper.DoubleApproxGreaterEqual(windowLength, Math.Abs(timeDiff)))
                .Count();
        }

        private static float EvaluateModel(float noteDensity, float averageTimeDifference, float countNoteDensityPerWindow, float peakNoteDensity, float typicalTimeDifference) {
            string path = Path.Combine(Path.GetTempPath(), "Edda-MLDP_temp.onnx");
            File.WriteAllBytes(path, Properties.Resources.Edda_MLDP);

            var sourceData = new float[] { noteDensity, averageTimeDifference, countNoteDensityPerWindow, peakNoteDensity, typicalTimeDifference };
            var dimensions = new int[] { 1, sourceData.Length }; // dimensions are batch_size (1) times number of features in sourceData

            using var session = new InferenceSession(path);
            var tensor = new DenseTensor<float>(sourceData, dimensions);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) }.AsReadOnly();
            using var outputs = session.Run(inputs);

            return outputs[0].AsTensor<float>().First();
        }

        private static bool CheckParameterRanges(double noteDensity, double averageTimeDifference, double countNoteDensityPerWindow, double peakNoteDensity, double typicalTimeDifference) {
            return Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxNoteDensity, noteDensity) &&
                Helper.DoubleApproxGreaterEqual(averageTimeDifference, DifficultyPrediction.MinAverageTimeDifference) &&
                Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxCountNoteDensityPerWindow, countNoteDensityPerWindow) &&
                Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxPeakNoteDensity, peakNoteDensity) &&
                Helper.DoubleApproxGreaterEqual(typicalTimeDifference, DifficultyPrediction.MinTypicalTimeDifference);
        }
    }
}
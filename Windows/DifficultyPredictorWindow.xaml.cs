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
                var localNoteDensity = GetLocalNoteDensity(timeSeries, timeDifferencesNoZero);
                var longestHighDensitySequence = GetLongestHighDensitySequence(timeDifferencesNoZero);
                var highLocalNoteDensity = Helper.GetQuantile(localNoteDensity, 0.95);

                var parametersInRange = CheckParameterRanges(noteDensity, averageTimeDifference, longestHighDensitySequence, highLocalNoteDensity);

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
                    var predictedDiff = EvaluateModel((float)noteDensity, (float)averageTimeDifference, longestHighDensitySequence, (float)highLocalNoteDensity);
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

        private static List<double> GetLocalNoteDensity(List<double> timeSeries, List<double> timeDiff, double windowLength = 2.75) {
            var cumulativeTime = timeDiff
                .Select((t, i) => timeDiff.Take(i + 1).Sum())
                .ToList();
            var startTimes = timeSeries
                .Select(t => t - windowLength)
                .ToList();
            var startIndices = startTimes
                .Select(cumulativeTime.BinarySearch)
                .Select(idx => idx >= 0 ? idx : ~idx)
                .ToList();
            var endIndices = timeSeries
                .Select(cumulativeTime.BinarySearch)
                .Select(idx => idx >= 0 ? idx : ~idx)
                .ToList();
            return startIndices
                .Zip(endIndices, (start, end) => (end - start) / windowLength)
                .ToList();
        }

        private static int GetLongestHighDensitySequence(List<double> timeDiffs) {
            var uniqueValues = timeDiffs.Distinct().ToList();
            var indices = uniqueValues.Select(val => timeDiffs.IndexOf(val)).ToList();
            var counts = uniqueValues.Select(val => timeDiffs.Count(t => t == val)).ToList();

            var matrix = new int[uniqueValues.Count, timeDiffs.Count];

            for (int i = 0; i < indices.Count; i++) {
                var idx = indices[i];
                var count = counts[i];
                var valIndex = uniqueValues.IndexOf(timeDiffs[idx]);

                for (int j = idx; j < idx + count; j++) {
                    matrix[valIndex, j] = 1;
                }
            }

            var rowSums = Enumerable
                .Range(0, uniqueValues.Count)
                .Select(i => Enumerable.Range(0, timeDiffs.Count).Sum(j => matrix[i, j]))
                .ToList();

            var maxDensityIndex = rowSums.IndexOf(rowSums.Max());
            var maxDensity = (double)rowSums[maxDensityIndex] / timeDiffs.Count;

            var seriesStart = Array.IndexOf(matrix.Cast<int>().ToArray(), 1, maxDensityIndex * timeDiffs.Count);
            var seriesEnd = Array.LastIndexOf(matrix.Cast<int>().ToArray(), 1, (maxDensityIndex + 1) * timeDiffs.Count - 1);

            return seriesEnd - seriesStart + 1;
        }

        private static float EvaluateModel(float noteDensity, float averageTimeDifference, float longestHighDensitySequence, float peakNoteDensity) {
            string path = Path.Combine(Path.GetTempPath(), "Edda-MLDP_temp.onnx");
            File.WriteAllBytes(path, Properties.Resources.Edda_MLDP);

            var sourceData = new float[] { noteDensity, averageTimeDifference, longestHighDensitySequence, peakNoteDensity };
            var dimensions = new int[] { 1, 4 }; // batch_size = 1, 4 features

            using var session = new InferenceSession(path);
            var tensor = new DenseTensor<float>(sourceData, dimensions);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) }.AsReadOnly();
            using var outputs = session.Run(inputs);

            return outputs[0].AsTensor<float>().First();
        }

        private static bool CheckParameterRanges(double noteDensity, double averageTimeDifference, int longestHighDensitySequence, double peakNoteDensity) {
            return Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxNoteDensity, noteDensity) &&
                Helper.DoubleApproxGreaterEqual(averageTimeDifference, DifficultyPrediction.MinAverageTimeDifference) &&
                longestHighDensitySequence <= DifficultyPrediction.MaxLongestHighDensitySequence &&
                Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxPeakNoteDensity, peakNoteDensity);
        }
    }
}
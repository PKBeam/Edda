using Edda.Const;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Edda.Classes.MapEditorNS.Stats {
    public class DifficultyPredictor {

        public static float? PredictDifficulty(MapEditor mapEditor, int difficultyIndex) {
            var diff = mapEditor.GetDifficulty(difficultyIndex);
            var timeSeries = diff.notes.Select(note => 60 / mapEditor.GlobalBPM * note.beat).ToList();
            if (timeSeries.Count == 0) return 0;
            var timeDifferences = timeSeries.Zip(timeSeries.Skip(1), (a, b) => b - a).ToList();
            var timeDifferencesNoZero = timeDifferences.Where(timeDiff => !Helper.DoubleApproxEqual(timeDiff, 0)).ToList();
            var maxTime = timeSeries.Last() - timeSeries.First();

            var noteDensity = GetNoteDensity(timeSeries, maxTime);
            var averageTimeDifference = timeDifferencesNoZero.Average();
            var countNoteDensityPerWindow = GetCountNoteDensityPerWindow(timeDifferencesNoZero);
            var localNoteDensity = GetNoteDensitiesPerWindow(timeSeries, 2);
            var highLocalNoteDensity = Helper.GetQuantile(localNoteDensity, 0.95);
            var typicalTimeDifference = Helper.GetQuantile(timeDifferencesNoZero, 0.4);

            return CheckParameterRanges(noteDensity, averageTimeDifference, countNoteDensityPerWindow, highLocalNoteDensity, typicalTimeDifference)
                ? EvaluateModel((float)noteDensity, (float)averageTimeDifference, countNoteDensityPerWindow, (float)highLocalNoteDensity, (float)typicalTimeDifference)
                : null;
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
        private static bool CheckParameterRanges(double noteDensity, double averageTimeDifference, double countNoteDensityPerWindow, double peakNoteDensity, double typicalTimeDifference) {
            return Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxNoteDensity, noteDensity) &&
                Helper.DoubleApproxGreaterEqual(averageTimeDifference, DifficultyPrediction.MinAverageTimeDifference) &&
                Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxCountNoteDensityPerWindow, countNoteDensityPerWindow) &&
                Helper.DoubleApproxGreaterEqual(DifficultyPrediction.MaxPeakNoteDensity, peakNoteDensity) &&
                Helper.DoubleApproxGreaterEqual(typicalTimeDifference, DifficultyPrediction.MinTypicalTimeDifference);
        }
    }
}
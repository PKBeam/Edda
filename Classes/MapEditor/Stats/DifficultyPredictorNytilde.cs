using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Const;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Edda.Classes.MapEditorNS.Stats.IDifficultyPredictor.Features;

namespace Edda.Classes.MapEditorNS.Stats {
    /// <summary>
    /// Difficulty predictor utilizing PCA-based ONNX model developed by <see href="https://github.com/Nytilde">Nytilde</see> and trained on
    /// all Ragnarock OST and RAID maps up to (and including) Jonathan Young RAID. <br/>
    /// When map parameters are outside of the trained data range, a fallback one-dimensional PCA model is used for extrapolation instead.
    /// </summary>
    public class DifficultyPredictorNytilde : IDifficultyPredictor {
        public static readonly DifficultyPredictorNytilde SINGLETON = new();

        public IDifficultyPredictor.Features GetSupportedFeatures() {
            return PreciseFloat | AlwaysPredict | RealTime;
        }

        public float? PredictDifficulty(MapEditor mapEditor, int difficultyIndex) {
            var diff = mapEditor.GetDifficulty(difficultyIndex);
            var timeSeries = diff.notes.Select(note => 60 / mapEditor.GlobalBPM * note.beat).ToList();
            if (timeSeries.Count == 0) return 0;
            var timeDifferences = timeSeries.Zip(timeSeries.Skip(1), (a, b) => b - a).ToList();
            var timeDifferencesNoZero = timeDifferences.Where(timeDiff => !Helper.DoubleApproxEqual(timeDiff, 0)).ToList();
            var maxTime = timeSeries.Last() - timeSeries.First();

            var noteDensity = GetNoteDensity(timeSeries, maxTime);
            var averageTimeDifference = timeDifferencesNoZero.Count > 0 ? timeDifferencesNoZero.Average() : 0.0;
            var melchiorDiffScore = GetMelchiorDifficultyScore(diff.notes, mapEditor.GlobalBPM);
            var localNoteDensity = GetNoteDensitiesPerWindow(timeSeries, 2);
            var highLocalNoteDensity = Helper.GetQuantile(localNoteDensity, 0.95);
            var typicalTimeDifference = Helper.GetQuantile(timeDifferencesNoZero, 0.3);
            var countNoteDensityPerWindow = GetCountNoteDensityPerWindow(timeDifferencesNoZero);

            var model = CheckParameterRanges(noteDensity, averageTimeDifference, countNoteDensityPerWindow, highLocalNoteDensity, typicalTimeDifference)
                ? Properties.Resources.Edda_MLDP_Nytilde
                : Properties.Resources.Edda_MLDP_Nytilde_Fallback;

            return EvaluateModel(model, (float)noteDensity, (float)averageTimeDifference, (float)melchiorDiffScore, countNoteDensityPerWindow, (float)highLocalNoteDensity, (float)typicalTimeDifference);
        }

        private static float EvaluateModel(byte[] model, float noteDensity, float averageTimeDifference, float melchiorDiffScore, float countNoteDensityPerWindow, float peakNoteDensity, float typicalTimeDifference) {
            string path = Path.Combine(Path.GetTempPath(), "Edda-MLDP_Nytilde_temp.onnx");
            File.WriteAllBytes(path, model);

            var sourceData = new float[] { noteDensity, averageTimeDifference, melchiorDiffScore, peakNoteDensity, typicalTimeDifference, countNoteDensityPerWindow };
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

        private static double GetMelchiorDifficultyScore(SortedSet<Note> notes, double globalBPM, double timeThreshold = 0.02) {
            if (notes.Count < 2) return 0;

            var handNotes = notes.Take(2).OrderBy(note => note.col).ToList();
            var diffPoints = 0.0;
            foreach (var note in notes.Skip(2)) {
                var handDiffPoints = handNotes
                    .Select(handNote => (Math.Abs(handNote.col - note.col) + 1) / Math.Max(timeThreshold, Math.Pow(60 * (note.beat - handNote.beat) / globalBPM, 2)))
                    .ToList();
                var bestScore = handDiffPoints.Min();
                var bestHand = handDiffPoints.IndexOf(bestScore);
                diffPoints += bestScore;
                handNotes[bestHand] = note;
            }

            return diffPoints;
        }

        private static bool CheckParameterRanges(double noteDensity, double averageTimeDifference, double countNoteDensityPerWindow, double peakNoteDensity, double typicalTimeDifference) {
            // TODO: Add MelchiorDiffScore range
            return Helper.DoubleApproxGreaterEqual(DifficultyPrediction.Nytilde.MaxNoteDensity, noteDensity) &&
                Helper.DoubleApproxGreaterEqual(averageTimeDifference, DifficultyPrediction.Nytilde.MinAverageTimeDifference) &&
                Helper.DoubleApproxGreaterEqual(DifficultyPrediction.Nytilde.MaxCountNoteDensityPerWindow, countNoteDensityPerWindow) &&
                Helper.DoubleApproxGreaterEqual(DifficultyPrediction.Nytilde.MaxPeakNoteDensity, peakNoteDensity) &&
                Helper.DoubleApproxGreaterEqual(typicalTimeDifference, DifficultyPrediction.Nytilde.MinTypicalTimeDifference);
        }
    }
}
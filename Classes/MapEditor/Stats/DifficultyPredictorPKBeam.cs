using Edda.Classes.MapEditorNS.NoteNS;
using Syncfusion.PMML;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using static Edda.Classes.MapEditorNS.Stats.IDifficultyPredictor.Features;

namespace Edda.Classes.MapEditorNS.Stats {
    /// <summary>
    /// Difficulty predictor utilizing a PMML model developed by <see href="https://github.com/PKBeam">PKBeam</see> and trained on selected custom maps from RagnaCustoms.<br/>
    /// This model is intended to be used with fully completed maps, so the results for incomplete maps might be inaccurate.
    /// </summary>
    public class DifficultyPredictorPKBeam : IDifficultyPredictor {
        public static readonly DifficultyPredictorPKBeam SINGLETON = new();

        public IDifficultyPredictor.Features GetSupportedFeatures() {
            return PreciseFloat | AlwaysPredict | RealTime;
        }

        public float? PredictDifficulty(MapEditor mapEditor, int difficultyIndex) {
            var globalBpm = mapEditor.GlobalBPM;
            var globalSongDuration = mapEditor.SongDuration;
            var diff = mapEditor.GetDifficulty(difficultyIndex);
            var diffNotes = diff.notes;
            var songDuration = diffNotes.Count > 0 ? 60 / globalBpm * diffNotes.Last().beat : globalSongDuration;
            var noteDensity = GetNoteDensity(diffNotes, songDuration);
            var localNoteDensity = GetLocalNoteDensity(diffNotes, songDuration, globalBpm);
            var highLocalNoteDensity = Helper.GetQuantile(localNoteDensity, 0.95);
            return (float)EvaluateModel(mapEditor.GlobalBPM, noteDensity, highLocalNoteDensity);
        }

        private static double EvaluateModel(double bpm, double noteDensity, double peakNoteDensity) {
            string path = Path.Combine(Path.GetTempPath(), "Edda-MLDP_PKBeam_temp.pmml");
            File.WriteAllBytes(path, Properties.Resources.Edda_MLDP_PKBeam);

            var features = new {
                BPM = bpm,
                NoteDensity = noteDensity,
                HighNoteDensity2s = peakNoteDensity,
            };
            var reader = File.OpenText(path);
            var pmmlDocument = new PMMLDocument(reader);
            // Syncfusion package uses current culture for parsing doubles, so we need to make sure it's consistent.
            var currentCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try {
                var supportVector = new SupportVectorMachineModelEvaluator(pmmlDocument);
                var predictedResult = supportVector.GetResult(features, null);
                supportVector.Dispose();

                XmlDocument xmlDoc = new();
                xmlDoc.Load(path);

                // Syncfusion package has a bug where it ignores rescaling of output value... so we have to do it ourselves
                XmlNodeList constants = xmlDoc.GetElementsByTagName("Constant");
                var unvariance = Helper.DoubleParseInvariant(constants[0].InnerText);
                var unmean = Helper.DoubleParseInvariant(constants[1].InnerText);
                return ((double)predictedResult.PredictedValue * unvariance) + unmean;
            } finally {
                CultureInfo.CurrentCulture = currentCulture;
                reader.Close();
            }
        }

        private static double GetNoteDensity(IEnumerable<Note> notes, double songDuration) {
            return notes.Count() / songDuration;
        }
        private static List<double> GetLocalNoteDensity(IEnumerable<Note> notes, double songDuration, double globalBpm, double windowLength = 2.75, double step = 0.25) {
            var densities = new List<double>();
            var windowLower = 0.0;
            var windowUpper = windowLength;
            do { // we would like this to run at least once so we have some data when songDuration < windowLength
                var numNotes = 0;
                foreach (var n in notes) {
                    var noteTime = 60 / globalBpm * n.beat;
                    if (windowLower <= noteTime && noteTime <= windowUpper) {
                        numNotes += 1;
                    }
                }
                densities.Add(numNotes / windowLength);
                windowLower += step;
                windowUpper += step;
            } while (windowUpper < songDuration);
            return densities;
        }
        // upper 25%
        private static double GetUpperQuartileColumnVariety(List<Note> notes, double songDuration, double globalBpm, double windowLength = 2.75, double step = 0.25) {
            var variety = new List<double>();
            var beatsPerWindow = globalBpm / 60 * windowLength;
            var windowLower = 0.0;
            var windowUpper = windowLength;
            while (windowUpper < songDuration) {
                var localVariety = new List<int>() { 0, 0, 0, 0 };
                foreach (var n in notes) {
                    var noteTime = 60 / globalBpm * n.beat;
                    var noteCol = n.col;
                    if (windowUpper <= noteTime) {
                        break;
                    }
                    if (windowLower <= noteTime) {
                        localVariety[noteCol] += 1;
                    }
                    var l1Norm = Helper.LpNorm(localVariety, 1);
                    if (l1Norm > 0) {
                        // L1-normalise or normalise for the amount of notes
                        var normLocalVariety = Helper.LpNormalise(localVariety, 1);
                        // maps with higher column variety will have a distribution closer to [.25, .25, .25, .25]
                        var score = Helper.LpDistance(normLocalVariety, new List<double>() { 0.25, 0.25, 0.25, 0.25 }, 2);
                        // higher is better
                        variety.Add(-1 * score);

                    }
                }
                windowLower += step;
                windowUpper += step;
            }
            return Helper.GetQuantile(variety, 0.75);
        }
    }
}
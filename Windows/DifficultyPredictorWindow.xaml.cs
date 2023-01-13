using System.Windows;
using System.IO;
using Syncfusion.PMML;
using System.Diagnostics;
using System.Xml;
using System.Printing;
using System;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;
using System.Windows.Controls;
using System.Globalization;

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
            var mapEditor = mainWindow.mapEditor;
            var globalBpm = mapEditor.globalBPM;
            var globalSongDuration = mapEditor.songDuration;
            for (int i = 0; i < mapEditor.numDifficulties; i++) {
                var diff = mapEditor.GetDifficulty(i);
                var diffNotes = diff.notes;
                // treat the song as ending on the last placed note if map is WIP
                var songDuration = CheckTreatMapsWip.IsChecked == true ? 60 / globalBpm * diffNotes.Last().beat : globalSongDuration; 
                var noteDensity = GetNoteDensity(diffNotes, songDuration);
                var maxLocalNoteDensity = GetMaxLocalNoteDensity(diffNotes, songDuration, globalBpm);
                var upperColumnVariety = GetUpperQuartileColumnVariety(diffNotes, songDuration, globalBpm);
                var predictedDiff = EvaluateModel(mapEditor.globalBPM, noteDensity, maxLocalNoteDensity, upperColumnVariety);

                Label diffLabel;
                switch (i) {
                    case 0: diffLabel = lblDifficultyRank1; btnDifficulty0.IsEnabled = true; break;
                    case 1: diffLabel = lblDifficultyRank2; btnDifficulty1.IsEnabled = true; break;
                    case 2: diffLabel = lblDifficultyRank3; btnDifficulty2.IsEnabled = true; break;
                    default: diffLabel = null; break;
                }
                var predictionDisplay = Math.Round(predictedDiff, CheckShowPreciseValues.IsChecked == true ? 1 : 0);
                diffLabel.Content = $"{predictionDisplay}";
            }
            PanelPredictionResults.Visibility = Visibility.Visible;
        }
        /*

        def getLocalColumnVariety(diffMapData, duration, bpm, windowLength= 2.75, step= 0.25) :
            variety = []
                beatsPerWindow = bpm/60 * windowLength
                windowLower = 0
            windowUpper = windowLength
            while windowUpper<duration:
                localVariety = np.array([0, 0, 0, 0])
                for n in diffMapData["_notes"]:
                    noteTime = beatToSec(n["_time"], bpm)
                    noteCol = n["_lineIndex"]
                    if windowUpper <= noteTime:
                        break
                    if windowLower <= noteTime:
                        localVariety[noteCol] += 1
                    if np.linalg.norm(localVariety, 1) > 0:
                        # L1-normalise or normalise for the amount of notes
                        normLocalVariety = localVariety / np.linalg.norm(localVariety, 1)
                        # maps with higher column variety will have a distribution closer to [.25, .25, .25, .25]
                        score = np.linalg.norm(normLocalVariety - np.array([0.25, 0.25, 0.25, 0.25]), 2)
                        # higher is better
                        variety.append(-1 * score)
        
                windowLower += step
                windowUpper += step
        */
        private double GetNoteDensity(List<Note> notes, double songDuration) {
            return notes.Count / songDuration;
        }
        private double GetMaxLocalNoteDensity(List<Note> notes, double songDuration, double globalBpm, double windowLength = 2.75, double step = 0.25) {
            var densities = new List<double>();
            var beatsPerWindow = globalBpm / 60 * windowLength;
            var windowLower = 0.0;
            var windowUpper = windowLength;
            while (windowUpper < songDuration) {
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
            }
            return densities.Max();
        }
        // upper 25%
        private double GetUpperQuartileColumnVariety(List<Note> notes, double songDuration, double globalBpm, double windowLength = 2.75, double step = 0.25) {
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
        private double EvaluateModel(double bpm, double noteDensity, double peakNoteDensity, double peakColumnVariety) {
            string path = Path.Combine(Path.GetTempPath(), "Edda-MLDP_temp.pmml");
            File.WriteAllBytes(path, Properties.Resources.Edda_MLDP);

            var features = new {
                BPM = bpm,
                NoteDensity = noteDensity,
                PeakNoteDensity = peakNoteDensity,
                PeakColumnVariety = peakColumnVariety
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

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(path);

                // Syncfusion package has a bug where it ignores rescaling of output value... so we have to do it ourselves
                XmlNodeList constants = xmlDoc.GetElementsByTagName("Constant");
                var unvariance = double.Parse(constants[0].InnerText);
                var unmean = double.Parse(constants[1].InnerText);
                return ((double)predictedResult.PredictedValue * unvariance) + unmean;
            } finally {
                CultureInfo.CurrentCulture = currentCulture;
            }
        }
    }
}

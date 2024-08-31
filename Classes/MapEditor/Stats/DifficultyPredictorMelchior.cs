using Edda.Const;
using System;
using System.Collections.Generic;
using System.Linq;
using static Edda.Classes.MapEditorNS.Stats.IDifficultyPredictor.Features;

namespace Edda.Classes.MapEditorNS.Stats {
    /// <summary>
    /// Difficulty calculation algorithm suggested by Melchior on RagnaCustoms Discord server. <br/>
    /// After an initial score is calculated (based on the horizontal and vertical distances between notes), 
    /// it's fitted into a 1-10 range by using data from all Ragnarock OST and RAID maps up to (and including) Jonathan Young RAID.
    /// </summary>
    public class DifficultyPredictorMelchior : IDifficultyPredictor {
        public static readonly DifficultyPredictorMelchior SINGLETON = new();

        public IDifficultyPredictor.Features GetSupportedFeatures() {
            return PreciseFloat | AlwaysPredict | RealTime;
        }

        public float? PredictDifficulty(MapEditor mapEditor, int difficultyIndex) {
            var diff = mapEditor.GetDifficulty(difficultyIndex);
            if (diff.notes.Count == 0) return 0;
            var melchiorDiffScore = GetMelchiorDifficultyScore(diff.notes, mapEditor.GlobalBPM);
            var maxTime = 60 / mapEditor.GlobalBPM * (diff.notes.Last().beat - diff.notes.First().beat);
            if (Helper.DoubleApproxEqual(maxTime, 0)) return 0;
            return (float)(DifficultyPrediction.Melchior.FitCoefficient * Math.Sqrt(melchiorDiffScore / maxTime)); // Fitting to a sqrt shape
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
    }
}
using System;

namespace Edda.Classes.MapEditorNS.Stats {
    public interface IDifficultyPredictor {
        Features GetSupportedFeatures();
        float? PredictDifficulty(MapEditor mapEditor, int difficultyIndex);


        [Flags]
        public enum Features {
            None = 0,
            PreciseFloat = 1, // supports "precise" predictions with float values
            AlwaysPredict = 2, // guarantees to always returns a valid value
            RealTime = 4 // supports real-time difficulty prediction of incomplete maps
        }
    }
}

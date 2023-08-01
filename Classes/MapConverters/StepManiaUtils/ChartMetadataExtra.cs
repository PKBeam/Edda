using System.Collections.Generic;
using System.Linq;
using StepmaniaUtils.Enums;

namespace StepmaniaUtils.StepData {
    public class ChartMetadataExtra {
        private readonly List<StepMetadataExtra> _stepCharts;

        public IReadOnlyList<StepMetadataExtra> StepCharts => _stepCharts.AsReadOnly();

        public ChartMetadataExtra() {
            _stepCharts = new List<StepMetadataExtra>();
        }

        internal void Add(StepMetadataExtra stepData) {
            _stepCharts.Add(stepData);
        }

        public StepMetadataExtra GetSteps(PlayStyle style, SongDifficulty difficulty) {
            return StepCharts.FirstOrDefault(c => c.PlayStyle == style && c.Difficulty == difficulty);
        }

        public SongDifficulty GetHighestChartedDifficulty(PlayStyle style) {
            return
                StepCharts
                    .Where(c => c.PlayStyle == style)
                    .OrderByDescending(c => c.Difficulty)
                    .Select(d => d.Difficulty)
                    .FirstOrDefault();
        }
    }
}
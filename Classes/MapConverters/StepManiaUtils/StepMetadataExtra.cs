using System.Diagnostics;
using System.Collections.Generic;
using StepmaniaUtils.Enums;

namespace StepmaniaUtils.StepData
{
    [DebuggerDisplay("{PlayStyle} - {Difficulty} - {DifficultyRating} - {ChartAuthor}")]
    public class StepMetadataExtra
    {
        public StepMetadataExtra(StepMetadata stepMetadata)
        {
            this.PlayStyle = stepMetadata.PlayStyle;
            this.Difficulty = stepMetadata.Difficulty;
            this.DifficultyRating = stepMetadata.DifficultyRating;
            this.ChartAuthor = stepMetadata.ChartAuthor;
            this.ChartName = stepMetadata.ChartName;
            this._measures = new List<IEnumerable<string>>();
        }

        public PlayStyle PlayStyle { get; set; }
        public SongDifficulty Difficulty { get; set; }
        public int DifficultyRating { get; set; }
        public string ChartAuthor { get; set; }

        public string ChartName { get; set; }

        private readonly List<IEnumerable<string>> _measures;

        public IReadOnlyList<IEnumerable<string>> Measures => _measures.AsReadOnly();

        internal void Add(IEnumerable<string> measure)
        {
            _measures.Add(measure);
        }
    }
}
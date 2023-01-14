using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using StepmaniaUtils.Enums;
using StepmaniaUtils.Readers;
using StepmaniaUtils.StepData;

namespace StepmaniaUtils
{
    /// <summary>
    /// The StepmaniaUtils.SmFile class in the package doesn't parse the note data and labels, which will be needed.
    /// They've also wrote this class in a way where I can't extend it easily to parse out additional data, so this implementation is based on source code from https://github.com/StefanoFiumara/stepmania-utils/blob/master/StepmaniaUtils.Core/Core/SmFile.cs
    /// </summary>
    public class SmFileExtra
    {
        public string this[SmFileAttribute attribute] =>
            Attributes.ContainsKey(attribute) ? Attributes[attribute] : string.Empty;

        public string SongTitle => this[SmFileAttribute.TITLE];
        public string Artist => this[SmFileAttribute.ARTIST];

        public string Directory { get; }
        public string BannerPath => this[SmFileAttribute.BANNER];
        public string Group { get; }
        public string FilePath { get; }

        public ChartMetadataExtra ChartMetadata { get; private set; }

        private IDictionary<SmFileAttribute, string> _attributes;

        public IReadOnlyDictionary<SmFileAttribute, string> Attributes => new ReadOnlyDictionary<SmFileAttribute, string>(_attributes);

        public SmFileExtra(string filePath)
        {
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(filePath);
            }

            var validExtensions = new[] { ".sm", ".ssc" };

            if (File.Exists(filePath) == false || !validExtensions.Contains(Path.GetExtension(filePath)))
            {
                throw new ArgumentException($"The given .sm or .ssc file path is either invalid or a file was not found. Path: {filePath}");
            }

            FilePath = filePath;

            Group = Path.GetFullPath(Path.Combine(filePath, @"..\.."))
                        .Split(Path.DirectorySeparatorChar)
                        .Last();

            Directory = Path.GetDirectoryName(filePath);

            ChartMetadata = new ChartMetadataExtra();
            _attributes = new Dictionary<SmFileAttribute, string>();

            ParseFile();
        }

        private void ParseFile()
        {
            using (var reader = StepmaniaFileReaderFactory.CreateReader(FilePath))
            {
                while (reader.ReadNextTag(out SmFileAttribute tag))
                {
                    if (reader.State == ReaderState.ReadingChartMetadata)
                    {
                        var stepData = new StepMetadataExtra(reader.ReadStepchartMetadata());

                        while (reader.IsParsingNoteData)
                        {
                            stepData.Add(reader.ReadMeasure());
                        }

                        ChartMetadata.Add(stepData);
                    }
                    else
                    {
                        var value = reader.ReadTagValue();

                        if (!_attributes.ContainsKey(tag))
                        {
                            _attributes.Add(tag, value);
                        }
                    }
                }
            }
        }

        private void RefreshMetadata()
        {
            ChartMetadata = new ChartMetadataExtra();
            _attributes = new Dictionary<SmFileAttribute, string>();

            ParseFile();
        }
    }
}
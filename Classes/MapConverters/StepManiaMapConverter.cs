using Edda.Const;
using StepmaniaUtils;
using StepmaniaUtils.Enums;
using StepmaniaUtils.StepData;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Converts .sm or .ssc files into Ragnarock beat maps.
/// https://github.com/stepmania/stepmania/wiki/sm
/// https://github.com/stepmania/stepmania/wiki/ssc
/// https://github.com/StefanoFiumara/stepmania-utils
/// </summary>
public class StepManiaMapConverter : IMapConverter {
    private List<TimingMetadata> timingMetadatas;

    public void Convert(string file, RagnarockMap beatmap) {
        // StepmaniaUtils doesn't parse note data we'll need in their SmFile implementation, so we have our own.
        SmFileExtra smFile = new SmFileExtra(file);

        // TITLE
        beatmap.SetValue("_songName", smFile[SmFileAttribute.TITLE]);

        // SUBTITLE - technically it's in the RagnarockMap definition, but it's not currently used. I'll populate it nonetheless in case it will be supported in the future.
        beatmap.SetValue("_songSubName", smFile[SmFileAttribute.SUBTITLE]);

        // ARTIST
        beatmap.SetValue("_songAuthorName", smFile[SmFileAttribute.ARTIST]);

        // TITLETRANSLIT, SUBTITLETRANSLIT, ARTISTTRANSLIT not supported in RagnarockMap currently
        // GENRE not supported in RagnarockMap currently
        // ORIGIN not supported in RagnarockMap currently

        ParseAuthor(beatmap, smFile);
        ParseSong(beatmap, smFile);

        // BACKGROUND not supported in RagnarockMap currently. 
        // Could be abused to specify the environment, but I'd rather leave it unsupported and just leave the default environment, as this would not be very clear to anyone importing existing StepMania maps or mapping for Ragnarock through ArrowVortex.

        // PREVIEWVID not supported in RagnarockMap currently
        // JACKET not supported in RagnarockMap currently
        // CDIMAGE, DISCIMAGE from SSC are not supported in ArrowVortex currently - it's still using CDTITLE only. In any case, these are not supported in RagnarockMap.
        // CDTITLE not supported in RagnarockMap currently
        // LYRICSPATH not supported in RagnarockMap currently
        // SELECTABLE not supported in RagnarockMap currently

        ParseCover(beatmap, smFile);
        ParsePreview(beatmap, smFile);

        // OFFSET - this impacts ALL of the timings on the song - needs to be added to all notes, bookmarks and BPM changes, so that the first beat will start with the first BPM change.
        // As a sidenote, there seems to be matching parameter _songTimeOffset in RagnarockMap, but I'm not sure if and how it's currently supported.
        double.TryParse(smFile[SmFileAttribute.OFFSET], CultureInfo.InvariantCulture, out double songTimeOffset);
        // beatmap.SetValue("_songTimeOffset", songTimeOffset);

        List<BPMChange> bpms = ParseBPMChanges(beatmap, smFile);
        PrepareTimingMetadatas(bpms, songTimeOffset, (double)beatmap.GetValue("_beatsPerMinute"));
        List<BPMChange> convertedBPMs = ConvertBPMChangesToRagnarockBeat(bpms);

        // DISPLAYBPMS not supported in RagnarockMap currently
        // STOPS, DELAYS, WARPS, TIMESIGNATURES, TICKCOUNTS, COMBOS, SPEEDS, SCROLLS, FAKES not supported in RagnarockMap currently
        // LABELS - bookmarks; would be nice to have, but StepmaniaUtils don't support them
        // LASTSECONDHINT not supported in RagnarockMap currently
        // BGCHANGES not supported in RagnarockMap currently
        // KEYSOUNDS not supported in RagnarockMap currently
        // ATTACKS not supported in RagnarockMap currently

        ParseCharts(beatmap, smFile, convertedBPMs);
    }

    /// <summary>
    /// NOTEDATA<br/>
    /// STEPSTYPE - only dance-single is supported<br/>
    /// DESCRIPTION - mapper of the specific chart - can be used as fallback for the author<br/>
    /// DIFFICULTY - name of the difficulty, possible ones are: Beginner, Easy, Medium, Hard, Challenge, Edit.<br/>
    /// StepMania supports more difficulties than Ragnarock. We use only the highest 3 defined, examples:
    /// 1. simfile has charts for Beginner, Easy, Medium, Hard - we use Easy, Medium, Hard.
    /// 2. simfile has charts for Easy, Hard, Challenge, Edit - we use Hard, Challenge, Edit.<br/>
    /// METER - difficulty rating<br/>
    /// RADARVALUES - not supported in RagnarockMap currently
    /// </summary>
    private void ParseCharts(RagnarockMap beatmap, SmFileExtra smFile, List<BPMChange> convertedBPMs) {
        int difficultyIndex = 0;
        foreach (StepmaniaUtils.StepData.StepMetadataExtra steps in
            Enum.GetValues<SongDifficulty>()
                .Select(diff => smFile.ChartMetadata.GetSteps(PlayStyle.Single, diff))
                .Where(steps => steps != null).TakeLast(3)) {
            while (difficultyIndex >= beatmap.numDifficulties) {
                beatmap.AddMap();
            }
            // Set difficulty rank
            beatmap.SetValueForDifficultyMap(difficultyIndex, "_difficultyRank", steps.DifficultyRating);

            // Set notes
            List<Note> notes = ParseNotes(steps);
            beatmap.SetNotesForMap(difficultyIndex, notes);

            // Set previously parsed data applicable to all maps
            beatmap.SetBPMChangesForMap(difficultyIndex, convertedBPMs);
            // beatmap.SetBookmarksForMap(difficultyIndex, convertedBookmarks);

            difficultyIndex++;
        }
    }

    /// <summary>
    /// NOTES - placement of the notes<br/>
    /// This is done in measures - 4-beat sections consisting of 4-character lines. Number of lines determines the beat division for the measure, e.g.
    /// 32 lines in a measure - each line represents 1/8th of a beat (32 / 4 = 8 lines per beat)
    /// Each character in a line is '1' or '0', marking if there is a note on the specific column or not.
    /// </summary>
    private List<Note> ParseNotes(StepMetadataExtra steps) {
        List<Note> notes = new List<Note>();
        foreach (var (measure, measureIndex) in steps.Measures.Select((measure, index) => (measure, index))) {
            double beatDivision = measure.Count() / 4;
            ParseMeasureLines(notes, measure, measureIndex, beatDivision);
        }

        return notes;
    }

    private void ParseMeasureLines(List<Note> notes, IEnumerable<string> measure, int measureIndex, double beatDivision) {
        foreach (var (line, lineIndex) in measure.Select((line, index) => (line, index))) {
            double? beat = ConvertFromStepManiaBeatToRagnarockBeat((4.0 * measureIndex) + (lineIndex / beatDivision));
            if (!beat.HasValue) {
                continue;
            }
            for (int i = 0; i < line.Length; ++i) {
                if (line[i] == '1') {
                    notes.Add(new Note(beat.Value, i));
                }
            }
        }
    }

    /// <summary>
    /// CREDIT - it's also specified on each Chart, but RagnarockMap only supports one value for the map as a whole.
    /// If it's not specified on the simfile level, author name of the highest charted difficulty is used instead.
    /// </summary>
    private void ParseAuthor(RagnarockMap beatmap, SmFileExtra smFile) {
        String levelAuthorName = smFile[SmFileAttribute.CREDIT];
        if (levelAuthorName == string.Empty) {
            SongDifficulty highestDiff = smFile.ChartMetadata.GetHighestChartedDifficulty(PlayStyle.Single);
            levelAuthorName = smFile.ChartMetadata.GetSteps(PlayStyle.Single, highestDiff).ChartAuthor;
        }
        beatmap.SetValue("_levelAuthorName", levelAuthorName);
    }

    /// <summary>
    /// MUSIC - copy as song.ogg into the beatmap folder.
    /// .sm and .ssc also support .mp3 - in this case we skip copying it over
    /// </summary>
    private void ParseSong(RagnarockMap beatmap, SmFileExtra smFile) {
        String songFilename = smFile[SmFileAttribute.MUSIC];
        string songURL = beatmap.PathOf(BeatmapDefaults.SongFilename);
        if (songFilename != string.Empty && Path.GetExtension(songFilename) == ".ogg") {
            // can't copy over an existing file
            if (File.Exists(songURL)) {
                File.Delete(songURL);
            }
            File.Copy(Path.Combine(smFile.Directory, songFilename), songURL);
        }
    }

    /// <summary>
    /// BANNER - this one looks most likely to be supported as cover, but the aspect ratio might be wrong - 256x80 is the recommended size in ArrowVortex, while Ragnarock needs a square cover image.
    /// In this case, I hope it will be recognisable from the preview after loading the map, otherwise it might be good to show a warning somewhere.
    /// </summary>
    private void ParseCover(RagnarockMap beatmap, SmFileExtra smFile) {
        string coverImageFilename = smFile[SmFileAttribute.BANNER];
        if (coverImageFilename != string.Empty) {
            string newCoverFilename = Helper.SanitiseCoverFileName(coverImageFilename);
            string newCoverUrl = beatmap.PathOf(newCoverFilename);
            Helper.FileDeleteIfExists(newCoverUrl);
            File.Copy(Path.Combine(smFile.Directory, coverImageFilename), newCoverUrl);
            beatmap.SetValue("_coverImageFilename", newCoverFilename);
        }
    }

    /// <summary>
    /// PREVIEW should take precedence over SAMPLESTART, SAMPLELENGTH according to .ssc documentation, but it isn't supported by ArrowVortex and StepmaniaUtils.<br/>
    /// SAMPLESTART, SAMPLELENGTH - if both are provided, generate preview file based on those settings. We also need to have the music file already.
    /// </summary>
    private void ParsePreview(RagnarockMap beatmap, SmFileExtra smFile) {
        string songURL = beatmap.PathOf((string)beatmap.GetValue("_songFilename"));
        if (double.TryParse(smFile[SmFileAttribute.SAMPLESTART], CultureInfo.InvariantCulture, out double sampleStart) &&
            double.TryParse(smFile[SmFileAttribute.SAMPLELENGTH], CultureInfo.InvariantCulture, out double sampleLength) &&
            File.Exists(songURL)) {
            string saveURL = beatmap.PathOf(BeatmapDefaults.PreviewFilename);
            int startMin = (int)(sampleStart / 60);
            int startSec = (int)(sampleStart - 60 * startMin);
            int endMin = (int)((sampleStart + sampleLength) / 60);
            int endSec = (int)(sampleStart + sampleLength - 60 * endMin);
            int exitCode = Helper.FFmpeg(beatmap.PathOf(""), $"-i \"{songURL}\" -y -ss 00:{startMin:D2}:{startSec:D2} -to 00:{endMin:D2}:{endSec:D2} -vn \"{saveURL}\"");
            if (exitCode != 0) {
                System.Diagnostics.Trace.WriteLine($"WARNING: Failed to generate {BeatmapDefaults.PreviewFilename}");
            }
        }
    }

    private List<BPMChange> ParseBPMChanges(RagnarockMap beatmap, SmFileExtra smFile) {
        List<BPMChange> bpms = smFile[SmFileAttribute.BPMS].Split(',').Select(pair => pair.Split('=')).Select(splitPair => {
            double time = Helper.DoubleParseInvariant(splitPair[0].Trim());
            double bpm = Helper.DoubleParseInvariant(splitPair[1].Trim());
            return new BPMChange(time, bpm, 4);
        }).ToList();
        bpms.Sort();
        if (bpms.Count > 0) {
            beatmap.SetValue("_beatsPerMinute", bpms.First().BPM);
        }

        return bpms;
    }

    private List<BPMChange> ConvertBPMChangesToRagnarockBeat(List<BPMChange> bpms) {
        List<BPMChange> convertedBPMs = new List<BPMChange>();
        foreach (BPMChange bpm in bpms) {
            double? globalBeat = ConvertFromStepManiaBeatToRagnarockBeat(bpm.globalBeat);
            if (globalBeat.HasValue) {
                bpm.globalBeat = globalBeat.Value;
                convertedBPMs.Add(bpm);
            }
        }

        return convertedBPMs;
    }

    /// <summary>
    /// Calculates timing of StepMania beats to Ragnarock beats. 
    /// This is done because all the timings in .sm and .ssc are kept track in "local" beats, while Ragnarock stores note, bookmark and BPM change timings in "global" beats.
    /// Additionally, we need to take into account the song offset - there's no issue if it's negative (skip beginning of the song), but if it's positive (add silence before song),
    /// some of the notes, bookmarks and BPM changes might need to be cut, as RagnarockMap doesn't support anything on negative "global" beat.
    /// </summary>
    private void PrepareTimingMetadatas(List<BPMChange> stepManiaBPMChanges, double stepManiaOffset, double ragnarockGlobalBPM) {
        timingMetadatas = new List<TimingMetadata>();
        double ragnarockGlobalBeatSoFar = ConvertSecondsToBeats(-stepManiaOffset, ragnarockGlobalBPM);
        double stepManiaBeatSoFar = 0;
        double stepManiaLastBPM = ragnarockGlobalBPM;
        foreach (BPMChange bpmChange in stepManiaBPMChanges) {
            double deltaInSeconds = ConvertBeatsToSeconds(bpmChange.globalBeat - stepManiaBeatSoFar, stepManiaLastBPM);
            double deltaInRagnarockGlobalBeats = ConvertSecondsToBeats(deltaInSeconds, ragnarockGlobalBPM);
            if (ragnarockGlobalBeatSoFar < 0 && ragnarockGlobalBeatSoFar + deltaInRagnarockGlobalBeats > 0) {
                // This is in case the offset was positive - we partially save the timing of the BPM change that started with negative "global" beat, but ended with positive "global" beat.
                // Thanks to this, we can still re-time notes and bookmarks that happen halfway through this BPM change.
                double deltaInSecondsToZero = ConvertBeatsToSeconds(-ragnarockGlobalBeatSoFar, ragnarockGlobalBPM);
                double stepManiaBeatAtZero = stepManiaBeatSoFar + ConvertSecondsToBeats(deltaInSecondsToZero, stepManiaLastBPM);
                timingMetadatas.Add(new TimingMetadata(stepManiaBeatAtZero, stepManiaLastBPM, 0, ragnarockGlobalBPM));
            }
            stepManiaBeatSoFar = bpmChange.globalBeat;
            stepManiaLastBPM = bpmChange.BPM;
            ragnarockGlobalBeatSoFar += deltaInRagnarockGlobalBeats;
            if (ragnarockGlobalBeatSoFar > 0) {
                timingMetadatas.Add(new TimingMetadata(stepManiaBeatSoFar, stepManiaLastBPM, ragnarockGlobalBeatSoFar, ragnarockGlobalBPM));
            }
        }
    }

    private double? ConvertFromStepManiaBeatToRagnarockBeat(double stepManiaBeat) {
        TimingMetadata matchingTiming = null;
        foreach (TimingMetadata timingMetadata in timingMetadatas) {
            if (stepManiaBeat < timingMetadata.stepManiaBeat) {
                break;
            }
            matchingTiming = timingMetadata;
        }
        if (matchingTiming == null) {
            return null;
        } else {
            double deltaInSeconds = ConvertBeatsToSeconds(stepManiaBeat - matchingTiming.stepManiaBeat, matchingTiming.stepManiaBPM);
            double deltaInRagnarockGlobalBeats = ConvertSecondsToBeats(deltaInSeconds, matchingTiming.ragnarockGlobalBPM);
            return matchingTiming.ragnarockGlobalBeat + deltaInRagnarockGlobalBeats;
        }
    }

    private double ConvertBeatsToSeconds(double beats, double beatsPerMinute) {
        return 60.0 * beats / beatsPerMinute;
    }

    private double ConvertSecondsToBeats(double seconds, double beatsPerMinute) {
        return beatsPerMinute * seconds / 60.0;
    }

    private class TimingMetadata {
        public double stepManiaBeat { get; set; }
        public double stepManiaBPM { get; set; }
        public double ragnarockGlobalBeat { get; set; }
        public double ragnarockGlobalBPM { get; set; }

        public TimingMetadata(double stepManiaBeat, double stepManiaBPM, double ragnarockGlobalBeat, double ragnarockGlobalBPM) {
            this.stepManiaBeat = stepManiaBeat;
            this.stepManiaBPM = stepManiaBPM;
            this.ragnarockGlobalBeat = ragnarockGlobalBeat;
            this.ragnarockGlobalBPM = ragnarockGlobalBPM;
        }
    }
}
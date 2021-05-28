using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Note = System.ValueTuple<double, int>;


public class RagnarockMap {

    // constants
    private readonly string[] difficultyNames = { "Easy", "Normal", "Hard" };
    private readonly int defaultNoteJumpMovementSpeed = 15;
    private readonly double defaultBPM = 120;
    private readonly string defaultSongName = "song.ogg";
    private readonly double defaultGridSpacing = 1.0;
    private readonly int defaultGridDivision = 4;

    // public state variables
    public int numDifficulties {
        get {
            var obj = JObject.Parse(infoStr);
            var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
            return res.Count();
        }
    }
    public string folderPath;

    // private state variables
    private string infoStr;
    private string[] difficultyMaps = new string[3];
    private string eddaVersionNumber;

    public RagnarockMap(string folderPath, bool makeNew, string eddaVersionNumber) {
        this.folderPath = folderPath;
        this.eddaVersionNumber = eddaVersionNumber;
        if (makeNew) {
            InitInfo();
            AddDifficultyMap(difficultyNames[0]);
            WriteDifficultyMap(0);
            WriteInfo();
        } else {
            ReadInfo();
            for (int i = 0; i < numDifficulties; i++) {
                ReadDifficultyMap(i);
            }
            // handle edda-specific custom fields for compatibility with MMA2 maps
            for (var i = 0; i < numDifficulties; i++) {
                if (GetCustomValueForDifficultyMap("_editorGridSpacing", i) == null) {
                    SetCustomValueForDifficultyMap("_editorGridSpacing", defaultGridSpacing, i);
                }
                if (GetCustomValueForDifficultyMap("_editorGridDivision", i) == null) {
                    SetCustomValueForDifficultyMap("_editorGridDivision", defaultGridDivision, i);
                }
            }
        }
    }
    private void InitInfo() {
        // init info.dat json
        var infoDat = new {
            _version = "1",
            _songName = "",
            _songSubName = "",                              // unused?
            _songAuthorName = "",
            _levelAuthorName = "",
            _beatsPerMinute = defaultBPM,
            _shuffle = 0,                                   // unused?
            _shufflePeriod = 0.5,                           // unused?
            _previewStartTime = 0,                          // unused?
            _previewDuration = 0,                           // unused?
            _songApproximativeDuration = 0,
            _songFilename = defaultSongName,
            _coverImageFilename = "",
            _environmentName = "DefaultEnvironment",
            _songTimeOffset = 0,
            _customData = new {
                _contributors = new List<string>(),
                _editors = new {
                    Edda = new {
                        version = eddaVersionNumber,
                    },
                    _lastEditedBy = "Edda"
                },
            },
            _difficultyBeatmapSets = new[] {
            new {
                _beatmapCharacteristicName = "Standard",
                _difficultyBeatmaps = new List<object> {},
            },
        },
        };
        infoStr = JsonConvert.SerializeObject(infoDat, Formatting.Indented);
    }
    public void ReadInfo() {
        infoStr = File.ReadAllText(AbsPath("info.dat"));
        if (!ValidateInfo()) {
            throw new Exception("Invalid info.dat file");
        }
    }
    public void WriteInfo() {
        File.WriteAllText(AbsPath("info.dat"), infoStr);
    }
    public void SetValue(string key, object value) {
        var obj = JObject.Parse(infoStr);
        obj[key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken GetValue(string key) {
        var obj = JObject.Parse(infoStr);
        var res = obj[key];
        return res;
    }
    public void SetValueForDifficultyMap(string key, object value, int indx) {
        var obj = JObject.Parse(infoStr);
        obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken GetValueForDifficultyMap(string key, int indx) {
        var obj = JObject.Parse(infoStr);
        var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key];
        return res;
    }
    public void SetCustomValueForDifficultyMap(string key, object value, int indx) {
        var obj = JObject.Parse(infoStr);
        obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx]["_customData"][key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken GetCustomValueForDifficultyMap(string key, int indx) {
        var obj = JObject.Parse(infoStr);
        var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx]["_customData"][key];
        return res;
    }
    public void AddDifficultyMap(string difficulty) {
        if (numDifficulties == 3) {
            return;
        }
        var obj = JObject.Parse(infoStr);
        var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        var beatmapDat = new {
            _difficulty = difficulty,
            _difficultyRank = 1,
            _beatmapFilename = $"{difficulty}.dat",
            _noteJumpMovementSpeed = defaultNoteJumpMovementSpeed,
            _noteJumpStartBeatOffset = 0,
            _customData = new {
                _editorOffset = 0,
                _editorOldOffset = 0,
                _editorGridSpacing = 1,
                _editorGridDivision = 4,
                _warnings = new List<string>(),
                _information = new List<string>(),
                _suggestions = new List<string>(),
                _requirements = new List<string>(),
            },
        };
        beatmaps.Add(JToken.FromObject(beatmapDat));
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        var mapDat = new {
            _version = "1",
            _customData = new {
                _time = 0,
                _BPMChanges = new List<object>(),
                _bookmarks = new List<object>(),
            },
            _events = new List<object>(),
            _notes = new List<object>(),
            _obstacles = new List<object>(),
        };
        var mapStr = JsonConvert.SerializeObject(mapDat, Formatting.Indented);
        difficultyMaps[numDifficulties - 1] = mapStr;
    }
    public void ReadDifficultyMap(int indx) {
        var filename = (string)GetValueForDifficultyMap("_beatmapFilename", indx);
        difficultyMaps[indx] = File.ReadAllText(AbsPath(filename));
    }
    public void WriteDifficultyMap(int indx) {
        var filename = (string)GetValueForDifficultyMap("_beatmapFilename", indx);
        File.WriteAllText(AbsPath(filename), difficultyMaps[indx]);
    }
    public void DeleteDifficultyMap(int indx) {
        if (numDifficulties == 1) {
            return;
        }
        var filename = (string)GetValueForDifficultyMap("_beatmapFilename", indx);
        File.Delete(AbsPath(filename));
        for (int i = indx; i < numDifficulties - 1; i++) {
            difficultyMaps[i] = difficultyMaps[i + 1];
        }
        difficultyMaps[numDifficulties - 1] = null;

        var obj = JObject.Parse(infoStr);
        var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        beatmaps.RemoveAt(indx);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        RenameDifficultyMaps();
        WriteInfo();
        //writeDifficultyMap(indx);
    }
    private void RenameDifficultyMaps() {
        for (int i = 0; i < numDifficulties; i++) {
            var oldFile = (string)GetValueForDifficultyMap("_beatmapFilename", i);
            File.Move(AbsPath(oldFile), AbsPath($"{difficultyNames[i]}_temp.dat"));
            SetValueForDifficultyMap("_difficulty", difficultyNames[i], i);
            SetValueForDifficultyMap("_beatmapFilename", $"{difficultyNames[i]}.dat", i);
        }
        for (int i = 0; i < numDifficulties; i++) {
            File.Move(AbsPath($"{difficultyNames[i]}_temp.dat"), AbsPath($"{difficultyNames[i]}.dat"));
        }
    }
    public List<Note> GetNotesForMap(int indx) {
        var obj = JObject.Parse(difficultyMaps[indx]);
        var res = obj["_notes"];
        List<Note> output = new List<Note>();
        foreach (JToken n in res) {
            double time = double.Parse((string)n["_time"], CultureInfo.InvariantCulture);
            int colIndex = int.Parse((string)n["_lineIndex"]);
            output.Add((time, colIndex));
        }
        return output;
    }
    public void SetNotesForMap(List<Note> notes, int indx) {
        var numNotes = notes.Count;
        var notesObj = new Object[numNotes];
        for (int i = 0; i < numNotes; i++) {
            var thisNote = notes[i];
            var thisNoteObj = new {
                _time = thisNote.Item1,
                _lineIndex = thisNote.Item2,
                _lineLayer = 1,
                _type = 0,
                _cutDirection = 1
            };
            notesObj[i] = thisNoteObj;
        }
        var thisMapStr = JObject.Parse(difficultyMaps[indx]);
        thisMapStr["_notes"] = JToken.FromObject(notesObj);
        difficultyMaps[indx] = JsonConvert.SerializeObject(thisMapStr, Formatting.Indented);
        //mapsStr[selectedDifficulty]["_notes"] = jObj;
    }
    private bool ValidateInfo() {

        List<JTokenType> stringTypes = new List<JTokenType>() { JTokenType.String };
        List<JTokenType> numericTypes = new List<JTokenType>() { JTokenType.Float, JTokenType.Integer };
        List<JTokenType> arrayTypes = new List<JTokenType>() { JTokenType.Array };
        (float, float) positiveNumeric = (0, float.PositiveInfinity);
        (float, float) anyNumeric = (float.NegativeInfinity, float.PositiveInfinity);

        Dictionary<string, List<JTokenType>> expectedTypesL1 = new Dictionary<string, List<JTokenType>> {
            {"_version",                   stringTypes  },
            {"_songName",                  stringTypes  },
            {"_songSubName",               stringTypes  },
            {"_songAuthorName",            stringTypes  },
            {"_levelAuthorName",           stringTypes  },
            {"_beatsPerMinute",            numericTypes },
            {"_shuffle",                   numericTypes },
            {"_shufflePeriod",             numericTypes },
            {"_previewStartTime",          numericTypes },
            {"_previewDuration",           numericTypes },
            {"_songApproximativeDuration", numericTypes },
            {"_songFilename",              stringTypes  },
            {"_coverImageFilename",        stringTypes  },
            {"_environmentName",           stringTypes  },
            {"_songTimeOffset",            numericTypes },
            {"_difficultyBeatmapSets",     arrayTypes   }
        };
        Dictionary<string, (float, float)> expectedValuesL1 = new Dictionary<string, (float, float)> {
            {"_beatsPerMinute",            positiveNumeric },
            {"_shuffle",                   positiveNumeric },
            {"_shufflePeriod",             positiveNumeric },
            {"_previewStartTime",          positiveNumeric },
            {"_previewDuration",           positiveNumeric },
            {"_songApproximativeDuration", positiveNumeric },
            {"_songTimeOffset",            anyNumeric      },
        };
        Dictionary<string, List<JTokenType>> expectedTypesL2 = new Dictionary<string, List<JTokenType>> {
            {"_beatmapCharacteristicName", stringTypes },
            {"_difficultyBeatmaps",        arrayTypes  },
        };
        Dictionary<string, List<JTokenType>> expectedTypesL3 = new Dictionary<string, List<JTokenType>> {
            {"_difficulty",              stringTypes  },
            {"_difficultyRank",          numericTypes },
            {"_beatmapFilename",         stringTypes  },
            {"_noteJumpMovementSpeed",   numericTypes },
            {"_noteJumpStartBeatOffset", numericTypes },
        };
        Dictionary<string, (float, float)> expectedValuesL3 = new Dictionary<string, (float, float)> {
            // this is handled specially
            /*{"_difficultyRank",          (0, 11)},*/ 
            {"_noteJumpMovementSpeed",   positiveNumeric },
            {"_noteJumpStartBeatOffset", anyNumeric      },
        };
        try {
            // validate all fields and types
            var obj = JObject.Parse(infoStr);
            foreach (var i in expectedTypesL1) {
                // validate type
                if (!i.Value.Contains(obj[i.Key].Type)) {
                    return false;
                }
                // validate value
                if (i.Value == numericTypes) {
                    var val = Helper.DoubleParseInvariant((string)obj[i.Key]);
                    if (!Helper.RangeCheck(val, expectedValuesL1[i.Key].Item1, expectedValuesL1[i.Key].Item2)) {
                        return false;
                    }
                }
            }
            // validate array
            var dbs = (JArray)obj["_difficultyBeatmapSets"];
            foreach (var dbsItem in dbs) {
                foreach (var i in expectedTypesL2) {
                    // validate type
                    if (!i.Value.Contains(dbsItem[i.Key].Type)) {
                        return false;
                    }
                }
                // validate array
                var db = (JArray)dbsItem["_difficultyBeatmaps"];
                foreach (var dbItem in db) {
                    foreach (var i in expectedTypesL3) {
                        // validate type
                        if (!i.Value.Contains(dbItem[i.Key].Type)) {
                            return false;
                        }
                        // validate value
                        if (i.Value == numericTypes) {
                            var val = Helper.DoubleParseInvariant((string)dbItem[i.Key]);
                            // special case
                            if (i.Key == "_difficultyRank") {
                                if (val < 1 || 10 < val) {
                                    return false;
                                }
                            } else if (!Helper.RangeCheck(val, expectedValuesL3[i.Key].Item1, expectedValuesL3[i.Key].Item2)) {
                                return false;
                            }
                        }
                    }
                }
            }
        } catch (Exception e) {
            return false;
        }
        // create _customData if it isnt there
        return true;
    }
    private bool ValidateMap() {

    }
    private void InitCustomData() {

    }
    private void InitCustomDataForMap() {

    }
    // helper functions
    private string AbsPath(string f) {
        return Path.Combine(folderPath, f);
    }
}

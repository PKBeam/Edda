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
    readonly int gridDivisionMax = 12;

    // -- data validation
    private readonly List<JTokenType?> stringTypes = new List<JTokenType?>() { JTokenType.String };
    private readonly List<JTokenType?> numericTypes = new List<JTokenType?>() { JTokenType.Float, JTokenType.Integer };
    private readonly List<JTokenType?> arrayTypes = new List<JTokenType?>() { JTokenType.Array };
    private readonly (float, float) positiveNumeric = (0, float.PositiveInfinity);
    private readonly (float, float) anyNumeric = (float.NegativeInfinity, float.PositiveInfinity);

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
                _contributors = new List<object>(),
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
        ValidateInfo();
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
                _warnings = new List<object>(),
                _information = new List<object>(),
                _suggestions = new List<object>(),
                _requirements = new List<object>(),
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
        ValidateMap(indx);
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
    private void ValidateInfo() {

        Dictionary<string, List<JTokenType?>> expectedTypesL1 = new Dictionary<string, List<JTokenType?>> {
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
        Dictionary<string, List<JTokenType?>> expectedTypesL2 = new Dictionary<string, List<JTokenType?>> {
            {"_beatmapCharacteristicName", stringTypes },
            {"_difficultyBeatmaps",        arrayTypes  },
        };
        Dictionary<string, List<JTokenType?>> expectedTypesL3 = new Dictionary<string, List<JTokenType?>> {
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

        // validate all fields and types
        var obj = JObject.Parse(infoStr);
        foreach (var i in expectedTypesL1) {
            // validate type
            if (!i.Value.Contains(obj[i.Key].Type)) {
                throw new Exception($"Incorrect or missing key {i.Key}");
            }
            // validate value
            if (i.Value == numericTypes) {
                var val = Helper.DoubleParseInvariant((string)obj[i.Key]);
                if (!Helper.RangeCheck(val, expectedValuesL1[i.Key].Item1, expectedValuesL1[i.Key].Item2)) {
                    throw new Exception($"Bad value for key {i.Key}");
                }
            }
        }
        // validate array
        var dbs = (JArray)obj["_difficultyBeatmapSets"];
        foreach (var dbsItem in dbs) {
            foreach (var i in expectedTypesL2) {
                // validate type
                if (!i.Value.Contains(dbsItem[i.Key].Type)) {
                    throw new Exception($"Incorrect or missing key {i.Key}");
                }
            }
            // validate array
            var db = (JArray)dbsItem["_difficultyBeatmaps"];
            foreach (var dbItem in db) {
                foreach (var i in expectedTypesL3) {
                    // validate type
                    if (!i.Value.Contains(dbItem[i.Key].Type)) {
                        throw new Exception($"Incorrect or missing key {i.Key}");
                    }
                    // validate value
                    if (i.Value == numericTypes) {
                        var val = Helper.DoubleParseInvariant((string)dbItem[i.Key]);
                        // special case
                        if (i.Key == "_difficultyRank") {
                            if (val < 1 || 10 < val) {
                                throw new Exception($"Bad value for key {i.Key}");
                            }
                        } else if (!Helper.RangeCheck(val, expectedValuesL3[i.Key].Item1, expectedValuesL3[i.Key].Item2)) {
                            throw new Exception($"Bad value for key {i.Key}");
                        }
                    }
                }
            }
        }
        // create _customData if it isnt there
        InitCustomData();
    }
    private void InitCustomData() {
        var obj = JObject.Parse(infoStr);

        // top level custom data
        if (obj["_customData"]?.Type != JTokenType.Object) {
            var customDataObject = new {
                _contributors = new List<object>(),
                _editors = new {
                    Edda = new {
                        version = eddaVersionNumber,
                    },
                    _lastEditedBy = "Edda"
                },
            };
            obj["_customData"] = JToken.FromObject(customDataObject);
        }
        var customData = obj["_customData"];
        if (customData["_contributors"]?.Type != JTokenType.Array) {
            customData["_contributors"] = JToken.FromObject(new List<string>());
        }
        if (customData["_editors"]?.Type != JTokenType.Object) {
            var editorsObject = new {
                Edda = new {
                    version = eddaVersionNumber,
                },
                _lastEditedBy = "Edda"
            };
            customData["_editors"] = JToken.FromObject(editorsObject);
        }
        if (customData["_editors"]["Edda"]?.Type != JTokenType.Object) {
            customData["_editors"]["Edda"] = JToken.FromObject(new { version = eddaVersionNumber });
        }
        //if (customData["_editors"]["_lastEditedBy"]?.Type != JTokenType.String) {
            customData["_editors"]["_lastEditedBy"] = JToken.FromObject("Edda");
        //}

        // per beatmap custom data
        Dictionary<string, List<JTokenType?>> expectedTypes = new Dictionary<string, List<JTokenType?>> {
            {"_editorOffset",       numericTypes },
            {"_editorOldOffset",    numericTypes },
            {"_editorGridSpacing",  numericTypes },
            {"_editorGridDivision", numericTypes },
            {"_warnings",           arrayTypes },
            {"_information",        arrayTypes },
            {"_suggestions",        arrayTypes },
            {"_requirements",       arrayTypes }
        };
        Dictionary<string, (float, float)> expectedValues = new Dictionary<string, (float, float)> {
            {"_editorOffset",       anyNumeric },
            {"_editorOldOffset",    anyNumeric },
            {"_editorGridSpacing",  positiveNumeric },
            {"_editorGridDivision", positiveNumeric },
        };
        Dictionary<string, float> defaultValues = new Dictionary<string, float> {
            {"_editorOffset",       0 },
            {"_editorOldOffset",    0 },
            {"_editorGridSpacing",  1 },
            {"_editorGridDivision", 4 },
        };

        var beatmaps = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        foreach (var map in beatmaps) {
            if (map["_customData"]?.Type != JTokenType.Object) {
                var customDataObject = new {
                    _editorOffset = 0,
                    _editorOldOffset = 0,
                    _editorGridSpacing = 1,
                    _editorGridDivision = 4,
                    _warnings = new List<object>(),
                    _information = new List<object>(),
                    _suggestions = new List<object>(),
                    _requirements = new List<object>(),
                };
                map["_customData"] = JToken.FromObject(customDataObject);
            }
        }
        foreach (var map in beatmaps) {
            var mapCustomData = map["_customData"];
            foreach (var i in expectedTypes) {
                // validate type
                if (!i.Value.Contains(mapCustomData[i.Key].Type)) {
                    mapCustomData[i.Key] = defaultValues[i.Key];
                }
                // validate value
                if (i.Value == numericTypes) {
                    var val = Helper.DoubleParseInvariant((string)mapCustomData[i.Key]);
                    // special case
                    if (i.Key == "_editorGridDivision") {
                        if ((int)val != val || val < 1 || gridDivisionMax < val) {
                            mapCustomData[i.Key] = defaultValues[i.Key];
                        }
                    } else if (!Helper.RangeCheck(val, expectedValues[i.Key].Item1, expectedValues[i.Key].Item2)) {
                        mapCustomData[i.Key] = defaultValues[i.Key];
                    }
                }
            }
        }

        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    private void ValidateMap(int indx) {
        Dictionary<string, List<JTokenType?>> expectedTypesL1 = new Dictionary<string, List<JTokenType?>> {
            {"_version",   stringTypes },
            {"_events",    arrayTypes },
            {"_notes",     arrayTypes },
            {"_obstacles", arrayTypes }
        };

        Dictionary<string, List<JTokenType?>> expectedTypesL2 = new Dictionary<string, List<JTokenType?>> {
            {"_time",         numericTypes },
            {"_lineIndex",    numericTypes },
            {"_lineLayer",    numericTypes },
            {"_type",         numericTypes },
            {"_cutDirection", numericTypes }
        };

        var obj = JObject.Parse(difficultyMaps[indx]);
        foreach (var i in expectedTypesL1) {
            // validate type
            if (!i.Value.Contains(obj[i.Key].Type)) {
                throw new Exception($"Incorrect or missing key {i.Key}");
            }
        }
        var notes = (JArray)obj["_notes"];
        foreach (var note in notes) {
            foreach (var i in expectedTypesL2) {
                // validate type
                if (!i.Value.Contains(note[i.Key].Type)) {
                    throw new Exception($"Incorrect or missing key {i.Key}");
                }
                // validate value
                if (i.Value == numericTypes) {
                    var val = Helper.DoubleParseInvariant((string)note[i.Key]);
                    // special case
                    Exception ex = new Exception($"Bad value for key {i.Key}");
                    switch (i.Key) {
                        case "_time":
                            if (val < 0) throw ex;
                            break;
                        case "_lineIndex":
                            if ((int)val != val || val < 0 || 3 < val) throw ex;
                            break;
                        case "_lineLayer":
                            if (val != 1) throw ex;
                            break;
                        case "_type":
                            if (val != 0) throw ex;
                            break;
                        case "_cutDirection":
                            if (val != 1) throw ex;
                            break;
                    }
                }
            }
        }
        
        InitCustomDataForMap(indx);
    }
    private void InitCustomDataForMap(int indx) {
        var obj = JObject.Parse(difficultyMaps[indx]);

        // top level custom data
        if (obj["_customData"]?.Type != JTokenType.Object) {
            var customDataObject = new {
                _time = 0,
                _BPMChanges = new List<object>(),
                _bookmarks = new List<object>(),
            };
            obj["_customData"] = JToken.FromObject(customDataObject);
        }
        var customData = obj["_customData"];
        if (!numericTypes.Contains(customData["_time"]?.Type)) {
            customData["_time"] = 0;
        }
        // TODO: validate individual array objects
        if (customData["_BPMChanges"]?.Type != JTokenType.Array) {
            customData["_BPMChanges"] = JToken.FromObject(new List<object>());
        }
        if (customData["_bookmarks"]?.Type != JTokenType.Array) {
            customData["_bookmarks"] = JToken.FromObject(new List<object>());
        }
        difficultyMaps[indx] = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }

    // helper functions
    private string AbsPath(string f) {
        return Path.Combine(folderPath, f);
    }
}

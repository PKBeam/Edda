using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Edda;
using Edda.Const;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class RagnarockMap {

    // -- data validation
    private readonly List<JTokenType?> stringTypes = new() { JTokenType.String };
    private readonly List<JTokenType?> numericTypes = new() { JTokenType.Float, JTokenType.Integer };
    private readonly List<JTokenType?> arrayTypes = new() { JTokenType.Array };
    private readonly List<JTokenType?> boolTypes = new() { JTokenType.Boolean };
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

    // private state variables
    private string folderPath;
    private string infoStr;
    private string[] difficultyMaps = new string[3];
    public RagnarockMap(string folderPath, bool makeNew) {
        this.folderPath = folderPath;
        // TODO: automatically calculate makeNew?
        if (makeNew) {
            InitInfo();
            AddMap();
            //WriteMap(0);
            //WriteInfo();
        } else {
            ReadInfo();
            ValidateInfo();
            for (int i = 0; i < numDifficulties; i++) {
                ReadMap(i);
                ValidateMap(i);
            }
        }
    }

    public void SaveToFile() {
        UpdateEddaVersion();
        for (int i = 0; i < numDifficulties; i++) {
            WriteMap(i);
        }
        WriteInfo();
    }

    // info.dat operations
    public void ReadInfo() {
        infoStr = File.ReadAllText(PathOf("info.dat"));
    }
    public void WriteInfo() {
        File.WriteAllText(PathOf("info.dat"), infoStr);
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
    private void InitInfo() {
        // init info.dat json
        var infoDat = new {
            _version = "1", // what is this for?
            _songName = "",
            _songSubName = "",                              // unused?
            _songAuthorName = "",
            _levelAuthorName = "",
            _explicit = "false",
            _beatsPerMinute = BeatmapDefaults.BeatsPerMinute,
            _shuffle = BeatmapDefaults.Shuffle,              // unused?
            _shufflePeriod = BeatmapDefaults.ShufflePeriod,  // unused?
            _previewStartTime = 0,                          // unused?
            _previewDuration = 0,                           // unused?
            _songApproximativeDuration = 0,
            _songFilename = BeatmapDefaults.SongFilename,
            _coverImageFilename = "",
            _environmentName = BeatmapDefaults.EnvironmentNames[0],
            _songTimeOffset = 0,
            _customData = new {
                _contributors = new List<object>(),
                _editors = new {
                    Edda = new {
                        version = Program.VersionString,
                    },
                    _lastEditedBy = Program.Name,
                },
            },
            _difficultyBeatmapSets = new[] {
                new {
                    _beatmapCharacteristicName = BeatmapDefaults.BeatmapCharacteristicName,
                    _difficultyBeatmaps = new List<object> {},
                },
            },
        };
        infoStr = JsonConvert.SerializeObject(infoDat, Formatting.Indented);
    }
    private void ValidateInfo() {

        Dictionary<string, List<JTokenType?>> expectedTypesL1 = new Dictionary<string, List<JTokenType?>> {
            {"_version",                   stringTypes  },
            {"_songName",                  stringTypes  },
            {"_songSubName",               stringTypes  },
            {"_songAuthorName",            stringTypes  },
            {"_levelAuthorName",           stringTypes  },
            {"_explicit",                  stringTypes    },
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
            if (i.Key == "_songApproximativeDuration" && obj[i.Key]?.Type != JTokenType.Integer) {
                SetValue("_songApproximativeDuration", 1);
                continue;
            }
            if (i.Key == "_explicit" && (obj[i.Key] == null)) {
                SetValue("_explicit", "false");
                continue;
            }
            if (!i.Value.Contains(obj[i.Key]?.Type)) {
                throw new Exception($"Incorrect or missing key {i.Key}");
            }
            // validate value
            if (i.Value == numericTypes) {
                var val = Helper.DoubleParseInvariant((string)obj[i.Key]);
                if (!Helper.DoubleRangeCheck(val, expectedValuesL1[i.Key].Item1, expectedValuesL1[i.Key].Item2)) {
                    throw new Exception($"Bad value for key {i.Key}");
                }
            }
        }
        // validate array
        var dbs = (JArray)obj["_difficultyBeatmapSets"];
        foreach (var dbsItem in dbs) {
            foreach (var i in expectedTypesL2) {
                // validate type
                if (!i.Value.Contains(dbsItem[i.Key]?.Type)) {
                    throw new Exception($"Incorrect or missing key {i.Key}");
                }
            }
            // validate array
            var db = (JArray)dbsItem["_difficultyBeatmaps"];
            foreach (var dbItem in db) {
                foreach (var i in expectedTypesL3) {
                    // validate type
                    if (!i.Value.Contains(dbItem[i.Key]?.Type)) {
                        throw new Exception($"Incorrect or missing key {i.Key}");
                    }
                    // validate value
                    if (i.Value == numericTypes) {
                        var val = Helper.DoubleParseInvariant((string)dbItem[i.Key]);
                        // special case
                        if (i.Key == "_difficultyRank") {
                            if (val < Editor.DifficultyLevelMin || val > Editor.DifficultyLevelMax) {
                                throw new Exception($"Bad value for key {i.Key}");
                            }
                        } else if (!Helper.DoubleRangeCheck(val, expectedValuesL3[i.Key].Item1, expectedValuesL3[i.Key].Item2)) {
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
                        version = Program.VersionString,
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
                    version = Program.VersionString,
                },
                _lastEditedBy = "Edda"
            };
            customData["_editors"] = JToken.FromObject(editorsObject);
        }
        if (customData["_editors"]["Edda"]?.Type != JTokenType.Object) {
            customData["_editors"]["Edda"] = JToken.FromObject(new { version = Program.DisplayVersionString });
        }
        //if (customData["_editors"]["_lastEditedBy"]?.Type != JTokenType.String) {
        customData["_editors"]["_lastEditedBy"] = JToken.FromObject("Edda");
        //}

        // per beatmap custom data
        Dictionary<string, List<JTokenType?>> expectedTypes = new Dictionary<string, List<JTokenType?>> {
            {"_editorOldOffset",    numericTypes },
            {"_editorGridSpacing",  numericTypes },
            {"_editorGridDivision", numericTypes },
            {"_warnings",           arrayTypes },
            {"_information",        arrayTypes },
            {"_suggestions",        arrayTypes },
            {"_requirements",       arrayTypes }
        };
        Dictionary<string, (float, float)> expectedValues = new Dictionary<string, (float, float)> {
            {"_editorOldOffset",    anyNumeric },
            {"_editorGridSpacing",  positiveNumeric },
            {"_editorGridDivision", positiveNumeric },
        };
        Dictionary<string, float> defaultValues = new Dictionary<string, float> {
            {"_editorOldOffset",    0 },
            {"_editorGridSpacing",  (float)Editor.DefaultGridSpacing },
            {"_editorGridDivision", (float)Editor.DefaultGridDivision },
        };

        var beatmaps = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        foreach (var map in beatmaps) {
            if (map["_customData"]?.Type != JTokenType.Object) {
                var customDataObject = new {
                    _editorOldOffset = 0,
                    _editorGridSpacing = Editor.DefaultGridSpacing,
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
                if (!i.Value.Contains(mapCustomData[i.Key]?.Type)) {
                    mapCustomData[i.Key] = defaultValues[i.Key];
                }
                // validate value
                if (i.Value == numericTypes) {
                    var val = Helper.DoubleParseInvariant((string)mapCustomData[i.Key]);
                    // special case
                    if (i.Key == "_editorGridDivision") {
                        if ((int)val != val || val < 1 || Editor.GridDivisionMax < val) {
                            mapCustomData[i.Key] = defaultValues[i.Key];
                        }
                    } else if (!Helper.DoubleRangeCheck(val, expectedValues[i.Key].Item1, expectedValues[i.Key].Item2)) {
                        mapCustomData[i.Key] = defaultValues[i.Key];
                    }
                }
            }
        }

        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    private void UpdateEddaVersion() {
        var obj = JObject.Parse(infoStr);
        obj["_customData"]["_editors"]["Edda"]["version"] = Program.VersionString;
        obj["_customData"]["_editors"]["_lastEditedBy"] = Program.Name;
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }

    // per-map operations
    public void SetValueForDifficultyMap(int indx, string key, object value) {
        var obj = JObject.Parse(infoStr);
        obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken GetValueForDifficultyMap(int indx, string key) {
        var obj = JObject.Parse(infoStr);
        var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key];
        return res;
    }
    public void SetCustomValueForDifficultyMap(int indx, string key, object value) {
        var obj = JObject.Parse(infoStr);
        obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx]["_customData"][key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken GetCustomValueForDifficultyMap(int indx, string key) {
        var obj = JObject.Parse(infoStr);
        var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx]["_customData"][key];
        return res;
    }
    public void AddMap() {
        if (numDifficulties == 3) {
            return;
        }
        var mapName = BeatmapDefaults.DifficultyNames[numDifficulties];
        var obj = JObject.Parse(infoStr);
        var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        var beatmapDat = new {
            _difficulty = mapName,
            _difficultyRank = 1,
            _beatmapFilename = $"{mapName}.dat",
            _noteJumpMovementSpeed = BeatmapDefaults.GetPreferredNoteJumpMovementSpeed(),
            _noteJumpStartBeatOffset = 0,
            _customData = new {
                _editorOldOffset = 0,
                _editorGridSpacing = Editor.DefaultGridSpacing,
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
        WriteMap(numDifficulties - 1);
    }
    public void ReadMap(int indx) {
        var filename = (string)GetValueForDifficultyMap(indx, "_beatmapFilename");
        difficultyMaps[indx] = File.ReadAllText(PathOf(filename));
    }
    public void WriteMap(int indx) {
        var filename = (string)GetValueForDifficultyMap(indx, "_beatmapFilename");
        File.WriteAllText(PathOf(filename), difficultyMaps[indx]);
    }
    public void DeleteMap(int indx) {
        if (numDifficulties == 1) {
            return;
        }
        var filename = (string)GetValueForDifficultyMap(indx, "_beatmapFilename");
        File.Delete(PathOf(filename));
        for (int i = indx; i < numDifficulties - 1; i++) {
            difficultyMaps[i] = difficultyMaps[i + 1];
        }
        difficultyMaps[numDifficulties - 1] = null;

        var obj = JObject.Parse(infoStr);
        var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        beatmaps.RemoveAt(indx);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        RenameMaps();
        WriteInfo();
    }
    public void SwapMaps(int i, int j) {
        if (i == j) {
            return;
        }
        var temp = difficultyMaps[i];
        difficultyMaps[i] = difficultyMaps[j];
        difficultyMaps[j] = temp;

        var obj = JObject.Parse(infoStr);
        var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        
        var tempMap = beatmaps[i];
        beatmaps[i] = beatmaps[j];
        beatmaps[j] = tempMap;

        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        RenameMaps();
        WriteInfo();
    }
    public List<Note> GetNotesForMap(int indx) {
        var obj = JObject.Parse(difficultyMaps[indx]);
        var res = obj["_notes"];
        List<Note> output = new List<Note>();
        foreach (JToken n in res) {
            double time = Helper.DoubleParseInvariant((string)n["_time"]);
            int colIndex = int.Parse((string)n["_lineIndex"]);
            output.Add(new Note(time, colIndex));
        }
        output.Sort();
        return output;
    }
    public void SetNotesForMap(int indx, List<Note> notes) {
        var numNotes = notes.Count;
        var notesObj = new Object[numNotes];
        for (int i = 0; i < numNotes; i++) {
            var thisNote = notes[i];
            var thisNoteObj = new {
                _time = thisNote.beat,
                _lineIndex = thisNote.col,
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
    private void RenameMaps() {
        for (int i = 0; i < numDifficulties; i++) {
            var fileName = BeatmapDefaults.DifficultyNames[i];
            var oldFile = (string)GetValueForDifficultyMap(i, "_beatmapFilename");
            File.Move(PathOf(oldFile), PathOf($"{fileName}_temp.dat"));
            SetValueForDifficultyMap(i, "_difficulty", fileName);
            SetValueForDifficultyMap(i, "_beatmapFilename", $"{fileName}.dat");
        }
        for (int i = 0; i < numDifficulties; i++) {
            var fileName = BeatmapDefaults.DifficultyNames[i];
            File.Move(PathOf($"{fileName}_temp.dat"), PathOf($"{fileName}.dat"));
        }
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
            if (!i.Value.Contains(obj[i.Key]?.Type)) {
                throw new Exception($"Incorrect or missing key {i.Key}");
            }
        }
        var notes = (JArray)obj["_notes"];
        foreach (var note in notes) {
            foreach (var i in expectedTypesL2) {
                // validate type
                if (!i.Value.Contains(note[i.Key]?.Type)) {
                    throw new Exception($"Note at time {note["_time"]} has incorrect or missing key {i.Key}");
                }
                // validate value
                if (i.Value == numericTypes) {
                    var val = Helper.DoubleParseInvariant((string)note[i.Key]);
                    Exception ex = new Exception($"Note at time {note["_time"]} has bad value for key {i.Key}");
                    switch (i.Key) {
                        case "_time":
                            if (val < 0) throw ex;
                            break;
                        case "_lineIndex":
                            if ((int)val != val || val < 0 || 3 < val) 
                                throw ex;
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
    public string PathOf(string f) {
        return Path.Combine(folderPath, f);
    }
    public int GetMedalDistanceForMap(int indx, int medal) {
        JArray info = (JArray)GetCustomValueForDifficultyMap(indx, "_information");
        string splitter = $"medal_{medal}=";
        foreach (JToken t in info) {
            string s = (string)t;
            if (s.StartsWith(splitter)) {
                return int.Parse(s.Split(splitter)[1]);
            }
        }
        return 0;
    }
    public void SetMedalDistanceForMap(int indx, int medal, int dist) {
        JArray info = (JArray)GetCustomValueForDifficultyMap(indx, "_information");
        string splitter = $"medal_{medal}=";
        JToken insert = JToken.FromObject($"{splitter}{dist}");
        bool found = false;
        for (int i = 0; i < info.Count; i++) {
            if (((string)info[i]).StartsWith(splitter)) {
                found = true;
                if (dist == 0) {
                    info.RemoveAt(i);
                } else {
                    info[i] = insert;
                }
                
                break;
            }
        }
        if (!found && dist != 0) {
            info.Add(insert);
        }
        SetCustomValueForDifficultyMap(indx, "_information", info);
    }
    public List<BPMChange> GetBPMChangesForMap(int indx) {
        List<BPMChange> BPMChanges = new List<BPMChange>();
        var obj = JObject.Parse(difficultyMaps[indx]);
        var res = obj["_customData"]["_BPMChanges"];
        foreach (JToken bcObj in res) {
            double beat = Helper.DoubleParseInvariant((string)bcObj["_time"]);
            double bpm = Helper.DoubleParseInvariant((string)bcObj["_BPM"]);
            // what happens if an incompatible grid division (>24) is passed in?
            int gridDivision = int.Parse((string)bcObj["_beatsPerBar"]);
            BPMChange bc = new BPMChange(beat, bpm, gridDivision);
            BPMChanges.Add(bc);
        }
        return BPMChanges; 
    }
    public void SetBPMChangesForMap(int indx, List<BPMChange> BPMChanges) {
        JArray bcArr = new JArray();
        foreach (BPMChange bc in BPMChanges) {
            bcArr.Add(JToken.FromObject(new {
                _BPM = bc.BPM,
                _time = bc.globalBeat,
                _beatsPerBar = bc.gridDivision,
                _metronomeOffset = bc.gridDivision // what does this field do???
            }));
        }

        var thisMapStr = JObject.Parse(difficultyMaps[indx]);
        thisMapStr["_customData"]["_BPMChanges"] = bcArr;
        difficultyMaps[indx] = JsonConvert.SerializeObject(thisMapStr, Formatting.Indented);
    }
    public List<Bookmark> GetBookmarksForMap(int indx) {
        List<Bookmark> bookmarks = new List<Bookmark>();
        var obj = JObject.Parse(difficultyMaps[indx]);
        var res = obj["_customData"]["_bookmarks"];
        foreach (JToken bcObj in res) {
            double beat = Helper.DoubleParseInvariant((string)bcObj["_time"]);
            string name = (string)bcObj["_name"];
            Bookmark b = new Bookmark(beat, name);
            bookmarks.Add(b);
        }
        return bookmarks;
    }
    public void SetBookmarksForMap(int indx, List<Bookmark> bookmarks) {
        JArray bArr = new JArray();
        foreach (Bookmark b in bookmarks) {
            bArr.Add(JToken.FromObject(new {
                _time = b.beat,
                _name = b.name
            })); ;
        }

        var thisMapStr = JObject.Parse(difficultyMaps[indx]);
        thisMapStr["_customData"]["_bookmarks"] = bArr;
        difficultyMaps[indx] = JsonConvert.SerializeObject(thisMapStr, Formatting.Indented);
    }
}

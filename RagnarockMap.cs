using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Note = System.ValueTuple<double, int>;

public class RagnarockMap {
    private string folderPath;
	private string infoStr;
    private string[] difficultyMaps = new string[3];
    private readonly string[] difficultyNames = { "Easy", "Normal", "Hard" };
    private readonly int defaultNoteJumpMovementSpeed = 15;
    private readonly double defaultBPM = 120;
    private readonly string eddaVersionNumber = "0.0.3";
    private string absPath(string f) {
        return Path.Combine(folderPath, f);
    }

    int numDifficulties {
        get {
            var obj = JObject.Parse(infoStr);
            var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
            return res.Count();
        }
    }

    public RagnarockMap(string folderPath, bool makeNew) {
        this.folderPath = folderPath;
        if (makeNew) {
            initInfo();
            addDifficultyMap(difficultyNames[0]);
            writeDifficultyMap(0);
            writeInfo();
        } else {
            readInfo();
            for (int i = 0; i < numDifficulties; i++) {
                readDifficultyMap(i);
            }
        }
    }

    private void initInfo() {
        // init info.dat json
        var infoDat = new {
            _version = "1",
            _songName = "",
            _songSubName = "",                              // dummy
            _songAuthorName = "",
            _levelAuthorName = "",
            _beatsPerMinute = defaultBPM,
            _shuffle = 0,                                   // dummy?
            _shufflePeriod = 0.5,                           // dummy?
            _previewStartTime = 0,                          // dummy?
            _previewDuration = 0,                           // dummy?
            _songApproximativeDuration = 0,
            _songFilename = "song.ogg",
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
    public void setValue(string key, object value) {
        var obj = JObject.Parse(infoStr);
        obj[key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken getValue(string key) {
        var obj = JObject.Parse(infoStr);
        var res = obj[key];
        return res;
    }
    public void setValueForDifficultyMap(string key, object value, int indx) {
        var obj = JObject.Parse(infoStr);
        obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken getValueForDifficultyMap(string key, int indx) {
        var obj = JObject.Parse(infoStr);
        var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx][key];
        return res;
    }
    public void setCustomValueForDifficultyMap(string key, object value, int indx) {
        var obj = JObject.Parse(infoStr);
        obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx]["_customData"][key] = JToken.FromObject(value);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
    public JToken getCustomValueForDifficultyMap(string key, int indx) {
        var obj = JObject.Parse(infoStr);
        var res = obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"][indx]["_customData"][key];
        return res;
    }
    public void readInfo() {
        infoStr = File.ReadAllText(absPath("info.dat"));
    }
    public void writeInfo() {
        File.WriteAllText(absPath("info.dat"), infoStr);
    }
    public void addDifficultyMap(string difficulty) {
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
        mapsStr[numDifficulties - 1] = JsonConvert.SerializeObject(mapDat, Formatting.Indented);
        updateDifficultyButtonVisibility();
    }
    public void readDifficultyMap(int indx) {
        var filename = (string)getValueForDifficultyMap("_beatmapFilename", indx);
        difficultyMaps[indx] = File.ReadAllText(absPath(filename));
    }
    public void writeDifficultyMap(int indx) {
        var filename = (string)getValueForDifficultyMap("_beatmapFilename", indx);
        File.WriteAllText(absPath(filename), difficultyMaps[indx]);
    }
    public void deleteDifficultyMap(int indx) {
        if (numDifficulties == 1) {
            return;
        }
        var filename = (string)getValueForDifficultyMap("_beatmapFilename", indx);
        File.Delete(absPath(filename));
        difficultyMaps[indx] = null;

        var obj = JObject.Parse(infoStr);
        var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        beatmaps.RemoveAt(indx);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        //selectedDifficulty = Math.Min(selectedDifficulty, numDifficulties - 1);
        renameDifficultyMap();
        writeInfo();
        writeDifficultyMap(indx);
    }
    public void renameDifficultyMap() {
        for (int i = 0; i < numDifficulties; i++) {
            setValueForDifficultyMap("_difficulty", difficultyNames[i], i);
            var oldFile = (string)getValueForDifficultyMap("_beatmapFilename", i);
            File.Move(absPath(oldFile), absPath($"{difficultyNames[i]}_temp.dat"));
            setValueForDifficultyMap("_beatmapFilename", $"{difficultyNames[i]}.dat", i);
        }
        for (int i = 0; i < numDifficulties; i++) {
            File.Move(absPath($"{difficultyNames[i]}_temp.dat"), absPath($"{difficultyNames[i]}.dat"));
        }
    }
    public Note[] getMapStrNotes(int indx) {
        var obj = JObject.Parse(difficultyMaps[indx]);
        var res = obj["_notes"];
        Note[] output = new Note[res.Count()];
        var i = 0;
        foreach (JToken n in res) {
            double time = double.Parse((string)n["_time"]);
            int colIndex = int.Parse((string)n["_lineIndex"]);
            output[i] = (time, colIndex);
            i++;
        }
        return output;
    }
    public void setMapStrNotes(int indx) {
        var numNotes = selectedDifficultyNotes.Length;
        var notes = new Object[numNotes];
        for (int i = 0; i < numNotes; i++) {
            var thisNote = selectedDifficultyNotes[i];
            var thisNoteObj = new {
                _time = thisNote.Item1,
                _lineIndex = thisNote.Item2,
                _lineLayer = 1,
                _type = 0,
                _cutDirection = 1
            };
            notes[i] = thisNoteObj;
        }
        var thisMapStr = JObject.Parse(mapsStr[selectedDifficulty]);
        thisMapStr["_notes"] = JToken.FromObject(notes);
        mapsStr[selectedDifficulty] = JsonConvert.SerializeObject(thisMapStr, Formatting.Indented);
        //mapsStr[selectedDifficulty]["_notes"] = jObj;
    }


}

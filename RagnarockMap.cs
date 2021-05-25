using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Note = System.ValueTuple<double, int>;

public class RagnarockMap {

    // constants
    private readonly string[] difficultyNames = { "Easy", "Normal", "Hard" };
    private readonly int      defaultNoteJumpMovementSpeed = 15;
    public readonly double    defaultBPM = 120;
    private readonly string   defaultSongName = "song.ogg";

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
    private string   infoStr;
    private string[] difficultyMaps = new string[3];
    private string   eddaVersionNumber;
    public RagnarockMap(string folderPath, bool makeNew, string eddaVersionNumber) {
        this.folderPath = folderPath;
        this.eddaVersionNumber = eddaVersionNumber;
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
    public void readInfo() {
        infoStr = File.ReadAllText(absPath("info.dat"));
    }
    public void writeInfo() {
        File.WriteAllText(absPath("info.dat"), infoStr);
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
    public void addDifficultyMap(string difficulty) {
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
        for (int i = indx; i < numDifficulties - 1; i++) {
            difficultyMaps[i] = difficultyMaps[i + 1];
        }
        difficultyMaps[numDifficulties - 1] = null;

        var obj = JObject.Parse(infoStr);
        var beatmaps = (JArray)obj["_difficultyBeatmapSets"][0]["_difficultyBeatmaps"];
        beatmaps.RemoveAt(indx);
        infoStr = JsonConvert.SerializeObject(obj, Formatting.Indented);
        renameDifficultyMaps();
        writeInfo();
        //writeDifficultyMap(indx);
    }
    private void renameDifficultyMaps() {
        for (int i = 0; i < numDifficulties; i++) {
            var oldFile = (string)getValueForDifficultyMap("_beatmapFilename", i);
            File.Move(absPath(oldFile), absPath($"{difficultyNames[i]}_temp.dat"));
            setValueForDifficultyMap("_difficulty", difficultyNames[i], i);
            setValueForDifficultyMap("_beatmapFilename", $"{difficultyNames[i]}.dat", i);
        }
        for (int i = 0; i < numDifficulties; i++) {
            File.Move(absPath($"{difficultyNames[i]}_temp.dat"), absPath($"{difficultyNames[i]}.dat"));
        }
    }
    public List<Note> getNotesForMap(int indx) {
        var obj = JObject.Parse(difficultyMaps[indx]);
        var res = obj["_notes"];
        List<Note> output = new List<Note>();
        foreach (JToken n in res) {
            double time = double.Parse((string)n["_time"]);
            int colIndex = int.Parse((string)n["_lineIndex"]);
            output.Add((time, colIndex));
        }
        return output;
    }
    public void setNotesForMap(List<Note> notes, int indx) {
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

    // helper functions
    private string absPath(string f) {
        return Path.Combine(folderPath, f);
    }
}

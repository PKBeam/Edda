using Edda;
using Edda.Classes.MapEditorNS;
using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using Newtonsoft.Json.Linq;
using RagnaRuneString;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

#nullable enable

public class MapDifficulty {
    public SortedSet<Note> notes;
    public SortedSet<Bookmark> bookmarks;
    public SortedSet<BPMChange> bpmChanges;
    public SortedSet<Note> selectedNotes;
    public EditHistory<Note> editorHistory;
    public bool needsSave = false;
    public MapDifficulty(IEnumerable<Note> notes, IEnumerable<BPMChange> bpmChanges, IEnumerable<Bookmark> bookmarks) {
        this.bpmChanges = new(bpmChanges);
        this.bookmarks = new(bookmarks);
        this.notes = new(notes);
        this.selectedNotes = new();
        this.editorHistory = new(Editor.HistoryMaxSize);
    }
    // Utility functions for syntax clarity with MapDifficulty? variables.
    public void MarkDirty() {
        this.needsSave = true;
    }
    public void MarkSaved() {
        this.needsSave = false;
    }

    public SortedSet<Note> GetNotesRange(double startBeat, double endBeat) {
        // We use non-existent columns to generate a smaller subset guaranteed to contain all of the notes between given beats.
        return notes.GetViewBetween(new Note(startBeat, -1), new Note(endBeat, -1));
    }
}
public enum RagnarockMapDifficulties {
    Current = -1,
    First = 0,
    Second = 1,
    Third = 2
}
public enum RagnarockScoreMedals {
    Bronze = 0,
    Silver = 1,
    Gold = 2
}

public enum MoveNote {
    MOVE_BEAT_UP,
    MOVE_BEAT_DOWN,
    MOVE_GRID_UP,
    MOVE_GRID_DOWN
}

public class MapEditor : IDisposable {
    public string mapFolder;
    RagnarockMap beatMap;
    MainWindow parent;
    double globalBPM;
    public int defaultGridDivision;
    double songDuration; // duration of song in seconds
    public int currentDifficultyIndex = -1;
    MapStats currentDifficultyStats;
    MapDifficulty?[] difficultyMaps = new MapDifficulty[3];

    public bool needsSave = false;

    public double GlobalBPM {
        get {
            return globalBPM;
        }
        set {
            globalBPM = value;
            currentDifficultyStats.globalBPM = value;
            RecalculateMapStats();
        }
    }

    public double SongDuration { // duration of song in seconds
        get {
            return songDuration;
        }
        set {
            songDuration = value;
            currentDifficultyStats.songDuration = value;
            RecalculateMapStats();
        }
    }

    public string NotePasteBehavior {
        get {
            return parent.GetUserSetting(UserSettingsKey.NotePasteBehavior);
        }
    }

    public int numDifficulties {
        get {
            return beatMap.numDifficulties;
        }
    }
    public MapDifficulty? currentMapDifficulty {
        get {
            if (currentDifficultyIndex < 0) {
                return null;
            } else {
                return difficultyMaps[currentDifficultyIndex];
            }
        }
    }
    public bool saveIsNeeded {
        get {
            bool save = needsSave;
            for (int i = 0; i < numDifficulties; ++i) {
                save = save || (difficultyMaps[i]?.needsSave ?? false);
            }
            return save;
        }
    }

    public MapEditor(MainWindow parent, string folderPath, bool makeNewMap) {
        this.parent = parent;
        this.mapFolder = folderPath;
        beatMap = new RagnarockMap(folderPath, makeNewMap);
        for (int indx = 0; indx < beatMap.numDifficulties; indx++) {
            difficultyMaps[indx] = new MapDifficulty(
                beatMap.GetNotesForMap(indx),
                beatMap.GetBPMChangesForMap(indx),
                beatMap.GetBookmarksForMap(indx)
            );
        }
        this.currentDifficultyStats = new MapStats(globalBPM, songDuration);
    }

    public void Dispose() {
        beatMap = null;
        parent = null;
        difficultyMaps = null;
        GC.SuppressFinalize(this);
    }

    public MapDifficulty? GetDifficulty(int indx) {
        return difficultyMaps[indx];
    }
    public void SaveMap() {
        SaveMap(currentDifficultyIndex);
        beatMap.SaveToFile();
        needsSave = false;
        for (int i = 0; i < numDifficulties; i++) {
            difficultyMaps[i]?.MarkSaved();
        }
    }
    public void SaveMap(int indx) {
        var thisDifficultyMap = difficultyMaps[indx];
        if (thisDifficultyMap != null) {
            beatMap.SetBPMChangesForMap(indx, thisDifficultyMap.bpmChanges.ToList());
            beatMap.SetBookmarksForMap(indx, thisDifficultyMap.bookmarks.ToList());
            beatMap.SetNotesForMap(indx, thisDifficultyMap.notes.ToList());
        }
    }
    public void ClearSelectedDifficulty() {
        currentMapDifficulty?.MarkDirty();
        currentMapDifficulty?.notes.Clear();
        currentMapDifficulty?.bookmarks.Clear();
        currentMapDifficulty?.bpmChanges.Clear();
        RecalculateMapStats();
    }
    public bool DeleteDifficulty() {
        return DeleteDifficulty(currentDifficultyIndex);
    }
    public bool DeleteDifficulty(int indx) {
        for (int i = indx; i < difficultyMaps.Length - 1; i++) {
            difficultyMaps[i] = difficultyMaps[i + 1];
        }
        difficultyMaps[difficultyMaps.Length - 1] = null;

        beatMap.DeleteMap(indx);
        needsSave = false; // beatMap.DeleteMap force-saves info.dat

        var selectedDifficultyIsStillValid = currentDifficultyIndex <= beatMap.numDifficulties - 1;
        if (selectedDifficultyIsStillValid) {
            RecalculateMapStats();
        }
        return selectedDifficultyIsStillValid;
    }
    public void CreateDifficulty(bool copyCurrentMarkers) {
        needsSave = true; // beatMap.AddMap doesn't save info.dat (even though SwapMaps can do that later when sorting difficulties)
        beatMap.AddMap();
        int newMap = beatMap.numDifficulties - 1;
        if (copyCurrentMarkers) {
            beatMap.SetBookmarksForMap(newMap, beatMap.GetBookmarksForMap(currentDifficultyIndex));
            beatMap.SetBPMChangesForMap(newMap, beatMap.GetBPMChangesForMap(currentDifficultyIndex));
        }
        difficultyMaps[newMap] = new MapDifficulty(
            beatMap.GetNotesForMap(newMap),
            beatMap.GetBPMChangesForMap(newMap),
            beatMap.GetBookmarksForMap(newMap)
        );
        if (copyCurrentMarkers) {
            difficultyMaps[newMap]?.MarkDirty();
        }
    }
    public void SelectDifficulty(int indx) {
        // before switching - save the notes for the current difficulty, if it still exists
        if (currentDifficultyIndex != -1 && currentDifficultyIndex < beatMap.numDifficulties) {
            beatMap.SetNotesForMap(currentDifficultyIndex, currentMapDifficulty?.notes?.ToList());
            beatMap.SetBookmarksForMap(currentDifficultyIndex, currentMapDifficulty?.bookmarks?.ToList());
            beatMap.SetBPMChangesForMap(currentDifficultyIndex, currentMapDifficulty?.bpmChanges?.ToList());
        }
        currentDifficultyIndex = indx;
        RecalculateMapStats();
    }
    public void SortDifficulties() {
        //needsSave = true; - not needed, SwapDifficulties updates info.dat if anything changes

        // bubble sort
        bool swap;
        do {
            swap = false;
            for (int i = 0; i < beatMap.numDifficulties - 1; i++) {
                int lowDiff = (int)beatMap.GetValueForDifficultyMap(i, "_difficultyRank");
                int highDiff = (int)beatMap.GetValueForDifficultyMap(i + 1, "_difficultyRank");
                if (lowDiff > highDiff) {
                    SwapDifficulties(i, i + 1);
                    if (currentDifficultyIndex == i) {
                        currentDifficultyIndex++;
                    } else if (currentDifficultyIndex == i + 1) {
                        currentDifficultyIndex--;
                    }
                    swap = true;
                }
            }
        } while (swap);
        SelectDifficulty(currentDifficultyIndex);
    }
    private void SwapDifficulties(int i, int j) {
        var temp = difficultyMaps[i];
        difficultyMaps[i] = difficultyMaps[j];
        difficultyMaps[j] = temp;

        beatMap.SwapMaps(i, j);
        needsSave = false; // beatMap.SwapMaps force-saves the info.dat
        parent.UpdateDifficultyButtons();
    }
    public void AddBPMChange(BPMChange b, bool redraw = true) {
        currentMapDifficulty?.MarkDirty();
        currentMapDifficulty?.bpmChanges.Add(b);
        if (redraw) {
            parent.DrawEditorGrid(false);
        }
        parent.RefreshBPMChanges();
    }
    public void RemoveBPMChange(BPMChange b, bool redraw = true) {
        currentMapDifficulty?.MarkDirty();
        currentMapDifficulty?.bpmChanges.Remove(b);
        if (redraw) {
            parent.DrawEditorGrid(false);
        }
        parent.RefreshBPMChanges();
    }
    public void AddBookmark(Bookmark b) {
        currentMapDifficulty?.MarkDirty();
        currentMapDifficulty?.bookmarks.Add(b);
        parent.DrawEditorGrid(false);
    }
    public void RemoveBookmark(Bookmark b) {
        currentMapDifficulty?.MarkDirty();
        currentMapDifficulty?.bookmarks.Remove(b);
        parent.DrawEditorGrid(false);
    }
    public void RenameBookmark(Bookmark b, string newName) {
        currentMapDifficulty?.MarkDirty();
        b.name = newName;
        parent.DrawEditorGrid(false);
    }
    public void AddNotes(IEnumerable<Note> notes, bool updateHistory = true) {
        currentMapDifficulty?.MarkDirty();
        var drawNotes = notes.Where(n => currentMapDifficulty?.notes?.Add(n) == true).ToList();
        // draw the added notes
        // note: by drawing this note out of order, it is inconsistently layered with other notes.
        //       should we take the performance hit of redrawing the entire grid for visual consistency?
        parent.gridController.DrawNotes(drawNotes);
        parent.gridController.DrawNavNotes(drawNotes);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(true, drawNotes));
        }
        currentMapDifficulty?.editorHistory.Print();
        parent.RefreshDiscordPresence();
        RecalculateMapStats();
    }
    public void AddNotes(Note n, bool updateHistory = true) {
        AddNotes(new List<Note>() { n }, updateHistory);
    }
    public void UpdateNotes(IEnumerable<Note> newNotes, IEnumerable<Note> oldNotes, bool updateHistory = true) {
        currentMapDifficulty?.MarkDirty();

        // remove all old Notes
        var undrawNotes = oldNotes.Where(n => currentMapDifficulty?.notes?.Remove(n) == true).ToList();

        // undraw the added notes
        parent.gridController.UndrawNotes(undrawNotes);
        parent.gridController.UndrawNavNotes(undrawNotes);

        // add new notes
        var drawNotes = newNotes.Where(n => currentMapDifficulty?.notes?.Add(n) == true).ToList();

        // draw new notes
        parent.gridController.DrawNotes(drawNotes);
        parent.gridController.DrawNavNotes(drawNotes);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(false, undrawNotes));
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(true, drawNotes));
            currentMapDifficulty?.editorHistory.Consolidate(2);
        }
        currentMapDifficulty?.editorHistory.Print();
        SelectNewNotes(drawNotes);
        parent.RefreshDiscordPresence();
        RecalculateMapStats();
    }

    public void UpdateNotes(Note o, Note n, bool updateHistory = true) {
        UpdateNotes(new List<Note>() { o }, new List<Note>() { n }, updateHistory);
    }
    public void RemoveNotes(IEnumerable<Note> notes, bool updateHistory = true) {
        currentMapDifficulty?.MarkDirty();

        var noteList = notes.ToList();

        // undraw the added notes
        parent.gridController.UndrawNotes(noteList);
        parent.gridController.UndrawNavNotes(noteList);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(false, noteList));
        }
        // finally, unselect all removed notes
        foreach (Note n in noteList) {
            currentMapDifficulty?.notes.Remove(n);
            UnselectNote(n, false);
        }
        currentMapDifficulty?.editorHistory.Print();
        parent.RefreshDiscordPresence();
        RecalculateMapStats();
    }
    public void RemoveNotes(Note n, bool updateHistory = true) {
        RemoveNotes([n], updateHistory);
    }
    internal void RemoveNote(Note n) {
        if (currentMapDifficulty?.notes?.Contains(n) == true) {
            RemoveNotes(n);
        } else {
            UnselectAllNotes();
        }
    }
    public void RemoveSelectedNotes(bool updateHistory = true) {
        RemoveNotes(currentMapDifficulty?.selectedNotes ?? new SortedSet<Note>(), updateHistory);
    }
    public void ToggleSelection(IEnumerable<Note> notes) {
        foreach (Note n in notes) {
            ToggleSelection(n, false);
        }
        RecalculateMapStats();
    }
    public void ToggleSelection(Note n, bool updateMapStats = true) {
        if (currentMapDifficulty?.selectedNotes.Contains(n) ?? false) {
            UnselectNote(n, updateMapStats);
        } else {
            SelectNotes(n, updateMapStats);
        }
    }
    public void SelectNotes(Note n, bool updateMapStats = true) {
        SelectNotes([n], updateMapStats);
    }
    public void SelectNotes(IEnumerable<Note> notes, bool updateMapStats = true) {
        var selectNotes = notes.Where(n => currentMapDifficulty?.selectedNotes?.Add(n) == true).ToList();
        parent.gridController.HighlightNotes(selectNotes);
        parent.gridController.HighlightNavNotes(selectNotes);
        if (updateMapStats) {
            RecalculateMapStats();
        }
    }
    public void SelectAllNotes() {
        if (currentMapDifficulty == null) {
            return;
        }
        foreach (var note in currentMapDifficulty.notes) {
            currentMapDifficulty.selectedNotes.Add(note);
        }
        parent.gridController.HighlightAllNotes();
        parent.gridController.HighlightAllNavNotes();
        RecalculateMapStats();
    }
    public void SelectNewNotes(IEnumerable<Note> notes, bool updateMapStats = true) {
        UnselectAllNotes(false);
        SelectNotes(notes, updateMapStats);
    }
    public void SelectNewNotes(Note n, bool updateMapStats = true) {
        SelectNewNotes(new List<Note>() { n }, updateMapStats);
    }
    public void UnselectNote(Note n, bool updateMapStats = true) {
        if (currentMapDifficulty?.selectedNotes == null) {
            return;
        }
        parent.gridController.UnhighlightNotes(n);
        parent.gridController.UnhighlightNavNotes(n);
        currentMapDifficulty.selectedNotes.Remove(n);
        if (updateMapStats) {
            RecalculateMapStats();
        }
    }
    public void UnselectAllNotes(bool updateMapStats = true) {
        if (currentMapDifficulty?.selectedNotes == null) {
            return;
        }
        parent.gridController.UnhighlightAllNotes();
        parent.gridController.UnhighlightAllNavNotes();
        currentMapDifficulty.selectedNotes.Clear();
        if (updateMapStats) {
            RecalculateMapStats();
        }
    }
    public void CopySelection() {
        if (currentMapDifficulty == null) {
            return;
        }
        try {
            var noteSelection = new NoteSelection(this);
            var clipboardData = new DataObject("Edda_Note_Selection", noteSelection);
            RagnaRuneString.Version1.RuneStringData runeStringData = noteSelection;
            clipboardData.SetText(RuneStringSerializer.Serialize(runeStringData, RagnaRuneString.Version.VERSION_1));
            Clipboard.SetDataObject(clipboardData, true);
        } catch (Exception ex) {
            Trace.WriteLine($"WARNING: Failed to copy notes due to an exception: {ex}");
        }
    }
    public void CutSelection() {
        if (currentMapDifficulty == null) {
            return;
        }
        CopySelection();
        RemoveNotes(currentMapDifficulty.selectedNotes);
    }
    public void PasteClipboard(double beatOffset, int? colStart) {
        if (currentMapDifficulty == null) {
            return;
        }

        NoteSelection? noteSelection = null;
        var clipboardData = Clipboard.GetDataObject();
        if (clipboardData == null) return;
        if (clipboardData.GetDataPresent("Edda_Note_Selection")) {
            object? data = clipboardData.GetData("Edda_Note_Selection");
            if (data == null || data is not NoteSelection) return;
            noteSelection = (NoteSelection)data;
        } else if (clipboardData.GetDataPresent(DataFormats.Text)) {
            string clipboardText = (clipboardData.GetData(DataFormats.Text) as string)!;
            try {
                noteSelection = new NoteSelection(RuneStringSerializer.DeserializeV1(clipboardText.Trim()));
            } catch (Exception) {
                Trace.WriteLine($"DEBUG: tried to paste invalid rune string: \"{clipboardText}\"");
                return;
            }
        }

        if (noteSelection == null || noteSelection?.notes.Count == 0) return;

        AddNotes(noteSelection!.GetPasteNotes(this, beatOffset, colStart));
    }
    public void QuantizeSelection() {
        if (currentMapDifficulty == null) {
            return;
        }

        // Quantize each note and add it to the new list
        var quantizedNotes = currentMapDifficulty.selectedNotes
            .Select(n => {
                BPMChange lastBeatChange = GetLastBeatChange(n.beat);
                double defaultGridLength = GetGridLength(lastBeatChange.BPM, lastBeatChange.gridDivision);
                double offset = 0.0;
                if (lastBeatChange.globalBeat > 0.0) {
                    double differenceDefaultNew = Math.Floor(lastBeatChange.globalBeat / defaultGridLength) * defaultGridLength;
                    offset = lastBeatChange.globalBeat - differenceDefaultNew;
                }

                double newBeat = Math.Round(n.beat / defaultGridLength) * defaultGridLength + offset;
                if (Helper.DoubleApproxEqual(n.beat, newBeat) || Helper.DoubleApproxEqual(n.beat, newBeat - defaultGridLength)) {
                    newBeat = n.beat;
                }

                return new Note(newBeat, n.col);
            });
        UpdateNotes(quantizedNotes, currentMapDifficulty.selectedNotes);
    }
    public double GetGridLength(double bpm, int gridDivision) {
        double scaleFactor = globalBPM / bpm;
        return scaleFactor / gridDivision;
    }
    public BPMChange GetLastBeatChange(double beat) {
        return currentMapDifficulty?.bpmChanges
            .Where(obj => Helper.DoubleApproxGreaterEqual(beat, obj.globalBeat))
            .LastOrDefault() ?? new BPMChange(0.0, globalBPM, defaultGridDivision);
    }
    private void ApplyEdit(EditList<Note> e) {
        var notesToAdd = e.items.Where(edit => edit.isAdd).Select(edit => edit.item).ToList();
        if (notesToAdd.Count > 0) AddNotes(notesToAdd, false);
        var notesToRemove = e.items.Where(edit => !edit.isAdd).Select(edit => edit.item).ToList();
        if (notesToRemove.Count > 0) RemoveNotes(notesToRemove, false);
        UnselectAllNotes();
    }
    public void MirrorSelection() {
        if (currentMapDifficulty == null) {
            return;
        }

        var mirroredSelection = currentMapDifficulty.selectedNotes
            .Select(note => new Note(note.beat, 3 - note.col));

        UpdateNotes(mirroredSelection, currentMapDifficulty.selectedNotes);
    }
    public void ShiftSelectionByBeat(MoveNote direction) {
        if (currentMapDifficulty == null) {
            return;
        }
        var movedNotes = currentMapDifficulty.selectedNotes
            .Select(n => {
                BPMChange lastBeatChange = GetLastBeatChange(n.beat);
                double beatOffset = 0.0;
                switch (direction) {
                    case MoveNote.MOVE_BEAT_DOWN:
                    case MoveNote.MOVE_BEAT_UP:
                        double defaultBeatLength = GetGridLength(lastBeatChange.BPM, 1);
                        beatOffset = (direction == MoveNote.MOVE_BEAT_DOWN) ? -defaultBeatLength : defaultBeatLength;
                        break;
                    case MoveNote.MOVE_GRID_DOWN:
                    case MoveNote.MOVE_GRID_UP:
                        double defaultGridLength = GetGridLength(lastBeatChange.BPM, lastBeatChange.gridDivision);
                        beatOffset = (direction == MoveNote.MOVE_GRID_DOWN) ? -defaultGridLength : defaultGridLength;
                        break;
                }
                double newBeat = n.beat + beatOffset;
                return new Note(newBeat, n.col);
            });
        UpdateNotes(movedNotes, currentMapDifficulty.selectedNotes);
    }
    public void ShiftSelectionByCol(int offset) {
        if (currentMapDifficulty == null) {
            return;
        }

        var movedSelection = currentMapDifficulty.selectedNotes
            .Select(n => {
                int newCol = (n.col + offset) % 4;
                if (newCol < 0) {
                    newCol += 4;
                }
                return new Note(n.beat, newCol);
            });

        UpdateNotes(movedSelection, currentMapDifficulty.selectedNotes);
    }
    public void Undo() {
        if (currentMapDifficulty == null) {
            return;
        }
        EditList<Note> edit = currentMapDifficulty.editorHistory.Undo();
        ApplyEdit(edit);
    }
    public void Redo() {
        if (currentMapDifficulty == null) {
            return;
        }
        EditList<Note> edit = currentMapDifficulty.editorHistory.Redo();
        ApplyEdit(edit);
    }
    public int GetMedalDistance(RagnarockScoreMedals medal, RagnarockMapDifficulties? difficulty = null) {
        return beatMap.GetMedalDistanceForMap(MapDifficultyIndex(difficulty), (int)medal);
    }
    public void SetMedalDistance(RagnarockScoreMedals medal, int distance, RagnarockMapDifficulties? difficulty = null) {
        int index = MapDifficultyIndex(difficulty);
        if (index != -1) {
            difficultyMaps[index]?.MarkDirty();
        }
        beatMap.SetMedalDistanceForMap(index, (int)medal, distance);
    }
    public JToken GetMapValue(string key, RagnarockMapDifficulties? difficulty = null, bool custom = false) {
        JToken result;
        if (difficulty != null) {
            int indx = MapDifficultyIndex(difficulty);
            if (custom) {
                result = beatMap.GetCustomValueForDifficultyMap(indx, key);
            } else {
                result = beatMap.GetValueForDifficultyMap(indx, key);
            }
        } else {
            result = beatMap.GetValue(key);
        }
        return result;
    }
    public void SetMapValue(string key, JToken value, RagnarockMapDifficulties? difficulty = null, bool custom = false) {
        if (difficulty != null) {
            int indx = difficulty == RagnarockMapDifficulties.Current ? currentDifficultyIndex : (int)difficulty;
            difficultyMaps[indx]?.MarkDirty();
            if (custom) {
                beatMap.SetCustomValueForDifficultyMap(indx, key, value);
            } else {
                beatMap.SetValueForDifficultyMap(indx, key, value);
            }
        } else {
            needsSave = true;
            beatMap.SetValue(key, value);
        }
    }
    private int MapDifficultyIndex(RagnarockMapDifficulties? d) {
        if (d == null) {
            return -1;
        }
        return d == RagnarockMapDifficulties.Current ? currentDifficultyIndex : (int)d;
    }
    internal void RetimeNotesAndMarkers(double newBPM, double oldBPM) {
        if (currentMapDifficulty == null) {
            return;
        }
        currentMapDifficulty.MarkDirty();

        double scaleFactor = newBPM / oldBPM;
        foreach (var bc in currentMapDifficulty.bpmChanges) {
            bc.globalBeat *= scaleFactor;
        }
        foreach (var n in currentMapDifficulty.notes) {
            n.beat *= scaleFactor;
        }
        foreach (var b in currentMapDifficulty.bookmarks) {
            b.beat *= scaleFactor;
        }
    }
    internal void SelectNotesInBookmark(Bookmark b) {
        if (currentMapDifficulty == null) {
            return;
        }
        double endBeat = currentMapDifficulty.bookmarks
            .GetViewBetween(b, new Bookmark(double.PositiveInfinity, "songEnd"))
            .Skip(1)
            .Select(x => x.beat)
            .FirstOrDefault(double.PositiveInfinity);
        var notes = currentMapDifficulty.GetNotesRange(b.beat, endBeat);


        if (parent.shiftKeyDown) {
            ToggleSelection(notes);
        } else {
            SelectNewNotes(notes);
        }
    }

    internal void SelectNotesInBPMChange(BPMChange bpmChange) {
        if (currentMapDifficulty == null) {
            return;
        }
        double endBeat = currentMapDifficulty.bpmChanges
            .GetViewBetween(bpmChange, new BPMChange(double.PositiveInfinity, 0, 0))
            .Skip(1)
            .Select(x => x.globalBeat)
            .FirstOrDefault(double.PositiveInfinity);
        var notes = currentMapDifficulty.GetNotesRange(bpmChange.globalBeat, endBeat);

        if (parent.shiftKeyDown) {
            ToggleSelection(notes);
        } else {
            SelectNewNotes(notes);
        }
    }

    internal void RecalculateMapStats() {
        if (currentMapDifficulty == null) {
            currentDifficultyStats.Recalculate(new(), new());
        } else {
            currentDifficultyStats.Recalculate(currentMapDifficulty.notes, currentMapDifficulty.selectedNotes);
        }
        parent.SetMapStats(currentDifficultyStats);
    }
}
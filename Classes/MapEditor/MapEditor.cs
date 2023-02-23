using Edda;
using Edda.Const;
using Newtonsoft.Json.Linq;
using SoundTouch;
using System;
using System.Linq;
using System.Collections.Generic;

#nullable enable

public class MapDifficulty {
    public List<Note> notes;
    public List<Bookmark> bookmarks;
    public List<BPMChange> bpmChanges;
    public List<Note> selectedNotes;
    public EditHistory<Note> editorHistory;
    public bool needsSave = false;
    public MapDifficulty(List<Note> notes, List<BPMChange> bpmChanges, List<Bookmark> bookmarks) {
        this.bpmChanges = bpmChanges ?? new();
        this.bookmarks = bookmarks ?? new();
        this.notes = notes ?? new();
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

public class MapEditor {
    public string mapFolder;
    RagnarockMap beatMap;
    MainWindow parent;
    public double globalBPM;
    public double songDuration; // duration of song in seconds
    public int currentDifficultyIndex = -1;
    MapDifficulty?[] difficultyMaps = new MapDifficulty[3];
    List<Note> clipboard;

    public bool needsSave = false;

    public int numDifficulties {
        get {
            return beatMap.numDifficulties;
        }
    }
    public MapDifficulty? currentMapDifficulty {
        get {
            if (currentDifficultyIndex < 0) {
                return null;
            }  else {
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
        this.clipboard = new();
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
            beatMap.SetBPMChangesForMap(indx, thisDifficultyMap.bpmChanges);
            beatMap.SetBookmarksForMap(indx, thisDifficultyMap.bookmarks);
            beatMap.SetNotesForMap(indx, thisDifficultyMap.notes);
        }
    }
    public void ClearSelectedDifficulty() {
        currentMapDifficulty?.MarkDirty();
        currentMapDifficulty?.notes.Clear();
        currentMapDifficulty?.bookmarks.Clear();
        currentMapDifficulty?.bpmChanges.Clear();
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

        return currentDifficultyIndex <= beatMap.numDifficulties - 1;
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
            beatMap.SetNotesForMap(currentDifficultyIndex, currentMapDifficulty?.notes);
            beatMap.SetBookmarksForMap(currentDifficultyIndex, currentMapDifficulty?.bookmarks);
            beatMap.SetBPMChangesForMap(currentDifficultyIndex, currentMapDifficulty?.bpmChanges);
        }
        currentDifficultyIndex = indx;
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
        currentMapDifficulty?.bpmChanges.Sort();
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
    public void AddNotes(List<Note> notes, bool updateHistory = true) {
        currentMapDifficulty?.MarkDirty();
        List<Note> drawNotes = new();
        foreach (Note n in notes) {
            if (Helper.InsertSortedUnique(this.currentMapDifficulty?.notes, n)) {
                drawNotes.Add(n);
            }
        }
        // draw the added notes
        // note: by drawing this note out of order, it is inconsistently layered with other notes.
        //       should we take the performance hit of redrawing the entire grid for visual consistency?
        parent.gridController.DrawNotes(drawNotes);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(true, drawNotes));
        }
        currentMapDifficulty?.editorHistory.Print();
    }
    public void AddNotes(Note n, bool updateHistory = true) {
        AddNotes(new List<Note>() { n }, updateHistory);
    }
    public void RemoveNotes(List<Note> notes, bool updateHistory = true) {
        currentMapDifficulty?.MarkDirty();
        // undraw the added notes
        parent.gridController.UndrawNotes(notes);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(false, notes));
        }
        // finally, unselect all removed notes
        foreach (Note n in new List<Note>(notes)) {
            currentMapDifficulty?.notes.Remove(n);
            UnselectNote(n);
        }
        currentMapDifficulty?.editorHistory.Print();
    }
    public void RemoveNotes(Note n, bool updateHistory = true) {
        RemoveNotes(new List<Note>() { n }, updateHistory);
    }
    internal void RemoveNote(Note n) {
        if (currentMapDifficulty?.notes?.Contains(n) == true) {
            RemoveNotes(n);
        } else {
            UnselectAllNotes();
        }
    }
    public void RemoveSelectedNotes(bool updateHistory = true) {
        RemoveNotes(currentMapDifficulty?.selectedNotes ?? new List<Note>(), updateHistory);
    }
    public void ToggleSelection(Note n) {
        if (currentMapDifficulty?.selectedNotes.Contains(n) ?? false) {
            UnselectNote(n);
        } else {
            SelectNotes(n);
        }
    }
    public void SelectNotes(Note n) {
        SelectNotes(new List<Note>() { n });
    }
    public void SelectNotes(List<Note> notes) {
        foreach (Note n in notes) {
            Helper.InsertSortedUnique(currentMapDifficulty?.selectedNotes, n);        
        }
        parent.gridController.HighlightNotes(notes);
    }
    public void SelectAllNotes() {
        if (currentMapDifficulty == null) {
            return;
        }
        SelectNewNotes(currentMapDifficulty.notes);
    }
    public void SelectNewNotes(List<Note> notes) {
        UnselectAllNotes();
        foreach (Note n in notes) {
            SelectNotes(n);
        }
    }
    public void SelectNewNotes(Note n) {
        SelectNewNotes(new List<Note>() { n });
    }
    public void UnselectNote(Note n) {
        if (currentMapDifficulty?.selectedNotes == null) {
            return;
        }
        parent.gridController.UnhighlightNotes(n);
        currentMapDifficulty?.selectedNotes.Remove(n);
    }
    public void UnselectAllNotes() {
        if (currentMapDifficulty?.selectedNotes == null) {
            return;
        }
        parent.gridController.UnhighlightNotes(currentMapDifficulty?.selectedNotes);
        currentMapDifficulty?.selectedNotes.Clear();
    }
    public void CopySelection() {
        if (currentMapDifficulty == null) {
            return;
        }
        clipboard.Clear();
        clipboard.AddRange(currentMapDifficulty.selectedNotes);
        clipboard.Sort();
    }
    public void CutSelection() {
        if (currentMapDifficulty == null) {
            return;
        }
        CopySelection();
        RemoveNotes(currentMapDifficulty.selectedNotes);
    }
    public void PasteClipboard(double beatOffset, int? colStart) {
        if (currentMapDifficulty == null || clipboard.Count == 0) {
            return;
        }
        // paste notes so that the first note lands on the given beat offset
        double rowOffset = beatOffset - clipboard[0].beat;
        int colOffset = colStart == null ? 0 : (int)colStart - clipboard[0].col;
        List<Note> notes = new List<Note>();
        for (int i = 0; i < clipboard.Count; i++) {
            double newBeat = clipboard[i].beat + rowOffset;
            int newCol = clipboard[i].col + colOffset;

            // don't paste the note if it goes beyond the duration of the song
            if (newBeat > globalBPM * songDuration / 60) {
                continue;
            }

            // don't paste the note if it overflows on the columns
            if (newCol < 0 || 3 < newCol) {
                continue;
            }

            Note n = new Note(newBeat, newCol);
            notes.Add(n);
        }
        AddNotes(notes);
    }

public void QuantizeSelection() {
        if (currentMapDifficulty == null) {
            return;
        }

        // default value of 1/4 tact if no bpm and grid change is made
        int defaultGridDivision = 4;
        double defaultBeats = 60.0 / (globalBPM * (defaultGridDivision / 2));

        List<Note> notesToAdd = new List<Note>();
        List<Note> notesToRemove = new List<Note>();


        foreach (Note n in currentMapDifficulty.selectedNotes) {
            BPMChange? currentBeat = currentMapDifficulty
                .bpmChanges
                .OrderByDescending(obj => obj.globalBeat)
                .Where(obj => obj.globalBeat <= n.beat)
                .Select(obj => obj)
                .FirstOrDefault();
            
            double offset = 0.0;

            // beat has been changed
            if (currentBeat != null){

                // use current values for defaultBeats calculation
                defaultBeats = 60.0 / (currentBeat.BPM * (currentBeat.gridDivision / 2));

                // calculate time difference petween old beat and start time
                double differenceDefaultNew = Math.Floor(currentBeat.globalBeat / defaultBeats) * defaultBeats;

                // calculate offset
                offset = currentBeat.globalBeat - differenceDefaultNew;

            }
            double newBeat = Math.Round(n.beat / defaultBeats) * defaultBeats;
            newBeat += offset;

            if (newBeat != n.beat) {
                Note newNote = new Note(newBeat, n.col);
                notesToAdd.Add(newNote);
                notesToRemove.Add(n);
            }
        }

        AddNotes(notesToAdd);            
        RemoveNotes(notesToRemove);
    }

    private void ApplyEdit(EditList<Note> e) {
        foreach (var edit in e.items) {
            if (edit.isAdd) {
                AddNotes(edit.item, false);
            } else {
                RemoveNotes(edit.item, false);
            }
            UnselectAllNotes();
        }

    }
    public void TransformSelection(Func<Note, Note> transform) {
        if (currentMapDifficulty == null) {
            return;
        }
        // prepare new selection
        List<Note> transformedSelection = new List<Note>();
        for (int i = 0; i < currentMapDifficulty.selectedNotes.Count; i++) {
            Note transformed = transform.Invoke(currentMapDifficulty.selectedNotes[i]);
            if (transformed != null) {
                transformedSelection.Add(transformed);
            }
        }
        if (transformedSelection.Count == 0) {
            return;
        }
        RemoveNotes(currentMapDifficulty.selectedNotes);
        AddNotes(transformedSelection);
        currentMapDifficulty?.editorHistory.Consolidate(2);
        SelectNewNotes(transformedSelection);
    }
    public void MirrorSelection() {
        TransformSelection(NoteTransforms.Mirror());
    }
    public void ShiftSelectionByBeat(double beat) {
        TransformSelection(NoteTransforms.RowShift(beat));
    }
    public void ShiftSelectionByCol(int cols) {
        TransformSelection(NoteTransforms.ColShift(cols));
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
}

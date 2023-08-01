using Edda;
using Edda.Const;
using Newtonsoft.Json.Linq;
using SoundTouch;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

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
    public double globalBPM;
    public int defaultGridDivision;
    public double songDuration; // duration of song in seconds
    public int currentDifficultyIndex = -1;
    MapDifficulty?[] difficultyMaps = new MapDifficulty[3];
    SortedSet<Note> clipboard;

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
            }
            else {
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

    public void Dispose() {
        beatMap = null;
        parent = null;
        difficultyMaps = null;
        clipboard = null;
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
            beatMap.SetNotesForMap(currentDifficultyIndex, currentMapDifficulty?.notes?.ToList());
            beatMap.SetBookmarksForMap(currentDifficultyIndex, currentMapDifficulty?.bookmarks?.ToList());
            beatMap.SetBPMChangesForMap(currentDifficultyIndex, currentMapDifficulty?.bpmChanges?.ToList());
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
                    }
                    else if (currentDifficultyIndex == i + 1) {
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

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(true, drawNotes));
        }
        currentMapDifficulty?.editorHistory.Print();
        parent.RefreshDiscordPresence();
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

        // add new notes
        var drawNotes = newNotes.Where(n => currentMapDifficulty?.notes?.Add(n) == true).ToList();

        // draw new notes
        parent.gridController.DrawNotes(drawNotes);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(false, undrawNotes));
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(true, drawNotes));
            currentMapDifficulty?.editorHistory.Consolidate(2);
        }
        currentMapDifficulty?.editorHistory.Print();
        SelectNewNotes(drawNotes);
        parent.RefreshDiscordPresence();
    }

    public void UpdateNotes(Note o, Note n, bool updateHistory = true) {
        UpdateNotes(new List<Note>() { o }, new List<Note>() { n }, updateHistory);
    }
    public void RemoveNotes(IEnumerable<Note> notes, bool updateHistory = true) {
        currentMapDifficulty?.MarkDirty();

        var noteList = notes.ToList();

        // undraw the added notes
        parent.gridController.UndrawNotes(noteList);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(false, noteList));
        }
        // finally, unselect all removed notes
        foreach (Note n in noteList) {
            currentMapDifficulty?.notes.Remove(n);
            UnselectNote(n);
        }
        currentMapDifficulty?.editorHistory.Print();
        parent.RefreshDiscordPresence();
    }
    public void RemoveNotes(Note n, bool updateHistory = true) {
        RemoveNotes(new List<Note>() { n }, updateHistory);
    }
    internal void RemoveNote(Note n) {
        if (currentMapDifficulty?.notes?.Contains(n) == true) {
            RemoveNotes(n);
        }
        else {
            UnselectAllNotes();
        }
    }
    public void RemoveSelectedNotes(bool updateHistory = true) {
        RemoveNotes(currentMapDifficulty?.selectedNotes ?? new SortedSet<Note>(), updateHistory);
    }
    public void ToggleSelection(IEnumerable<Note> notes) {
        foreach (Note n in notes) {
            ToggleSelection(n);
        }
    }
    public void ToggleSelection(Note n) {
        if (currentMapDifficulty?.selectedNotes.Contains(n) ?? false) {
            UnselectNote(n);
        }
        else {
            SelectNotes(n);
        }
    }
    public void SelectNotes(Note n) {
        SelectNotes(new List<Note>() { n });
    }
    public void SelectNotes(IEnumerable<Note> notes) {
        var selectNotes = notes.Where(n => currentMapDifficulty?.selectedNotes?.Add(n) == true);
        parent.gridController.HighlightNotes(selectNotes);
    }
    public void SelectAllNotes() {
        if (currentMapDifficulty == null) {
            return;
        }
        SelectNewNotes(currentMapDifficulty.notes);
    }
    public void SelectNewNotes(IEnumerable<Note> notes) {
        UnselectAllNotes();
        SelectNotes(notes);
    }
    public void SelectNewNotes(Note n) {
        SelectNewNotes(new List<Note>() { n });
    }
    public void UnselectNote(Note n) {
        if (currentMapDifficulty?.selectedNotes == null) {
            return;
        }
        parent.gridController.UnhighlightNotes(n);
        currentMapDifficulty.selectedNotes.Remove(n);
    }
    public void UnselectAllNotes() {
        if (currentMapDifficulty?.selectedNotes == null) {
            return;
        }
        parent.gridController.UnhighlightNotes(currentMapDifficulty.selectedNotes);
        currentMapDifficulty.selectedNotes.Clear();
    }
    public void CopySelection() {
        if (currentMapDifficulty == null) {
            return;
        }
        clipboard.Clear();
        foreach (var n in currentMapDifficulty.selectedNotes) {
            clipboard.Add(n);
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
        if (currentMapDifficulty == null || clipboard.Count() == 0) {
            return;
        }
        // paste notes so that the first note lands on the given beat offset
        Note clipboardFirst = clipboard.First();
        double rowOffset = beatOffset - clipboardFirst.beat;
        int colOffset = colStart == null ? 0 : (int)colStart - clipboardFirst.col;
        AddNotes(clipboard
            .Select(n => new Note(n.beat + rowOffset, n.col + colOffset))
            // don't paste the note if it goes beyond the duration of the song or overflows on the columns
            .Where(n => n.beat <= globalBPM * songDuration / 60 && n.col >= 0 && n.col <= 3)
        );
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
            .OrderByDescending(obj => obj.globalBeat)
            .Where(obj => obj.globalBeat <= beat)
            .Select(obj => obj)
            .FirstOrDefault() ?? new BPMChange(0.0, globalBPM, defaultGridDivision);
    }
    private void ApplyEdit(EditList<Note> e) {
        foreach (var edit in e.items) {
            if (edit.isAdd) {
                AddNotes(edit.item, false);
            }
            else {
                RemoveNotes(edit.item, false);
            }
            UnselectAllNotes();
        }

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
            }
            else {
                result = beatMap.GetValueForDifficultyMap(indx, key);
            }
        }
        else {
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
            }
            else {
                beatMap.SetValueForDifficultyMap(indx, key, value);
            }
        }
        else {
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
        }
        else {
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
        }
        else {
            SelectNewNotes(notes);
        }
    }
}

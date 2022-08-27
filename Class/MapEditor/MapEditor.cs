using Edda;
using Edda.Class;
using System;
using System.Collections.Generic;

#nullable enable

public class MapDifficulty {
    public List<Note> notes;
    public List<Bookmark> bookmarks;
    public List<BPMChange> bpmChanges;
    public List<Note> clipboard;
    public List<Note> selectedNotes;
    public EditHistory<Note> editorHistory;
    public MapDifficulty(List<Note> notes, List<BPMChange> bpmChanges, List<Bookmark> bookmarks, List<Note> clipboard) {
        this.bpmChanges = bpmChanges;
        this.bookmarks = bookmarks;
        this.notes = notes;
        this.clipboard = clipboard;
        this.selectedNotes = new();
        this.editorHistory = new(Editor.HistoryMaxSize);
    }
}
public class MapEditor {
    RagnarockMap beatMap;
    MainWindow parent;
    public double globalBPM;
    public int currentDifficultyIndex = -1;
    MapDifficulty?[] difficultyMaps = new MapDifficulty[3];
    public MapDifficulty? currentMapDifficulty {
        get {
            if (currentDifficultyIndex < 0) {
                return null;
            }  else {
                return difficultyMaps[currentDifficultyIndex];
            }
        }
    }

    public MapEditor(MainWindow parent) {
        this.parent = parent;
        beatMap = parent.beatMap;
        for (int indx = 0; indx < beatMap.numDifficulties; indx++) {
            difficultyMaps[indx] = new MapDifficulty(
                beatMap.GetNotesForMap(indx), 
                beatMap.GetBPMChangesForMap(indx), 
                beatMap.GetBookmarksForMap(indx), 
                parent.editorClipboard
            );
        }
    }
    public void SaveMap() {
        SaveMap(currentDifficultyIndex);
    }
    public void SaveMap(int indx) {
        beatMap.SetBPMChangesForMap(indx, difficultyMaps[indx].bpmChanges);
        beatMap.SetBookmarksForMap(indx, difficultyMaps[indx].bookmarks);
        beatMap.SetNotesForMap(indx, difficultyMaps[indx].notes);
    }
    public void ClearSelectedDifficulty() {
        currentMapDifficulty?.notes.Clear();
        currentMapDifficulty?.bookmarks.Clear();
        currentMapDifficulty?.bpmChanges.Clear();
    }
    public void DeleteDifficulty() {
        DeleteDifficulty(currentDifficultyIndex);
    }
    public void DeleteDifficulty(int indx) {
        for (int i = indx; i < difficultyMaps.Length - 1; i++) {
            difficultyMaps[i] = difficultyMaps[i + 1];
        }
        difficultyMaps[difficultyMaps.Length - 1] = null;

        beatMap.DeleteMap(indx);
        SelectDifficulty(Math.Min(indx, beatMap.numDifficulties - 1));
    }
    public void CreateDifficulty(bool copyFromCurrent) {
        beatMap.AddMap();
        int newMap = beatMap.numDifficulties - 1;
        if (copyFromCurrent) {
            beatMap.SetBookmarksForMap(newMap, beatMap.GetBookmarksForMap(currentDifficultyIndex));
            beatMap.SetBPMChangesForMap(newMap, beatMap.GetBPMChangesForMap(currentDifficultyIndex));
        }
        difficultyMaps[newMap] = new MapDifficulty(
            beatMap.GetNotesForMap(currentDifficultyIndex),
            beatMap.GetBPMChangesForMap(currentDifficultyIndex),
            beatMap.GetBookmarksForMap(currentDifficultyIndex),
            parent.editorClipboard
        );
        SelectDifficulty(newMap);
        SortDifficulties();
    }
    public void SelectDifficulty(int indx) {
        if (currentDifficultyIndex != -1) {
            beatMap.SetNotesForMap(currentDifficultyIndex, currentMapDifficulty?.notes);
            beatMap.SetBookmarksForMap(currentDifficultyIndex, currentMapDifficulty?.bookmarks);
            beatMap.SetBPMChangesForMap(currentDifficultyIndex, currentMapDifficulty?.bpmChanges);
        }
        currentDifficultyIndex = indx;
    }
    public void SortDifficulties() {
        // bubble sort
        bool swap;
        do {
            swap = false;
            for (int i = 0; i < beatMap.numDifficulties - 1; i++) {
                int lowDiff = (int)beatMap.GetValueForMap(i, "_difficultyRank");
                int highDiff = (int)beatMap.GetValueForMap(i + 1, "_difficultyRank");
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
        parent.UpdateDifficultyButtons();
    }
    public void AddBPMChange(BPMChange b, bool redraw = true) {
        currentMapDifficulty?.bpmChanges.Add(b);
        currentMapDifficulty?.bpmChanges.Sort();
        if (redraw) {
            parent.DrawEditorGrid(false);
        }
        parent.RefreshBPMChanges();
    }
    public void RemoveBPMChange(BPMChange b, bool redraw = true) {
        currentMapDifficulty?.bpmChanges.Remove(b);
        if (redraw) {
            parent.DrawEditorGrid(false);
        }
        parent.RefreshBPMChanges();
    }
    public void AddBookmark(Bookmark b) {
        currentMapDifficulty?.bookmarks.Add(b);
        parent.editorUI.DrawNavBookmarks();
        parent.DrawEditorGrid(false);
    }
    public void RemoveBookmark(Bookmark b) {
        currentMapDifficulty?.bookmarks.Remove(b);
        parent.editorUI.DrawNavBookmarks();
        parent.DrawEditorGrid(false);
    }
    public void RenameBookmark(Bookmark b, string newName) {
        b.name = newName;
        parent.editorUI.DrawNavBookmarks();
        parent.DrawEditorGrid(false);
    }
    public void AddNotes(List<Note> notes, bool updateHistory = true) {
        List<Note> drawNotes = new();
        foreach (Note n in notes) {
            if (Helper.InsertSortedUnique(this.currentMapDifficulty?.notes, n)) {
                drawNotes.Add(n);
            }
        }
        // draw the added notes
        // note: by drawing this note out of order, it is inconsistently layered with other notes.
        //       should we take the performance hit of redrawing the entire grid for visual consistency?
        parent.editorUI.DrawNotes(drawNotes);

        if (updateHistory) {
            currentMapDifficulty?.editorHistory.Add(new EditList<Note>(true, drawNotes));
        }
        currentMapDifficulty?.editorHistory.Print();
    }
    public void AddNotes(Note n, bool updateHistory = true) {
        AddNotes(new List<Note>() { n }, updateHistory);
    }
    public void RemoveNotes(List<Note> notes, bool updateHistory = true) {

        // undraw the added notes
        parent.editorUI.UndrawNotes(notes);

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
        parent.editorUI.HighlightNotes(notes);
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
        parent.editorUI.UnhighlightNotes(n);
        currentMapDifficulty?.selectedNotes.Remove(n);
    }
    public void UnselectAllNotes() {
        if (currentMapDifficulty?.selectedNotes == null) {
            return;
        }
        parent.editorUI.UnhighlightNotes(currentMapDifficulty?.selectedNotes);
        currentMapDifficulty?.selectedNotes.Clear();
    }
    public void CopySelection() {
        if (currentMapDifficulty == null) {
            return;
        }
        currentMapDifficulty.clipboard.Clear();
        currentMapDifficulty.clipboard.AddRange(currentMapDifficulty.selectedNotes);
        currentMapDifficulty.clipboard.Sort();
    }
    public void CutSelection() {
        if (currentMapDifficulty == null) {
            return;
        }
        CopySelection();
        RemoveNotes(currentMapDifficulty.selectedNotes);
    }
    public void PasteClipboard(double beatOffset) {
        if (currentMapDifficulty == null || currentMapDifficulty.clipboard.Count == 0) {
            return;
        }
        // paste notes so that the first note lands on the given beat offset
        double offset = beatOffset - currentMapDifficulty.clipboard[0].beat;
        List<Note> notes = new List<Note>();
        for (int i = 0; i < currentMapDifficulty.clipboard.Count; i++) {
            Note n = new Note(currentMapDifficulty.clipboard[i].beat + offset, currentMapDifficulty.clipboard[i].col);
            notes.Add(n);
        }
        AddNotes(notes);
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
}

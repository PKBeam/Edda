using Edda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class MapEditor {

    MainWindow parent;
    public List<Note> notes;
    public List<Bookmark> bookmarks;
    public List<BPMChange> bpmChanges;
    List<Note> clipboard;
    List<Note> selectedNotes;
    EditHistory<Note> editorHistory;
    public MapEditor(MainWindow parent, List<Note> notes, List<BPMChange> bpmChanges, List<Bookmark> bookmarks, List<Note> clipboard) {
        this.parent = parent;
        this.bpmChanges = bpmChanges;
        this.bookmarks = bookmarks;
        this.notes = notes;
        this.clipboard = clipboard;
        this.selectedNotes = new();
        this.editorHistory = new(Const.Editor.HistoryMaxSize);
    }
    public void AddBPMChange(BPMChange b, bool redraw = true) {
        bpmChanges.Add(b);
        bpmChanges.Sort();
        if (redraw) {
            parent.DrawEditorGrid(false);
        }
        parent.RefreshBPMChanges();
    }
    public void RemoveBPMChange(BPMChange b, bool redraw = true) {
        bpmChanges.Remove(b);
        if (redraw) {
            parent.DrawEditorGrid(false);
        }
        parent.RefreshBPMChanges();
    }
    public void AddBookmark(Bookmark b) {
        bookmarks.Add(b);
        parent.DrawNavBookmarks();
        parent.DrawEditorGrid(false);
    }
    public void RemoveBookmark(Bookmark b) {
        bookmarks.Remove(b);
        parent.DrawNavBookmarks();
        parent.DrawEditorGrid(false);
    }
    public void RenameBookmark(Bookmark b, string newName) {
        b.name = newName;
        parent.DrawNavBookmarks();
        parent.DrawEditorGrid(false);
    }
    public void AddNotes(List<Note> notes, bool updateHistory = true) {
        List<Note> drawNotes = new();
        foreach (Note n in notes) {
            if (Helper.InsertSortedUnique(this.notes, n)) {
                drawNotes.Add(n);
            }
        }
        // draw the added notes
        // note: by drawing this note out of order, it is inconsistently layered with other notes.
        //       should we take the performance hit of redrawing the entire grid for visual consistency?
        parent.DrawEditorNotes(drawNotes);

        if (updateHistory) {
            editorHistory.Add(new EditList<Note>(true, drawNotes));
        }
        editorHistory.Print();
    }
    public void AddNotes(Note n, bool updateHistory = true) {
        AddNotes(new List<Note>() { n }, updateHistory);
    }
    public void RemoveNotes(List<Note> notes, bool updateHistory = true) {

        // undraw the added notes
        parent.UndrawEditorNotes(notes);

        if (updateHistory) {
            editorHistory.Add(new EditList<Note>(false, notes));
        }
        // finally, unselect all removed notes
        foreach (Note n in new List<Note>(notes)) {
            this.notes.Remove(n);
            UnselectNote(n);
        }
        editorHistory.Print();
    }
    public void RemoveNotes(Note n, bool updateHistory = true) {
        RemoveNotes(new List<Note>() { n }, updateHistory);
    }
    public void RemoveSelectedNotes(bool updateHistory = true) {
        RemoveNotes(selectedNotes, updateHistory);
    }
    public void ToggleSelection(Note n) {
        if (selectedNotes.Contains(n)) {
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
            Helper.InsertSortedUnique(selectedNotes, n);        
        }
        parent.HighlightEditorNotes(notes);
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
        if (selectedNotes == null) {
            return;
        }
        parent.UnhighlightEditorNotes(n);
        selectedNotes.Remove(n);
    }
    public void UnselectAllNotes() {
        if (selectedNotes == null) {
            return;
        }
        parent.UnhighlightEditorNotes(selectedNotes);
        selectedNotes.Clear();
    }
    public void CopySelection() {
        clipboard.Clear();
        clipboard.AddRange(selectedNotes);
        clipboard.Sort();
    }
    public void CutSelection() {
        CopySelection();
        RemoveNotes(selectedNotes);
    }
    public void PasteClipboard(double beatOffset) {
        if (clipboard.Count == 0) {
            return;
        }
        // paste notes so that the first note lands on the given beat offset
        double offset = beatOffset - clipboard[0].beat;
        List<Note> notes = new List<Note>();
        for (int i = 0; i < clipboard.Count; i++) {
            Note n = new Note(clipboard[i].beat + offset, clipboard[i].col);
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
        // prepare new selection
        List<Note> transformedSelection = new List<Note>();
        for (int i = 0; i < selectedNotes.Count; i++) {
            Note transformed = transform.Invoke(selectedNotes[i]);
            if (transformed != null) {
                transformedSelection.Add(transformed);
            }
        }
        if (transformedSelection.Count == 0) {
            return;
        }
        RemoveNotes(selectedNotes);
        AddNotes(transformedSelection);
        editorHistory.Consolidate(2);
        SelectNewNotes(transformedSelection);
    }
    public void Undo() {
        EditList<Note> edit = editorHistory.Undo();
        ApplyEdit(edit);
    }
    public void Redo() {
        EditList<Note> edit = editorHistory.Redo();
        ApplyEdit(edit);
    }
}

using System;
using System.Collections.Generic;

public class EditHistory<T> {
    private int bufferSize;
    private List<EditList<T>> history;
    private int currentIndex; // index of the edit item after the last action

    public EditHistory(int bufferSize) {
        this.bufferSize = bufferSize;
        this.Clear();
    }
    public void Add(EditList<T> edits) {
        EditList<T> e = (EditList<T>)edits.Clone();
        if (currentIndex == bufferSize) {
            RemoveFirst();
        }
        history.Insert(currentIndex, e);
        currentIndex++;
        // clear all of the "future" history
        history.RemoveRange(currentIndex, history.Count - currentIndex);
    }

    // returns the edits that need to be applied on the object being tracked
    public EditList<T> Undo() {
        if (currentIndex == 0) {
            return new EditList<T>();
        }
        return history[--currentIndex].Inverted();
    }

    // returns the edits that need to be applied on the object being tracked
    public EditList<T> Redo() {
        if (currentIndex == history.Count) {
            return new EditList<T>();
        }
        return history[currentIndex++];
    }
    public void Consolidate(int n) { // consolidate the last n entries into one
        EditList<T> e = new();
        for (int i = 0; i < n; i++) {
            e.items.AddRange(history[currentIndex - 1].items);
            currentIndex--;
        }
        this.Add(e);
    }
    public void Clear() {
        this.history = new();
        this.currentIndex = 0;
    }
    public void Print() {
        var outStr = $"(Pos: {this.currentIndex})";
        foreach (EditList<T> e in history) {
            outStr += e.ToString();
        }
        Console.WriteLine(outStr);
    }
    private void RemoveFirst() {
        history.RemoveAt(0);
        currentIndex--;
    }
}

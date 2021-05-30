using System.Collections.Generic;
using System.Diagnostics;

public class Edits<T> {
    public List<(bool, T)> items;

    public Edits<T> Inverted() {
        var e = new List<(bool, T)>();
        foreach (var i in items) {
            e.Add((!i.Item1, i.Item2));
        }
        return new Edits<T>(e);
    }
    public Edits(List<(bool, T)> items) {
        this.items = items;
    }
    public Edits() {
        this.items = new List<(bool, T)>();
    }
    public override string ToString() {
        var outStr = "[";
        foreach ((bool, T) e in this.items) {
            outStr += $"{(e.Item1 ? "+" : "-")}{e.Item2}";
        }
        outStr += "]";
        return outStr;
    }
    public void Print() {
        Trace.WriteLine(this.ToString());
    }
}

public class EditHistory<T> {
    private int bufferSize;
    private List<Edits<T>> history;
    private int currentIndex; // index of the edit item after the last action

    public EditHistory(int bufferSize) {
        this.bufferSize = bufferSize;
        this.Clear();
    }
    public void Add(bool isAdding, List<T> items) {
        List<(bool, T)> e = new();
        foreach (var i in items) {
            e.Add((isAdding, i));
        }
        this.Add(e);
    }
    public void Add(List<(bool, T)> items) {
        Edits<T> e = new(items);
        if (currentIndex == bufferSize) {
            RemoveFirst();
        }
        history.Insert(currentIndex, e);
        currentIndex++;
        // clear all of the "future" history
        history.RemoveRange(currentIndex, history.Count - currentIndex);
    }
    public Edits<T> Undo() {
        if (currentIndex == 0) {
            return new Edits<T>();
        }
        return history[--currentIndex].Inverted();
    }
    public Edits<T> Redo() {
        if (currentIndex == history.Count) {
            return new Edits<T>();
        }
        return history[currentIndex++];
    }
    public void Consolidate(int entries) { // consolidate the last n entries into one
        Edits<T> e = new();
        for (int i = 0; i < entries; i++) {
            var edits = this.history[currentIndex - 1];
            foreach (var edit in edits.items) {
                e.items.Add(edit);
            }
            currentIndex--;
        }
        this.Add(e.items);
    }
    public void Clear() {
        this.history = new();
        this.currentIndex = 0;
    }
    public void Print() {
        var outStr = $"(Pos: {this.currentIndex})";
        foreach (Edits<T> e in history) {
            outStr += e.ToString();
        }
        Trace.WriteLine(outStr);
    }
    private void RemoveFirst() {
        history.RemoveAt(0);
        currentIndex--;
    }
}

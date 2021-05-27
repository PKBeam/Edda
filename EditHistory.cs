using System.Collections.Generic;
using System.Diagnostics;

public class Edits<T> {
    public List<(bool, T)> items;

	public Edits<T> inverted() {
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
	public string toString() {
		var outStr = "[";
		foreach ((bool, T) e in this.items) {
			outStr += $"{(e.Item1 ? "+" : "-")}{e.Item2}";
        }
		outStr += "]";
		return outStr;
    }
	public void print() {
		Trace.WriteLine(this.toString());
    }
}

public class EditHistory<T> {
	private int bufferSize;
	private List<Edits<T>> history;
	private int currentIndex; // index of the edit item after the last action

	public EditHistory(int bufferSize) {
		this.bufferSize = bufferSize;
		this.clear();
	}
	public void add(bool isAdding, List<T> items) {
		List<(bool, T)> e = new List<(bool, T)>();
		foreach (var i in items) {
			e.Add((isAdding, i));
        }
		this.add(e);
	}
	public void add(List<(bool, T)> items) {
		Edits<T> e = new Edits<T>(items);
		if (currentIndex == bufferSize) {
			removeFirst();
        }
		history.Insert(currentIndex, e);
		currentIndex++;
		// clear all of the "future" history
		history.RemoveRange(currentIndex, history.Count - currentIndex);
	}
	public Edits<T> undo() {
		if (currentIndex == 0) {
			return new Edits<T>();
        }
		return history[--currentIndex].inverted();
    }
	public Edits<T> redo() {
		if (currentIndex == history.Count) {
			return new Edits<T>();
        }
		return history[currentIndex++];
    }
	public void consolidate(int entries) {
		Edits<T> e = new Edits<T>();
		for (int i = 0; i < entries; i++) {
			var edits = this.history[currentIndex - 1];
			foreach (var edit in edits.items) {
				e.items.Add(edit);
            }
			currentIndex--;
        }
		this.add(e.items);
	}
	public void clear() {
		this.history = new List<Edits<T>>();
		this.currentIndex = 0;
	}
	public void print() {
		var outStr = $"(Pos: {this.currentIndex})";
		foreach (Edits<T> e in history) {
			outStr += e.toString();
        }
		Trace.WriteLine(outStr);
    }
	private void removeFirst() {
		history.RemoveAt(0);
		currentIndex--;
    }
}

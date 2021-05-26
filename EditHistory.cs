using System.Collections.Generic;
using System.Diagnostics;

public class Edit<T> {
	public bool isAdding;
	public List<T> items;

	public Edit<T> inverted() {
		return new Edit<T>(!this.isAdding, new List<T>(this.items));
	}
	public Edit(bool isAdding, List<T> items) {
		this.isAdding = isAdding;
		this.items = items;
	}
	public Edit() {
		this.isAdding = true;
		this.items = new List<T>();
	}
	public string toString() {
		var outStr = "[";
		foreach (T t in this.items) {
			outStr += $"({t})";
        }
		outStr += "]";
		return outStr;
    }
}

public class EditHistory<T> {
	private int bufferSize;
	private List<Edit<T>> history;
	private int currentIndex; // index of the edit item after the last action

	public EditHistory(int bufferSize) {
		this.bufferSize = bufferSize;
		this.history = new List<Edit<T>>();
		currentIndex = 0;
	}
	public void add(bool isAdding, List<T> items) {
		Edit<T> e = new Edit<T>(isAdding, items);
		if (currentIndex == bufferSize) {
			removeFirst();
        }
		history.Add(e);
		currentIndex++;
		// clear all of the "future" history
		history.RemoveRange(currentIndex, history.Count - currentIndex);
	}
	public Edit<T> undo() {
		if (currentIndex == 0) {
			return new Edit<T>();
        }
		return history[--currentIndex].inverted();
    }
	public Edit<T> redo() {
		if (currentIndex == history.Count) {
			return new Edit<T>();
        }
		return history[currentIndex++];
    }
	public void print() {
		var outStr = $"(Pos: {this.currentIndex}) [";
		foreach (Edit<T> e in history) {
			outStr += $"({(e.isAdding ? "Add" : "Del")}, {e.toString()})";
        }
		outStr += "]";
		Trace.WriteLine(outStr);
    }
	private void removeFirst() {
		history.RemoveAt(0);
		currentIndex--;
    }
}

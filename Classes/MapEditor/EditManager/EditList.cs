using System;
using System.Collections.Generic;

public class EditList<T> : ICloneable {
    public List<Edit<T>> items;
    public EditList(List<Edit<T>> items) {
        this.items = items;
    }
    public EditList() {
        this.items = new List<Edit<T>>();
    }
    public EditList(bool isAdding, IEnumerable<T> items) {
        this.items = new List<Edit<T>>();
        foreach (T item in items) {
            this.items.Add(new Edit<T>(isAdding, item));
        }
    }
    public override string ToString() {
        var outStr = "[";
        foreach (Edit<T> e in this.items) {
            outStr += $"{(e.isAdd ? "+" : "-")}{e.item}";
        }
        outStr += "]";
        return outStr;
    }
    // returns this Edits<T> with the isAdd field inverted for all edits
    public EditList<T> Inverted() {
        var e = new List<Edit<T>>();
        foreach (var i in items) {
            Edit<T> inverted = new Edit<T>(!i.isAdd, i.item);
            e.Add(inverted);
        }
        return new EditList<T>(e);
    }
    public void Print() {
        Console.WriteLine(this.ToString());
    }

    public object Clone() {
        EditList<T> clone = new();
        foreach (Edit<T> edit in items) {
            clone.items.Add((Edit<T>)edit.Clone());
        }
        return clone;
    }
}
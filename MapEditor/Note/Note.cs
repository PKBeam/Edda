using System;

public class Note: IComparable, IEquatable<Note> {
	public double beat;
	public int col;
	public Note(double beat, int col) {
		this.beat = beat;
		this.col = col;
	}
	public Note() : this(0, 0) {}

    public int CompareTo(object obj) {
        if (!(obj is Note n)) {
            throw new Exception();
        }
        Note m = this;
        if (m.Equals(n)) {
            return 0;
        }
        if (Helper.DoubleApproxGreater(m.beat, n.beat)) {
            return 1;
        }
        if (Helper.DoubleApproxEqual(n.beat, this.beat) && Helper.DoubleApproxGreater(m.col, n.col)) {
            return 1;
        }
        return -1;
    }

    public bool Equals(Note n) {
        return Helper.DoubleApproxEqual(n.beat, this.beat) && Helper.DoubleApproxEqual(n.col, this.col);
    }
}

﻿using System;

public class Note: IComparable {
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
        if (m.beat == n.beat && m.col == n.col) {
            return 0;
        }
        if (m.beat > n.beat) {
            return 1;
        }
        if (m.beat == n.beat && m.col > n.col) {
            return 1;
        }
        return -1;
    }
}
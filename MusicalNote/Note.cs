using System;

public class Note {
	public double beat;
	public int col;
	public Note(double beat, int col) {
		this.beat = beat;
		this.col = col;
	}
	public Note() : this(0, 0) {}
}

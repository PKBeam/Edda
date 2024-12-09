using System;

namespace Edda.Classes.MapEditorNS.NoteNS {
    [Serializable]
    public class Note(double beat, int col) : IComparable, IEquatable<Note> {
        public double beat = beat;
        public int col = col;

        public Note() : this(0, 0) { }

        public int CompareTo(object obj) {
            if (obj is not Note n) {
                throw new Exception();
            }
            if (Equals(n)) {
                return 0;
            }
            if (Helper.DoubleApproxGreater(beat, n.beat)) {
                return 1;
            }
            if (Helper.DoubleApproxEqual(n.beat, beat) && col > n.col) {
                return 1;
            }
            return -1;
        }

        public override bool Equals(object obj) => obj is Note n && Equals(n);
        public override int GetHashCode() => HashCode.Combine(Math.Round(beat, 4), col);
        public bool Equals(Note n) => Helper.DoubleApproxEqual(n.beat, beat) && n.col == col;
    }
}
using System;
public static class NoteTransforms {
	public static Func<Note, Note> Mirror() {
        Func<Note, Note> f = n => {
            return new Note(n.beat, 3 - n.col);
        };
        return f;
        
    }

    // parametrised vertical shift
    public static Func<Note, Note> RowShift(double beatOffset) {
        Func<Note, Note> f = n => {
            double newBeat = n.beat + beatOffset;
            if (n.beat + beatOffset >= 0) {
                return new Note(newBeat, n.col);
            }
            return null;
        };
        return f;
    }

    // parametrised horizontal shift
    public static Func<Note, Note> ColShift(int offset) {
        Func<Note, Note> f = n => {
            int newCol = n.col + offset;
            if (Helper.RangeCheck(newCol, 0, 3)) {
                return new Note(n.beat, newCol);
            }
            return null;
        };
        return f;
    }
}

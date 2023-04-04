using System;
public static class NoteTransforms {
    public static Func<Note, Note> Mirror() {
        Func<Note, Note> f = n => {
            return new Note(n.beat, 3 - n.col);
        };
        return f;
    }

    // parametrised horizontal shift
    public static Func<Note, Note> ColShift(int offset) {
        Func<Note, Note> f = n => {
            int newCol = (n.col + offset) % 4;
            if (newCol < 0) {
                newCol += 4;
            }
            if (Helper.DoubleRangeCheck(newCol, 0, 3)) {
                return new Note(n.beat, newCol);
            }
            return null;
        };
        return f;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using static Edda.Const.Editor;

namespace Edda.Classes.MapEditorNS.NoteNS {
    [Serializable]
    public class NoteSelection {
        public SortedSet<Note> notes = [];
        public SortedSet<BPMChange> bpmChanges = [];

        public NoteSelection(MapEditor editor) {
            notes = editor.currentMapDifficulty.selectedNotes;
            bpmChanges = new(
                editor.currentMapDifficulty.bpmChanges
                    .Append(new BPMChange(-1, editor.GlobalBPM, 4)) // RagnaRuneString represents global BPM as a BPM change with timing -1.
            );
        }

        public NoteSelection(RagnaRuneString.Version1.RuneStringData runeStringData) {
            notes = new SortedSet<Note>(runeStringData.runes.Select(r => new Note(r.time, r.lineIndex)));
            bpmChanges = new SortedSet<BPMChange>(runeStringData.bpmChanges.Select(b => new BPMChange(b.startTime, b.bpm, 4)));
        }

        public static implicit operator RagnaRuneString.Version1.RuneStringData(NoteSelection noteSelection) {
            return new RagnaRuneString.Version1.RuneStringData(
                runes: noteSelection.notes.Select(n => new RagnaRuneString.Version1.Rune(n.beat, n.col)),
                bpmChanges: noteSelection.bpmChanges.Select(b => new RagnaRuneString.Version1.BPMChange(b.BPM, b.globalBeat))
            );
        }

        public IEnumerable<Note> GetPasteNotes(MapEditor editor, double beatOffset, int? colStart) {
            return (editor.NotePasteBehavior switch {
                NotePasteBehavior.AlignToGlobalBeat => GetPasteNotesAlignToGlobalBeat(beatOffset, colStart),
                NotePasteBehavior.AlignToFirstNoteBPM => GetPasteNotesAlignToFirstNoteBPM(editor, beatOffset, colStart),
                NotePasteBehavior.AlignToNoteBPM => GetPasteNotesAlignToNoteBPM(editor, beatOffset, colStart),
                _ => GetPasteNotesAlignToNoteBPM(editor, beatOffset, colStart)
            })
                // don't paste the note if it goes beyond the duration of the song or overflows on the columns
                .Where(n => n.beat <= editor.GlobalBPM * editor.SongDuration / 60 && n.col >= 0 && n.col <= 3);
        }

        /// <summary>
        /// Paste notes as-if they were copied from the same global BPM and disregard any BPM changes.
        /// </summary>
        /// <example>
        /// [Note(0.25, 0), Note(0.5, 1), Note(0.75, 2), Note(1, 3)] copied from a song with 140 BPM.
        /// Pasting into a song with 120 BPM at global beat offset 51.5, they'll be added as new notes:
        /// [Note(51.5, 0), Note(51.75, 1), Note(52, 2), Note(52.25, 3)]
        /// </example>
        /// <param name="beatOffset">global beat offset to start the pasting</param>
        /// <param name="colStart">column offset to start the pasting</param>
        /// <returns>collection of notes to paste</returns>
        private IEnumerable<Note> GetPasteNotesAlignToGlobalBeat(double beatOffset, int? colStart) {
            Note firstNote = notes.First();
            double rowOffset = beatOffset - firstNote.beat;
            int colOffset = colStart == null ? 0 : (int)colStart - firstNote.col;
            return notes.Select(n => new Note(n.beat + rowOffset, n.col + colOffset));
        }

        /// <summary>
        /// Paste notes only scaling by a factor based on the first pasted note BPM and mouse position BPM.
        /// </summary>
        /// <example>
        /// [Note(10.25, 0), Note(10.5, 1), Note(10.75, 2), Note(11, 3)] copied from a song with 140 BPM, with BPM change to 70 at global beat 10.
        /// Pasting into a song with 120 BPM at global beat offset 51.5, they'll be added as new notes:
        /// [Note(51.5, 0), Note(52, 1), Note(52.5, 2), Note(53, 3)]
        /// </example>
        /// <param name="editor">map editor</param>
        /// <param name="beatOffset">global beat offset to start the pasting</param>
        /// <param name="colStart">column offset to start the pasting</param>
        /// <returns>collection of notes to paste</returns>
        private IEnumerable<Note> GetPasteNotesAlignToFirstNoteBPM(MapEditor editor, double beatOffset, int? colStart) {
            Note firstNote = notes.First();
            int colOffset = colStart == null ? 0 : (int)colStart - firstNote.col;
            var notesGlobalBPM = GetLastBeatChange(-1)?.BPM ?? editor.GlobalBPM;
            var firstNoteBPM = GetLastBeatChange(firstNote.beat)?.BPM ?? notesGlobalBPM;
            var scaleFactor = editor.GetGridLength(editor.GetLastBeatChange(beatOffset).BPM, 1) / (notesGlobalBPM / firstNoteBPM);
            return notes.Select(n => new Note((n.beat - firstNote.beat) * scaleFactor + beatOffset, n.col + colOffset));
        }

        /// <summary>
        /// Paste notes by aligning each note timing based on the BPM changes in the pasted notes BPM changes in the editor where the notes are pasted.
        /// </summary>
        /// <example>
        /// [Note(10.25, 0), Note(10.5, 1), Note(10.75, 2), Note(11, 3)] copied from a song with 140 BPM, with BPM changes to 70 at global beat 10 and to 140 at global beat 10.5.
        /// Pasting into a song with 120 BPM at global beat offset 51.5 and a BPM change to 240 at global beat 52.25, they'll be added as new notes:
        /// [Note(51.5, 0), Note(52, 1), Note(52.25, 2), Note(52.375, 3)]
        /// </example>
        /// <param name="editor">map editor</param>
        /// <param name="beatOffset">global beat offset to start the pasting</param>
        /// <param name="colStart">column offset to start the pasting</param>
        /// <returns>collection of notes to paste</returns>
        private IEnumerable<Note> GetPasteNotesAlignToNoteBPM(MapEditor editor, double beatOffset, int? colStart) {
            Note firstNote = notes.First();
            int colOffset = colStart == null ? 0 : (int)colStart - firstNote.col;
            var notesGlobalBPM = GetLastBeatChange(-1)?.BPM ?? editor.GlobalBPM;
            var currentEditorBeatChange = editor.GetLastBeatChange(beatOffset);
            double lastNoteBeat = firstNote.beat;
            foreach (var note in notes) {
                var noteBPM = GetLastBeatChange(note.beat)?.BPM ?? notesGlobalBPM;
                var scaleFactor = editor.GetGridLength(currentEditorBeatChange.BPM, 1) / (notesGlobalBPM / noteBPM);
                var projectedBeat = (note.beat - lastNoteBeat) * scaleFactor + beatOffset;
                var projectedEditorBeatChange = editor.GetLastBeatChange(projectedBeat);
                while (!projectedEditorBeatChange.Equals(currentEditorBeatChange)) {
                    // We went through (potentially several) BPM changes between notes, so we need to first re-align offsets to the last one before projected note placement
                    foreach (var bpmChange in editor.currentMapDifficulty.bpmChanges.GetViewBetween(currentEditorBeatChange, projectedEditorBeatChange)) {
                        lastNoteBeat += (bpmChange.globalBeat - beatOffset) / editor.GetGridLength(currentEditorBeatChange.BPM, 1);
                        beatOffset = bpmChange.globalBeat;
                        currentEditorBeatChange = bpmChange;
                    }
                    // Recalculate projected beat
                    scaleFactor = editor.GetGridLength(currentEditorBeatChange.BPM, 1) / (notesGlobalBPM / noteBPM);
                    projectedBeat = (note.beat - lastNoteBeat) * scaleFactor + beatOffset;
                    projectedEditorBeatChange = editor.GetLastBeatChange(projectedBeat);
                }
                yield return new Note(projectedBeat, note.col + colOffset);
                beatOffset = projectedBeat;
                currentEditorBeatChange = projectedEditorBeatChange;
                lastNoteBeat = note.beat;
            }
        }

        private BPMChange? GetLastBeatChange(double beat) {
            return bpmChanges
                .Where(obj => Helper.DoubleApproxGreaterEqual(beat, obj.globalBeat))
                .LastOrDefault();
        }
    }
}
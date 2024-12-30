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
            var notesGlobalBeatChange = GetLastBeatChange(-1) ?? editor.GetLastBeatChange(-1);

            // We start from the first note beat assuming it will be placed on the specified beat offset.
            // Then, we "march" through both note selection and editor beats while keeping them in sync until we arrive at the next note to place.

            var noteSelectionBeat = firstNote.beat;
            var noteSelectionBeatChange = GetLastBeatChange(noteSelectionBeat) ?? notesGlobalBeatChange;

            var editorBeat = beatOffset;
            var editorBeatChange = editor.GetLastBeatChange(editorBeat);

            foreach (var note in notes) {
                var noteBeatChange = GetLastBeatChange(note.beat) ?? notesGlobalBeatChange;
                while (Helper.DoubleApproxGreater(note.beat, noteSelectionBeat)) {
                    // Decide the next target to march towards. There are 3 possibilities:
                    // 1. We first hit a BPM change on the note selection
                    // 2. We first hit a BPM change on the editor
                    // 3. We first hit the note.
                    var nextNoteSelectionBeatChange = noteSelectionBeatChange.Equals(noteBeatChange) ? null : bpmChanges.GetViewBetween(noteSelectionBeatChange, noteBeatChange).SkipWhile(bpmChange => bpmChange.Equals(noteSelectionBeatChange)).FirstOrDefault() ?? noteSelectionBeatChange;
                    var noteSelectionMarchBeat = nextNoteSelectionBeatChange?.globalBeat ?? note.beat;

                    var scaleFactor = editor.GetGridLength(editorBeatChange.BPM, 1) / (notesGlobalBeatChange.BPM / noteSelectionBeatChange.BPM);
                    var noteSelectionMarchEditorBeat = (noteSelectionMarchBeat - noteSelectionBeat) * scaleFactor + editorBeat;

                    var noteSelectionMarchEditorBeatChange = editor.GetLastBeatChange(noteSelectionMarchEditorBeat);
                    if (!noteSelectionMarchEditorBeatChange.Equals(editorBeatChange)) {
                        // There is a BPM change on the editor grid before the note selection march destination - case 2.
                        var nextEditorBeatChange = editor.currentMapDifficulty.bpmChanges.GetViewBetween(editorBeatChange, noteSelectionMarchEditorBeatChange).SkipWhile(bpmChange => bpmChange.Equals(editorBeatChange)).FirstOrDefault() ?? editorBeatChange;
                        noteSelectionBeat += (nextEditorBeatChange.globalBeat - editorBeat) / scaleFactor;
                        noteSelectionBeatChange = GetLastBeatChange(noteSelectionBeat) ?? notesGlobalBeatChange;
                        editorBeat = nextEditorBeatChange.globalBeat;
                        editorBeatChange = nextEditorBeatChange;
                    } else if (nextNoteSelectionBeatChange != null) {
                        // There are no BPM changes on the editor grid, but there is a BPM change on the note selection grid - case 1.
                        noteSelectionBeat = noteSelectionMarchBeat;
                        noteSelectionBeatChange = nextNoteSelectionBeatChange;
                        editorBeat = noteSelectionMarchEditorBeat;
                        editorBeatChange = noteSelectionMarchEditorBeatChange;
                    } else {
                        // There are no BPM changes on the editor grid or note selection grid before the note - case 3.
                        noteSelectionBeat = noteSelectionMarchBeat;
                        noteSelectionBeatChange = GetLastBeatChange(noteSelectionBeat) ?? notesGlobalBeatChange;
                        editorBeat = noteSelectionMarchEditorBeat;
                        editorBeatChange = noteSelectionMarchEditorBeatChange;
                    }
                }
                yield return new Note(editorBeat, note.col + colOffset);
            }
        }

        private BPMChange? GetLastBeatChange(double beat) {
            return bpmChanges
                .Where(obj => Helper.DoubleApproxGreaterEqual(beat, obj.globalBeat))
                .LastOrDefault();
        }
    }
}
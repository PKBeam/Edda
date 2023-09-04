using System;
using System.Collections.Generic;
using System.Linq;

namespace Edda.Classes.MapEditor.Stats {
    public class MapStats {
        /// <summary>
        /// Song properties needed for calculations
        /// </summary>
        public double globalBPM;
        public double songDuration;

        /// <summary>
        /// Notes
        /// </summary>
        public int allNotes;
        public int selectedNotes;
        public int singleNotes;
        public int doubleNotes;

        /// <summary>
        /// Column variety
        /// </summary>
        public int[] columnCounts = new int[4];
        public double[] columnPercentages = new double[4];

        /// <summary>
        /// NPS
        /// </summary>
        public double npsSong;
        public double npsMapped;
        public double nps16Beat;
        public double nps8Beat;
        public double nps4Beat;

        public MapStats(double globalBPM, double songDuration) {
            this.globalBPM = globalBPM;
            this.songDuration = songDuration;
        }

        public void Recalculate(SortedSet<Note> notes, SortedSet<Note> selectedNotes) {
            RecalculateNotes(notes, selectedNotes);
            RecalculateColumnVariety(notes);
            RecalculateNPS(notes);
        }

        public void RecalculateNotes(SortedSet<Note> notes, SortedSet<Note> selectedNotes) {
            this.allNotes = notes.Count;
            this.selectedNotes = selectedNotes.Count;
            this.singleNotes = CalculateRowNotes(notes, 1);
            this.doubleNotes = CalculateRowNotes(notes, 2);
        }

        private int CalculateRowNotes(SortedSet<Note> notes, int rowCount) {
            int rowNoteCount = 0;
            var noteIt = notes.GetEnumerator();
            Note? lineStartNote = null;
            var lineNoteCount = 0;


            while (noteIt.MoveNext()) {
                if (lineStartNote == null || !Helper.DoubleApproxEqual(noteIt.Current.beat, lineStartNote.beat)) {
                    if (lineNoteCount == rowCount) {
                        rowNoteCount++;
                    }
                    lineStartNote = noteIt.Current;
                    lineNoteCount = 1;
                } else {
                    lineNoteCount++;
                }
            }
            // last row to consider after all the notes were processed
            if (lineNoteCount == rowCount) {
                rowNoteCount++;
            }

            return rowNoteCount;
        }

        public void RecalculateColumnVariety(SortedSet<Note> notes) {
            for (int i = 0; i < 4; i++) {
                this.columnCounts[i] = notes.Where(note => note.col == i).Count();
                this.columnPercentages[i] = double.Round(100 * (double)this.columnCounts[i] / Math.Max(this.allNotes, 1), 1);
            }
        }

        public void RecalculateNPS(SortedSet<Note> notes) {
            this.npsSong = double.Round(this.allNotes / this.songDuration, 2);
            this.npsMapped = CalculateNPSMapped(notes);
            this.nps16Beat = CalculateNPSPeakBeat(notes, 16);
            this.nps8Beat = CalculateNPSPeakBeat(notes, 8);
            this.nps4Beat = CalculateNPSPeakBeat(notes, 4);
        }

        private double CalculateNPSPeakBeat(SortedSet<Note> notes, double beats) {
            var peakNPB = 0.0;
            var startIt = notes.GetEnumerator();
            startIt.MoveNext();
            var startIndex = 0;
            var noteIt = notes.GetEnumerator();
            var noteIndex = 0;

            while (noteIt.MoveNext()) {
                while (Helper.DoubleApproxGreater(noteIt.Current.beat - startIt.Current.beat, beats)) {
                    startIt.MoveNext();
                    startIndex++;
                }
                peakNPB = Math.Max(peakNPB, (noteIndex - startIndex + 1) / beats);
                noteIndex++;
            }

            return double.Round(peakNPB * this.globalBPM / 60, 2);
        }

        private double CalculateNPSMapped(SortedSet<Note> notes) {
            double startBeat = notes.FirstOrDefault(new Note()).beat;
            double endBeat = notes.LastOrDefault(new Note()).beat;
            if (Helper.DoubleApproxGreater(endBeat, startBeat)) {
                return double.Round(this.allNotes * this.globalBPM / ((endBeat - startBeat) * 60), 2);
            } else {
                return 0; // We need notes in at least two different beat positions to have non-zero map length
            }
        }
    }
}
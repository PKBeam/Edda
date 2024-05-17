﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Edda.Classes.MapEditorNS.Stats {
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
        public int tripleNotes;
        public int quadrupleNotes;

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
            allNotes = notes.Count;
            this.selectedNotes = selectedNotes.Count;
            singleNotes = CalculateRowNotes(notes, 1);
            doubleNotes = CalculateRowNotes(notes, 2);
            tripleNotes = CalculateRowNotes(notes, 3);
            quadrupleNotes = CalculateRowNotes(notes, 4);
        }

        private static int CalculateRowNotes(SortedSet<Note> notes, int rowCount) {
            return notes
                .GroupBy(note => note.beat, new Helper.DoubleApproxEqualComparer())
                .Select(group => group.Count())
                .Count(count => count == rowCount);
        }

        public void RecalculateColumnVariety(SortedSet<Note> notes) {
            for (int i = 0; i < 4; i++) {
                columnCounts[i] = notes.Where(note => note.col == i).Count();
                columnPercentages[i] = double.Round(100 * (double)columnCounts[i] / Math.Max(allNotes, 1), 1);
            }
        }

        public void RecalculateNPS(SortedSet<Note> notes) {
            npsSong = double.Round(allNotes / songDuration, 2);
            npsMapped = CalculateNPSMapped(notes);
            nps16Beat = CalculateNPSPeakBeat(notes, 16);
            nps8Beat = CalculateNPSPeakBeat(notes, 8);
            nps4Beat = CalculateNPSPeakBeat(notes, 4);
        }

        private double CalculateNPSPeakBeat(SortedSet<Note> notes, double beats) {
            var peakNPB = notes
                .Select((note, noteIndex) => notes
                    .GetViewBetween(new Note(note.beat - beats, -1), note)
                    .Count
                )
                .Select(noteCount => noteCount / beats)
                .DefaultIfEmpty(0)
                .Max();

            return double.Round(peakNPB * globalBPM / 60, 2);
        }

        private double CalculateNPSMapped(SortedSet<Note> notes) {
            double startBeat = notes.FirstOrDefault(new Note()).beat;
            double endBeat = notes.LastOrDefault(new Note()).beat;
            if (Helper.DoubleApproxGreater(endBeat, startBeat)) {
                return double.Round(allNotes * globalBPM / ((endBeat - startBeat) * 60), 2);
            } else {
                return 0; // We need notes in at least two different beat positions to have non-zero map length
            }
        }
    }
}
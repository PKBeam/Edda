using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class BeatScanner : AudioScanner {
    public BeatScanner(ParallelAudioPlayer parallelAudioPlayer) : base(parallelAudioPlayer) {
    }

    public void Start(int millisecStart, List<double> beats, double globalBPM) {
        Start(millisecStart, BeatsToNotes(beats), globalBPM);
    }

    private List<Note> BeatsToNotes(List<double> beats) {
        List<Note> notes = new();
        foreach (double b in beats) {
            notes.Add(new Note(b, 0));
        }
        return notes;
    }
}
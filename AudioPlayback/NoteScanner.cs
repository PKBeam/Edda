using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class NoteScanner {
    int noteScanIndex;
    int noteScanStopwatchOffset = 0;
    Stopwatch noteScanStopwatch;
    CancellationTokenSource noteScanTokenSource;
    CancellationToken noteScanToken;

    double bpm;
    public List<Note> notes;

    DrumPlayer drummer;

    public NoteScanner(double bpm, List<Note> notes, DrumPlayer drummer) {
        this.bpm = bpm;
        this.notes = notes;
        this.drummer = drummer;
        this.noteScanStopwatch = new Stopwatch();
    }

    public void Start(int millisecStart) {
        noteScanStopwatchOffset = millisecStart; // set user audio delay
        SetScanStart();

        // start scanning for notes
        noteScanTokenSource = new CancellationTokenSource();
        noteScanToken = noteScanTokenSource.Token;
        Task.Run(() => BeginScan(noteScanStopwatchOffset, noteScanToken), noteScanToken);

        noteScanStopwatch.Start();
    }

    public void Stop() {
        if (noteScanTokenSource != null) {
            noteScanTokenSource.Cancel();
        }
        noteScanStopwatch.Reset();
    }
    public void SetScanStart() {
        // calculate scan index for playing drum hits
        var seekBeat = noteScanStopwatchOffset * bpm / 60000;
        var newNoteScanIndex = 0;
        foreach (var n in notes) {
            if (Helper.DoubleApproxGreaterEqual(n.beat, seekBeat)) {
                break;
            }
            newNoteScanIndex++;
        }
        noteScanIndex = newNoteScanIndex;
    }
    private void BeginScan(int startFrom, CancellationToken ct) {
        // NOTE: this function is called on a separate thread

        // scan notes while song is still playing
        var nextPollTime = Const.Audio.NotePollRate;
        while (!ct.IsCancellationRequested) {
            if (noteScanStopwatch.ElapsedMilliseconds + startFrom >= nextPollTime) {
                ScanNotes();
                nextPollTime += Const.Audio.NotePollRate;
            }
        }
    }
    private void ScanNotes() {
        var currentTime = noteScanStopwatch.ElapsedMilliseconds + noteScanStopwatchOffset;
        // check if we started past the last note in the song
        if (noteScanIndex < notes.Count) {
            var noteTime = 60000 * notes[noteScanIndex].beat / bpm;
            var drumHits = 0;

            // check if any notes were missed
            while (currentTime - noteTime >= Const.Audio.NoteDetectionDelta && noteScanIndex < notes.Count - 1) {
                Trace.WriteLine($"WARNING: A note was played late during playback. (Delta: {Math.Round(currentTime - noteTime, 2)})");
                drumHits++;
                noteScanIndex++;
                noteTime = 60000 * notes[noteScanIndex].beat / bpm;
            }

            // check if we need to play any notes
            while (Math.Abs(currentTime - noteTime) < Const.Audio.NoteDetectionDelta) {
                //Trace.WriteLine($"Played note at beat {selectedDifficultyNotes[noteScanIndex].Item1}");

                drumHits++;
                noteScanIndex++;
                if (noteScanIndex >= notes.Count) {
                    break;
                }
                noteTime = 60000 * notes[noteScanIndex].beat / bpm;
            }

            // play all pending drum hits
            if (drummer.PlayDrum(drumHits) == false) {
                Trace.WriteLine("WARNING: Drummer skipped a drum hit");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class AudioScanner {
    int scanIndex;
    int stopwatchOffset = 0;
    Stopwatch stopwatch;
    CancellationTokenSource tokenSource;
    CancellationToken token;

    double globalBPM;
    public List<Note> notes;

    ParallelAudioPlayer parallelAudioPlayer;

    public AudioScanner(ParallelAudioPlayer parallelAudioPlayer) {
        this.parallelAudioPlayer = parallelAudioPlayer;
        this.stopwatch = new Stopwatch();
    }

    public void Start(int millisecStart, List<Note> notes, double globalBPM) {
        this.globalBPM = globalBPM;
        this.notes = notes;
        stopwatchOffset = millisecStart;
        SetScanStart();

        // start scanning for notes
        tokenSource = new CancellationTokenSource();
        token = tokenSource.Token;
        Task.Run(() => BeginScan(stopwatchOffset, token), token);

        stopwatch.Start();
    }
    public void Stop() {
        if (tokenSource != null) {
            tokenSource.Cancel();
        }
        stopwatch.Reset();
    }
    private void SetScanStart() {
        // calculate scan index for playing drum hits
        var seekBeat = stopwatchOffset * globalBPM / 60000;
        var newNoteScanIndex = 0;
        foreach (var n in notes) {
            if (Helper.DoubleApproxGreaterEqual(n.beat, seekBeat)) {
                break;
            }
            newNoteScanIndex++;
        }
        scanIndex = newNoteScanIndex;
    }
    private void BeginScan(int startFrom, CancellationToken ct) {
        // NOTE: this function is called on a separate thread

        // scan notes while song is still playing
        var nextPollTime = Const.Audio.NotePollRate;
        while (!ct.IsCancellationRequested) {
            if (stopwatch.ElapsedMilliseconds + startFrom >= nextPollTime) {
                ScanNotes();
                nextPollTime += Const.Audio.NotePollRate;
            }
        }
    }
    private void ScanNotes() {
        OnNoteScanBegin();

        var currentTime = stopwatch.ElapsedMilliseconds + stopwatchOffset;
        // check if we started past the last note in the song
        if (scanIndex >= notes.Count) {
            return;
        }
        var noteTime = 60000 * notes[scanIndex].beat / globalBPM;
        var noteHits = 0;

        // check if any notes were missed
        while (currentTime - noteTime >= Const.Audio.NoteDetectionDelta && scanIndex < notes.Count - 1) {
            Trace.WriteLine($"WARNING: Scanner played audio late (Delta: {Math.Round(currentTime - noteTime, 2)})");
            OnNoteScanLateHit(notes[scanIndex]);
            noteHits++;
            scanIndex++;
            noteTime = 60000 * notes[scanIndex].beat / globalBPM;
        }

        // check if we need to play any notes
        while (Math.Abs(currentTime - noteTime) < Const.Audio.NoteDetectionDelta) {
            //Trace.WriteLine($"Played note at beat {selectedDifficultyNotes[noteScanIndex].Item1}");
            OnNoteScanHit(notes[scanIndex]);
            noteHits++;
            scanIndex++;
            if (scanIndex >= notes.Count) {
                break;
            }
            noteTime = 60000 * notes[scanIndex].beat / globalBPM;
        }

        // play all pending drum hits
        if (parallelAudioPlayer.isEnabled && parallelAudioPlayer.Play(noteHits) == false) {
            Trace.WriteLine("WARNING: Scanner skipped a note");
        }

        OnNoteScanFinish();
    }
    protected virtual void OnNoteScanBegin() {
    }
    protected virtual void OnNoteScanLateHit(Note n) {
    }
    protected virtual void OnNoteScanHit(Note n) {
    }
    protected virtual void OnNoteScanFinish() {
    }
}

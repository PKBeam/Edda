using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Edda.Const;

public class AudioScanner : IDisposable {
    int scanIndex;
    double tempo;
    protected int stopwatchOffset = 0;
    protected Stopwatch stopwatch;
    CancellationTokenSource tokenSource;
    CancellationToken token;

    protected double globalBPM;
    public List<Note> notes;

    ParallelAudioPlayer parallelAudioPlayer;

    public AudioScanner(ParallelAudioPlayer parallelAudioPlayer, double tempo) {
        SetAudioPlayer(parallelAudioPlayer);
        this.stopwatch = new Stopwatch();
        this.tempo = tempo;
    }

    public AudioScanner(ParallelAudioPlayer parallelAudioPlayer) : this(parallelAudioPlayer, 1.0) {
    }

    public virtual void Dispose()
    {
        stopwatch?.Stop();
        stopwatch = null;

        tokenSource?.Cancel();
        tokenSource?.Dispose();
        tokenSource = null;

        notes = null;
        parallelAudioPlayer = null;
    }

    public void SetTempo(double newTempo) {
        tempo = newTempo;
    }

    public void SetAudioPlayer(ParallelAudioPlayer parallelAudioPlayer) {
        this.parallelAudioPlayer = parallelAudioPlayer;
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
        while (!ct.IsCancellationRequested) {
            ScanNotes();
            Thread.Sleep(Audio.NotePollRate);
        }
    }
    private void ScanNotes() {
        OnNoteScanBegin();

        var currentTime = stopwatch.ElapsedMilliseconds * tempo + stopwatchOffset;
        // check if we started past the last note in the song
        if (scanIndex >= notes.Count) {
            return;
        }
        var noteTime = 60000 * notes[scanIndex].beat / globalBPM;
        var noteHits = 0;

        // check if any notes were missed
        while (currentTime - noteTime >= Audio.NoteDetectionDelta && scanIndex < notes.Count - 1) {
            
            if (parallelAudioPlayer?.Play(notes[scanIndex].col) == false) {
                Helper.ThreadedPrint("WARNING: Scanner skipped a note that was already late");
            } else {
                Helper.ThreadedPrint($"WARNING: Scanner played audio late (Delta: {Math.Round(currentTime - noteTime, 2)})");
            }
            OnNoteScanLateHit(notes[scanIndex]);
            noteHits++;
            scanIndex++;
            noteTime = 60000 * notes[scanIndex].beat / globalBPM;
        }

        // check if we need to play any notes
        while (Math.Abs(currentTime - noteTime) < Audio.NoteDetectionDelta) {

            if (parallelAudioPlayer?.Play(notes[scanIndex].col) == false) {
                Helper.ThreadedPrint("WARNING: Scanner skipped a note");
            }

            OnNoteScanHit(notes[scanIndex]);
            noteHits++;
            scanIndex++;
            if (scanIndex >= notes.Count) {
                break;
            }
            noteTime = 60000 * notes[scanIndex].beat / globalBPM;
        }

        // play all pending drum hits
        //if (parallelAudioPlayer.isEnabled && parallelAudioPlayer.Play(noteHits) == false) {
        //    Trace.WriteLine("WARNING: Scanner skipped a note");
        //}

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

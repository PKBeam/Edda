using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class BeatScanner {
    int beatScanIndex;
    int beatScanStopwatchOffset = 0;
    Stopwatch beatScanStopwatch;
    CancellationTokenSource beatScanTokenSource;
    CancellationToken beatScanToken;

    double globalBPM;
    public List<double> beats;

    Metronome metronome;

    public BeatScanner(Metronome metronome) {
        this.beatScanStopwatch = new Stopwatch();
        this.metronome = metronome;
    }

    public void Start(int millisecStart, List<double> beats, double globalBPM) {
        this.beats = beats;
        this.globalBPM = globalBPM;
        beatScanStopwatchOffset = millisecStart; // set user audio delay
        SetScanStart();

        // start scanning for notes
        beatScanTokenSource = new CancellationTokenSource();
        beatScanToken = beatScanTokenSource.Token;
        Task.Run(() => BeginScan(beatScanStopwatchOffset, beatScanToken), beatScanToken);

        beatScanStopwatch.Start();
    }
    public void Stop() {
        if (beatScanTokenSource != null) {
            beatScanTokenSource.Cancel();
        }
        beatScanStopwatch.Reset();
    }
    private void SetScanStart() {
        // calculate scan index for playing drum hits
        var seekBeat = beatScanStopwatchOffset * globalBPM / 60000;
        var newNoteScanIndex = 0;
        foreach (var b in beats) {
            if (Helper.DoubleApproxGreaterEqual(b, seekBeat)) {
                break;
            }
            newNoteScanIndex++;
        }
        beatScanIndex = newNoteScanIndex;
    }
    private void BeginScan(int startFrom, CancellationToken ct) {
        // NOTE: this function is called on a separate thread

        // scan notes while song is still playing
        var nextPollTime = Const.Audio.NotePollRate;
        while (!ct.IsCancellationRequested) {
            if (beatScanStopwatch.ElapsedMilliseconds + startFrom >= nextPollTime) {
                ScanBeats();
                nextPollTime += Const.Audio.NotePollRate;
            }
        }
    }
    private void ScanBeats() {
        var currentTime = beatScanStopwatch.ElapsedMilliseconds + beatScanStopwatchOffset;
        // check if we started past the last note in the song
        if (beatScanIndex < beats.Count) {
            var noteTime = 60000 * beats[beatScanIndex] / globalBPM;

            // check if any notes were missed
            while (currentTime - noteTime >= Const.Audio.NoteDetectionDelta && beatScanIndex < beats.Count - 1) {
                Trace.WriteLine($"WARNING: A beat was played late during playback. (Delta: {Math.Round(currentTime - noteTime, 2)})");

                metronome.Play();

                beatScanIndex++;
                noteTime = 60000 * beats[beatScanIndex] / globalBPM;
            }

            // check if we need to play any notes
            while (Math.Abs(currentTime - noteTime) < Const.Audio.NoteDetectionDelta) {
                //Trace.WriteLine($"Played note at beat {selectedDifficultyNotes[noteScanIndex].Item1}");

                metronome.Play();

                beatScanIndex++;
                if (beatScanIndex >= beats.Count) {
                    break;
                }
                noteTime = 60000 * beats[beatScanIndex] / globalBPM;
                
            }
        }
    }
}


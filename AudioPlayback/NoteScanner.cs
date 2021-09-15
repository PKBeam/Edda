using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Edda;

public class NoteScanner: AudioScanner {
    MainWindow caller;
    List<Note> notesPlayed;
    public NoteScanner(MainWindow caller, ParallelAudioPlayer parallelAudioPlayer): base(parallelAudioPlayer) {
        this.caller = caller;
    }
    protected override void OnNoteScanBegin() {
        notesPlayed = new List<Note>();
    }
    protected override void OnNoteScanLateHit(Note n) {
        notesPlayed.Add(n);
    }
    protected override void OnNoteScanHit(Note n) {
        notesPlayed.Add(n);
        double currentTime = stopwatch.ElapsedMilliseconds + stopwatchOffset;
        double songTime = caller.songStream.CurrentTime.TotalMilliseconds;
        //Trace.WriteLine($"Played note early by {currentTime - songTime:.##}ms");
    }
    protected override void OnNoteScanFinish() {
        foreach (Note n in notesPlayed) {
            caller.Dispatcher.Invoke(() => {
                caller.AnimateNote(n);
            });
        }
    }
}

using Edda;
using Edda.Classes.MapEditorNS.NoteNS;
using System.Collections.Generic;

public class NoteScanner : AudioScanner {
    MainWindow caller;
    List<Note> notesPlayed;
    public bool playedLateNote { get; set; }
    public NoteScanner(MainWindow caller, ParallelAudioPlayer parallelAudioPlayer, double tempo) : base(parallelAudioPlayer, tempo) {
        this.caller = caller;
        this.playedLateNote = false;
    }

    public override void Dispose() {
        base.Dispose();
        caller = null;
        notesPlayed = null;
    }

    protected override void OnNoteScanBegin() {
        notesPlayed = new List<Note>();
    }
    protected override void OnNoteScanLateHit(Note n) {
        notesPlayed.Add(n);
        playedLateNote = true;
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
                caller.AnimateDrum(n.col);
                caller.AnimateNote(n);
            });
        }
    }
}
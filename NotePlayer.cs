using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;
// todo: put more drum samples in?
public class NotePlayer : IDisposable {

    int streams;
    int uniqueSamples;
    int lastPlayedStream;
    DateTime[] lastPlayedTimes;
    AudioFileReader[] noteStreams;
    WasapiOut[] notePlayers;

    public NotePlayer(string[] filePaths, int streams, int desiredLatency) {
        this.lastPlayedStream = 0;
        this.streams = streams;
        this.uniqueSamples = filePaths.Length;
        noteStreams = new AudioFileReader[streams];
        notePlayers = new WasapiOut[streams];
        lastPlayedTimes = new DateTime[streams];
        for (int i = 0; i < streams; i++) {
            noteStreams[i] = new AudioFileReader(filePaths[streams % uniqueSamples]);
            noteStreams[i].Volume = 1.0f;
            notePlayers[i] = new WasapiOut(AudioClientShareMode.Shared, desiredLatency);
            notePlayers[i].Init(noteStreams[i]);
        }
    }
    public bool PlayDrum(int hits) {
        if (hits == 0) {
            return true;
        }
        int playedHits = 0;
        for (int i = 0; i < streams; i++) {
            // check that the stream is available to play, and that the sample file is not the same as the last
            if (DateTime.Now - lastPlayedTimes[i] > new TimeSpan(0, 0, 1) && (i % uniqueSamples != lastPlayedStream % uniqueSamples)) {
                noteStreams[i].CurrentTime = new TimeSpan(0, 0, 0, 0, 0);
                notePlayers[i].Play();
                this.lastPlayedStream = i;
                lastPlayedTimes[i] = DateTime.Now;
                playedHits++;
                if (playedHits == hits) {
                    return true;
                }
            }
        }
        return false;
    }
    public void ChangeVolume(double vol) {
        for (int i = 0; i < streams; i++) {
            noteStreams[i].Volume = (float)Math.Min(Math.Abs(vol), 1);
        }
    }
    public void Dispose() {
        Dispose(true);
    }
    protected virtual void Dispose(bool disposing) {
        // Cleanup
        for (int i = 0; i < this.streams; i++) {
            noteStreams[i].Dispose();
            notePlayers[i].Dispose();
        }
    }
}

using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Diagnostics;

public class Metronome : IDisposable {
    int streams;
    int lastPlayedStream;
    DateTime[] lastPlayedTimes;
    AudioFileReader[] noteStreams;
    WasapiOut[] notePlayers;
    bool isEnabled;
    public Metronome(string basePath, int streams, int desiredLatency, bool isEnabled) {
        this.isEnabled = isEnabled;
        this.streams = streams;
        noteStreams = new AudioFileReader[streams];
        notePlayers = new WasapiOut[streams];
        lastPlayedTimes = new DateTime[streams];
        for (int i = 0; i < streams; i++) {
            noteStreams[i] = new AudioFileReader(basePath);
            notePlayers[i] = new WasapiOut(AudioClientShareMode.Shared, desiredLatency);
            notePlayers[i].Init(noteStreams[i]);
        }
    }

    public void Play() {
        if (isEnabled) {
            for (int i = 0; i < streams; i++) {
                // check that the stream is available to play, and that the sample file is not the same as the last
                if (DateTime.Now - lastPlayedTimes[i] > new TimeSpan(0, 0, 0, 0, 500)) {
                    noteStreams[i].CurrentTime = TimeSpan.Zero;
                    notePlayers[i].Play();
                    this.lastPlayedStream = i;
                    lastPlayedTimes[i] = DateTime.Now;
                    return;
                }
            }
        }
    }

    public void Enable() {
        isEnabled = true;
    }

    public void Disable() {
        isEnabled = false;
    }

    public void Dispose() {
        // Cleanup
        for (int i = 0; i < this.streams; i++) {
            noteStreams[i].Dispose();
            notePlayers[i].Dispose();
        }
    }
}

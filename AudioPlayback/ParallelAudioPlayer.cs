using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;

public class ParallelAudioPlayer: IDisposable {
    int streams;
    int uniqueSamples;
    int lastPlayedStream;
    DateTime[] lastPlayedTimes;
    AudioFileReader[] noteStreams;
    WasapiOut[] notePlayers;    
    public bool isEnabled { get; set; }

    public ParallelAudioPlayer(string basePath, int streams, int desiredLatency, bool isEnabled) {
        this.lastPlayedStream = 0;
        this.streams = streams;
        this.isEnabled = isEnabled;

        this.uniqueSamples = 0;
        while (File.Exists(GetFilePath(basePath, this.uniqueSamples + 1))) {
            this.uniqueSamples++;
        }
        if (uniqueSamples < 1) {
            throw new FileNotFoundException();
        }

        noteStreams = new AudioFileReader[streams];
        notePlayers = new WasapiOut[streams];
        lastPlayedTimes = new DateTime[streams];
        for (int i = 0; i < streams; i++) {
            noteStreams[i] = new AudioFileReader(GetFilePath(basePath, (i % uniqueSamples) + 1)) {
                Volume = Const.Audio.DefaultNoteVolume
            };
            notePlayers[i] = new WasapiOut(AudioClientShareMode.Shared, desiredLatency);
            notePlayers[i].Init(noteStreams[i]);
            
        }
    }
    public ParallelAudioPlayer(string basePath, int streams, int desiredLatency) : this(basePath, streams, desiredLatency, true) { }

    public virtual bool Play() {
        return Play(1);
    }
    public virtual bool Play(int hits) {
        if (!isEnabled) {
            return false;
        }
        if (hits == 0) {
            return true;
        }
        int playedHits = 0;
        for (int i = 0; i < streams; i++) {
            // check that the stream is available to play
            if (DateTime.Now - lastPlayedTimes[i] > noteStreams[i].TotalTime) {
                // and that the sample file is not the same as the last
                if ((uniqueSamples <= 1) || (i % uniqueSamples != lastPlayedStream % uniqueSamples)) {
                    noteStreams[i].CurrentTime = TimeSpan.Zero;
                    notePlayers[i].Play();
                    this.lastPlayedStream = i;
                    lastPlayedTimes[i] = DateTime.Now;
                    playedHits++;
                    if (playedHits == hits) {
                        return true;
                    }
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
        for (int i = 0; i < this.streams; i++) {
            noteStreams[i].Dispose();
            notePlayers[i].Dispose();
        }
    }
    private string GetFilePath(string basePath, int sampleNumber) {
        return $"{Const.Program.ResourcesPath}{basePath}{sampleNumber}.wav";
    }
}

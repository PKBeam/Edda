using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Diagnostics;
using System.IO;

public class ParallelAudioPlayer: IDisposable {
    const int numChannels = 4;
    const float maxPan = Const.Audio.MaxPanDistance;
    int streams;
    int uniqueSamples;
    int lastPlayedStream;
    DateTime[] lastPlayedTimes;
    AudioFileReader[] noteStreams;
    WasapiOut[] notePlayers;    
    public bool isEnabled { get; set; }
    public bool isPanned { get; set; }

    public ParallelAudioPlayer(string basePath, int streams, int desiredLatency, bool isEnabled, bool isPanned) {
        this.lastPlayedStream = 0;
        this.streams = streams;
        this.isEnabled = isEnabled;
        this.isPanned = isPanned;

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
            noteStreams[i] = new AudioFileReader(GetFilePath(basePath, (i % numChannels % uniqueSamples) + 1)) {
                Volume = Const.Audio.DefaultNoteVolume
            };
            notePlayers[i] = new WasapiOut(AudioClientShareMode.Shared, desiredLatency);
            if (isPanned) {
                var mono = new StereoToMonoSampleProvider(noteStreams[i]);
                mono.LeftVolume = 1.0f;
                mono.RightVolume = 1.0f;
                var panProv = new PanningSampleProvider(mono);
                panProv.Pan = i % numChannels * 2 * maxPan / (numChannels - 1) - maxPan;
                notePlayers[i].Init(panProv);
            } else {
                notePlayers[i].Init(noteStreams[i]);
            } 
        }
    }
    public ParallelAudioPlayer(string basePath, int streams, int desiredLatency, bool isPanned) : this(basePath, streams, desiredLatency, true, isPanned) { }

    public virtual bool Play() {
        return Play(0);
    }

    public virtual bool Play(int channel) {
        if (!isEnabled) {
            return true;
        }
        for (int i = 0; i < streams; i++) {
            if (isPanned && i % numChannels != channel) {
                continue;
            }
            // check that the stream is available to play
            if (DateTime.Now - lastPlayedTimes[i] > noteStreams[i].TotalTime) {
                noteStreams[i].CurrentTime = TimeSpan.Zero;
                notePlayers[i].Play();
                this.lastPlayedStream = i;
                lastPlayedTimes[i] = DateTime.Now;
                return true;
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

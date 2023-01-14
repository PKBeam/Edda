using Edda.Const;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Diagnostics;
using System.IO;

public class ParallelAudioPlayer: IDisposable {
    const int numChannels = 4;
    const float maxPan = Audio.MaxPanDistance;
    int streams;
    int uniqueSamples;
    int lastPlayedStream;
    int desiredLatency;
    string basePath;
    DateTime[] lastPlayedTimes;
    AudioFileReader[] noteStreams;
    WasapiOut[] notePlayers;    
    public bool isEnabled { get; set; }
    public bool isPanned { get; set; }

    public ParallelAudioPlayer(string basePath, int streams, int desiredLatency, bool isEnabled, bool isPanned, float defaultVolume) {
        this.lastPlayedStream = 0;
        this.streams = streams;
        this.isEnabled = isEnabled;
        this.isPanned = isPanned;
        this.desiredLatency = desiredLatency;
        this.basePath = basePath;
        this.uniqueSamples = 0;
        while (File.Exists(GetFilePath(basePath, this.uniqueSamples + 1))) {
            this.uniqueSamples++;
        }
        if (uniqueSamples < 1) {
            throw new FileNotFoundException();
        }   
        InitAudioOut(defaultVolume);
    }

    public void InitAudioOut(float defaultVolume) {
        noteStreams = new AudioFileReader[streams];
        notePlayers = new WasapiOut[streams];
        lastPlayedTimes = new DateTime[streams];
        for (int i = 0; i < streams; i++) {
            noteStreams[i] = new AudioFileReader(GetFilePath(basePath, (i % numChannels % uniqueSamples) + 1)) {
                Volume = defaultVolume
            };
            notePlayers[i] = new WasapiOut(AudioClientShareMode.Shared, desiredLatency);
            if (isPanned && basePath != "mmatick") {
                var mono = new StereoToMonoSampleProvider(noteStreams[i]);
                if (basePath == "bassdrum") {
                    mono.LeftVolume = 1.0f;
                    mono.RightVolume = 0.0f;
                } else {
                    mono.LeftVolume = 0.5f;
                    mono.RightVolume = 0.5f;
                }

                var panProv = new PanningSampleProvider(mono);
                panProv.Pan = i % numChannels * 2 * maxPan / (numChannels - 1) - maxPan;
                notePlayers[i].Init(panProv);
            } else {
                notePlayers[i].Init(noteStreams[i]);
            }
        }
    }
    public ParallelAudioPlayer(string basePath, int streams, int desiredLatency, bool isPanned, float defaultVolume) : this(basePath, streams, desiredLatency, true, isPanned, defaultVolume) { }

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
            DateTime now = DateTime.Now;
            if (now - lastPlayedTimes[i] > noteStreams[i].TotalTime) {
                notePlayers[i].Pause();
                noteStreams[i].CurrentTime = TimeSpan.Zero;
                
                notePlayers[i].Play();
                this.lastPlayedStream = i;
                lastPlayedTimes[i] = now;
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
        return $"{Program.ResourcesPath}{basePath}{sampleNumber}.wav";
    }
}

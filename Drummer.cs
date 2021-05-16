using NAudio.Wave;
using System;
using System.IO;
using System.Diagnostics;

// todo: put more drum samples in?
public class Drummer : IDisposable {

	int streams;
	int uniqueSamples;
	int lastPlayed;
	AudioFileReader[] noteStreams;
	WaveOut[] notePlayers;

	public Drummer(string[] filePaths, int streams) {
		this.lastPlayed = 0;
		this.streams = streams;
		this.uniqueSamples = filePaths.Length;
		noteStreams = new AudioFileReader[streams];
		notePlayers = new WaveOut[streams];
		for (int i = 0; i < streams; i++) {
			noteStreams[i] = new AudioFileReader(filePaths[streams % uniqueSamples]);
			notePlayers[i] = new WaveOut();
			notePlayers[i].Volume = 1.0f;
			notePlayers[i].Init(noteStreams[i]);
        }
	}

	public bool playDrum() {
		for (int i = 0; i < streams; i++) {
			if (notePlayers[i].PlaybackState != PlaybackState.Playing && (i % uniqueSamples != lastPlayed % uniqueSamples)) {
				noteStreams[i].CurrentTime = new TimeSpan(0, 0, 0, 0, 0);
				notePlayers[i].Play();
				this.lastPlayed = i;
				return true;
			}
		}
		return false;
    }

	public void changeVolume(double vol) {
		for (int i = 0; i < streams; i++) {
			noteStreams[i].Volume = (float)Math.Max(Math.Abs(vol), 1);
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

using Edda.Const;
using NAudio.CoreAudioApi;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundTouch.Net.NAudioSupport;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Edda {
    public class SongPreviewController : IDisposable {

        // constructor variables
        MainWindow parentWindow;

        // used UI elements
        Button btnPlayPreview {
            get { return parentWindow.btnPlayPreview; }
        }
        Image imgPreviewButton {
            get { return parentWindow.imgPreviewButton; }
        }
        Slider sliderSongVol {
            get { return parentWindow.sliderSongVol; }
        }
        int editorAudioLatency {
            get { return parentWindow.editorAudioLatency; }
        }

        // audio playback state
        SampleChannel previewChannel;
        VorbisWaveReader previewStream;
        SoundTouchWaveStream previewTempoStream;
        WasapiOut previewPlayer;
        CancellationTokenSource previewPlaybackCancellationTokenSource;
        bool previewIsPlaying {
            set { btnPlayPreview.Tag = (value == false) ? 0 : 1; }
            get { return btnPlayPreview.Tag != null && (int)btnPlayPreview.Tag == 1; }
        }

        // constructor
        public SongPreviewController(
            MainWindow parentWindow
        ) {
            this.parentWindow = parentWindow;
        }

        public void Dispose() {
            // Clear the most memory-heavy components
            UnloadPreviewPlayer();
            UnloadPreviewStream();
            UnloadPreviewTempoStream();

            // Unbind references
            previewChannel = null;
            parentWindow = null;
        }

        private void UnloadPreviewPlayer() {
            var oldPreviewPlayer = previewPlayer;
            previewPlayer = null;
            oldPreviewPlayer?.Stop();
            oldPreviewPlayer?.Dispose();
        }

        private void UnloadPreviewStream() {
            var oldPreviewStream = previewStream;
            previewStream = null;
            oldPreviewStream?.Dispose();
        }

        private void UnloadPreviewTempoStream() {
            var oldPreviewTempoStream = previewTempoStream;
            previewTempoStream = null;
            oldPreviewTempoStream?.Dispose();
        }

        internal void LoadPreview(MapEditor mapEditor) {
            var previewPath = Path.Combine(mapEditor.mapFolder, BeatmapDefaults.PreviewFilename);
            try {
                previewStream = new VorbisWaveReader(previewPath);
                previewTempoStream = new SoundTouchWaveStream(previewStream);
                previewChannel = new SampleChannel(previewTempoStream);
                UpdateVolume();
                InitPreviewPlayer();
                EnablePreviewButton();
            } catch (Exception) {
                UnloadPreview();
            }
        }
        internal void InitPreviewPlayer() {
            var device = parentWindow.playbackDevice;
            if (device != null) {
                previewPlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, Audio.WASAPILatencyTarget);
                previewPlayer.Init(previewChannel);

                // subscribe to playbackstopped
                previewPlayer.PlaybackStopped += (sender, args) => { StopPreview(); };
            } else {
                previewPlayer = null;
            }
        }
        internal void UnloadPreview() {
            UnloadPreviewPlayer();
            UnloadPreviewStream();
            UnloadPreviewTempoStream();
            previewChannel = null;
            DisablePreviewButton();
        }
        internal void PlayPreview() {
            previewIsPlaying = true;

            // toggle button appearance
            imgPreviewButton.Source = Helper.BitmapGenerator("stopButton.png");

            // set seek position for preview on start
            previewStream.CurrentTime = TimeSpan.Zero;

            // play the preview
            if (editorAudioLatency == 0 || previewTempoStream.CurrentTime > new TimeSpan(0, 0, 0, 0, editorAudioLatency)) {
                previewTempoStream.CurrentTime = previewTempoStream.CurrentTime - new TimeSpan(0, 0, 0, 0, editorAudioLatency);
                previewPlayer?.Play();
            } else {
                previewTempoStream.CurrentTime = new TimeSpan(0);
                var oldPreviewPlaybackCancellationTokenSource = previewPlaybackCancellationTokenSource;
                previewPlaybackCancellationTokenSource = new();
                oldPreviewPlaybackCancellationTokenSource.Dispose();
                Task.Delay(new TimeSpan(0, 0, 0, 0, editorAudioLatency)).ContinueWith(o => {
                    if (!previewPlaybackCancellationTokenSource.IsCancellationRequested) {
                        previewPlayer?.Play();
                    }
                });
            }
        }
        internal void StopPreview() {
            if (!previewIsPlaying) {
                return;
            }
            previewIsPlaying = false;
            imgPreviewButton.Source = Helper.BitmapGenerator("playButton.png");

            previewPlayer?.Stop();
        }

        internal void TogglePreview() {
            if (!previewIsPlaying) {
                PlayPreview();
            } else {
                StopPreview();
            }
        }

        internal void Restart() {
            StopPreview();
            var oldPreviewPlayer = previewPlayer;
            InitPreviewPlayer();
            oldPreviewPlayer?.Dispose();
        }

        internal void UpdateVolume() {
            if (previewChannel != null) {
                previewChannel.Volume = (float)sliderSongVol.Value;
            }
        }

        internal void EnablePreviewButton() {
            if (previewPlayer != null) {
                btnPlayPreview.IsEnabled = true;
                imgPreviewButton.Opacity = 1;
            }
        }

        internal void DisablePreviewButton() {
            btnPlayPreview.IsEnabled = false;
            imgPreviewButton.Opacity = 0.5;
        }
    }
}
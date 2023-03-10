using NAudio.Vorbis;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System;
using System.IO;
using System.Threading;
using Edda.Const;
using Spectrogram;
using System.Threading.Channels;
using NAudio.Wave;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection.Metadata;

public class VorbisSpectrogramGenerator {

    private CancellationTokenSource tokenSource;
    private string filePath;
    private bool isDrawing;
    // Since the BMP generated for spectrogram doesn't depend on the component height/width FOR NOW, we can cache it to save on calculations.
    private ImageSource cachedSpectrogram;
    private string cachedBmpSpectrogramPath;

    public VorbisSpectrogramGenerator(string filePath) {
        RecreateTokens();
        this.filePath = filePath;
        var cacheDirectoryPath = Path.Combine(Path.GetDirectoryName(filePath), Program.CachePath);
        if (!Directory.Exists(cacheDirectoryPath)) {
            Directory.CreateDirectory(cacheDirectoryPath);
        }
        this.cachedBmpSpectrogramPath = Path.Combine(cacheDirectoryPath, Editor.Spectrogram.CachedBmpFilename);
        this.isDrawing = false;
    }
    public ImageSource Draw(double height, double width) {
        if (height == 0 || width == 0) {
            return null;
        }

        tokenSource.Cancel();
        while (isDrawing) {
            Thread.Sleep(100);
        }
        RecreateTokens();
        var largest = Math.Max(height, width);
        if (largest > Editor.Waveform.MaxDimension) {
            double scale = Editor.Waveform.MaxDimension / largest;
            height *= scale;
            width *= scale;
        }
        if (cachedSpectrogram == null) {
            try {
                // check for existing BMP first
                if (File.Exists(cachedBmpSpectrogramPath)) {
                    Bitmap bmp = (Bitmap) Bitmap.FromFile(cachedBmpSpectrogramPath);
                    cachedSpectrogram = TransformBitmap(bmp);
                } else {
                    cachedSpectrogram = _Draw(height, width, tokenSource.Token);
                }
            } catch (Exception ex) {
                isDrawing = false;
                Trace.WriteLine(ex);
            }
        }
        return cachedSpectrogram;
    }
    private ImageSource _Draw(double height, double width, CancellationToken ct) {
        isDrawing = true;
        VorbisWaveReader reader = new(filePath);
        int channels = reader.WaveFormat.Channels;
        var sampleRate = reader.WaveFormat.SampleRate;
        var bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
        var numSamples = reader.Length / bytesPerSample;
        
        var audioBuffer = new float[numSamples];
        reader.Read(audioBuffer, 0, (int)numSamples);
        reader.Dispose();

        var audioBufferDouble = Array.ConvertAll(audioBuffer, x => Editor.Spectrogram.AmplitudeScale * (double)x);

        // cancel task if required
        if (ct.IsCancellationRequested) {
            isDrawing = false;
            return null;
        }

        var fftSize = (int)Math.Pow(2, Editor.Spectrogram.FftSizeExp);
        var sg = new SpectrogramGenerator(sampleRate, fftSize: fftSize, stepSize: Editor.Spectrogram.StepSize, maxFreq: Editor.Spectrogram.MaxFreq);//, fixedWidth: (int)height);
        sg.Colormap = Colormap.Blues;
        sg.Add(audioBufferDouble);

        Bitmap bmp = sg.GetBitmapMel(melBinCount: Editor.Spectrogram.Width);
        bmp.Save(cachedBmpSpectrogramPath, ImageFormat.Png);
        sg = null;
        isDrawing = false;

        return TransformBitmap(bmp);
    }
    private ImageSource TransformBitmap(Bitmap bmp) {
        BitmapSource wpfBmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
           bmp.GetHbitmap(),
           IntPtr.Zero,
           Int32Rect.Empty,
           BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height)
        );
        isDrawing = false;

        var flipBmp = new TransformedBitmap(wpfBmp, new RotateTransform(-90));

        // need to freeze this otherwise it cannot be accessed
        flipBmp.Freeze();

        return flipBmp;
    }
    public void ClearCache() {
        cachedSpectrogram = null;
        if (File.Exists(cachedBmpSpectrogramPath)) {
            File.Delete(cachedBmpSpectrogramPath);
        }
    }

    private static void RenderTargetToDisk(RenderTargetBitmap input) {
        // https://stackoverflow.com/questions/13987408/convert-rendertargetbitmap-to-bitmapimage#13988871
        var bitmapEncoder = new PngBitmapEncoder();
        bitmapEncoder.Frames.Add(BitmapFrame.Create(input));

        // Save the image to a location on the disk.
        using (var fs = new FileStream("out.png", FileMode.Create)) {
            bitmapEncoder.Save(fs);
        }
    }
    private BitmapImage RenderTargetToImage(BitmapSource input) {
        // https://stackoverflow.com/questions/13987408/convert-rendertargetbitmap-to-bitmapimage#13988871
        var bitmapEncoder = new PngBitmapEncoder();
        bitmapEncoder.Frames.Add(BitmapFrame.Create(input));

        // Save the image to a location on the disk.
        //bitmapEncoder.Save(new System.IO.FileStream("out.png", System.IO.FileMode.Create));

        var bitmapImage = new BitmapImage();
        using (var stream = new MemoryStream()) {
            bitmapEncoder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
        }
        return bitmapImage;
    }
    private void RecreateTokens() {
        if (tokenSource != null) {
            tokenSource.Dispose();
        }
        tokenSource = new CancellationTokenSource();
    }
}
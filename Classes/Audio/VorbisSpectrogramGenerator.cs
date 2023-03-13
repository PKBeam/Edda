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
using DrawingColor = System.Drawing.Color;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection.Metadata;

public class VorbisSpectrogramGenerator {

    private CancellationTokenSource tokenSource;
    private string filePath;
    private SpectrogramType type;
    private int maxFreq;
    private string colormap;
    private bool isDrawing;
    // Since the BMP generated for spectrogram doesn't depend on the component height/width, we can cache it to save on calculations.
    private bool cache;
    private ImageSource cachedSpectrogram;
    private string cachedBmpSpectrogramPath;

    public VorbisSpectrogramGenerator(string filePath, bool cache, SpectrogramType? type, int? maxFreq, String colormap) {
        RecreateTokens();
        this.filePath = filePath;
        InitSettings(cache, type, maxFreq, colormap);
        this.isDrawing = false;
    }

    public void InitSettings(bool cache, SpectrogramType? type, int? maxFreq, String colormap) {
        this.cache = cache;
        this.type = type ?? SpectrogramType.Standard;
        this.maxFreq = maxFreq ?? Editor.Spectrogram.DefaultFreq;
        this.colormap = colormap ?? Colormap.Blues.Name;
        if (cache) {
            var cacheDirectoryPath = Path.Combine(Path.GetDirectoryName(filePath), Program.CachePath);
            if (!Directory.Exists(cacheDirectoryPath)) {
                Directory.CreateDirectory(cacheDirectoryPath);
            }
            this.cachedBmpSpectrogramPath = Path.Combine(cacheDirectoryPath, String.Format(Editor.Spectrogram.CachedBmpFilenameFormat, this.type, this.maxFreq, colormap));
        }
        this.cachedSpectrogram = null;
    }

    public DrawingColor GetBackgroundColor() {
        return Colormap.GetColormap(colormap).GetColor(0);
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
                if (cache && File.Exists(cachedBmpSpectrogramPath)) {
                    using (Bitmap bmp = (Bitmap) Bitmap.FromFile(cachedBmpSpectrogramPath)) {
                        cachedSpectrogram = TransformBitmap(bmp);
                    }
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

        var audioBufferDouble = Array.ConvertAll(audioBuffer, x => maxFreq * (double)x);

        // cancel task if required
        if (ct.IsCancellationRequested) {
            isDrawing = false;
            return null;
        }

        var fftSize = (int)Math.Pow(2, Editor.Spectrogram.FftSizeExp);
        var sg = new SpectrogramGenerator(sampleRate, fftSize: fftSize, stepSize: Editor.Spectrogram.StepSize, maxFreq: maxFreq);
        sg.Colormap = Colormap.GetColormap(colormap);
        sg.Add(audioBufferDouble);

        Bitmap bmp = null;
        switch (type) {
            case SpectrogramType.Standard:
                bmp = sg.GetBitmap();
                break;
            case SpectrogramType.MelScale:
                bmp = sg.GetBitmapMel(melBinCount: Editor.Spectrogram.MelBinCount);
                break;
            case SpectrogramType.MaxScale:
                bmp = sg.GetBitmapMax();
                break;
        }
        if (cache) {
            bmp.Save(cachedBmpSpectrogramPath, ImageFormat.Png);
        }
        sg = null;
        isDrawing = false;

        ImageSource b = TransformBitmap(bmp);
        bmp.Dispose();
        return b;
    }
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);
    private ImageSource TransformBitmap(Bitmap bmp) {
        // https://stackoverflow.com/questions/1546091/wpf-createbitmapsourcefromhbitmap-memory-leak
        IntPtr hBitmap = bmp.GetHbitmap();
        try {
            BitmapSource wpfBmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height)
            );
            isDrawing = false;

            var flipBmp = new TransformedBitmap(wpfBmp, new RotateTransform(-90));

            // need to freeze this otherwise it cannot be accessed
            flipBmp.Freeze();

            return flipBmp;
        } finally {
            DeleteObject(hBitmap);
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
        var oldTokenSource = tokenSource;
        tokenSource = new CancellationTokenSource();
        oldTokenSource?.Dispose();
    }

    public enum SpectrogramType {
        Standard = 0,
        MelScale = 1,
        MaxScale = 2
    }
}
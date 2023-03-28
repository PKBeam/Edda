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
using System.Runtime.InteropServices;

public class VorbisSpectrogramGenerator {

    private CancellationTokenSource tokenSource;
    private string filePath;
    // Settings
    private SpectrogramType type;
    private SpectrogramQuality quality;
    private int maxFreq;
    private string colormap;
    private bool drawFlipped;
    private bool isDrawing;
    // Since the BMP generated for spectrogram doesn't depend on the component height/width, we can cache it to save on calculations.
    private bool cache;
    private ImageSource[] cachedSpectrograms;
    private string cachedBmpSpectrogramSearchPattern;

    public VorbisSpectrogramGenerator(string filePath, bool cache, SpectrogramType? type, SpectrogramQuality? quality, int? maxFreq, String colormap, bool drawFlipped) {
        this.filePath = filePath;
        InitSettings(cache, type, quality, maxFreq, colormap, drawFlipped);
    }

    public void InitSettings(bool cache, SpectrogramType? type, SpectrogramQuality? quality, int? maxFreq, String colormap, bool drawFlipped) {
        tokenSource?.Cancel();
        this.isDrawing = false;
        RecreateTokens();
        this.cache = cache;
        this.type = type ?? SpectrogramType.Standard;
        this.quality = quality ?? SpectrogramQuality.Medium;
        this.maxFreq = maxFreq ?? Editor.Spectrogram.DefaultFreq;
        this.colormap = colormap ?? Colormap.Blues.Name;
        this.drawFlipped = drawFlipped;
        if (cache) {
            var cacheDirectoryPath = Path.Combine(Path.GetDirectoryName(filePath), Program.CachePath);
            if (!Directory.Exists(cacheDirectoryPath)) {
                Directory.CreateDirectory(cacheDirectoryPath);
            }
            this.cachedBmpSpectrogramSearchPattern = String.Format(Editor.Spectrogram.CachedBmpFilenameFormat, this.type, this.quality, this.maxFreq, colormap);
        }
        this.cachedSpectrograms = null;
    }

    public DrawingColor GetBackgroundColor() {
        return Colormap.GetColormap(colormap).GetColor(0);
    }

    public ImageSource[] Draw(int numChunks) {
        if (numChunks == 0) {
            return null;
        }

        tokenSource.Cancel();
        while (isDrawing) {
            Thread.Sleep(100);
        }
        RecreateTokens();

        if (cachedSpectrograms == null || cachedSpectrograms.Length != numChunks || cachedSpectrograms.Any(img => img == null)) {
            cachedSpectrograms = new ImageSource[numChunks];
            // check for existing BMP first
            var cacheDirectoryPath = Path.Combine(Path.GetDirectoryName(filePath), Program.CachePath);
            if (cache && Directory.Exists(cacheDirectoryPath)) {
                var bmpFiles = Directory.GetFiles(cacheDirectoryPath, cachedBmpSpectrogramSearchPattern);
                if (bmpFiles.Length == numChunks) {
                    for (int i = 0; i < numChunks; ++i) {
                        using (Bitmap bmp = (Bitmap) Bitmap.FromFile(bmpFiles[i])) {
                            cachedSpectrograms[i] = TransformBitmap(bmp);
                        }
                    }
                    return cachedSpectrograms;
                } else {
                    // Clear the cache from old files
                    foreach (var bmpFile in bmpFiles) {
                        File.Delete(bmpFile);
                    }
                }
            }
            // fallback to generating BMPs if not found
            try {
                cachedSpectrograms = _Draw(numChunks, tokenSource.Token);
            } catch (Exception ex) {
                isDrawing = false;
                Trace.WriteLine(ex);
            }
        }

        return cachedSpectrograms;
    }
    private ImageSource[] _Draw(int numChunks, CancellationToken ct) {
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
        var sg = new SpectrogramGenerator(sampleRate, fftSize: fftSize, stepSize: Editor.Spectrogram.StepSize * (int) quality, maxFreq: maxFreq);
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

        // cancel task if required
        if (ct.IsCancellationRequested) {
            isDrawing = false;
            bmp.Dispose();
            return null;
        }

        Bitmap[] splitBmps = SplitBitmapHorizontally(bmp, numChunks);

        if (ct.IsCancellationRequested) {
            isDrawing = false;
            for (int i = 0; i < numChunks; ++i) {
                splitBmps[i].Dispose();
            }
            bmp.Dispose();
            return null;
        }

        if (cache) {
            for (int i = 0; i < numChunks; ++i) {
                var cachedBmpSpectrogramPath = Path.Combine(Path.GetDirectoryName(filePath), Program.CachePath, cachedBmpSpectrogramSearchPattern.Replace("*", String.Format("{0:000}", i)));
                try {
                    splitBmps[i].Save(cachedBmpSpectrogramPath, ImageFormat.Png);
                } catch (ExternalException ex) {
                    Trace.WriteLine($"WARNING: Exception when saving spectrogram BMP: ({ex})");
                    File.Delete(cachedBmpSpectrogramPath);
                    // pretty annoying -> MessageBox.Show("Couldn't generate spectrogram due to song length - only songs up to 5 minutes with High quality spectrogram are supported.\n\nYou can try lowering the quality of the spectrogram or enabling chunking in Settings, which supports songs up to an hour with High quality, although takes longer to load.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
        }
        sg = null;
        isDrawing = false;

        ImageSource[] b = new ImageSource[numChunks];
        for (int i = 0; i < numChunks; ++i) {
            b[i] = TransformBitmap(splitBmps[i]);
            splitBmps[i].Dispose();
        }
        bmp.Dispose();
        return b;
    }

    private static Bitmap[] SplitBitmapHorizontally(Bitmap source, int numChunks) {
        Bitmap[] splitBmps = new Bitmap[numChunks];
        if (numChunks == 1) {
            // Skip the redraw if not needed
            splitBmps[0] = source;
        } else {
            for (int i = 0; i < numChunks; ++i) {
                var startPixel = source.Width * i / numChunks;
                var endPixel = source.Width * (i+1) / numChunks;
                Bitmap bmp = new Bitmap(endPixel - startPixel, source.Height);
                using (Graphics g = Graphics.FromImage(bmp)) {
                    g.DrawImage(source, 0, 0, new Rectangle(startPixel, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
                }
                splitBmps[i] = bmp;
            }
        }
        return splitBmps;
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

            TransformGroup transform = new();
            transform.Children.Add(new RotateTransform(-90));
            if (drawFlipped) {
                transform.Children.Add(new ScaleTransform(-1, 1));
            }
            var flipBmp = new TransformedBitmap(wpfBmp, transform);

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

    public enum SpectrogramQuality {
        Low = 4,
        Medium = 2,
        High = 1
    }
}
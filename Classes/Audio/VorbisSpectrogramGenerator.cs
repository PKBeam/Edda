using NAudio.Vorbis;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System;
using System.IO;
using System.Threading;
using Edda.Const;
using Spectrogram;
using DrawingColor = System.Drawing.Color;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class VorbisSpectrogramGenerator : IDisposable {

    private CancellationTokenSource tokenSource;
    private string filePath;
    // Settings
    private SpectrogramType type;
    private SpectrogramQuality quality;
    private int maxFreq;
    private string colormap;
    private bool drawFlipped;
    private bool isDrawing;
    private bool cache;
    // Since the BMP generated for spectrogram doesn't depend on the component height/width, we can cache it to save on calculations.
    private ImageSource[] cachedSpectrograms;
    private string cachedBmpSpectrogramSearchPattern;

    public VorbisSpectrogramGenerator(string filePath, bool cache, SpectrogramType? type, SpectrogramQuality? quality, int? maxFreq, String colormap, bool drawFlipped) {
        this.filePath = filePath;
        InitSettings(cache, type, quality, maxFreq, colormap, drawFlipped);
    }

    public void Dispose()
    {
        tokenSource?.Cancel();
        tokenSource = null;
        cachedSpectrograms = null;
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
            this.cachedBmpSpectrogramSearchPattern = String.Format(Editor.Spectrogram.CachedBmpFilenameFormat, this.type, this.quality, this.maxFreq, this.colormap);
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

        // if we have valid cache, don't bother doing anything
        if (!CacheIsValid(numChunks)) {
            cachedSpectrograms = new ImageSource[numChunks];
            // check for existing BMP files in the map cache folder first
            try {
                if (LoadMapCacheChunkFiles(numChunks)) {
                    return cachedSpectrograms;
                }
            } catch (Exception ex) {
                Trace.WriteLine(ex);
            }
            // fallback to generating BMPs if needed
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

        // We bail if we know that resulting BMPs would be too large to save anyway
        if (numSamples > numChunks * Editor.Spectrogram.MaxSampleSteps * Editor.Spectrogram.StepSize * (int)quality)
        {
            isDrawing = false;
            return null;
        }
        
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
                    // Tried putting up a message, but it's pretty annoying - pops up multiple times.
                    // MessageBox.Show("Couldn't generate spectrogram due to song length - only songs up to 5 minutes with High quality spectrogram are supported.\n\nYou can try lowering the quality of the spectrogram or enabling chunking in Settings, which supports songs up to an hour with High quality, although takes longer to load.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

    /// <summary>
    /// Splits source bitmap horizontally into chunks of (roughly) equal width.
    /// </summary>
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

    /// <summary>
    /// Checks if in-memory cache for the spectrogram chunks is still valid.
    /// </summary>
    private bool CacheIsValid(int numChunks) {
        return cachedSpectrograms != null && cachedSpectrograms.Length == numChunks && cachedSpectrograms.All(img => img != null);
    }

    /// <summary>
    /// Loads BMP chunks that were previously saved to cache folder, if user has enabled caching.
    /// In case the number of files is wrong, it clears them out for recreation.
    /// Return value indicates if the files were loaded.
    /// </summary>
    private bool LoadMapCacheChunkFiles(int numChunks) {
        var cacheDirectoryPath = Path.Combine(Path.GetDirectoryName(filePath), Program.CachePath);
        if (cache && Directory.Exists(cacheDirectoryPath)) {
            var bmpFiles = Directory.GetFiles(cacheDirectoryPath, cachedBmpSpectrogramSearchPattern);
            if (bmpFiles.Length == numChunks) {
                for (int i = 0; i < numChunks; ++i) {
                    using (Bitmap bmp = (Bitmap) Bitmap.FromFile(bmpFiles[i])) {
                        cachedSpectrograms[i] = TransformBitmap(bmp);
                    }
                }
                return true;
            } else {
                // Clear the cache from old files - either not all chunks were saved correctly or number of chunks changed in the meantime.
                foreach (var bmpFile in bmpFiles) {
                    File.Delete(bmpFile);
                }
            }
        }
        return false;
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

    /// <summary>
    /// Value defines multiplier for step size in the SpectrogramGenerator
    /// </summary>
    public enum SpectrogramQuality {
        Low = 4,
        Medium = 2,
        High = 1
    }
}
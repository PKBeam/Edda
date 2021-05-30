using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Windows.Media;
using System.IO;
using NAudio.Wave;

public class AudioVisualiser_Float32 {

    private CancellationTokenSource tokenSource;
    private CancellationToken token;
    private WaveStream reader;
    private bool isDrawing;

    public AudioVisualiser_Float32(WaveStream reader) {
        RecreateTokens();
        this.reader = reader;
        this.isDrawing = false;
    }

    public BitmapSource Draw(double height, double width, bool useGDI = false, double offsetStart = 0, double offsetEnd = 0) {
        var largest = Math.Max(height, width);
        if (largest > Constants.Editor.WaveformMaxDimension) {
            double scale = Constants.Editor.WaveformMaxDimension / largest;
            height *= scale;
            width *= scale;
        }
        return useGDI ? Draw_GDI(height, width) : Draw_WPF(height, width, offsetStart, offsetEnd);
    }
    private BitmapSource Draw_GDI(double height, double width) {
        tokenSource.Cancel();
        RecreateTokens();
        token = tokenSource.Token;
        while (isDrawing) { }
        return _Draw_GDI(token, height, width);
    }
    private BitmapSource Draw_WPF(double height, double width, double offsetStart = 0, double offsetEnd = 0) {
        tokenSource.Cancel();
        RecreateTokens();
        token = tokenSource.Token;
        while (isDrawing) { }
        return _Draw_WPF(token, height, width, offsetStart, offsetEnd);
    }

    // originally from https://stackoverflow.com/questions/2042155/high-quality-graph-waveform-display-component-in-c-sharp,
    // adapted to use the pcm_f32le format and replace GDI+ with WPF drawing
    private BitmapSource _Draw_GDI(CancellationToken ct, double height, double width) {
        isDrawing = true;
        reader.Position = 0;
        int bytesPerSample = (reader.WaveFormat.BitsPerSample / 8) * reader.WaveFormat.Channels;
        //Give a size to the bitmap; either a fixed size, or something based on the length of the audio
        System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap((int)width, (int)height);
        System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
        graphics.Clear(System.Drawing.Color.Transparent);
        System.Drawing.Pen bluePen = new System.Drawing.Pen(Constants.Editor.WaveformColourGDI);

        int samplesPerPixel = (int)(reader.Length / (double)(height * bytesPerSample));
        int bytesPerPixel = bytesPerSample * samplesPerPixel;
        int bytesRead;
        byte[] waveData = new byte[bytesPerPixel];
        // draw each pixel of height
        for (int y = 0; y < height; y++) {
            bytesRead = reader.Read(waveData, 0, bytesPerPixel);
            if (bytesRead == 0)
                break;

            float low = 0;
            float high = 0;
            // read all samples for this pixel and take the extreme values
            for (int n = 0; n < bytesRead; n += bytesPerSample) {
                float sample = BitConverter.ToSingle(waveData, n);
                if (sample < low) {
                    low = sample;
                }
                if (sample > high) {
                    high = sample;
                }
                if (ct.IsCancellationRequested) {
                    isDrawing = false;
                    return null;
                }
            }
            float lowPercent = (low + 1) / 2;
            float highPercent = (high + 1) / 2;
            float lowValue = (float)width * lowPercent;
            float highValue = (float)width * highPercent;
            graphics.DrawLine(bluePen, lowValue, (int)height - y, highValue, (int)height - y);
        }
        //bitmap.Save("out.bmp");
        // https://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap#1069509
        BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight((int)width, (int)height));
        b.Freeze();
        isDrawing = false;
        return b;
    }
    private BitmapSource _Draw_WPF(CancellationToken ct, double height, double width, double offsetStart = 0, double offsetEnd = 0) {
        if (offsetEnd == 0) {
            offsetEnd = height;
        }
        isDrawing = true;
        reader.Seek(0, SeekOrigin.Begin);
        int bytesPerSample = reader.WaveFormat.BitsPerSample / 8 * reader.WaveFormat.Channels;
        DrawingVisual dv = new DrawingVisual();
        DrawingContext dc = dv.RenderOpen();
        Pen bluePen = new Pen(new SolidColorBrush(Constants.Editor.WaveformColourWPF), Constants.Editor.WaveformThicknessWPF);
        bluePen.Freeze();

        int samplesPerPixel = (int)(reader.Length / (double)(height * bytesPerSample));
        int bytesPerPixel = bytesPerSample * samplesPerPixel;
        int bytesRead;
        byte[] waveData = new byte[bytesPerPixel];
        // draw each pixel of height
        for (int y = 0; y <= height; y++) {
            bytesRead = reader.Read(waveData, 0, bytesPerPixel);
            if (bytesRead == 0)
                break;
            if (y < offsetStart) {
                continue;
            }
            if (y > offsetEnd) {
                break;
            }
            float low = 0;
            float high = 0;
            // read all samples for this pixel and take the extreme values
            for (int n = 0; n < bytesRead; n += bytesPerSample) {
                float sample = BitConverter.ToSingle(waveData, n);
                if (sample < low) {
                    low = sample;
                }
                if (sample > high) {
                    high = sample;
                }
                if (ct.IsCancellationRequested) {
                    isDrawing = false;
                    return null;
                }
            }
            float lowPercent = (low + 1) / 2;
            float highPercent = (high + 1) / 2;
            float lowValue = (float)width * lowPercent;
            float highValue = (float)width * highPercent;
            dc.DrawLine(bluePen, new Point(lowValue, (int)(offsetEnd - offsetStart - y)), new Point(highValue, (int)(offsetEnd - offsetStart - y)));
        }
        dc.Close();
        RenderTargetBitmap bmp = new RenderTargetBitmap((int)width, (int)(offsetEnd - offsetStart), 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();

        //Trace.WriteLine("Draw complete");
        isDrawing = false;

        // program crashes with UCEERR_RENDERTHREADFAILURE if this isnt converted to a BitmapImage
        // https://github.com/dotnet/wpf/issues/3100
        return RenderTargetToImage(bmp);
    }

    private BitmapImage RenderTargetToImage(RenderTargetBitmap input) {
        DateTime start = DateTime.Now;
        // https://stackoverflow.com/questions/13987408/convert-rendertargetbitmap-to-bitmapimage#13988871
        var bitmapEncoder = new PngBitmapEncoder();
        bitmapEncoder.Frames.Add(BitmapFrame.Create(input));

        // Save the image to a location on the disk.
        //bitmapEncoder.Save(new System.IO.FileStream("out.png", System.IO.FileMode.Create));

        var stream = new MemoryStream();
        bitmapEncoder.Save(stream);
        stream.Seek(0, SeekOrigin.Begin);

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = stream;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }
    private void RecreateTokens() {
        tokenSource = new CancellationTokenSource();
        token = tokenSource.Token;
    }
}
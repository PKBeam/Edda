using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Windows.Media;
using System.IO;
using System.Windows.Threading;

public class AudioWaveformDrawerF32: DispatcherObject {
    private readonly double maxDimension = 50000;
    private readonly Color waveformColour = Color.FromArgb(180, 0, 0, 255);
    private CancellationTokenSource tokenSource;
    private CancellationToken token;
    private WaveStream reader;
    private bool isDrawing;
    private DrawingVisual dv;
    private DrawingContext dc;
    public AudioWaveformDrawerF32(WaveStream reader) {
        recreateTokens();
        this.reader = reader;
        this.isDrawing = false;
    }

    public BitmapSource draw(double height, double width) {
        var largest = Math.Max(height, width);
        if (largest > maxDimension) {
            double scale = (maxDimension / largest);
            height *= scale;
            width *= scale;
        }
        return drawWPF(height, width);
    }
    private BitmapSource drawGDI(double height, double width) {
        tokenSource.Cancel();
        recreateTokens();
        token = tokenSource.Token;
        while (isDrawing) { }
        return _drawGDI(token, height, width);
    }
    private BitmapSource drawWPF(double height, double width) {
        tokenSource.Cancel();
        recreateTokens();
        token = tokenSource.Token;
        while (isDrawing) { }
        return _drawWPF(token, height, width);
    }

    // originally from https://stackoverflow.com/questions/2042155/high-quality-graph-waveform-display-component-in-c-sharp,
    // adapted to use the pcm_f32le format and replace GDI+ with WPF drawing
    private BitmapSource _drawGDI(CancellationToken ct, double height, double width) {
        isDrawing = true;
        reader.Position = 0;
        int bytesPerSample = (reader.WaveFormat.BitsPerSample / 8) * reader.WaveFormat.Channels;
        //Give a size to the bitmap; either a fixed size, or something based on the length of the audio
        System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap((int)width, (int)height);
        System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.Transparent);
        System.Drawing.Pen bluePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 0, 0, 255));

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
        //Bitmap bt = new Bitmap(bitmap);
        //bt.Save("out.bmp");
        // https://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap#1069509
        BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight((int)width, (int)height));
        b.Freeze();
        isDrawing = false;
        return b;
    }
    private BitmapSource _drawWPF(CancellationToken ct, double height, double width) {
        isDrawing = true;
        reader.Position = 0;
        int bytesPerSample = (reader.WaveFormat.BitsPerSample / 8) * reader.WaveFormat.Channels;
        DrawingVisual dv = new DrawingVisual();
        DrawingContext dc = dv.RenderOpen();
        Pen bluePen = new Pen(new SolidColorBrush(waveformColour), 1);
        bluePen.Freeze();

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
            dc.DrawLine(bluePen, new Point(lowValue, (int)height - y), new Point(highValue, (int)height - y));
        }
        dc.Close();
        RenderTargetBitmap bmp = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();

        // Save the image to a location on the disk.
        //PngBitmapEncoder encoder = new PngBitmapEncoder();
        //encoder.Frames.Add(BitmapFrame.Create(bmp));
        //encoder.Save(new System.IO.FileStream("out.png", System.IO.FileMode.Create));
        //Trace.WriteLine("Draw complete");
        isDrawing = false;

        // program crashes with UCEERR_RENDERTHREADFAILURE if this isnt converted to a BitmapImage
        // https://github.com/dotnet/wpf/issues/3100
        return renderTargetToImage(bmp);
    }

    private BitmapImage renderTargetToImage(RenderTargetBitmap input) {
        DateTime start = DateTime.Now;
        // https://stackoverflow.com/questions/13987408/convert-rendertargetbitmap-to-bitmapimage#13988871
        var bitmapEncoder = new PngBitmapEncoder();
        bitmapEncoder.Frames.Add(BitmapFrame.Create(input));

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
    private void recreateTokens() {
        tokenSource = new CancellationTokenSource();
        token = tokenSource.Token;
    }
}
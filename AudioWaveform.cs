using NAudio.Wave;
using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media.Imaging;

public partial class AudioWaveform {
    // originally from https://stackoverflow.com/questions/2042155/high-quality-graph-waveform-display-component-in-c-sharp,
    // adapted to use pcm_f32le WAV files
    public static BitmapSource createf32(WaveStream reader, double height, double width) {
        reader.Position = 0;
        int bytesPerSample = (reader.WaveFormat.BitsPerSample / 8) * reader.WaveFormat.Channels;
        //Give a size to the bitmap; either a fixed size, or something based on the length of the audio
        Bitmap bitmap = new Bitmap((int)width, (int)height);
        Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        Pen bluePen = new Pen(Color.Blue);

        int samplesPerPixel = (int)(reader.Length / (double)(height * bytesPerSample));
        int bytesPerPixel = bytesPerSample * samplesPerPixel;
        int bytesRead;
        byte[] waveData = new byte[bytesPerPixel];
        List<(float, float)> samples = new List<(float, float)>();
        for (int y = 0; y < height; y++) {
            bytesRead = reader.Read(waveData, 0, bytesPerPixel);
            if (bytesRead == 0)
                break;

            float low = 0;
            float high = 0;
            for (int n = 0; n < bytesRead; n += bytesPerSample) {
                float sample = BitConverter.ToSingle(waveData, n);
                if (sample < low) {
                    low = sample;
                }
                if (sample > high) {
                    high = sample;
                }
            }
            float lowPercent = (low + 1) / 2;
            float highPercent = (high + 1) / 2;
            float lowValue = (float)width * lowPercent;
            float highValue = (float)width * highPercent;
            graphics.DrawLine(bluePen, lowValue, (int)height - y, highValue, (int)height - y);
        }
        bitmap.Save("out.bmp");
        // https://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap#1069509
        BitmapSource b = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight((int)width, (int)height));
        return b;
    }
}
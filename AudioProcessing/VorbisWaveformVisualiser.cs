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

public class VorbisWaveformVisualiser {

	private CancellationTokenSource tokenSource;
	private VorbisWaveReader reader;
	private bool isDrawing;
	public VorbisWaveformVisualiser(VorbisWaveReader reader) {
		RecreateTokens();
		this.reader = reader;
		this.isDrawing = false;
	}
	public BitmapImage Draw(double height, double width) {
		tokenSource.Cancel();
		while (isDrawing) {
			Thread.Sleep(100);
		}
		RecreateTokens();
		var largest = Math.Max(height, width);
		if (largest > Const.Editor.Waveform.MaxDimension) {
			double scale = Const.Editor.Waveform.MaxDimension / largest;
			height *= scale;
			width *= scale;
		}
		return _Draw(height, width, tokenSource.Token);
	}
	private BitmapImage _Draw(double height, double width, CancellationToken ct) {
		isDrawing = true;
		 reader.Position = 0;
		DrawingVisual dv = new DrawingVisual();
		using (DrawingContext dc = dv.RenderOpen()) {
			Pen bluePen = new Pen(new SolidColorBrush(Const.Editor.Waveform.ColourWPF), Const.Editor.Waveform.ThicknessWPF);
			bluePen.Freeze();

			int channels = reader.WaveFormat.Channels;
			var bytesPerSample = reader.WaveFormat.BitsPerSample / 8 * channels;
			var numSamples = reader.Length / bytesPerSample;

			int samplesPerPixel = (int)(numSamples / height) * channels;
			double samplesPerPixel_d = numSamples / height * channels;
			int totalSamples = 0;
			double totalSamples_d = 0;

			var buffer = new float[samplesPerPixel + channels];
			for (int pixel = 0; pixel < height; pixel++) {

				// read samples
				int samplesRead = reader.Read(buffer, 0, samplesPerPixel);
				if (samplesRead == 0) {
					break;
				}

				// correct floating point rounding errors
				totalSamples += samplesPerPixel;
				totalSamples_d += samplesPerPixel_d;
				if (totalSamples_d - totalSamples > channels) {
					totalSamples += channels;
					reader.Read(buffer, samplesPerPixel, channels);
				}

				var samples = new List<float>(buffer);
				samples.Sort();
				float lowPercent = (samples[(int)((samples.Count - 1) * 0.05)] + 1) / 2;
				float highPercent = (samples[(int)((samples.Count - 1) * 0.95)] + 1) / 2;
				float lowValue = (float)width * lowPercent;
				float highValue = (float)width * highPercent;
				dc.DrawLine(
					bluePen,
					new Point(lowValue, (int)(height - pixel)),
					new Point(highValue, (int)(height - pixel))
				);

				// cancel task if required
				if (ct.IsCancellationRequested) {
					isDrawing = false;
					return null;
				}
			}
		}
		RenderTargetBitmap bmp = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
		bmp.Render(dv);
		bmp.Freeze();
		//RenderTargetToDisk(bmp);
		isDrawing = false;
		return RenderTargetToImage(bmp);
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
	private BitmapImage RenderTargetToImage(RenderTargetBitmap input) {
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
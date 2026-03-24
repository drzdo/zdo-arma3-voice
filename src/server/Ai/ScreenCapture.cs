using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ZdoArmaVoice.Server.Ai;

[SupportedOSPlatform("windows")]
public class ScreenCapture : IScreenCapture
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public Task<LlmImage?> CaptureAsync()
    {
        try
        {
            var desktop = GetDesktopWindow();
            GetWindowRect(desktop, out var rect);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            }

            // Resize to max 1280px wide to keep tokens reasonable
            var maxWidth = 1280;
            Bitmap final;
            if (width > maxWidth)
            {
                var scale = (float)maxWidth / width;
                var newHeight = (int)(height * scale);
                final = new Bitmap(bmp, new Size(maxWidth, newHeight));
            }
            else
            {
                final = bmp;
            }

            using (final == bmp ? null : final)
            {
                using var ms = new MemoryStream();
                var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 80L);
                final.Save(ms, jpegEncoder, encoderParams);

                var base64 = Convert.ToBase64String(ms.ToArray());
                Log.Info("ScreenCapture", $"Captured {final.Width}x{final.Height}, {ms.Length / 1024}KB JPEG");
                return Task.FromResult<LlmImage?>(new LlmImage(base64, "image/jpeg"));
            }
        }
        catch (Exception ex)
        {
            Log.Error("ScreenCapture", $"Capture failed: {ex.Message}");
            return Task.FromResult<LlmImage?>(null);
        }
    }
}

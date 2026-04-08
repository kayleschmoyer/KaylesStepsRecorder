using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.Capture;

/// <summary>
/// Provides screen and window capture, thumbnail generation, click highlighting,
/// and region redaction. All images are saved as PNG.
/// <para>
/// This class depends on System.Drawing.Common, which requires the following
/// NuGet package reference in the .csproj if not already present:
/// <c>&lt;PackageReference Include="System.Drawing.Common" Version="8.*" /&gt;</c>
/// </para>
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<ScreenCaptureService> _logger;
    private readonly WindowCaptureService _windowCaptureService = new();

    public ScreenCaptureService(ILogger<ScreenCaptureService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string?> CaptureWindowAsync(
        WindowInfo window,
        string outputDirectory,
        string filePrefix)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);

        return await Task.Run(() =>
        {
            try
            {
                using var bitmap = _windowCaptureService.CaptureWindow(window.Handle);
                if (bitmap is null)
                {
                    _logger.LogWarning(
                        "Window capture returned null for handle {Handle} ({Title})",
                        window.Handle, window.Title);
                    return null;
                }

                string filePath = BuildOutputPath(outputDirectory, filePrefix);
                SaveAsPng(bitmap, filePath);

                _logger.LogDebug(
                    "Captured window {Title} ({Width}x{Height}) to {Path}",
                    window.Title, bitmap.Width, bitmap.Height, filePath);

                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to capture window {Handle} ({Title})",
                    window.Handle, window.Title);
                return null;
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> CaptureFullScreenAsync(
        int screenX,
        int screenY,
        string outputDirectory,
        string filePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);

        return await Task.Run(() =>
        {
            try
            {
                // Determine which monitor contains the given point.
                var point = new NativeMethods.POINT { X = screenX, Y = screenY };
                IntPtr hMonitor = NativeMethods.MonitorFromPoint(
                    point, NativeMethods.MONITOR_DEFAULTTONEAREST);

                if (hMonitor == IntPtr.Zero)
                {
                    _logger.LogWarning(
                        "MonitorFromPoint returned null for ({X}, {Y})", screenX, screenY);
                    return null;
                }

                var monitorInfo = new NativeMethods.MONITORINFO
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
                };

                if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    _logger.LogWarning("GetMonitorInfo failed for monitor at ({X}, {Y})", screenX, screenY);
                    return null;
                }

                var monitorRect = monitorInfo.rcMonitor;
                int width = monitorRect.Width;
                int height = monitorRect.Height;

                if (width <= 0 || height <= 0)
                {
                    _logger.LogWarning(
                        "Monitor has invalid dimensions ({Width}x{Height})", width, height);
                    return null;
                }

                using var bitmap = CaptureScreenRegion(
                    monitorRect.Left, monitorRect.Top, width, height);

                if (bitmap is null)
                {
                    return null;
                }

                string filePath = BuildOutputPath(outputDirectory, filePrefix);
                SaveAsPng(bitmap, filePath);

                _logger.LogDebug(
                    "Captured full screen ({Width}x{Height}) at monitor containing ({X}, {Y}) to {Path}",
                    width, height, screenX, screenY, filePath);

                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to capture full screen at ({X}, {Y})", screenX, screenY);
                return null;
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> CaptureRegionAsync(
        int x, int y, int width, int height,
        string outputDirectory,
        string filePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);

        if (width <= 0 || height <= 0)
        {
            _logger.LogWarning("Invalid capture region dimensions ({Width}x{Height})", width, height);
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                using var bitmap = CaptureScreenRegion(x, y, width, height);
                if (bitmap is null)
                {
                    return null;
                }

                string filePath = BuildOutputPath(outputDirectory, filePrefix);
                SaveAsPng(bitmap, filePath);

                _logger.LogDebug(
                    "Captured region ({X}, {Y}, {Width}x{Height}) to {Path}",
                    x, y, width, height, filePath);

                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to capture region ({X}, {Y}, {Width}x{Height})",
                    x, y, width, height);
                return null;
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public string? CreateThumbnail(string sourcePath, int maxWidth = 320)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (maxWidth <= 0)
        {
            _logger.LogWarning("Invalid thumbnail maxWidth {MaxWidth}", maxWidth);
            return null;
        }

        try
        {
            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning("Source image not found: {Path}", sourcePath);
                return null;
            }

            using var source = new Bitmap(sourcePath);

            // If the source is already at or below the target width, just copy it.
            if (source.Width <= maxWidth)
            {
                string copyPath = BuildThumbnailPath(sourcePath);
                File.Copy(sourcePath, copyPath, overwrite: true);
                return copyPath;
            }

            double ratio = (double)maxWidth / source.Width;
            int thumbWidth = maxWidth;
            int thumbHeight = (int)Math.Round(source.Height * ratio);

            if (thumbHeight <= 0)
            {
                thumbHeight = 1;
            }

            using var thumbnail = new Bitmap(thumbWidth, thumbHeight, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(thumbnail))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawImage(source, 0, 0, thumbWidth, thumbHeight);
            }

            string thumbnailPath = BuildThumbnailPath(sourcePath);
            SaveAsPng(thumbnail, thumbnailPath);

            _logger.LogDebug(
                "Created thumbnail ({Width}x{Height}) from {Source} to {Dest}",
                thumbWidth, thumbHeight, sourcePath, thumbnailPath);

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create thumbnail for {Path}", sourcePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> AddClickHighlightAsync(
        string imagePath,
        int relativeX,
        int relativeY,
        int radius = 20)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    _logger.LogWarning("Image not found for click highlight: {Path}", imagePath);
                    return null;
                }

                using var bitmap = new Bitmap(imagePath);
                using var graphics = Graphics.FromImage(bitmap);

                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Semi-transparent red fill.
                using var fillBrush = new SolidBrush(Color.FromArgb(80, 255, 0, 0));
                // Solid red outline.
                using var outlinePen = new Pen(Color.FromArgb(200, 255, 0, 0), 2.0f);

                int diameter = radius * 2;
                var ellipseRect = new Rectangle(
                    relativeX - radius,
                    relativeY - radius,
                    diameter,
                    diameter);

                graphics.FillEllipse(fillBrush, ellipseRect);
                graphics.DrawEllipse(outlinePen, ellipseRect);

                // Draw a small crosshair at the click center for precision.
                int crosshairSize = Math.Max(4, radius / 3);
                using var crosshairPen = new Pen(Color.FromArgb(220, 255, 0, 0), 1.5f);
                graphics.DrawLine(crosshairPen,
                    relativeX - crosshairSize, relativeY,
                    relativeX + crosshairSize, relativeY);
                graphics.DrawLine(crosshairPen,
                    relativeX, relativeY - crosshairSize,
                    relativeX, relativeY + crosshairSize);

                // Overwrite the original image with the highlight.
                SaveAsPng(bitmap, imagePath);

                _logger.LogDebug(
                    "Added click highlight at ({X}, {Y}) with radius {Radius} on {Path}",
                    relativeX, relativeY, radius, imagePath);

                return imagePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to add click highlight at ({X}, {Y}) on {Path}",
                    relativeX, relativeY, imagePath);
                return null;
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> RedactRegionAsync(
        string imagePath,
        int x, int y, int width, int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        if (width <= 0 || height <= 0)
        {
            _logger.LogWarning("Invalid redaction dimensions ({Width}x{Height})", width, height);
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    _logger.LogWarning("Image not found for redaction: {Path}", imagePath);
                    return null;
                }

                using var bitmap = new Bitmap(imagePath);
                using var graphics = Graphics.FromImage(bitmap);

                // Clamp the redaction region to the image boundaries.
                int clampedX = Math.Max(0, x);
                int clampedY = Math.Max(0, y);
                int clampedRight = Math.Min(bitmap.Width, x + width);
                int clampedBottom = Math.Min(bitmap.Height, y + height);
                int clampedWidth = clampedRight - clampedX;
                int clampedHeight = clampedBottom - clampedY;

                if (clampedWidth <= 0 || clampedHeight <= 0)
                {
                    _logger.LogWarning(
                        "Redaction region ({X}, {Y}, {Width}x{Height}) is outside image bounds ({ImgW}x{ImgH})",
                        x, y, width, height, bitmap.Width, bitmap.Height);
                    return null;
                }

                var redactRect = new Rectangle(clampedX, clampedY, clampedWidth, clampedHeight);
                using var brush = new SolidBrush(Color.Black);
                graphics.FillRectangle(brush, redactRect);

                // Overwrite the original image with the redaction applied.
                SaveAsPng(bitmap, imagePath);

                _logger.LogDebug(
                    "Redacted region ({X}, {Y}, {Width}x{Height}) on {Path}",
                    clampedX, clampedY, clampedWidth, clampedHeight, imagePath);

                return imagePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to redact region ({X}, {Y}, {Width}x{Height}) on {Path}",
                    x, y, width, height, imagePath);
                return null;
            }
        }).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Captures an arbitrary rectangular region of the screen using BitBlt.
    /// </summary>
    private Bitmap? CaptureScreenRegion(int x, int y, int width, int height)
    {
        IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
        {
            _logger.LogWarning("GetDC(desktop) returned null");
            return null;
        }

        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;

        try
        {
            hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero)
            {
                _logger.LogWarning("CreateCompatibleDC failed");
                return null;
            }

            hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, width, height);
            if (hBitmap == IntPtr.Zero)
            {
                _logger.LogWarning("CreateCompatibleBitmap failed for {Width}x{Height}", width, height);
                return null;
            }

            hOldBitmap = NativeMethods.SelectObject(hdcMem, hBitmap);

            bool success = NativeMethods.BitBlt(
                hdcMem,
                0, 0,
                width, height,
                hdcScreen,
                x, y,
                NativeMethods.SRCCOPY);

            if (!success)
            {
                _logger.LogWarning("BitBlt failed for region ({X}, {Y}, {Width}x{Height})", x, y, width, height);
                return null;
            }

            // Deselect the bitmap before creating the managed Bitmap.
            NativeMethods.SelectObject(hdcMem, hOldBitmap);
            hOldBitmap = IntPtr.Zero;

            return Image.FromHbitmap(hBitmap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during screen region capture");
            return null;
        }
        finally
        {
            if (hOldBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(hdcMem, hOldBitmap);
            }
            if (hBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(hBitmap);
            }
            if (hdcMem != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(hdcMem);
            }
            NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    /// <summary>
    /// Saves a Bitmap as a PNG file. Ensures the output directory exists.
    /// </summary>
    private static void SaveAsPng(Bitmap bitmap, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        bitmap.Save(filePath, ImageFormat.Png);
    }

    /// <summary>
    /// Builds a unique output file path with a timestamp to avoid collisions.
    /// </summary>
    private static string BuildOutputPath(string outputDirectory, string filePrefix)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        string fileName = $"{filePrefix}_{timestamp}.png";
        return Path.Combine(outputDirectory, fileName);
    }

    /// <summary>
    /// Builds a thumbnail file path by inserting "_thumb" before the extension.
    /// </summary>
    private static string BuildThumbnailPath(string sourcePath)
    {
        string directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        string nameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = Path.GetExtension(sourcePath);
        return Path.Combine(directory, $"{nameWithoutExt}_thumb{extension}");
    }
}

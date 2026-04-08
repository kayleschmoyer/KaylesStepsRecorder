using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KaylesStepsRecorder.Capture;

/// <summary>
/// Captures individual windows using PrintWindow (preferred) or BitBlt (fallback).
/// Handles DPI scaling and DWM-composited window bounds.
/// </summary>
internal sealed class WindowCaptureService
{
    /// <summary>
    /// Captures the specified window and returns a Bitmap, or null if the window
    /// is minimized, invisible, or cannot be captured.
    /// </summary>
    /// <param name="hwnd">Handle to the window to capture.</param>
    /// <returns>A Bitmap of the window contents, or null on failure.</returns>
    public Bitmap? CaptureWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        // Skip minimized or invisible windows.
        if (NativeMethods.IsIconic(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
        {
            return null;
        }

        // Try to get the DWM extended frame bounds, which excludes the invisible
        // resize borders / drop-shadow region added by Windows 10+.
        if (!TryGetWindowBounds(hwnd, out var bounds))
        {
            return null;
        }

        int width = bounds.Width;
        int height = bounds.Height;

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        // Attempt capture with PrintWindow first (works for DWM-composited / off-screen windows).
        Bitmap? bitmap = CaptureWithPrintWindow(hwnd, width, height);

        if (bitmap is null)
        {
            // Fall back to BitBlt-based capture (works for most on-screen windows).
            bitmap = CaptureWithBitBlt(hwnd, bounds);
        }

        return bitmap;
    }

    /// <summary>
    /// Determines the actual visible bounds of the window, preferring the DWM extended
    /// frame bounds over GetWindowRect so that invisible borders are excluded.
    /// </summary>
    private static bool TryGetWindowBounds(IntPtr hwnd, out NativeMethods.RECT bounds)
    {
        // DwmGetWindowAttribute with DWMWA_EXTENDED_FRAME_BOUNDS gives us the rendered
        // bounds without the invisible resize borders on Windows 10+.
        int hr = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out bounds,
            Marshal.SizeOf<NativeMethods.RECT>());

        if (hr == 0 && bounds.Width > 0 && bounds.Height > 0)
        {
            return true;
        }

        // Fall back to GetWindowRect if DWM is unavailable.
        return NativeMethods.GetWindowRect(hwnd, out bounds) && bounds.Width > 0 && bounds.Height > 0;
    }

    /// <summary>
    /// Captures the window using PrintWindow with PW_RENDERFULLCONTENT.
    /// This method works even when the window is partially or fully occluded.
    /// </summary>
    private static Bitmap? CaptureWithPrintWindow(IntPtr hwnd, int width, int height)
    {
        // PrintWindow renders into a DC at the window's own coordinates, so we need
        // the full window rect (not the DWM extended frame) to determine the bitmap size.
        if (!NativeMethods.GetWindowRect(hwnd, out var windowRect))
        {
            return null;
        }

        int bmpWidth = windowRect.Width;
        int bmpHeight = windowRect.Height;

        if (bmpWidth <= 0 || bmpHeight <= 0)
        {
            return null;
        }

        Bitmap bitmap = new(bmpWidth, bmpHeight, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        IntPtr hdc = graphics.GetHdc();

        try
        {
            bool success = NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT);
            if (!success)
            {
                bitmap.Dispose();
                return null;
            }
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        // If the DWM extended frame bounds are smaller than the full window rect,
        // crop the bitmap to exclude the invisible borders.
        if (!NativeMethods.GetWindowRect(hwnd, out var fullRect))
        {
            return bitmap;
        }

        int hr = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out var extendedBounds,
            Marshal.SizeOf<NativeMethods.RECT>());

        if (hr != 0 || extendedBounds.Width <= 0 || extendedBounds.Height <= 0)
        {
            return bitmap;
        }

        // Calculate the offset of the extended frame within the full window rect.
        int cropLeft = extendedBounds.Left - fullRect.Left;
        int cropTop = extendedBounds.Top - fullRect.Top;
        int cropWidth = extendedBounds.Width;
        int cropHeight = extendedBounds.Height;

        // Only crop if there is actually a difference.
        if (cropLeft == 0 && cropTop == 0 && cropWidth == bmpWidth && cropHeight == bmpHeight)
        {
            return bitmap;
        }

        // Clamp to bitmap dimensions.
        cropLeft = Math.Max(0, cropLeft);
        cropTop = Math.Max(0, cropTop);
        cropWidth = Math.Min(cropWidth, bmpWidth - cropLeft);
        cropHeight = Math.Min(cropHeight, bmpHeight - cropTop);

        if (cropWidth <= 0 || cropHeight <= 0)
        {
            return bitmap;
        }

        try
        {
            Bitmap cropped = bitmap.Clone(
                new Rectangle(cropLeft, cropTop, cropWidth, cropHeight),
                bitmap.PixelFormat);
            bitmap.Dispose();
            return cropped;
        }
        catch
        {
            // If cloning fails for any reason, return the original uncropped bitmap.
            return bitmap;
        }
    }

    /// <summary>
    /// Captures the window using BitBlt from the screen DC. This is the traditional
    /// approach and works well for on-screen windows but cannot capture occluded content.
    /// </summary>
    private static Bitmap? CaptureWithBitBlt(IntPtr hwnd, NativeMethods.RECT bounds)
    {
        int width = bounds.Width;
        int height = bounds.Height;

        IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
        {
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
                return null;
            }

            hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, width, height);
            if (hBitmap == IntPtr.Zero)
            {
                return null;
            }

            hOldBitmap = NativeMethods.SelectObject(hdcMem, hBitmap);

            bool success = NativeMethods.BitBlt(
                hdcMem,
                0, 0,
                width, height,
                hdcScreen,
                bounds.Left, bounds.Top,
                NativeMethods.SRCCOPY);

            if (!success)
            {
                return null;
            }

            // Restore the old bitmap before creating the managed Bitmap.
            NativeMethods.SelectObject(hdcMem, hOldBitmap);
            hOldBitmap = IntPtr.Zero;

            Bitmap bitmap = Image.FromHbitmap(hBitmap);
            return bitmap;
        }
        catch
        {
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
}

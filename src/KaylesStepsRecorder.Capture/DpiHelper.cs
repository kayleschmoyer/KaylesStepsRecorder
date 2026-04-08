namespace KaylesStepsRecorder.Capture;

/// <summary>
/// Provides helper methods for DPI-aware operations on Windows.
/// </summary>
public static class DpiHelper
{
    private const double DefaultDpi = 96.0;

    /// <summary>
    /// Gets the DPI scale factor for the specified window.
    /// Returns 1.0 as a safe fallback if the call fails (e.g., on older Windows versions).
    /// </summary>
    /// <param name="hwnd">The window handle to query DPI for.</param>
    /// <returns>A scale factor where 1.0 = 96 DPI (100%), 1.25 = 120 DPI (125%), etc.</returns>
    public static double GetDpiForWindowSafe(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero)
            {
                return 1.0;
            }

            uint dpi = NativeMethods.GetDpiForWindow(hwnd);

            // GetDpiForWindow returns 0 on failure or on systems that don't support it.
            if (dpi == 0)
            {
                return 1.0;
            }

            return dpi / DefaultDpi;
        }
        catch
        {
            // GetDpiForWindow is available on Windows 10 1607+. Fall back gracefully.
            return 1.0;
        }
    }

    /// <summary>
    /// Gets the system default DPI by querying the desktop window (hWnd = IntPtr.Zero).
    /// </summary>
    /// <returns>A scale factor where 1.0 = 96 DPI (100%).</returns>
    public static double GetSystemDpi()
    {
        IntPtr hdc = IntPtr.Zero;
        try
        {
            hdc = NativeMethods.GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
            {
                return 1.0;
            }

            // LOGPIXELSX = 88
            int dpiX = GetDeviceCaps(hdc, 88);
            return dpiX > 0 ? dpiX / DefaultDpi : 1.0;
        }
        catch
        {
            return 1.0;
        }
        finally
        {
            if (hdc != IntPtr.Zero)
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, hdc);
            }
        }
    }

    /// <summary>
    /// Scales a pixel value by the given DPI scale factor.
    /// </summary>
    /// <param name="value">The pixel value at 96 DPI.</param>
    /// <param name="dpiScale">The DPI scale factor (e.g. 1.5 for 144 DPI).</param>
    /// <returns>The scaled pixel value, rounded to the nearest integer.</returns>
    public static int ScaleForDpi(int value, double dpiScale)
    {
        if (dpiScale <= 0)
        {
            return value;
        }

        return (int)Math.Round(value * dpiScale);
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
}

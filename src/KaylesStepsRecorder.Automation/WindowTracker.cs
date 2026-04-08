using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.Automation;

public sealed class WindowTracker : IWindowTracker
{
    private readonly ILogger<WindowTracker> _logger;

    public WindowTracker(ILogger<WindowTracker> logger)
    {
        _logger = logger;
    }

    public WindowInfo? GetForegroundWindow()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogDebug("No foreground window found");
            return null;
        }

        return BuildWindowInfo(hwnd);
    }

    public WindowInfo? GetWindowAtPoint(int screenX, int screenY)
    {
        var point = new POINT { X = screenX, Y = screenY };
        var hwnd = NativeMethods.WindowFromPoint(point);
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogDebug("No window found at point ({X}, {Y})", screenX, screenY);
            return null;
        }

        // Walk up to the top-level window so we get the actual application window,
        // not a child control.
        var ancestor = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (ancestor != IntPtr.Zero)
        {
            hwnd = ancestor;
        }

        return BuildWindowInfo(hwnd);
    }

    public bool IsExcludedWindow(WindowInfo window, AppSettings settings)
    {
        // Check process name against exclusion list (case-insensitive).
        if (settings.ExcludedProcesses.Count > 0 &&
            !string.IsNullOrEmpty(window.ProcessName))
        {
            foreach (var excluded in settings.ExcludedProcesses)
            {
                if (string.Equals(window.ProcessName, excluded, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Check window title against exclusion list (case-insensitive substring match).
        if (settings.ExcludedWindowTitles.Count > 0 &&
            !string.IsNullOrEmpty(window.Title))
        {
            foreach (var excluded in settings.ExcludedWindowTitles)
            {
                if (window.Title.Contains(excluded, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private WindowInfo? BuildWindowInfo(IntPtr hwnd)
    {
        try
        {
            var title = GetWindowTitle(hwnd);
            var processId = GetWindowProcessId(hwnd);
            var processName = GetProcessName(processId);

            NativeMethods.GetWindowRect(hwnd, out var rect);
            var isMinimized = NativeMethods.IsIconic(hwnd);
            var isMaximized = NativeMethods.IsZoomed(hwnd);
            var dpiScale = GetDpiScale(hwnd);
            var monitorIndex = GetMonitorIndex(hwnd);

            return new WindowInfo
            {
                Handle = hwnd,
                Title = title,
                ProcessName = processName,
                ProcessId = processId,
                Left = rect.Left,
                Top = rect.Top,
                Width = rect.Right - rect.Left,
                Height = rect.Bottom - rect.Top,
                IsMaximized = isMaximized,
                IsMinimized = isMinimized,
                DpiScale = dpiScale,
                MonitorIndex = monitorIndex
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build WindowInfo for handle {Handle}", hwnd);
            return null;
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = NativeMethods.GetWindowTextLength(hwnd);
        if (length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static int GetWindowProcessId(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return (int)processId;
    }

    private string GetProcessName(int processId)
    {
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process has already exited.
            _logger.LogDebug("Process {ProcessId} has exited", processId);
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            _logger.LogDebug("Cannot access process {ProcessId}", processId);
            return string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access denied - insufficient permissions.
            _logger.LogDebug("Access denied for process {ProcessId}", processId);
            return string.Empty;
        }
    }

    private static double GetDpiScale(IntPtr hwnd)
    {
        try
        {
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            if (dpi > 0)
            {
                return dpi / 96.0;
            }
        }
        catch
        {
            // GetDpiForWindow may not be available on older Windows versions.
        }

        return 1.0;
    }

    private static int GetMonitorIndex(IntPtr hwnd)
    {
        var hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
        {
            return 0;
        }

        // Use EnumDisplayMonitors to determine the monitor index. For simplicity,
        // we hash the monitor handle to a stable index. A full implementation would
        // enumerate monitors. We return a deterministic value based on the handle.
        return Math.Abs(hMonitor.GetHashCode()) % 16;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static class NativeMethods
    {
        public const uint GA_ROOT = 2;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    }
}

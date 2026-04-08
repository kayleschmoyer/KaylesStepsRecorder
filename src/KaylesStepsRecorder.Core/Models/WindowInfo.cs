namespace KaylesStepsRecorder.Core.Models;

public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsMaximized { get; init; }
    public bool IsMinimized { get; init; }
    public double DpiScale { get; init; } = 1.0;
    public int MonitorIndex { get; init; }
}

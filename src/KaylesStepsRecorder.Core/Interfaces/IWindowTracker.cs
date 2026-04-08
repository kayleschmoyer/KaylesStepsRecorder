using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface IWindowTracker
{
    WindowInfo? GetForegroundWindow();
    WindowInfo? GetWindowAtPoint(int screenX, int screenY);
    bool IsExcludedWindow(WindowInfo window, AppSettings settings);
}

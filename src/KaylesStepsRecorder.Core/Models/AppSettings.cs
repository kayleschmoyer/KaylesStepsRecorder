namespace KaylesStepsRecorder.Core.Models;

public sealed class AppSettings
{
    public int CaptureDelayMs { get; set; } = 150;
    public int DebounceIntervalMs { get; set; } = 200;
    public bool CaptureScrollEvents { get; set; }
    public bool CaptureKeyboardShortcuts { get; set; } = true;
    public bool CaptureTextEntry { get; set; } = true;
    public bool ShowClickHighlight { get; set; } = true;
    public int ClickHighlightRadius { get; set; } = 20;
    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 30;
    public bool DarkMode { get; set; }
    public string SessionStoragePath { get; set; } = string.Empty;
    public List<string> ExcludedProcesses { get; set; } = new() { "KaylesStepsRecorder" };
    public List<string> ExcludedWindowTitles { get; set; } = new();
    public bool PauseOnExcludedApp { get; set; } = true;
    public string DefaultTesterName { get; set; } = string.Empty;
    public string DefaultEnvironment { get; set; } = string.Empty;
    public int ScreenshotQuality { get; set; } = 90;
    public bool MinimizeOnRecord { get; set; } = true;
    public string HotkeyStartStop { get; set; } = "Ctrl+Shift+F9";
    public string HotkeyPauseResume { get; set; } = "Ctrl+Shift+F10";
    public string HotkeyCancel { get; set; } = "Ctrl+Shift+F11";

    public static string DefaultStoragePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaylesStepsRecorder", "Sessions");
}

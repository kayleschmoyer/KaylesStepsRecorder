using KaylesStepsRecorder.Core.Enums;

namespace KaylesStepsRecorder.Core.Models;

public sealed class RecordedStep
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public int StepNumber { get; set; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public ActionType ActionType { get; init; }
    public string Description { get; set; } = string.Empty;
    public string? UserNote { get; set; }
    public StepFlag Flags { get; set; } = StepFlag.None;

    // Location data
    public ClickCoordinates? Coordinates { get; init; }
    public WindowInfo? Window { get; init; }
    public ElementInfo? Element { get; init; }

    // Screenshot
    public string? ScreenshotPath { get; set; }
    public string? ThumbnailPath { get; set; }

    // Keyboard data
    public string? KeysPressed { get; set; }
    public string? TextEntered { get; set; }

    // Scroll data
    public int ScrollDelta { get; init; }
    public bool ScrolledDown => ScrollDelta < 0;

    // Annotation settings per step
    public bool ShowClickHighlight { get; set; } = true;
    public bool IsRedacted { get; set; }
    public bool IsDeleted { get; set; }
}

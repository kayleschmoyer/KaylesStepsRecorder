using KaylesStepsRecorder.Core.Enums;

namespace KaylesStepsRecorder.Core.Models;

public sealed class InputEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public ActionType ActionType { get; init; }
    public int ScreenX { get; init; }
    public int ScreenY { get; init; }
    public int ScrollDelta { get; init; }
    public string? KeyData { get; init; }
    public bool IsModifierKey { get; init; }
}

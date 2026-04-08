namespace KaylesStepsRecorder.Core.Models;

public sealed class ElementInfo
{
    public string Name { get; init; } = string.Empty;
    public string ControlType { get; init; } = string.Empty;
    public string AutomationId { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool IsOffscreen { get; init; }
    public int BoundingLeft { get; init; }
    public int BoundingTop { get; init; }
    public int BoundingWidth { get; init; }
    public int BoundingHeight { get; init; }

    public bool HasUsefulName => !string.IsNullOrWhiteSpace(Name) && Name != ControlType;
}

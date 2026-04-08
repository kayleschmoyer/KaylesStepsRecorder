using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface IStepDescriptionBuilder
{
    string BuildDescription(ActionType actionType, WindowInfo? window, ElementInfo? element,
        ClickCoordinates? coordinates, string? keyData = null, string? textEntered = null,
        int scrollDelta = 0);
}

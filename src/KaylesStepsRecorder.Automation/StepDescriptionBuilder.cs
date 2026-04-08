using System.Text;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Automation;

public sealed class StepDescriptionBuilder : IStepDescriptionBuilder
{
    public string BuildDescription(ActionType actionType, WindowInfo? window, ElementInfo? element,
        ClickCoordinates? coordinates, string? keyData = null, string? textEntered = null,
        int scrollDelta = 0)
    {
        var sb = new StringBuilder();

        switch (actionType)
        {
            case ActionType.LeftClick:
                sb.Append("Clicked ");
                AppendClickTarget(sb, element, coordinates);
                break;

            case ActionType.RightClick:
                sb.Append("Right-clicked ");
                AppendClickTarget(sb, element, coordinates);
                break;

            case ActionType.DoubleClick:
                sb.Append("Double-clicked ");
                AppendClickTarget(sb, element, coordinates);
                break;

            case ActionType.MiddleClick:
                sb.Append("Middle-clicked ");
                AppendClickTarget(sb, element, coordinates);
                break;

            case ActionType.KeyboardInput:
                AppendKeyboardInput(sb, keyData);
                break;

            case ActionType.KeyboardShortcut:
                AppendKeyboardShortcut(sb, keyData);
                break;

            case ActionType.TextEntry:
                AppendTextEntry(sb, element, textEntered);
                break;

            case ActionType.Scroll:
                AppendScroll(sb, scrollDelta);
                break;

            case ActionType.WindowActivated:
                sb.Append("Switched to");
                break;

            case ActionType.WindowClosed:
                sb.Append("Closed");
                break;

            case ActionType.MenuItemSelected:
                sb.Append("Selected ");
                AppendMenuTarget(sb, element);
                break;

            case ActionType.DragAndDrop:
                sb.Append("Dragged ");
                AppendDragTarget(sb, element, coordinates);
                break;

            case ActionType.ManualNote:
                if (!string.IsNullOrWhiteSpace(textEntered))
                {
                    return textEntered;
                }
                sb.Append("Note added");
                break;

            default:
                sb.Append("Performed action");
                break;
        }

        AppendWindowContext(sb, window, actionType);

        return sb.ToString();
    }

    private static void AppendClickTarget(StringBuilder sb, ElementInfo? element,
        ClickCoordinates? coordinates)
    {
        if (element != null && element.HasUsefulName)
        {
            sb.Append('\'');
            sb.Append(element.Name);
            sb.Append("' ");
            sb.Append(element.ControlType);
        }
        else if (element != null && !string.IsNullOrWhiteSpace(element.AutomationId))
        {
            sb.Append('[');
            sb.Append(element.AutomationId);
            sb.Append("] ");
            sb.Append(element.ControlType);
        }
        else if (element != null && !string.IsNullOrWhiteSpace(element.ControlType))
        {
            sb.Append(element.ControlType);
        }
        else if (coordinates != null)
        {
            sb.Append("at (");
            sb.Append(coordinates.ScreenX);
            sb.Append(", ");
            sb.Append(coordinates.ScreenY);
            sb.Append(')');
        }
        else
        {
            sb.Append("unknown area");
        }
    }

    private static void AppendKeyboardInput(StringBuilder sb, string? keyData)
    {
        if (!string.IsNullOrWhiteSpace(keyData))
        {
            sb.Append("Pressed ");
            sb.Append(keyData);
        }
        else
        {
            sb.Append("Pressed key");
        }
    }

    private static void AppendKeyboardShortcut(StringBuilder sb, string? keyData)
    {
        if (!string.IsNullOrWhiteSpace(keyData))
        {
            sb.Append("Pressed ");
            sb.Append(keyData);
        }
        else
        {
            sb.Append("Pressed keyboard shortcut");
        }
    }

    private static void AppendTextEntry(StringBuilder sb, ElementInfo? element, string? textEntered)
    {
        if (element != null && element.HasUsefulName)
        {
            sb.Append("Typed text in '");
            sb.Append(element.Name);
            sb.Append("' ");
            sb.Append(element.ControlType);
        }
        else if (element != null && !string.IsNullOrWhiteSpace(element.ControlType))
        {
            sb.Append("Entered text in ");
            sb.Append(element.ControlType);
        }
        else
        {
            sb.Append("Typed text in active field");
        }
    }

    private static void AppendScroll(StringBuilder sb, int scrollDelta)
    {
        if (scrollDelta > 0)
        {
            sb.Append("Scrolled up");
        }
        else if (scrollDelta < 0)
        {
            sb.Append("Scrolled down");
        }
        else
        {
            sb.Append("Scrolled");
        }
    }

    private static void AppendMenuTarget(StringBuilder sb, ElementInfo? element)
    {
        if (element != null && element.HasUsefulName)
        {
            sb.Append('\'');
            sb.Append(element.Name);
            sb.Append("' ");
            sb.Append(element.ControlType);
        }
        else if (element != null && !string.IsNullOrWhiteSpace(element.ControlType))
        {
            sb.Append(element.ControlType);
        }
        else
        {
            sb.Append("menu item");
        }
    }

    private static void AppendDragTarget(StringBuilder sb, ElementInfo? element,
        ClickCoordinates? coordinates)
    {
        if (element != null && element.HasUsefulName)
        {
            sb.Append('\'');
            sb.Append(element.Name);
            sb.Append("' ");
            sb.Append(element.ControlType);
        }
        else if (coordinates != null)
        {
            sb.Append("from (");
            sb.Append(coordinates.ScreenX);
            sb.Append(", ");
            sb.Append(coordinates.ScreenY);
            sb.Append(')');
        }
        else
        {
            sb.Append("item");
        }
    }

    private static void AppendWindowContext(StringBuilder sb, WindowInfo? window,
        ActionType actionType)
    {
        if (window == null || string.IsNullOrWhiteSpace(window.Title))
        {
            return;
        }

        // For WindowActivated / WindowClosed, use the window title as the primary subject.
        if (actionType == ActionType.WindowActivated || actionType == ActionType.WindowClosed)
        {
            sb.Append(' ');
            sb.Append(window.Title);
        }
        else
        {
            sb.Append(" in ");
            sb.Append(window.Title);
        }
    }
}

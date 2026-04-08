using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.App.ViewModels;

/// <summary>
/// Observable wrapper around a <see cref="RecordedStep"/> for the UI.
/// </summary>
public sealed class StepViewModel : ViewModelBase
{
    private readonly RecordedStep _step;

    public StepViewModel(RecordedStep step)
    {
        _step = step ?? throw new ArgumentNullException(nameof(step));
    }

    public RecordedStep Model => _step;

    public string Id => _step.Id;

    public int StepNumber
    {
        get => _step.StepNumber;
        set { _step.StepNumber = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _step.Description;
        set { _step.Description = value; OnPropertyChanged(); }
    }

    public string? UserNote
    {
        get => _step.UserNote;
        set { _step.UserNote = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNote)); }
    }

    public bool HasNote => !string.IsNullOrWhiteSpace(_step.UserNote);

    public DateTime Timestamp => _step.Timestamp;
    public string TimestampText => _step.Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string? ThumbnailPath => _step.ThumbnailPath ?? _step.ScreenshotPath;
    public string? ScreenshotPath => _step.ScreenshotPath;
    public string WindowTitle => _step.Window?.Title ?? string.Empty;
    public ActionType ActionType => _step.ActionType;
    public string ActionTypeText => _step.ActionType.ToString();

    public bool IsImportant
    {
        get => (_step.Flags & StepFlag.Important) != 0;
        set
        {
            if (value) _step.Flags |= StepFlag.Important;
            else _step.Flags &= ~StepFlag.Important;
            OnPropertyChanged();
        }
    }

    public bool IsBug
    {
        get => (_step.Flags & StepFlag.Bug) != 0;
        set
        {
            if (value) _step.Flags |= StepFlag.Bug;
            else _step.Flags &= ~StepFlag.Bug;
            OnPropertyChanged();
        }
    }

    public bool IsExpectedResult
    {
        get => (_step.Flags & StepFlag.ExpectedResult) != 0;
        set
        {
            if (value) _step.Flags |= StepFlag.ExpectedResult;
            else _step.Flags &= ~StepFlag.ExpectedResult;
            OnPropertyChanged();
        }
    }

    public bool IsActualResult
    {
        get => (_step.Flags & StepFlag.ActualResult) != 0;
        set
        {
            if (value) _step.Flags |= StepFlag.ActualResult;
            else _step.Flags &= ~StepFlag.ActualResult;
            OnPropertyChanged();
        }
    }

    public bool IsRedacted
    {
        get => _step.IsRedacted;
        set { _step.IsRedacted = value; OnPropertyChanged(); }
    }

    public bool IsDeleted
    {
        get => _step.IsDeleted;
        set { _step.IsDeleted = value; OnPropertyChanged(); }
    }
}

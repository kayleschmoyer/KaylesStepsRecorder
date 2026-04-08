using System.Collections.ObjectModel;
using System.Windows.Input;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.App.ViewModels;

/// <summary>
/// Step review/edit screen: edit descriptions, add notes, delete, reorder, flag.
/// </summary>
public sealed class StepEditorViewModel : ViewModelBase
{
    private RecordingSession? _session;
    public RecordingSession? Session
    {
        get => _session;
        set
        {
            _session = value;
            ReloadSteps();
            OnPropertyChanged(nameof(Metadata));
            OnPropertyChanged();
        }
    }

    public ObservableCollection<StepViewModel> Steps { get; } = new();

    public SessionMetadata? Metadata => _session?.Metadata;

    private StepViewModel? _selectedStep;
    public StepViewModel? SelectedStep
    {
        get => _selectedStep;
        set => SetProperty(ref _selectedStep, value);
    }

    public ICommand DeleteStepCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand MarkImportantCommand { get; }
    public ICommand MarkBugCommand { get; }
    public ICommand AddNoteCommand { get; }

    public StepEditorViewModel()
    {
        DeleteStepCommand = new RelayCommand(DeleteSelected, () => SelectedStep != null);
        MoveUpCommand = new RelayCommand(MoveSelectedUp, () => CanMoveUp());
        MoveDownCommand = new RelayCommand(MoveSelectedDown, () => CanMoveDown());
        MarkImportantCommand = new RelayCommand(
            () => { if (SelectedStep != null) SelectedStep.IsImportant = !SelectedStep.IsImportant; },
            () => SelectedStep != null);
        MarkBugCommand = new RelayCommand(
            () => { if (SelectedStep != null) SelectedStep.IsBug = !SelectedStep.IsBug; },
            () => SelectedStep != null);
        AddNoteCommand = new RelayCommand(() => { /* handled in view */ }, () => SelectedStep != null);
    }

    private void ReloadSteps()
    {
        Steps.Clear();
        if (_session == null) return;
        foreach (var step in _session.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.StepNumber))
        {
            Steps.Add(new StepViewModel(step));
        }
    }

    private void DeleteSelected()
    {
        if (SelectedStep == null || _session == null) return;
        _session.RemoveStep(SelectedStep.Id);
        Steps.Remove(SelectedStep);
        Renumber();
    }

    private bool CanMoveUp()
    {
        if (SelectedStep == null) return false;
        return Steps.IndexOf(SelectedStep) > 0;
    }

    private bool CanMoveDown()
    {
        if (SelectedStep == null) return false;
        int idx = Steps.IndexOf(SelectedStep);
        return idx >= 0 && idx < Steps.Count - 1;
    }

    private void MoveSelectedUp()
    {
        if (SelectedStep == null || _session == null) return;
        int idx = Steps.IndexOf(SelectedStep);
        if (idx <= 0) return;
        _session.ReorderStep(idx, idx - 1);
        Steps.Move(idx, idx - 1);
        Renumber();
    }

    private void MoveSelectedDown()
    {
        if (SelectedStep == null || _session == null) return;
        int idx = Steps.IndexOf(SelectedStep);
        if (idx < 0 || idx >= Steps.Count - 1) return;
        _session.ReorderStep(idx, idx + 1);
        Steps.Move(idx, idx + 1);
        Renumber();
    }

    private void Renumber()
    {
        int n = 1;
        foreach (var s in Steps) s.StepNumber = n++;
    }
}

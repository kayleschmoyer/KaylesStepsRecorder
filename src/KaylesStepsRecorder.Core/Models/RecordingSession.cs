using KaylesStepsRecorder.Core.Enums;

namespace KaylesStepsRecorder.Core.Models;

public sealed class RecordingSession
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public RecordingState State { get; set; } = RecordingState.Idle;
    public SessionMetadata Metadata { get; set; } = new();
    public List<RecordedStep> Steps { get; init; } = new();
    public string SessionDirectory { get; set; } = string.Empty;

    // Recovery tracking
    public DateTime LastSavedAt { get; set; }
    public bool IsDirty { get; set; }
    public bool IsRecovered { get; set; }

    public int NextStepNumber => Steps.Count(s => !s.IsDeleted) + 1;

    public void AddStep(RecordedStep step)
    {
        step.StepNumber = NextStepNumber;
        Steps.Add(step);
        IsDirty = true;
    }

    public void RemoveStep(string stepId)
    {
        var step = Steps.FirstOrDefault(s => s.Id == stepId);
        if (step != null)
        {
            step.IsDeleted = true;
            RenumberSteps();
            IsDirty = true;
        }
    }

    public void ReorderStep(int oldIndex, int newIndex)
    {
        var activeSteps = Steps.Where(s => !s.IsDeleted).ToList();
        if (oldIndex < 0 || oldIndex >= activeSteps.Count || newIndex < 0 || newIndex >= activeSteps.Count)
            return;

        var step = activeSteps[oldIndex];
        activeSteps.RemoveAt(oldIndex);
        activeSteps.Insert(newIndex, step);

        // Rebuild the full list with deleted steps at their positions
        var deleted = Steps.Where(s => s.IsDeleted).ToList();
        Steps.Clear();
        Steps.AddRange(activeSteps);
        Steps.AddRange(deleted);
        RenumberSteps();
        IsDirty = true;
    }

    private void RenumberSteps()
    {
        int num = 1;
        foreach (var step in Steps.Where(s => !s.IsDeleted))
        {
            step.StepNumber = num++;
        }
    }
}

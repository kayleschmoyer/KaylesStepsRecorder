using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface IRecordingEngine : IDisposable
{
    event EventHandler<RecordedStep>? StepRecorded;
    event EventHandler<RecordingState>? StateChanged;
    event EventHandler<string>? Error;

    RecordingState CurrentState { get; }
    RecordingSession? CurrentSession { get; }

    Task StartAsync();
    void Pause();
    void Resume();
    Task StopAsync();
    void Cancel();

    void UpdateSettings(AppSettings settings);
}

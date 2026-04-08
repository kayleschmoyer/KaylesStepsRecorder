using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.App.ViewModels;

/// <summary>
/// View model for the recording screen: start/pause/resume/stop/cancel and
/// live step feed.
/// </summary>
public sealed class RecordingViewModel : ViewModelBase
{
    private readonly IRecordingEngine _engine;
    private readonly ILogger<RecordingViewModel> _logger;

    public ObservableCollection<StepViewModel> Steps { get; } = new();

    private RecordingState _state;
    public RecordingState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(StateText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsRecording => _state == RecordingState.Recording;
    public bool IsPaused => _state == RecordingState.Paused;
    public bool IsIdle => _state is RecordingState.Idle or RecordingState.Stopped;

    public string StateText => _state switch
    {
        RecordingState.Recording => "RECORDING",
        RecordingState.Paused => "PAUSED",
        RecordingState.Stopped => "STOPPED",
        _ => "READY",
    };

    private int _stepCount;
    public int StepCount
    {
        get => _stepCount;
        private set => SetProperty(ref _stepCount, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand CancelCommand { get; }

    public event EventHandler? RecordingStopped;

    public RecordingSession? CurrentSession => _engine.CurrentSession;

    public RecordingViewModel(IRecordingEngine engine, ILogger<RecordingViewModel> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _engine.StepRecorded += OnStepRecorded;
        _engine.StateChanged += OnStateChanged;
        _engine.Error += OnError;

        StartCommand = new AsyncRelayCommand(StartAsync, () => IsIdle);
        PauseCommand = new RelayCommand(() => _engine.Pause(), () => IsRecording);
        ResumeCommand = new RelayCommand(() => _engine.Resume(), () => IsPaused);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRecording || IsPaused);
        CancelCommand = new RelayCommand(Cancel, () => IsRecording || IsPaused);
    }

    public async Task StartAsync()
    {
        try
        {
            ErrorMessage = null;
            Steps.Clear();
            StepCount = 0;
            await _engine.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            ErrorMessage = "Failed to start recording: " + ex.Message;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await _engine.StopAsync();
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop recording");
            ErrorMessage = "Failed to stop recording: " + ex.Message;
        }
    }

    public void Cancel()
    {
        _engine.Cancel();
        Steps.Clear();
        StepCount = 0;
    }

    private void OnStepRecorded(object? sender, RecordedStep step)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Steps.Add(new StepViewModel(step));
            StepCount = Steps.Count;
        });
    }

    private void OnStateChanged(object? sender, RecordingState newState)
    {
        Application.Current?.Dispatcher.Invoke(() => State = newState);
    }

    private void OnError(object? sender, string message)
    {
        Application.Current?.Dispatcher.Invoke(() => ErrorMessage = message);
    }

    public void Cleanup()
    {
        _engine.StepRecorded -= OnStepRecorded;
        _engine.StateChanged -= OnStateChanged;
        _engine.Error -= OnError;
    }
}

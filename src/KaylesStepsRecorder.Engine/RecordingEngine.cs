using System.Collections.Concurrent;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.Engine;

/// <summary>
/// Central orchestrator for the recording lifecycle. Composes the input hook service,
/// screen capture, element inspection, window tracking and storage subsystems to produce
/// a stream of <see cref="RecordedStep"/>s.
/// </summary>
public sealed class RecordingEngine : IRecordingEngine
{
    private readonly IInputHookService _inputHooks;
    private readonly IScreenCaptureService _captureService;
    private readonly IElementInspector _elementInspector;
    private readonly IWindowTracker _windowTracker;
    private readonly IStepDescriptionBuilder _descriptionBuilder;
    private readonly ISessionStorage _sessionStorage;
    private readonly ILogger<RecordingEngine> _logger;

    private AppSettings _settings;
    private readonly StepProcessor _stepProcessor;

    private readonly ConcurrentQueue<InputEvent> _eventQueue = new();
    private readonly SemaphoreSlim _processingSignal = new(0);
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;

    private System.Threading.Timer? _autosaveTimer;
    private readonly object _stateLock = new();

    public event EventHandler<RecordedStep>? StepRecorded;
    public event EventHandler<RecordingState>? StateChanged;
    public event EventHandler<string>? Error;

    public RecordingState CurrentState { get; private set; } = RecordingState.Idle;
    public RecordingSession? CurrentSession { get; private set; }

    public RecordingEngine(
        IInputHookService inputHooks,
        IScreenCaptureService captureService,
        IElementInspector elementInspector,
        IWindowTracker windowTracker,
        IStepDescriptionBuilder descriptionBuilder,
        ISessionStorage sessionStorage,
        ILogger<RecordingEngine> logger,
        AppSettings settings)
    {
        _inputHooks = inputHooks ?? throw new ArgumentNullException(nameof(inputHooks));
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _elementInspector = elementInspector ?? throw new ArgumentNullException(nameof(elementInspector));
        _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
        _descriptionBuilder = descriptionBuilder ?? throw new ArgumentNullException(nameof(descriptionBuilder));
        _sessionStorage = sessionStorage ?? throw new ArgumentNullException(nameof(sessionStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _stepProcessor = new StepProcessor(logger);
        _inputHooks.InputReceived += OnInputReceived;
    }

    public async Task StartAsync()
    {
        lock (_stateLock)
        {
            if (CurrentState == RecordingState.Recording)
            {
                _logger.LogWarning("StartAsync called while already recording");
                return;
            }
        }

        try
        {
            var session = new RecordingSession
            {
                State = RecordingState.Recording,
                CreatedAt = DateTime.UtcNow,
            };

            session.SessionDirectory = await _sessionStorage.CreateSessionDirectoryAsync(session.SessionId)
                .ConfigureAwait(false);

            CurrentSession = session;
            await _sessionStorage.SaveSessionAsync(session).ConfigureAwait(false);

            _processingCts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessingLoopAsync(_processingCts.Token));

            _inputHooks.Install();

            StartAutosaveTimer();
            SetState(RecordingState.Recording);
            _logger.LogInformation("Recording started. SessionId={SessionId}", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            RaiseError("Failed to start recording: " + ex.Message);
            throw;
        }
    }

    public void Pause()
    {
        lock (_stateLock)
        {
            if (CurrentState != RecordingState.Recording)
                return;
        }

        try
        {
            _inputHooks.Uninstall();
            SetState(RecordingState.Paused);
            _logger.LogInformation("Recording paused");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing recording");
            RaiseError("Error pausing recording: " + ex.Message);
        }
    }

    public void Resume()
    {
        lock (_stateLock)
        {
            if (CurrentState != RecordingState.Paused)
                return;
        }

        try
        {
            _inputHooks.Install();
            SetState(RecordingState.Recording);
            _logger.LogInformation("Recording resumed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming recording");
            RaiseError("Error resuming recording: " + ex.Message);
        }
    }

    public async Task StopAsync()
    {
        lock (_stateLock)
        {
            if (CurrentState == RecordingState.Idle || CurrentState == RecordingState.Stopped)
                return;
        }

        try
        {
            if (_inputHooks.IsInstalled)
                _inputHooks.Uninstall();

            StopAutosaveTimer();

            // Wait for the processing queue to drain.
            var drainStart = DateTime.UtcNow;
            while (!_eventQueue.IsEmpty && DateTime.UtcNow - drainStart < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

            _processingCts?.Cancel();
            if (_processingTask != null)
            {
                try { await _processingTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            _stepProcessor.Reset();

            if (CurrentSession != null)
            {
                CurrentSession.CompletedAt = DateTime.UtcNow;
                CurrentSession.State = RecordingState.Stopped;
                await _sessionStorage.SaveSessionAsync(CurrentSession).ConfigureAwait(false);
            }

            SetState(RecordingState.Stopped);
            _logger.LogInformation("Recording stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping recording");
            RaiseError("Error stopping recording: " + ex.Message);
        }
    }

    public void Cancel()
    {
        try
        {
            if (_inputHooks.IsInstalled)
                _inputHooks.Uninstall();

            StopAutosaveTimer();
            _processingCts?.Cancel();
            _stepProcessor.Reset();

            // Keep the session on disk so the user can recover if they change their mind,
            // but mark it as cancelled.
            if (CurrentSession != null)
            {
                CurrentSession.State = RecordingState.Idle;
            }

            SetState(RecordingState.Idle);
            CurrentSession = null;
            _logger.LogInformation("Recording cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling recording");
        }
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger.LogDebug("Engine settings updated");
    }

    // ------------------------------------------------------------------
    //  Event pipeline
    // ------------------------------------------------------------------

    private void OnInputReceived(object? sender, InputEvent e)
    {
        if (CurrentState != RecordingState.Recording)
            return;

        // Fast path: enqueue and signal. Processing happens on the background loop.
        _eventQueue.Enqueue(e);
        try { _processingSignal.Release(); }
        catch (SemaphoreFullException) { /* benign */ }
    }

    private async Task ProcessingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _processingSignal.WaitAsync(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            while (_eventQueue.TryDequeue(out var input))
            {
                await HandleInputAsync(input).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleInputAsync(InputEvent input)
    {
        try
        {
            var session = CurrentSession;
            if (session == null) return;

            // Exclusion check: auto-pause on excluded windows.
            var activeWindow = _windowTracker.GetForegroundWindow();
            if (activeWindow != null && _windowTracker.IsExcludedWindow(activeWindow, _settings))
            {
                if (_settings.PauseOnExcludedApp && CurrentState == RecordingState.Recording)
                {
                    _logger.LogInformation(
                        "Auto-pausing recording for excluded window: {Title}", activeWindow.Title);
                    Pause();
                }
                return;
            }

            string screenshotDir = _sessionStorage.GetScreenshotDirectory(session.SessionDirectory);

            var step = await _stepProcessor.ProcessInputAsync(
                input, _settings, _windowTracker, _elementInspector,
                _captureService, _descriptionBuilder, screenshotDir).ConfigureAwait(false);

            if (step == null)
                return;

            session.AddStep(step);
            StepRecorded?.Invoke(this, step);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling input event");
        }
    }

    // ------------------------------------------------------------------
    //  Autosave
    // ------------------------------------------------------------------

    private void StartAutosaveTimer()
    {
        if (!_settings.AutoSaveEnabled) return;

        int interval = Math.Max(5, _settings.AutoSaveIntervalSeconds) * 1000;
        _autosaveTimer = new System.Threading.Timer(async _ => await AutosaveAsync().ConfigureAwait(false),
            null, interval, interval);
    }

    private void StopAutosaveTimer()
    {
        _autosaveTimer?.Dispose();
        _autosaveTimer = null;
    }

    private async Task AutosaveAsync()
    {
        try
        {
            if (CurrentSession != null && CurrentSession.IsDirty)
            {
                await _sessionStorage.SaveSessionAsync(CurrentSession).ConfigureAwait(false);
                CurrentSession.LastSavedAt = DateTime.UtcNow;
                CurrentSession.IsDirty = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Autosave failed");
        }
    }

    // ------------------------------------------------------------------
    //  State
    // ------------------------------------------------------------------

    private void SetState(RecordingState newState)
    {
        lock (_stateLock)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            if (CurrentSession != null)
                CurrentSession.State = newState;
        }
        StateChanged?.Invoke(this, newState);
    }

    private void RaiseError(string message)
    {
        try { Error?.Invoke(this, message); }
        catch { /* never crash on error handler */ }
    }

    public void Dispose()
    {
        try
        {
            _inputHooks.InputReceived -= OnInputReceived;
            StopAutosaveTimer();
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingSignal.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing RecordingEngine");
        }
    }
}

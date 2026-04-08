using System.Windows;
using System.Windows.Input;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.App.ViewModels;

public enum AppView
{
    Home,
    Recording,
    Review,
    Export,
    Settings,
}

/// <summary>
/// Root view model that tracks the currently visible "page" and owns the
/// child view models.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly IRecordingEngine _engine;
    private readonly ISessionStorage _storage;
    private readonly ILogger<MainViewModel> _logger;
    private readonly AppSettings _settings;

    public RecordingViewModel Recording { get; }
    public StepEditorViewModel Editor { get; }
    public ExportViewModel Export { get; }
    public SettingsViewModel Settings { get; }

    private AppView _currentView = AppView.Home;
    public AppView CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                OnPropertyChanged(nameof(IsHome));
                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(IsReview));
                OnPropertyChanged(nameof(IsExport));
                OnPropertyChanged(nameof(IsSettings));
            }
        }
    }

    public bool IsHome => _currentView == AppView.Home;
    public bool IsRecording => _currentView == AppView.Recording;
    public bool IsReview => _currentView == AppView.Review;
    public bool IsExport => _currentView == AppView.Export;
    public bool IsSettings => _currentView == AppView.Settings;

    private string? _recoveryBanner;
    public string? RecoveryBanner
    {
        get => _recoveryBanner;
        set => SetProperty(ref _recoveryBanner, value);
    }

    public ICommand NavigateHomeCommand { get; }
    public ICommand NavigateRecordingCommand { get; }
    public ICommand NavigateReviewCommand { get; }
    public ICommand NavigateExportCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NewSessionCommand { get; }
    public ICommand DismissRecoveryCommand { get; }
    public ICommand RestoreRecoveryCommand { get; }

    private RecordingSession? _recoveredSession;

    public MainViewModel(
        IRecordingEngine engine,
        ISessionStorage storage,
        AppSettings settings,
        RecordingViewModel recording,
        StepEditorViewModel editor,
        ExportViewModel export,
        SettingsViewModel settingsVm,
        ILogger<MainViewModel> logger)
    {
        _engine = engine;
        _storage = storage;
        _settings = settings;
        _logger = logger;

        Recording = recording;
        Editor = editor;
        Export = export;
        Settings = settingsVm;

        Recording.RecordingStopped += OnRecordingStopped;

        NavigateHomeCommand = new RelayCommand(() => CurrentView = AppView.Home);
        NavigateRecordingCommand = new RelayCommand(() => CurrentView = AppView.Recording);
        NavigateReviewCommand = new RelayCommand(
            () =>
            {
                Editor.Session = _engine.CurrentSession;
                CurrentView = AppView.Review;
            },
            () => _engine.CurrentSession != null);
        NavigateExportCommand = new RelayCommand(
            () =>
            {
                Export.Session = _engine.CurrentSession;
                Export.OutputPath ??= BuildDefaultExportPath();
                CurrentView = AppView.Export;
            },
            () => _engine.CurrentSession != null);
        NavigateSettingsCommand = new RelayCommand(() => CurrentView = AppView.Settings);
        NewSessionCommand = new RelayCommand(StartNewSession);
        DismissRecoveryCommand = new RelayCommand(DismissRecovery);
        RestoreRecoveryCommand = new RelayCommand(RestoreRecovery);
    }

    public async Task CheckForCrashRecoveryAsync()
    {
        try
        {
            _recoveredSession = await _storage.RecoverCrashedSessionAsync();
            if (_recoveredSession != null)
            {
                int stepCount = _recoveredSession.Steps.Count(s => !s.IsDeleted);
                RecoveryBanner = $"A previous session with {stepCount} step(s) was not closed cleanly. Restore it?";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Crash recovery check failed");
        }
    }

    private void StartNewSession()
    {
        Recording.Steps.Clear();
        CurrentView = AppView.Recording;
    }

    private void OnRecordingStopped(object? sender, EventArgs e)
    {
        // Once recording stops, automatically navigate to the review/edit screen.
        Editor.Session = _engine.CurrentSession;
        CurrentView = AppView.Review;
    }

    private void DismissRecovery()
    {
        _recoveredSession = null;
        RecoveryBanner = null;
    }

    private void RestoreRecovery()
    {
        if (_recoveredSession == null) return;
        Editor.Session = _recoveredSession;
        RecoveryBanner = null;
        CurrentView = AppView.Review;
    }

    private string BuildDefaultExportPath()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string name = string.IsNullOrWhiteSpace(_engine.CurrentSession?.Metadata.BugTitle)
            ? $"session_{DateTime.Now:yyyyMMdd_HHmm}"
            : SanitizeFileName(_engine.CurrentSession!.Metadata.BugTitle);
        return Path.Combine(docs, "KaylesStepsRecorder", name + ".html");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}

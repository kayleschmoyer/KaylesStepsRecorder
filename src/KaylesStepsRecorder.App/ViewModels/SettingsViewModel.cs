using System.Windows.Input;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.App.ViewModels;

/// <summary>
/// Editable view model for app settings. Dispatches changes to the engine
/// via the provided apply callback.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Action<AppSettings> _apply;

    public SettingsViewModel(AppSettings settings, Action<AppSettings> apply)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        SaveCommand = new RelayCommand(Save);
    }

    public int CaptureDelayMs
    {
        get => _settings.CaptureDelayMs;
        set { _settings.CaptureDelayMs = value; OnPropertyChanged(); }
    }

    public int DebounceIntervalMs
    {
        get => _settings.DebounceIntervalMs;
        set { _settings.DebounceIntervalMs = value; OnPropertyChanged(); }
    }

    public bool ShowClickHighlight
    {
        get => _settings.ShowClickHighlight;
        set { _settings.ShowClickHighlight = value; OnPropertyChanged(); }
    }

    public int ClickHighlightRadius
    {
        get => _settings.ClickHighlightRadius;
        set { _settings.ClickHighlightRadius = value; OnPropertyChanged(); }
    }

    public bool CaptureKeyboardShortcuts
    {
        get => _settings.CaptureKeyboardShortcuts;
        set { _settings.CaptureKeyboardShortcuts = value; OnPropertyChanged(); }
    }

    public bool CaptureTextEntry
    {
        get => _settings.CaptureTextEntry;
        set { _settings.CaptureTextEntry = value; OnPropertyChanged(); }
    }

    public bool CaptureScrollEvents
    {
        get => _settings.CaptureScrollEvents;
        set { _settings.CaptureScrollEvents = value; OnPropertyChanged(); }
    }

    public bool AutoSaveEnabled
    {
        get => _settings.AutoSaveEnabled;
        set { _settings.AutoSaveEnabled = value; OnPropertyChanged(); }
    }

    public int AutoSaveIntervalSeconds
    {
        get => _settings.AutoSaveIntervalSeconds;
        set { _settings.AutoSaveIntervalSeconds = value; OnPropertyChanged(); }
    }

    public bool DarkMode
    {
        get => _settings.DarkMode;
        set { _settings.DarkMode = value; OnPropertyChanged(); }
    }

    public bool MinimizeOnRecord
    {
        get => _settings.MinimizeOnRecord;
        set { _settings.MinimizeOnRecord = value; OnPropertyChanged(); }
    }

    public bool PauseOnExcludedApp
    {
        get => _settings.PauseOnExcludedApp;
        set { _settings.PauseOnExcludedApp = value; OnPropertyChanged(); }
    }

    public string ExcludedProcessesText
    {
        get => string.Join(", ", _settings.ExcludedProcesses);
        set
        {
            _settings.ExcludedProcesses = (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            OnPropertyChanged();
        }
    }

    public string DefaultTesterName
    {
        get => _settings.DefaultTesterName;
        set { _settings.DefaultTesterName = value; OnPropertyChanged(); }
    }

    public string DefaultEnvironment
    {
        get => _settings.DefaultEnvironment;
        set { _settings.DefaultEnvironment = value; OnPropertyChanged(); }
    }

    public ICommand SaveCommand { get; }

    private void Save() => _apply(_settings);
}

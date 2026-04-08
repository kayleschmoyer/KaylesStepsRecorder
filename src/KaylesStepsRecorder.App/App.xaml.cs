using System.IO;
using System.Windows;
using System.Windows.Threading;
using KaylesStepsRecorder.App.ViewModels;
using KaylesStepsRecorder.App.Views;
using KaylesStepsRecorder.Automation;
using KaylesStepsRecorder.Capture;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using KaylesStepsRecorder.Engine;
using KaylesStepsRecorder.Export;
using KaylesStepsRecorder.Hooks;
using KaylesStepsRecorder.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.App;

/// <summary>
/// Application entry point. Wires the DI container and bootstraps the main window.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SetupUnhandledExceptionHandlers();
        Services = BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        });

        // Settings
        var settings = LoadOrCreateSettings();
        services.AddSingleton(settings);

        // Infrastructure singletons
        services.AddSingleton<IInputHookService, InputHookService>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IElementInspector, ElementInspector>();
        services.AddSingleton<IWindowTracker, WindowTracker>();
        services.AddSingleton<IStepDescriptionBuilder, StepDescriptionBuilder>();
        services.AddSingleton<ISessionStorage, SessionStorage>();
        services.AddSingleton<IRecordingEngine, RecordingEngine>();

        // Export services
        services.AddSingleton<IExportService, HtmlExportService>();
        services.AddSingleton<IExportService, MarkdownExportService>();

        // View models
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<StepEditorViewModel>();
        services.AddSingleton<ExportViewModel>();
        services.AddSingleton<SettingsViewModel>(sp =>
        {
            var s = sp.GetRequiredService<AppSettings>();
            var engine = sp.GetRequiredService<IRecordingEngine>();
            return new SettingsViewModel(s, engine.UpdateSettings);
        });
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private static AppSettings LoadOrCreateSettings()
    {
        var settings = new AppSettings
        {
            SessionStoragePath = AppSettings.DefaultStoragePath
        };
        try
        {
            Directory.CreateDirectory(settings.SessionStoragePath);
        }
        catch { /* ignore - will fall back at runtime */ }
        return settings;
    }

    private void SetupUnhandledExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "Kayle's Steps Recorder",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Last-ditch logging - cannot recover from non-UI thread exceptions here.
        if (e.ExceptionObject is Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppSettings.DefaultStoragePath, "crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] {ex}\n\n");
            }
            catch { /* swallow */ }
        }
    }

    private static void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}

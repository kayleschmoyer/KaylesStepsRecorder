using System.Windows.Input;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.App.ViewModels;

/// <summary>
/// Configures and performs the export of a session.
/// </summary>
public sealed class ExportViewModel : ViewModelBase
{
    private readonly IEnumerable<IExportService> _exportServices;
    private readonly ILogger<ExportViewModel> _logger;

    private RecordingSession? _session;
    public RecordingSession? Session
    {
        get => _session;
        set { _session = value; OnPropertyChanged(); }
    }

    private ExportFormat _format = ExportFormat.HtmlFull;
    public ExportFormat Format { get => _format; set => SetProperty(ref _format, value); }

    private bool _includeScreenshots = true;
    public bool IncludeScreenshots { get => _includeScreenshots; set => SetProperty(ref _includeScreenshots, value); }

    private bool _includeTimestamps = true;
    public bool IncludeTimestamps { get => _includeTimestamps; set => SetProperty(ref _includeTimestamps, value); }

    private bool _includeCoordinates;
    public bool IncludeCoordinates { get => _includeCoordinates; set => SetProperty(ref _includeCoordinates, value); }

    private bool _includeNotes = true;
    public bool IncludeNotes { get => _includeNotes; set => SetProperty(ref _includeNotes, value); }

    private bool _embedImagesAsBase64 = true;
    public bool EmbedImagesAsBase64 { get => _embedImagesAsBase64; set => SetProperty(ref _embedImagesAsBase64, value); }

    private string? _outputPath;
    public string? OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }

    private string? _statusMessage;
    public string? StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isExporting;
    public bool IsExporting { get => _isExporting; set => SetProperty(ref _isExporting, value); }

    public ICommand ExportCommand { get; }

    public ExportViewModel(IEnumerable<IExportService> exportServices, ILogger<ExportViewModel> logger)
    {
        _exportServices = exportServices ?? throw new ArgumentNullException(nameof(exportServices));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ExportCommand = new AsyncRelayCommand(ExportAsync,
            () => !IsExporting && _session != null && !string.IsNullOrWhiteSpace(OutputPath));
    }

    public async Task ExportAsync()
    {
        if (Session == null || string.IsNullOrWhiteSpace(OutputPath))
            return;

        try
        {
            IsExporting = true;
            StatusMessage = "Exporting...";

            var options = new ExportOptions
            {
                Format = Format,
                OutputPath = OutputPath!,
                IncludeScreenshots = IncludeScreenshots,
                IncludeTimestamps = IncludeTimestamps,
                IncludeCoordinates = IncludeCoordinates,
                IncludeNotes = IncludeNotes,
                EmbedImagesAsBase64 = EmbedImagesAsBase64,
                IncludeMetadata = true,
            };

            var service = _exportServices.FirstOrDefault(s => s.SupportsFormat(Format))
                          ?? throw new InvalidOperationException($"No export service supports format {Format}");

            string path = await service.ExportAsync(Session, options);
            StatusMessage = "Exported to: " + path;
            _logger.LogInformation("Session exported to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            StatusMessage = "Export failed: " + ex.Message;
        }
        finally
        {
            IsExporting = false;
        }
    }
}

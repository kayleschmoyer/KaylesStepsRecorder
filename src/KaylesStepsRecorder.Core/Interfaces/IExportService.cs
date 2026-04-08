using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface IExportService
{
    Task<string> ExportAsync(RecordingSession session, ExportOptions options);
    bool SupportsFormat(Enums.ExportFormat format);
}

using KaylesStepsRecorder.Core.Enums;

namespace KaylesStepsRecorder.Core.Models;

public sealed class ExportOptions
{
    public ExportFormat Format { get; set; } = ExportFormat.HtmlFull;
    public string OutputPath { get; set; } = string.Empty;
    public bool IncludeScreenshots { get; set; } = true;
    public bool IncludeTimestamps { get; set; } = true;
    public bool IncludeCoordinates { get; set; }
    public bool IncludeMetadata { get; set; } = true;
    public bool IncludeNotes { get; set; } = true;
    public bool EmbedImagesAsBase64 { get; set; } = true;
    public int MaxImageWidth { get; set; } = 1200;
    public bool IncludeDeletedSteps { get; set; }
}

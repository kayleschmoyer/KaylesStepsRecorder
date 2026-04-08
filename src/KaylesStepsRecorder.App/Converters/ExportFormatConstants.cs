using KaylesStepsRecorder.Core.Enums;

namespace KaylesStepsRecorder.App.Converters;

/// <summary>
/// Exposes <see cref="ExportFormat"/> values as XAML-friendly constants so
/// they can be used with {x:Static} in ComboBoxItem tags.
/// </summary>
public static class ExportFormatConstants
{
    public static ExportFormat HtmlFull => ExportFormat.HtmlFull;
    public static ExportFormat HtmlCompact => ExportFormat.HtmlCompact;
    public static ExportFormat Markdown => ExportFormat.Markdown;
    public static ExportFormat Pdf => ExportFormat.Pdf;
}

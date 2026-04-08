using System.Text;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Export;

/// <summary>
/// Produces polished, self-contained HTML reports from a recording session.
/// </summary>
public sealed class HtmlExportService : IExportService
{
    public bool SupportsFormat(ExportFormat format)
        => format is ExportFormat.HtmlFull or ExportFormat.HtmlCompact or ExportFormat.Pdf;

    public async Task<string> ExportAsync(RecordingSession session, ExportOptions options)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.OutputPath))
            throw new ArgumentException("OutputPath is required", nameof(options));

        bool compact = options.Format == ExportFormat.HtmlCompact;
        string templateName = compact ? "report-compact.html" : "report-full.html";
        string template = ExportTemplateEngine.LoadEmbedded(templateName);

        var steps = session.Steps
            .Where(s => !s.IsDeleted || options.IncludeDeletedSteps)
            .OrderBy(s => s.StepNumber)
            .ToList();

        string bugTitle = string.IsNullOrWhiteSpace(session.Metadata.BugTitle)
            ? "Recorded Session"
            : session.Metadata.BugTitle;

        var tokens = new Dictionary<string, string>
        {
            ["TITLE"] = ExportTemplateEngine.HtmlEncode(bugTitle),
            ["BUG_TITLE"] = ExportTemplateEngine.HtmlEncode(bugTitle),
            ["GENERATED_AT"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            ["METADATA_ITEMS"] = BuildMetadataItems(session.Metadata),
            ["META_BADGES"] = BuildCompactBadges(session.Metadata),
            ["RESULTS_SECTION"] = BuildResultsSection(session.Metadata),
            ["NOTES_SECTION"] = BuildNotesSection(session.Metadata),
            ["STEPS"] = compact
                ? BuildCompactSteps(steps, options)
                : BuildFullSteps(steps, options),
        };

        string html = ExportTemplateEngine.Render(template, tokens);

        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
        await File.WriteAllTextAsync(options.OutputPath, html, Encoding.UTF8).ConfigureAwait(false);

        return options.OutputPath;
    }

    // ------------------------------------------------------------------
    //  Metadata rendering
    // ------------------------------------------------------------------

    private static string BuildMetadataItems(SessionMetadata m)
    {
        var sb = new StringBuilder();
        AddMeta(sb, "Application", m.ApplicationUnderTest);
        AddMeta(sb, "Build / Version", m.BuildVersion);
        AddMeta(sb, "Tester", m.TesterName);
        AddMeta(sb, "Environment", m.TestEnvironment);
        AddMeta(sb, "Priority", m.Priority);
        AddMeta(sb, "Severity", m.Severity);
        return sb.ToString();
    }

    private static void AddMeta(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.Append("<div class=\"meta-item\"><div class=\"meta-label\">")
          .Append(ExportTemplateEngine.HtmlEncode(label))
          .Append("</div><div class=\"meta-value\">")
          .Append(ExportTemplateEngine.HtmlEncode(value))
          .Append("</div></div>");
    }

    private static string BuildCompactBadges(SessionMetadata m)
    {
        var sb = new StringBuilder();
        AddBadge(sb, m.ApplicationUnderTest);
        AddBadge(sb, m.BuildVersion);
        AddBadge(sb, m.TesterName);
        AddBadge(sb, m.TestEnvironment);
        return sb.ToString();
    }

    private static void AddBadge(StringBuilder sb, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.Append("<span>").Append(ExportTemplateEngine.HtmlEncode(value)).Append("</span>");
    }

    private static string BuildResultsSection(SessionMetadata m)
    {
        if (string.IsNullOrWhiteSpace(m.ExpectedResult) && string.IsNullOrWhiteSpace(m.ActualResult))
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"section\"><h2>Expected vs Actual</h2><div class=\"result-grid\">");
        if (!string.IsNullOrWhiteSpace(m.ExpectedResult))
        {
            sb.Append("<div class=\"result-block expected\"><h3>Expected</h3>")
              .Append(ExportTemplateEngine.HtmlEncode(m.ExpectedResult))
              .Append("</div>");
        }
        if (!string.IsNullOrWhiteSpace(m.ActualResult))
        {
            sb.Append("<div class=\"result-block actual\"><h3>Actual</h3>")
              .Append(ExportTemplateEngine.HtmlEncode(m.ActualResult))
              .Append("</div>");
        }
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string BuildNotesSection(SessionMetadata m)
    {
        if (string.IsNullOrWhiteSpace(m.AdditionalNotes)) return string.Empty;
        return $"<div class=\"section\"><h2>Additional Notes</h2><p>{ExportTemplateEngine.HtmlEncode(m.AdditionalNotes)}</p></div>";
    }

    // ------------------------------------------------------------------
    //  Step rendering - full
    // ------------------------------------------------------------------

    private static string BuildFullSteps(List<RecordedStep> steps, ExportOptions options)
    {
        var sb = new StringBuilder();
        foreach (var step in steps)
        {
            string cls = "step";
            if ((step.Flags & StepFlag.Important) != 0) cls += " important";
            if ((step.Flags & StepFlag.Bug) != 0) cls += " bug";

            sb.Append("<div class=\"").Append(cls).Append("\">")
              .Append("<div class=\"step-number\">").Append(step.StepNumber).Append("</div>")
              .Append("<div class=\"step-body\">")
              .Append("<div class=\"step-description\">")
              .Append(ExportTemplateEngine.HtmlEncode(step.Description))
              .Append("</div>");

            sb.Append("<div class=\"step-meta\">");
            if (options.IncludeTimestamps)
            {
                sb.Append("<span>").Append(step.Timestamp.ToLocalTime().ToString("HH:mm:ss")).Append("</span>");
            }
            if (step.Window != null && !string.IsNullOrWhiteSpace(step.Window.Title))
            {
                sb.Append("<span class=\"tag\">")
                  .Append(ExportTemplateEngine.HtmlEncode(step.Window.Title))
                  .Append("</span>");
            }
            if ((step.Flags & StepFlag.Important) != 0)
                sb.Append("<span class=\"tag important\">IMPORTANT</span>");
            if ((step.Flags & StepFlag.Bug) != 0)
                sb.Append("<span class=\"tag bug\">BUG</span>");
            if ((step.Flags & StepFlag.ExpectedResult) != 0)
                sb.Append("<span class=\"tag\">Expected</span>");
            if ((step.Flags & StepFlag.ActualResult) != 0)
                sb.Append("<span class=\"tag\">Actual</span>");

            if (options.IncludeCoordinates && step.Coordinates != null)
            {
                sb.Append("<span>(")
                  .Append(step.Coordinates.ScreenX).Append(", ")
                  .Append(step.Coordinates.ScreenY).Append(")</span>");
            }
            sb.Append("</div>");

            if (options.IncludeNotes && !string.IsNullOrWhiteSpace(step.UserNote))
            {
                sb.Append("<div class=\"step-note\">")
                  .Append(ExportTemplateEngine.HtmlEncode(step.UserNote))
                  .Append("</div>");
            }

            if (options.IncludeScreenshots && !step.IsRedacted && !string.IsNullOrEmpty(step.ScreenshotPath))
            {
                string src = ResolveImageSrc(step.ScreenshotPath, options);
                if (!string.IsNullOrEmpty(src))
                {
                    sb.Append("<div class=\"step-screenshot\"><img src=\"")
                      .Append(src)
                      .Append("\" alt=\"Step ").Append(step.StepNumber).Append("\"/></div>");
                }
            }

            sb.Append("</div></div>");
        }
        return sb.ToString();
    }

    private static string BuildCompactSteps(List<RecordedStep> steps, ExportOptions options)
    {
        var sb = new StringBuilder();
        foreach (var step in steps)
        {
            sb.Append("<li>")
              .Append("<span class=\"step-desc\">")
              .Append(ExportTemplateEngine.HtmlEncode(step.Description))
              .Append("</span>");

            if (options.IncludeTimestamps)
            {
                sb.Append("<span class=\"step-time\">")
                  .Append(step.Timestamp.ToLocalTime().ToString("HH:mm:ss"))
                  .Append("</span>");
            }

            if (options.IncludeNotes && !string.IsNullOrWhiteSpace(step.UserNote))
            {
                sb.Append("<div class=\"step-note\">")
                  .Append(ExportTemplateEngine.HtmlEncode(step.UserNote))
                  .Append("</div>");
            }

            if (options.IncludeScreenshots && !step.IsRedacted
                && !string.IsNullOrEmpty(step.ThumbnailPath ?? step.ScreenshotPath))
            {
                string src = ResolveImageSrc(step.ThumbnailPath ?? step.ScreenshotPath!, options);
                if (!string.IsNullOrEmpty(src))
                {
                    sb.Append("<div class=\"step-thumb\"><img src=\"")
                      .Append(src)
                      .Append("\"/></div>");
                }
            }

            sb.Append("</li>");
        }
        return sb.ToString();
    }

    private static string ResolveImageSrc(string imagePath, ExportOptions options)
    {
        if (!File.Exists(imagePath))
            return string.Empty;

        if (options.EmbedImagesAsBase64)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(imagePath);
                string ext = Path.GetExtension(imagePath).TrimStart('.').ToLowerInvariant();
                string mime = ext switch { "jpg" or "jpeg" => "image/jpeg", "gif" => "image/gif", _ => "image/png" };
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch
            {
                return string.Empty;
            }
        }

        try
        {
            string outputDir = Path.GetDirectoryName(options.OutputPath) ?? string.Empty;
            return Path.GetRelativePath(outputDir, imagePath).Replace('\\', '/');
        }
        catch
        {
            return imagePath;
        }
    }
}

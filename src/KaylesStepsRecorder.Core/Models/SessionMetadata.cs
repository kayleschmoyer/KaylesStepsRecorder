namespace KaylesStepsRecorder.Core.Models;

public sealed class SessionMetadata
{
    public string BugTitle { get; set; } = string.Empty;
    public string ApplicationUnderTest { get; set; } = string.Empty;
    public string BuildVersion { get; set; } = string.Empty;
    public string TesterName { get; set; } = string.Empty;
    public string TestEnvironment { get; set; } = string.Empty;
    public string ActualResult { get; set; } = string.Empty;
    public string ExpectedResult { get; set; } = string.Empty;
    public string AdditionalNotes { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

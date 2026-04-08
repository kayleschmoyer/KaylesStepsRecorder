using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface ISessionStorage
{
    Task<string> CreateSessionDirectoryAsync(string sessionId);
    Task SaveSessionAsync(RecordingSession session);
    Task<RecordingSession?> LoadSessionAsync(string sessionDirectory);
    Task<List<string>> GetRecentSessionsAsync(int count = 10);
    Task DeleteSessionAsync(string sessionDirectory);
    Task<RecordingSession?> RecoverCrashedSessionAsync();
    string GetScreenshotDirectory(string sessionDirectory);
}

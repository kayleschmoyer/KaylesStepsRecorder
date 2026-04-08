using System.Text.Json;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.Storage;

/// <summary>
/// File-system-backed implementation of <see cref="ISessionStorage"/>.
/// Each session is stored in its own directory under the configured base path,
/// with a <c>session.json</c> manifest and a <c>screenshots</c> subdirectory.
/// </summary>
public sealed class SessionStorage : ISessionStorage
{
    private const string SessionFileName = "session.json";
    private const string LockFileName = ".lock";
    private const string ScreenshotsFolderName = "screenshots";

    private readonly ILogger<SessionStorage> _logger;
    private readonly string _basePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SessionStorage(ILogger<SessionStorage> logger, AppSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(settings);

        _basePath = string.IsNullOrWhiteSpace(settings.SessionStoragePath)
            ? AppSettings.DefaultStoragePath
            : settings.SessionStoragePath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new IntPtrJsonConverter() },
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
    }

    /// <inheritdoc />
    public async Task<string> CreateSessionDirectoryAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        string sessionDirectory = Path.Combine(_basePath, sessionId);
        string screenshotsDirectory = Path.Combine(sessionDirectory, ScreenshotsFolderName);

        Directory.CreateDirectory(sessionDirectory);
        Directory.CreateDirectory(screenshotsDirectory);

        _logger.LogInformation("Created session directory: {SessionDirectory}", sessionDirectory);

        // Return completed task with directory path.
        await Task.CompletedTask;
        return sessionDirectory;
    }

    /// <inheritdoc />
    public async Task SaveSessionAsync(RecordingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(session.SessionDirectory))
        {
            throw new InvalidOperationException(
                "Session directory must be set before saving. Call CreateSessionDirectoryAsync first.");
        }

        string sessionFilePath = Path.Combine(session.SessionDirectory, SessionFileName);
        string lockFilePath = Path.Combine(session.SessionDirectory, LockFileName);

        try
        {
            // Write a .lock file while the session is still active (not completed).
            if (session.State is RecordingState.Recording or RecordingState.Paused or RecordingState.Idle
                && session.CompletedAt is null)
            {
                await WriteLockFileAsync(lockFilePath);
            }

            // Serialize session to a temporary file, then move atomically to avoid corruption.
            string tempFilePath = sessionFilePath + ".tmp";
            await using (FileStream stream = new(tempFilePath, FileMode.Create, FileAccess.Write,
                             FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, session, _jsonOptions);
            }

            File.Move(tempFilePath, sessionFilePath, overwrite: true);

            session.LastSavedAt = DateTime.UtcNow;
            session.IsDirty = false;

            // Remove the lock file when the session is completed/stopped.
            if (session.State == RecordingState.Stopped && session.CompletedAt is not null)
            {
                RemoveLockFile(lockFilePath);
            }

            _logger.LogDebug("Saved session {SessionId} to {Path}", session.SessionId, sessionFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session {SessionId} to {Path}",
                session.SessionId, sessionFilePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<RecordingSession?> LoadSessionAsync(string sessionDirectory)
    {
        if (string.IsNullOrWhiteSpace(sessionDirectory))
        {
            _logger.LogWarning("LoadSessionAsync called with null or empty session directory.");
            return null;
        }

        string sessionFilePath = Path.Combine(sessionDirectory, SessionFileName);

        if (!File.Exists(sessionFilePath))
        {
            _logger.LogWarning("Session file not found: {Path}", sessionFilePath);
            return null;
        }

        try
        {
            await using FileStream stream = new(sessionFilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 4096, useAsync: true);

            RecordingSession? session = await JsonSerializer.DeserializeAsync<RecordingSession>(stream, _jsonOptions);

            if (session is not null)
            {
                session.SessionDirectory = sessionDirectory;
                _logger.LogDebug("Loaded session {SessionId} from {Path}", session.SessionId, sessionFilePath);
            }

            return session;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Corrupt session file at {Path}. The file could not be deserialized.",
                sessionFilePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error reading session file at {Path}.", sessionFilePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetRecentSessionsAsync(int count = 10)
    {
        List<string> sessionDirectories = new();

        if (!Directory.Exists(_basePath))
        {
            _logger.LogInformation("Base session directory does not exist: {BasePath}", _basePath);
            return sessionDirectories;
        }

        try
        {
            var directories = Directory.GetDirectories(_basePath);
            var sessionsWithDates = new List<(string Directory, DateTime CreatedAt)>();

            foreach (string dir in directories)
            {
                string sessionFilePath = Path.Combine(dir, SessionFileName);
                if (!File.Exists(sessionFilePath))
                {
                    continue;
                }

                try
                {
                    // Use the file's last-write time as a lightweight proxy for creation date
                    // to avoid deserializing every session just to sort.
                    DateTime lastWrite = File.GetLastWriteTimeUtc(sessionFilePath);
                    sessionsWithDates.Add((dir, lastWrite));
                }
                catch (IOException)
                {
                    // Skip directories where we can't read file metadata.
                }
            }

            sessionDirectories = sessionsWithDates
                .OrderByDescending(s => s.CreatedAt)
                .Take(count)
                .Select(s => s.Directory)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for recent sessions in {BasePath}", _basePath);
        }

        await Task.CompletedTask;
        return sessionDirectories;
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(string sessionDirectory)
    {
        if (string.IsNullOrWhiteSpace(sessionDirectory))
        {
            _logger.LogWarning("DeleteSessionAsync called with null or empty session directory.");
            return;
        }

        if (!Directory.Exists(sessionDirectory))
        {
            _logger.LogWarning("Session directory does not exist: {Path}", sessionDirectory);
            return;
        }

        try
        {
            Directory.Delete(sessionDirectory, recursive: true);
            _logger.LogInformation("Deleted session directory: {Path}", sessionDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session directory: {Path}", sessionDirectory);
            throw;
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<RecordingSession?> RecoverCrashedSessionAsync()
    {
        if (!Directory.Exists(_basePath))
        {
            _logger.LogInformation("Base session directory does not exist. No sessions to recover.");
            return null;
        }

        RecordingSession? mostRecentCrashed = null;
        DateTime mostRecentTime = DateTime.MinValue;

        try
        {
            var directories = Directory.GetDirectories(_basePath);

            foreach (string dir in directories)
            {
                string lockFilePath = Path.Combine(dir, LockFileName);
                string sessionFilePath = Path.Combine(dir, SessionFileName);

                // A crashed session has a .lock file but was never cleanly completed.
                if (!File.Exists(lockFilePath) || !File.Exists(sessionFilePath))
                {
                    continue;
                }

                RecordingSession? session = await LoadSessionAsync(dir);
                if (session is null)
                {
                    continue;
                }

                // Only recover sessions that were not completed (i.e., crashed mid-recording).
                if (session.CompletedAt is not null && session.State == RecordingState.Stopped)
                {
                    // The lock file is stale; clean it up.
                    RemoveLockFile(lockFilePath);
                    continue;
                }

                DateTime sessionTime = session.LastSavedAt != default
                    ? session.LastSavedAt
                    : session.CreatedAt;

                if (sessionTime > mostRecentTime)
                {
                    mostRecentTime = sessionTime;
                    mostRecentCrashed = session;
                }
            }

            if (mostRecentCrashed is not null)
            {
                mostRecentCrashed.IsRecovered = true;
                _logger.LogInformation(
                    "Recovered crashed session {SessionId} from {Path}",
                    mostRecentCrashed.SessionId, mostRecentCrashed.SessionDirectory);
            }
            else
            {
                _logger.LogDebug("No crashed sessions found to recover.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during crash recovery scan in {BasePath}", _basePath);
        }

        return mostRecentCrashed;
    }

    /// <inheritdoc />
    public string GetScreenshotDirectory(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        return Path.Combine(sessionDirectory, ScreenshotsFolderName);
    }

    private static async Task WriteLockFileAsync(string lockFilePath)
    {
        string lockContent = $"{Environment.MachineName}|{Environment.ProcessId}|{DateTime.UtcNow:O}";
        await File.WriteAllTextAsync(lockFilePath, lockContent);
    }

    private void RemoveLockFile(string lockFilePath)
    {
        try
        {
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not remove lock file: {Path}", lockFilePath);
        }
    }
}

using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.Storage;

/// <summary>
/// Manages screenshot image files on disk for recording sessions.
/// Provides save, cleanup, and disk-usage calculation capabilities.
/// </summary>
public sealed class ImageStorage
{
    private readonly ILogger<ImageStorage> _logger;

    public ImageStorage(ILogger<ImageStorage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Saves screenshot image data to disk with a unique file name.
    /// </summary>
    /// <param name="imageData">The raw image bytes (e.g., PNG-encoded screenshot).</param>
    /// <param name="directory">The directory to save the image in (typically the screenshots subdirectory).</param>
    /// <param name="prefix">
    /// A prefix for the file name, usually incorporating the step number (e.g., "step_003").
    /// </param>
    /// <returns>The full path to the saved image file.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="imageData"/> is empty
    /// or <paramref name="directory"/> is null/empty.</exception>
    public async Task<string> SaveScreenshotAsync(byte[] imageData, string directory, string prefix)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        if (imageData.Length == 0)
        {
            throw new ArgumentException("Image data must not be empty.", nameof(imageData));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        Directory.CreateDirectory(directory);

        // Build a unique file name: prefix_timestamp_guid.png
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        string uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        string fileName = $"{prefix}_{timestamp}_{uniqueSuffix}.png";
        string filePath = Path.Combine(directory, fileName);

        try
        {
            await using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 4096, useAsync: true);
            await stream.WriteAsync(imageData);

            _logger.LogDebug("Saved screenshot: {FilePath} ({Bytes} bytes)", filePath, imageData.Length);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save screenshot to {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Deletes image files in the session's screenshots directory that are not referenced by any step.
    /// </summary>
    /// <param name="sessionDirectory">The root directory of the session.</param>
    /// <param name="referencedPaths">
    /// All screenshot and thumbnail paths currently referenced by the session's steps.
    /// </param>
    /// <returns>The number of orphaned files that were deleted.</returns>
    public int CleanupOrphanedImages(string sessionDirectory, List<string> referencedPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        ArgumentNullException.ThrowIfNull(referencedPaths);

        string screenshotsDirectory = Path.Combine(sessionDirectory, "screenshots");
        if (!Directory.Exists(screenshotsDirectory))
        {
            _logger.LogDebug("Screenshots directory does not exist: {Path}", screenshotsDirectory);
            return 0;
        }

        // Normalize all referenced paths for reliable comparison.
        HashSet<string> normalizedReferences = new(
            referencedPaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetFullPath(p)),
            StringComparer.OrdinalIgnoreCase);

        int deletedCount = 0;
        string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        try
        {
            foreach (string filePath in Directory.EnumerateFiles(screenshotsDirectory))
            {
                string extension = Path.GetExtension(filePath);
                if (!imageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string normalizedFilePath = Path.GetFullPath(filePath);
                if (normalizedReferences.Contains(normalizedFilePath))
                {
                    continue;
                }

                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                    _logger.LogDebug("Deleted orphaned image: {FilePath}", filePath);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Could not delete orphaned image: {FilePath}", filePath);
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Cleaned up {Count} orphaned image(s) from {Path}", deletedCount, screenshotsDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during orphaned image cleanup in {Path}", screenshotsDirectory);
        }

        return deletedCount;
    }

    /// <summary>
    /// Calculates the total disk space used by all files in the session directory (recursively).
    /// </summary>
    /// <param name="sessionDirectory">The root directory of the session.</param>
    /// <returns>Total size in bytes, or 0 if the directory does not exist.</returns>
    public long GetSessionDiskUsage(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);

        if (!Directory.Exists(sessionDirectory))
        {
            _logger.LogDebug("Session directory does not exist for disk usage calculation: {Path}",
                sessionDirectory);
            return 0;
        }

        try
        {
            long totalBytes = 0;

            foreach (string filePath in Directory.EnumerateFiles(sessionDirectory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    FileInfo fileInfo = new(filePath);
                    totalBytes += fileInfo.Length;
                }
                catch (IOException)
                {
                    // File may have been deleted between enumeration and access; skip it.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files we cannot access.
                }
            }

            _logger.LogDebug("Session disk usage for {Path}: {Bytes} bytes", sessionDirectory, totalBytes);
            return totalBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating disk usage for {Path}", sessionDirectory);
            return 0;
        }
    }
}

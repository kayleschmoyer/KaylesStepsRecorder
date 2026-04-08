using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface IScreenCaptureService
{
    Task<string?> CaptureWindowAsync(WindowInfo window, string outputDirectory, string filePrefix);
    Task<string?> CaptureFullScreenAsync(int screenX, int screenY, string outputDirectory, string filePrefix);
    Task<string?> CaptureRegionAsync(int x, int y, int width, int height, string outputDirectory, string filePrefix);
    string? CreateThumbnail(string sourcePath, int maxWidth = 320);
    Task<string?> AddClickHighlightAsync(string imagePath, int relativeX, int relativeY, int radius = 20);
    Task<string?> RedactRegionAsync(string imagePath, int x, int y, int width, int height);
}

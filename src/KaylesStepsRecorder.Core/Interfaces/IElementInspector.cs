using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface IElementInspector
{
    Task<ElementInfo?> GetElementAtPointAsync(int screenX, int screenY, CancellationToken ct = default);
}

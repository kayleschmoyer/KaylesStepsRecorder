using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Core.Interfaces;

public interface IInputHookService : IDisposable
{
    event EventHandler<InputEvent>? InputReceived;
    void Install();
    void Uninstall();
    bool IsInstalled { get; }
}

using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Hooks;

public sealed class InputHookService : IInputHookService
{
    private readonly object _lock = new();
    private readonly EventDebouncer _debouncer;

    private MouseHookHandler? _mouseHandler;
    private KeyboardHookHandler? _keyboardHandler;
    private bool _disposed;

    public event EventHandler<InputEvent>? InputReceived;

    public bool IsInstalled
    {
        get
        {
            lock (_lock)
            {
                return _mouseHandler is not null || _keyboardHandler is not null;
            }
        }
    }

    public InputHookService()
        : this(TimeSpan.FromMilliseconds(200))
    {
    }

    public InputHookService(TimeSpan debounceInterval)
    {
        _debouncer = new EventDebouncer(debounceInterval);
        _debouncer.OnDebouncedEvent += OnDebouncedEvent;
    }

    public void Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_mouseHandler is not null || _keyboardHandler is not null)
                return;

            _debouncer.Reset();

            var mouse = new MouseHookHandler();
            var keyboard = new KeyboardHookHandler();

            try
            {
                mouse.OnMouseEvent += OnRawInputEvent;
                keyboard.OnKeyboardEvent += OnRawInputEvent;

                mouse.Install();
                keyboard.Install();

                _mouseHandler = mouse;
                _keyboardHandler = keyboard;
            }
            catch
            {
                // Clean up on partial failure.
                mouse.Dispose();
                keyboard.Dispose();
                throw;
            }
        }
    }

    public void Uninstall()
    {
        lock (_lock)
        {
            if (_mouseHandler is null && _keyboardHandler is null)
                return;

            _mouseHandler?.Dispose();
            _mouseHandler = null;

            _keyboardHandler?.Dispose();
            _keyboardHandler = null;

            // Flush any accumulated events (e.g. pending scroll).
            _debouncer.Flush();
        }
    }

    private void OnRawInputEvent(InputEvent inputEvent)
    {
        _debouncer.Submit(inputEvent);
    }

    private void OnDebouncedEvent(InputEvent inputEvent)
    {
        InputReceived?.Invoke(this, inputEvent);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Uninstall();
        _debouncer.OnDebouncedEvent -= OnDebouncedEvent;
    }
}

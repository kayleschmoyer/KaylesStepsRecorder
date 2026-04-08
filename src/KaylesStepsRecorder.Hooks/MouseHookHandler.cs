using System.Diagnostics;
using System.Runtime.InteropServices;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Hooks;

internal sealed class MouseHookHandler : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelHookProc? _hookProc;
    private bool _disposed;

    public event Action<InputEvent>? OnMouseEvent;

    public void Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookId != IntPtr.Zero)
            return;

        // Hold a reference to the delegate to prevent garbage collection.
        _hookProc = HookCallback;

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule
            ?? throw new InvalidOperationException("Unable to obtain the main module of the current process.");

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(currentModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Failed to install mouse hook. Win32 error code: {errorCode}");
        }
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _hookProc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            int message = wParam.ToInt32();

            InputEvent? inputEvent = CreateInputEvent(message, hookStruct);
            if (inputEvent is not null)
            {
                OnMouseEvent?.Invoke(inputEvent);
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static InputEvent? CreateInputEvent(int message, NativeMethods.MSLLHOOKSTRUCT hookStruct)
    {
        ActionType? actionType = message switch
        {
            NativeMethods.WM_LBUTTONDOWN => ActionType.LeftClick,
            NativeMethods.WM_RBUTTONDOWN => ActionType.RightClick,
            NativeMethods.WM_LBUTTONDBLCLK => ActionType.DoubleClick,
            NativeMethods.WM_MBUTTONDOWN => ActionType.MiddleClick,
            NativeMethods.WM_MOUSEWHEEL => ActionType.Scroll,
            _ => null
        };

        if (actionType is null)
            return null;

        int scrollDelta = 0;
        if (actionType == ActionType.Scroll)
        {
            // The high-order word of mouseData contains the wheel delta.
            // A positive value indicates forward rotation; negative indicates backward.
            scrollDelta = unchecked((short)(hookStruct.mouseData >> 16));
        }

        return new InputEvent
        {
            Timestamp = DateTime.UtcNow,
            ActionType = actionType.Value,
            ScreenX = hookStruct.pt.X,
            ScreenY = hookStruct.pt.Y,
            ScrollDelta = scrollDelta,
            KeyData = null,
            IsModifierKey = false
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Uninstall();
        _disposed = true;
    }
}

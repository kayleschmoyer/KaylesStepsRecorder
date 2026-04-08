using System.Diagnostics;
using System.Runtime.InteropServices;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Hooks;

internal sealed class KeyboardHookHandler : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelHookProc? _hookProc;
    private bool _disposed;

    // Modifier key tracking
    private bool _ctrlPressed;
    private bool _altPressed;
    private bool _shiftPressed;
    private bool _winPressed;

    // Virtual key codes for modifier keys
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RSHIFT = 0xA1;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;   // Left Alt
    private const uint VK_RMENU = 0xA5;   // Right Alt
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const uint VK_SHIFT = 0x10;
    private const uint VK_CONTROL = 0x11;
    private const uint VK_MENU = 0x12;     // Alt

    // WM_KEYUP and WM_SYSKEYUP for modifier release tracking
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    public event Action<InputEvent>? OnKeyboardEvent;

    public void Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookId != IntPtr.Zero)
            return;

        _hookProc = HookCallback;

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule
            ?? throw new InvalidOperationException("Unable to obtain the main module of the current process.");

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(currentModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Failed to install keyboard hook. Win32 error code: {errorCode}");
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
        ResetModifiers();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int message = wParam.ToInt32();

            UpdateModifierState(hookStruct.vkCode, message);

            if (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN)
            {
                InputEvent? inputEvent = CreateInputEvent(hookStruct.vkCode);
                if (inputEvent is not null)
                {
                    OnKeyboardEvent?.Invoke(inputEvent);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void UpdateModifierState(uint vkCode, int message)
    {
        bool isDown = message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN;

        switch (vkCode)
        {
            case VK_LCONTROL or VK_RCONTROL or VK_CONTROL:
                _ctrlPressed = isDown;
                break;
            case VK_LMENU or VK_RMENU or VK_MENU:
                _altPressed = isDown;
                break;
            case VK_LSHIFT or VK_RSHIFT or VK_SHIFT:
                _shiftPressed = isDown;
                break;
            case VK_LWIN or VK_RWIN:
                _winPressed = isDown;
                break;
        }
    }

    private InputEvent? CreateInputEvent(uint vkCode)
    {
        bool isModifier = IsModifierKey(vkCode);

        // Don't fire events for modifier keys pressed alone.
        if (isModifier)
            return null;

        string keyName = MapVirtualKeyToName(vkCode);
        bool hasModifier = _ctrlPressed || _altPressed || _winPressed;

        // Shift alone does not constitute a shortcut (Shift+A is just typing 'A').
        // But Shift combined with another modifier does.
        bool isShortcut = hasModifier || (_shiftPressed && hasModifier);

        if (isShortcut)
        {
            string combo = BuildComboString(keyName);
            return new InputEvent
            {
                Timestamp = DateTime.UtcNow,
                ActionType = ActionType.KeyboardShortcut,
                KeyData = combo,
                IsModifierKey = false
            };
        }

        return new InputEvent
        {
            Timestamp = DateTime.UtcNow,
            ActionType = ActionType.KeyboardInput,
            KeyData = keyName,
            IsModifierKey = false
        };
    }

    private string BuildComboString(string keyName)
    {
        var parts = new List<string>(4);

        if (_ctrlPressed) parts.Add("Ctrl");
        if (_altPressed) parts.Add("Alt");
        if (_shiftPressed) parts.Add("Shift");
        if (_winPressed) parts.Add("Win");

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static bool IsModifierKey(uint vkCode)
    {
        return vkCode is VK_LSHIFT or VK_RSHIFT or VK_SHIFT
            or VK_LCONTROL or VK_RCONTROL or VK_CONTROL
            or VK_LMENU or VK_RMENU or VK_MENU
            or VK_LWIN or VK_RWIN;
    }

    private static string MapVirtualKeyToName(uint vkCode) => vkCode switch
    {
        // Letters A-Z (0x41-0x5A)
        >= 0x41 and <= 0x5A => ((char)vkCode).ToString(),

        // Digits 0-9 (0x30-0x39)
        >= 0x30 and <= 0x39 => ((char)vkCode).ToString(),

        // Function keys F1-F24
        >= 0x70 and <= 0x87 => $"F{vkCode - 0x70 + 1}",

        // Numpad 0-9
        >= 0x60 and <= 0x69 => $"Num{vkCode - 0x60}",

        // Common keys
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x14 => "CapsLock",
        0x1B => "Escape",
        0x20 => "Space",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2C => "PrintScreen",
        0x2D => "Insert",
        0x2E => "Delete",

        // Numpad operators
        0x6A => "Num*",
        0x6B => "Num+",
        0x6D => "Num-",
        0x6E => "Num.",
        0x6F => "Num/",

        // Lock keys
        0x90 => "NumLock",
        0x91 => "ScrollLock",

        // OEM keys (US keyboard layout)
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",

        _ => $"VK_{vkCode:X2}"
    };

    private void ResetModifiers()
    {
        _ctrlPressed = false;
        _altPressed = false;
        _shiftPressed = false;
        _winPressed = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Uninstall();
        _disposed = true;
    }
}

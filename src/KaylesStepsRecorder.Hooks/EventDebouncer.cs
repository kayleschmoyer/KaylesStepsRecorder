using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Models;

namespace KaylesStepsRecorder.Hooks;

internal sealed class EventDebouncer
{
    private readonly TimeSpan _debounceInterval;
    private readonly object _lock = new();

    // Tracks the last emitted event per action type for deduplication.
    private DateTime _lastClickTime = DateTime.MinValue;
    private ActionType _lastClickAction;

    private DateTime _lastKeyTime = DateTime.MinValue;
    private string? _lastKeyData;

    // Scroll accumulation state
    private DateTime _lastScrollTime = DateTime.MinValue;
    private int _accumulatedScrollDelta;
    private int _lastScrollX;
    private int _lastScrollY;
    private Timer? _scrollFlushTimer;

    public event Action<InputEvent>? OnDebouncedEvent;

    public EventDebouncer(TimeSpan? debounceInterval = null)
    {
        _debounceInterval = debounceInterval ?? TimeSpan.FromMilliseconds(200);
    }

    public void Submit(InputEvent inputEvent)
    {
        lock (_lock)
        {
            switch (inputEvent.ActionType)
            {
                case ActionType.LeftClick:
                case ActionType.RightClick:
                case ActionType.DoubleClick:
                case ActionType.MiddleClick:
                    HandleClickEvent(inputEvent);
                    break;

                case ActionType.KeyboardInput:
                case ActionType.KeyboardShortcut:
                    HandleKeyboardEvent(inputEvent);
                    break;

                case ActionType.Scroll:
                    HandleScrollEvent(inputEvent);
                    break;

                default:
                    // All other event types pass through without debouncing.
                    EmitEvent(inputEvent);
                    break;
            }
        }
    }

    private void HandleClickEvent(InputEvent inputEvent)
    {
        DateTime now = inputEvent.Timestamp;

        // Suppress duplicate click of the same type within the debounce window.
        if (inputEvent.ActionType == _lastClickAction
            && (now - _lastClickTime) < _debounceInterval)
        {
            return;
        }

        // A DoubleClick naturally follows a LeftClick. When we see a DoubleClick
        // within the debounce window of a LeftClick, allow the DoubleClick through
        // because it is a different (more specific) action type.
        _lastClickTime = now;
        _lastClickAction = inputEvent.ActionType;

        EmitEvent(inputEvent);
    }

    private void HandleKeyboardEvent(InputEvent inputEvent)
    {
        DateTime now = inputEvent.Timestamp;

        // Suppress duplicate keyboard events with the same key data within the window.
        if (inputEvent.KeyData == _lastKeyData
            && (now - _lastKeyTime) < _debounceInterval)
        {
            return;
        }

        _lastKeyTime = now;
        _lastKeyData = inputEvent.KeyData;

        EmitEvent(inputEvent);
    }

    private void HandleScrollEvent(InputEvent inputEvent)
    {
        DateTime now = inputEvent.Timestamp;

        bool isNewScrollSequence = (now - _lastScrollTime) >= _debounceInterval;

        if (isNewScrollSequence && _accumulatedScrollDelta != 0)
        {
            // Flush the previous scroll batch.
            FlushScroll();
        }

        if (isNewScrollSequence)
        {
            _accumulatedScrollDelta = 0;
        }

        _accumulatedScrollDelta += inputEvent.ScrollDelta;
        _lastScrollX = inputEvent.ScreenX;
        _lastScrollY = inputEvent.ScreenY;
        _lastScrollTime = now;

        // Reset or start a timer to flush accumulated scroll after the debounce window.
        _scrollFlushTimer?.Dispose();
        _scrollFlushTimer = new Timer(
            _ => FlushScrollTimerCallback(),
            null,
            _debounceInterval,
            Timeout.InfiniteTimeSpan);
    }

    private void FlushScrollTimerCallback()
    {
        lock (_lock)
        {
            FlushScroll();
        }
    }

    private void FlushScroll()
    {
        if (_accumulatedScrollDelta == 0)
            return;

        var scrollEvent = new InputEvent
        {
            Timestamp = _lastScrollTime,
            ActionType = ActionType.Scroll,
            ScreenX = _lastScrollX,
            ScreenY = _lastScrollY,
            ScrollDelta = _accumulatedScrollDelta,
            KeyData = null,
            IsModifierKey = false
        };

        _accumulatedScrollDelta = 0;
        EmitEvent(scrollEvent);
    }

    private void EmitEvent(InputEvent inputEvent)
    {
        OnDebouncedEvent?.Invoke(inputEvent);
    }

    /// <summary>
    /// Flushes any pending scroll events. Call when stopping recording to ensure
    /// no accumulated scroll data is lost.
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            _scrollFlushTimer?.Dispose();
            _scrollFlushTimer = null;
            FlushScroll();
        }
    }

    /// <summary>
    /// Resets all internal tracking state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _scrollFlushTimer?.Dispose();
            _scrollFlushTimer = null;

            _lastClickTime = DateTime.MinValue;
            _lastClickAction = default;

            _lastKeyTime = DateTime.MinValue;
            _lastKeyData = null;

            _lastScrollTime = DateTime.MinValue;
            _accumulatedScrollDelta = 0;
            _lastScrollX = 0;
            _lastScrollY = 0;
        }
    }
}

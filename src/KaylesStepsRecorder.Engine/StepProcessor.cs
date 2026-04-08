using System.Collections.Concurrent;
using System.Text;
using KaylesStepsRecorder.Core.Enums;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.Engine;

/// <summary>
/// Handles the asynchronous pipeline of processing a single input event into a <see cref="RecordedStep"/>.
/// Extracted from <see cref="RecordingEngine"/> for testability and separation of concerns.
/// </summary>
internal sealed class StepProcessor
{
    private readonly ILogger _logger;
    private readonly object _textAccumulationLock = new();
    private readonly StringBuilder _accumulatedText = new();
    private DateTime _lastKeyTimestamp = DateTime.MinValue;
    private ActionType _lastKeyActionType = ActionType.KeyboardInput;

    /// <summary>
    /// Maximum gap between keystrokes before the accumulated text is flushed.
    /// Keystrokes arriving within this window are accumulated into a single TextEntry step.
    /// </summary>
    private static readonly TimeSpan KeyAccumulationWindow = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Timeout applied when inspecting the UI element at the click point.
    /// If automation is slow or unresponsive we do not want to block the pipeline.
    /// </summary>
    private static readonly TimeSpan ElementInspectionTimeout = TimeSpan.FromSeconds(2);

    public StepProcessor(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a single <see cref="InputEvent"/> through the full capture pipeline and
    /// returns a completed <see cref="RecordedStep"/>, or <c>null</c> if processing fails gracefully.
    /// </summary>
    public async Task<RecordedStep?> ProcessInputAsync(
        InputEvent input,
        AppSettings settings,
        IWindowTracker windowTracker,
        IElementInspector elementInspector,
        IScreenCaptureService captureService,
        IStepDescriptionBuilder descriptionBuilder,
        string screenshotDirectory)
    {
        try
        {
            // --- 1. Determine the target window ---
            WindowInfo? window = windowTracker.GetWindowAtPoint(input.ScreenX, input.ScreenY)
                                 ?? windowTracker.GetForegroundWindow();

            // --- 2. Handle keyboard text accumulation ---
            string? accumulatedTextSnapshot = null;
            ActionType effectiveActionType = input.ActionType;

            if (IsTextInputAction(input))
            {
                accumulatedTextSnapshot = AccumulateKeystrokes(input);

                // If accumulation is still in progress we return null so the engine
                // knows not to emit a step yet.  A later keystroke (or a non-keyboard
                // event) will flush the buffer.
                if (accumulatedTextSnapshot == null)
                {
                    return null;
                }

                effectiveActionType = ActionType.TextEntry;
            }
            else
            {
                // Any non-text event flushes accumulated text. The flushed text will
                // be returned as a separate step by the caller on a subsequent flush
                // check (see FlushAccumulatedText below). We don't create two steps here
                // to keep the method's contract simple.
            }

            // --- 3. Inspect the UI element at the click/action point ---
            ElementInfo? element = null;
            try
            {
                using var cts = new CancellationTokenSource(ElementInspectionTimeout);
                element = await elementInspector.GetElementAtPointAsync(
                    input.ScreenX, input.ScreenY, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Element inspection timed out at ({ScreenX}, {ScreenY})",
                    input.ScreenX, input.ScreenY);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Element inspection failed at ({ScreenX}, {ScreenY})",
                    input.ScreenX, input.ScreenY);
            }

            // --- 4. Wait for the UI to settle after the user action ---
            if (settings.CaptureDelayMs > 0)
            {
                await Task.Delay(settings.CaptureDelayMs).ConfigureAwait(false);
            }

            // --- 5. Compute click coordinates relative to the window ---
            ClickCoordinates? coordinates = ComputeClickCoordinates(input, window);

            // --- 6. Capture screenshot ---
            string? screenshotPath = null;
            try
            {
                screenshotPath = window != null
                    ? await captureService.CaptureWindowAsync(
                        window, screenshotDirectory, $"step_{input.Timestamp:yyyyMMdd_HHmmss_fff}").ConfigureAwait(false)
                    : await captureService.CaptureFullScreenAsync(
                        input.ScreenX, input.ScreenY, screenshotDirectory,
                        $"step_{input.Timestamp:yyyyMMdd_HHmmss_fff}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Screenshot capture failed");
            }

            // --- 7. Create thumbnail ---
            string? thumbnailPath = null;
            if (!string.IsNullOrEmpty(screenshotPath))
            {
                try
                {
                    thumbnailPath = captureService.CreateThumbnail(screenshotPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Thumbnail creation failed for {Path}", screenshotPath);
                }
            }

            // --- 8. Add click highlight overlay ---
            if (!string.IsNullOrEmpty(screenshotPath) && settings.ShowClickHighlight && coordinates != null
                && IsClickAction(effectiveActionType))
            {
                try
                {
                    int highlightX = window != null ? coordinates.WindowRelativeX : coordinates.ScreenX;
                    int highlightY = window != null ? coordinates.WindowRelativeY : coordinates.ScreenY;

                    await captureService.AddClickHighlightAsync(
                        screenshotPath, highlightX, highlightY,
                        settings.ClickHighlightRadius).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Click highlight failed");
                }
            }

            // --- 9. Build the human-readable description ---
            string description;
            try
            {
                description = descriptionBuilder.BuildDescription(
                    effectiveActionType, window, element, coordinates,
                    keyData: input.KeyData,
                    textEntered: accumulatedTextSnapshot,
                    scrollDelta: input.ScrollDelta);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Description builder failed");
                description = $"{effectiveActionType} at ({input.ScreenX}, {input.ScreenY})";
            }

            // --- 10. Assemble the RecordedStep ---
            var step = new RecordedStep
            {
                Timestamp = input.Timestamp,
                ActionType = effectiveActionType,
                Description = description,
                Coordinates = coordinates,
                Window = window,
                Element = element,
                ScreenshotPath = screenshotPath,
                ThumbnailPath = thumbnailPath,
                KeysPressed = input.KeyData,
                TextEntered = accumulatedTextSnapshot,
                ScrollDelta = input.ScrollDelta,
                ShowClickHighlight = settings.ShowClickHighlight && IsClickAction(effectiveActionType),
            };

            return step;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing input event {ActionType}", input.ActionType);
            return null;
        }
    }

    /// <summary>
    /// Attempts to flush any accumulated text that has been buffered from rapid keystrokes.
    /// Returns a partially built <see cref="RecordedStep"/> containing only keyboard/text fields,
    /// or <c>null</c> if no text is pending.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for filling in screenshot and other fields if desired.
    /// Typically this is called when a non-keyboard event arrives or when a timer fires.
    /// </remarks>
    public RecordedStep? FlushAccumulatedText()
    {
        lock (_textAccumulationLock)
        {
            if (_accumulatedText.Length == 0)
                return null;

            string text = _accumulatedText.ToString();
            _accumulatedText.Clear();

            return new RecordedStep
            {
                Timestamp = _lastKeyTimestamp,
                ActionType = ActionType.TextEntry,
                Description = $"Typed \"{TruncateForDisplay(text)}\"",
                TextEntered = text,
                KeysPressed = null,
            };
        }
    }

    /// <summary>
    /// Checks whether there is buffered text whose accumulation window has expired.
    /// </summary>
    public bool HasExpiredAccumulatedText()
    {
        lock (_textAccumulationLock)
        {
            return _accumulatedText.Length > 0
                   && DateTime.UtcNow - _lastKeyTimestamp > KeyAccumulationWindow;
        }
    }

    /// <summary>
    /// Resets any internal accumulated state. Call on session end/cancel.
    /// </summary>
    public void Reset()
    {
        lock (_textAccumulationLock)
        {
            _accumulatedText.Clear();
            _lastKeyTimestamp = DateTime.MinValue;
        }
    }

    // ------------------------------------------------------------------
    //  Private helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Accumulates keystrokes into a text buffer. Returns the accumulated text when
    /// the accumulation window expires, or <c>null</c> if accumulation is still in progress.
    /// </summary>
    private string? AccumulateKeystrokes(InputEvent input)
    {
        lock (_textAccumulationLock)
        {
            bool windowExpired = _accumulatedText.Length > 0
                                 && input.Timestamp - _lastKeyTimestamp > KeyAccumulationWindow;

            string? result = null;

            if (windowExpired)
            {
                // The previous accumulation window has expired -- flush it.
                result = _accumulatedText.ToString();
                _accumulatedText.Clear();
            }

            // Append the current keystroke.
            if (!string.IsNullOrEmpty(input.KeyData) && !input.IsModifierKey)
            {
                _accumulatedText.Append(input.KeyData);
            }

            _lastKeyTimestamp = input.Timestamp;
            _lastKeyActionType = input.ActionType;

            return result;
        }
    }

    private static ClickCoordinates? ComputeClickCoordinates(InputEvent input, WindowInfo? window)
    {
        if (input.ActionType == ActionType.Scroll && input.ScreenX == 0 && input.ScreenY == 0)
            return null;

        int relX = input.ScreenX;
        int relY = input.ScreenY;

        if (window != null)
        {
            relX = input.ScreenX - window.Left;
            relY = input.ScreenY - window.Top;
        }

        return new ClickCoordinates
        {
            ScreenX = input.ScreenX,
            ScreenY = input.ScreenY,
            WindowRelativeX = relX,
            WindowRelativeY = relY,
        };
    }

    private static bool IsTextInputAction(InputEvent input)
    {
        return input.ActionType is ActionType.TextEntry or ActionType.KeyboardInput
               && !input.IsModifierKey
               && input.ActionType != ActionType.KeyboardShortcut;
    }

    private static bool IsClickAction(ActionType actionType)
    {
        return actionType is ActionType.LeftClick
            or ActionType.RightClick
            or ActionType.DoubleClick
            or ActionType.MiddleClick
            or ActionType.MenuItemSelected;
    }

    private static string TruncateForDisplay(string text, int maxLength = 60)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Length <= maxLength
            ? text
            : string.Concat(text.AsSpan(0, maxLength), "...");
    }
}

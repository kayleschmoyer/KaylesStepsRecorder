using System.Windows.Automation;
using KaylesStepsRecorder.Core.Interfaces;
using KaylesStepsRecorder.Core.Models;
using Microsoft.Extensions.Logging;

namespace KaylesStepsRecorder.Automation;

public sealed class ElementInspector : IElementInspector
{
    private static readonly TimeSpan InspectionTimeout = TimeSpan.FromSeconds(2);

    private readonly ILogger<ElementInspector> _logger;

    public ElementInspector(ILogger<ElementInspector> logger)
    {
        _logger = logger;
    }

    public async Task<ElementInfo?> GetElementAtPointAsync(int screenX, int screenY,
        CancellationToken ct = default)
    {
        try
        {
            // Create a linked token that enforces the 2-second timeout.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(InspectionTimeout);
            var linkedToken = timeoutCts.Token;

            return await Task.Run(() => InspectElementAtPoint(screenX, screenY, linkedToken),
                linkedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Element inspection timed out at ({ScreenX}, {ScreenY})", screenX, screenY);
            return null;
        }
        catch (OperationCanceledException)
        {
            // Caller-requested cancellation; propagate as null rather than throwing,
            // keeping the "never throws" contract.
            _logger.LogDebug("Element inspection cancelled by caller");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Element inspection failed at ({ScreenX}, {ScreenY})", screenX, screenY);
            return null;
        }
    }

    private ElementInfo? InspectElementAtPoint(int screenX, int screenY,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        AutomationElement element;
        try
        {
            var point = new System.Windows.Point(screenX, screenY);
            element = AutomationElement.FromPoint(point);
        }
        catch (ElementNotAvailableException ex)
        {
            _logger.LogDebug(ex,
                "Element not available at ({ScreenX}, {ScreenY})", screenX, screenY);
            return null;
        }

        if (element == null)
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            var current = element.Current;

            var name = current.Name ?? string.Empty;
            var controlType = current.LocalizedControlType ?? string.Empty;
            var automationId = current.AutomationId ?? string.Empty;
            var className = current.ClassName ?? string.Empty;
            var isEnabled = current.IsEnabled;
            var isOffscreen = current.IsOffscreen;

            var boundingRect = current.BoundingRectangle;
            int boundingLeft = 0, boundingTop = 0, boundingWidth = 0, boundingHeight = 0;

            if (!boundingRect.IsEmpty && !double.IsInfinity(boundingRect.Width))
            {
                boundingLeft = (int)boundingRect.X;
                boundingTop = (int)boundingRect.Y;
                boundingWidth = (int)boundingRect.Width;
                boundingHeight = (int)boundingRect.Height;
            }

            // Attempt to read the Value property from the ValuePattern.
            var value = TryGetValueFromPattern(element);

            ct.ThrowIfCancellationRequested();

            return new ElementInfo
            {
                Name = name,
                ControlType = controlType,
                AutomationId = automationId,
                ClassName = className,
                Value = value,
                IsEnabled = isEnabled,
                IsOffscreen = isOffscreen,
                BoundingLeft = boundingLeft,
                BoundingTop = boundingTop,
                BoundingWidth = boundingWidth,
                BoundingHeight = boundingHeight
            };
        }
        catch (ElementNotAvailableException ex)
        {
            _logger.LogDebug(ex,
                "Element became unavailable during inspection at ({ScreenX}, {ScreenY})",
                screenX, screenY);
            return null;
        }
    }

    private string TryGetValueFromPattern(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) &&
                pattern is ValuePattern valuePattern)
            {
                return valuePattern.Current.Value ?? string.Empty;
            }
        }
        catch (ElementNotAvailableException)
        {
            // Element disappeared; not critical.
        }
        catch (InvalidOperationException)
        {
            // Pattern not supported or element state changed.
        }

        return string.Empty;
    }
}

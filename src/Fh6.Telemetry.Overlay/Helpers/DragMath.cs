using System.Windows;

namespace Fh6.Telemetry.Overlay.Helpers;

/// <summary>
/// Pure math helpers for widget drag operations.
/// No WPF dispatcher dependency — fully unit-testable.
/// </summary>
public static class DragMath
{
    /// <summary>
    /// Clamps a desired widget position so the widget stays fully within the canvas bounds.
    /// X is clamped to [0, canvas.Width - widget.Width].
    /// Y is clamped to [0, canvas.Height - widget.Height].
    /// If the widget is larger than the canvas in either dimension the result is 0 (top/left).
    /// </summary>
    /// <param name="desired">The unconstrained desired position (top-left of widget).</param>
    /// <param name="widget">The rendered size of the widget.</param>
    /// <param name="canvas">The size of the containing canvas.</param>
    public static Point Clamp(Point desired, Size widget, Size canvas)
    {
        double maxX = Math.Max(0, canvas.Width - widget.Width);
        double maxY = Math.Max(0, canvas.Height - widget.Height);

        double x = Math.Clamp(desired.X, 0, maxX);
        double y = Math.Clamp(desired.Y, 0, maxY);

        return new Point(x, y);
    }
}

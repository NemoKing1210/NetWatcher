using System.Windows;

namespace NetWatcher.Utils;

/// <summary>
/// Fits restored window geometry into one monitor's work area, so a window
/// saved on a larger screen or a disconnected monitor cannot open partly
/// off-screen or under the taskbar.
/// </summary>
internal static class WindowPlacement
{
    internal static Rect Clamp(Rect desired, Rect workArea, double minWidth, double minHeight)
    {
        var width = Normalize(desired.Width, workArea.Width, minWidth);
        var height = Normalize(desired.Height, workArea.Height, minHeight);

        return new Rect(
            ClampAxis(desired.Left, workArea.Left, workArea.Right - width),
            ClampAxis(desired.Top, workArea.Top, workArea.Bottom - height),
            width,
            height);
    }

    private static double Normalize(double value, double limit, double minimum)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            value = minimum;
        }

        return Math.Min(Math.Max(value, minimum), Math.Max(limit, 1));
    }

    private static double ClampAxis(double value, double minimum, double maximum)
    {
        if (!double.IsFinite(value))
        {
            return minimum;
        }

        return Math.Clamp(value, minimum, Math.Max(minimum, maximum));
    }
}

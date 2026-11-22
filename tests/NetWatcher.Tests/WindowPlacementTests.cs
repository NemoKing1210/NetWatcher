using System.Windows;
using NetWatcher.Utils;

namespace NetWatcher.Tests;

public sealed class WindowPlacementTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void Clamp_KeepsVisibleGeometry()
    {
        var desired = new Rect(120, 80, 1200, 720);

        var clamped = WindowPlacement.Clamp(desired, WorkArea, 900, 460);

        Assert.Equal(desired, clamped);
    }

    [Fact]
    public void Clamp_ShrinksWindowLargerThanWorkArea()
    {
        var desired = new Rect(0, 0, 3000, 2000);

        var clamped = WindowPlacement.Clamp(desired, WorkArea, 900, 460);

        Assert.Equal(new Rect(0, 0, 1920, 1040), clamped);
    }

    [Fact]
    public void Clamp_EnforcesMinimumSize()
    {
        var desired = new Rect(100, 100, 400, 300);

        var clamped = WindowPlacement.Clamp(desired, WorkArea, 900, 460);

        Assert.Equal(new Rect(100, 100, 900, 460), clamped);
    }

    [Fact]
    public void Clamp_ReplacesZeroSizeWithMinimum()
    {
        var desired = new Rect(100, 100, 0, 0);

        var clamped = WindowPlacement.Clamp(desired, WorkArea, 900, 460);

        Assert.Equal(new Rect(100, 100, 900, 460), clamped);
    }

    [Fact]
    public void Clamp_PullsOffscreenWindowBack()
    {
        var desired = new Rect(5000, -400, 1000, 700);

        var clamped = WindowPlacement.Clamp(desired, WorkArea, 900, 460);

        Assert.Equal(new Rect(920, 0, 1000, 700), clamped);
    }

    [Fact]
    public void Clamp_KeepsWindowOnSecondaryMonitor()
    {
        var workArea = new Rect(-1920, 0, 1920, 1040);
        var desired = new Rect(-1500, 100, 1000, 700);

        var clamped = WindowPlacement.Clamp(desired, workArea, 900, 460);

        Assert.Equal(desired, clamped);
    }
}

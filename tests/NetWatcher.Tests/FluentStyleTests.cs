using System.IO;

namespace NetWatcher.Tests;

/// <summary>
/// Pins the design rule behind the semantic colors in the app's XAML: they must
/// point straight at the WPF-UI theme brushes. A locally built
/// <c>SolidColorBrush</c> whose <c>Color</c> comes from a <c>DynamicResource</c>
/// resolves that expression once and keeps the startup theme's color forever,
/// so a light/dark switch leaves the old tint behind (unreadable in one of the
/// two themes).
/// </summary>
public sealed class FluentStyleTests
{
    private static readonly string[] ThemeBrushes =
    {
        "SystemFillColorSuccessBackgroundBrush",
        "SystemFillColorCautionBackgroundBrush",
        "SystemFillColorCautionBrush",
        "TextFillColorSecondaryBrush",
    };

    [Fact]
    public void Xaml_UsesThemeBrushesDirectly()
    {
        var root = Path.Combine(RepoRoot(), "src", "NetWatcher");
        var text = string.Join(
            '\n',
            Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("<SolidColorBrush", text, StringComparison.Ordinal);

        foreach (var brush in ThemeBrushes)
        {
            Assert.Contains($"{{DynamicResource {brush}}}", text, StringComparison.Ordinal);
        }
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NetWatcher.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (NetWatcher.slnx) not found above the test binaries.");
    }
}

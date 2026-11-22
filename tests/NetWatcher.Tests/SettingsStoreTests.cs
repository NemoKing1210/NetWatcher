using NetWatcher.Models;
using NetWatcher.Utils;

namespace NetWatcher.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsThemeAndGeometry()
    {
        var directory = TempDirectory();
        try
        {
            var store = new SettingsStore(directory);
            var settings = new AppSettings(new WindowSettings(1100, 700, 120, 80), AppTheme.Dark);

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal(settings, loaded);
            Assert.Contains("\"Dark\"", File.ReadAllText(Path.Combine(directory, "settings.json")));
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void Load_MigratesLegacyGeometryFile()
    {
        var directory = TempDirectory();
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "settings.json"),
                """{"Width":1100,"Height":700,"X":120,"Y":80}""");

            var loaded = new SettingsStore(directory).Load();

            Assert.Equal(new WindowSettings(1100, 700, 120, 80), loaded.Window);
            Assert.Equal(AppTheme.System, loaded.Theme);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void Load_MissingOrCorruptFile_ReturnsDefaults()
    {
        var missing = TempDirectory();
        Assert.Equal(new AppSettings(null, AppTheme.System), new SettingsStore(missing).Load());

        var corrupt = TempDirectory();
        Directory.CreateDirectory(corrupt);
        try
        {
            File.WriteAllText(Path.Combine(corrupt, "settings.json"), "{ not json");
            Assert.Equal(new AppSettings(null, AppTheme.System), new SettingsStore(corrupt).Load());
        }
        finally
        {
            Delete(corrupt);
        }
    }

    private static string TempDirectory() =>
        Path.Combine(Path.GetTempPath(), "netwatcher-settings-" + Guid.NewGuid().ToString("N"));

    private static void Delete(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

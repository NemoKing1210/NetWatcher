using NetWatcher.Utils;

namespace NetWatcher.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), "netwatcher-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(directory);
            var settings = new WindowSettings(1100, 700, 120, 80);

            store.Save(settings);

            Assert.Equal(settings, store.Load());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_ReturnsNullWhenMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "netwatcher-settings-" + Guid.NewGuid().ToString("N"));

        Assert.Null(new SettingsStore(directory).Load());
    }
}

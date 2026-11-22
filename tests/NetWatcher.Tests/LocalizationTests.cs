using System.Reflection;
using System.Xml.Linq;
using NetWatcher.Localization;

namespace NetWatcher.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void EveryCulture_HasTheSameKeysAsEnglish()
    {
        var catalogs = LoadCatalogs();
        var english = catalogs["Strings.resx"];

        foreach (var (file, catalog) in catalogs)
        {
            Assert.True(
                english.Keys.ToHashSet().SetEquals(catalog.Keys),
                $"{file} keys differ from Strings.resx: missing " +
                $"[{string.Join(", ", english.Keys.Except(catalog.Keys))}], extra " +
                $"[{string.Join(", ", catalog.Keys.Except(english.Keys))}]");

            Assert.All(catalog, pair => Assert.False(
                string.IsNullOrWhiteSpace(pair.Value),
                $"{file} has an empty value for {pair.Key}"));
        }
    }

    [Fact]
    public void StringsProperties_AreBackedByEnglishResources()
    {
        var keys = LoadCatalogs()["Strings.resx"].Keys.ToHashSet();

        var properties = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0);

        foreach (var property in properties)
        {
            Assert.Contains(property.Name, keys);
        }

        Assert.Equal(keys.Count, properties.Count());
    }

    [Theory]
    [InlineData("ru", "Подключения")]
    [InlineData("zh-Hans", "连接")]
    [InlineData("ja", "接続")]
    public void ResourceManager_LoadsSatelliteAssemblies(string culture, string expected)
    {
        var manager = new System.Resources.ResourceManager(
            "NetWatcher.Localization.Strings",
            typeof(Strings).Assembly);

        var value = manager.GetString("ConnectionsPageTitle", System.Globalization.CultureInfo.GetCultureInfo(culture));

        Assert.Equal(expected, value);
    }

    private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
    {
        var directory = Path.Combine(RepoRoot(), "src", "NetWatcher", "Localization");

        return Directory.EnumerateFiles(directory, "Strings*.resx")
            .ToDictionary(
                path => Path.GetFileName(path),
                path => XDocument.Load(path)
                    .Root!
                    .Elements("data")
                    .ToDictionary(
                        data => data.Attribute("name")!.Value,
                        data => data.Element("value")?.Value ?? string.Empty));
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

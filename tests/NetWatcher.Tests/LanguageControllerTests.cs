using System.Globalization;
using NetWatcher.Models;
using NetWatcher.Services.Localization;
using NetWatcher.Utils;

namespace NetWatcher.Tests;

public sealed class LanguageControllerTests
{
    [Fact]
    public void Initialize_UnsupportedSystemLanguage_FallsBackToEnglish()
    {
        var (controller, directory, applied) = Create(AppLanguage.System, "uk-UA");
        try
        {
            controller.Initialize();

            Assert.Equal(AppLanguage.System, controller.Preference);
            Assert.Equal("en-US", controller.Culture.Name);
            Assert.Equal("en-US", Assert.Single(applied).Name);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void Initialize_SystemRussian_UsesRussian()
    {
        var (controller, directory, applied) = Create(AppLanguage.System, "ru-RU");
        try
        {
            controller.Initialize();

            Assert.Equal(AppLanguage.System, controller.Preference);
            Assert.Equal("ru-RU", controller.Culture.Name);
            Assert.Equal("ru-RU", Assert.Single(applied).Name);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void Initialize_ExplicitRussian_OverridesGermanSystem()
    {
        var (controller, directory, applied) = Create(AppLanguage.Russian, "de-DE");
        try
        {
            controller.Initialize();

            Assert.Equal(AppLanguage.Russian, controller.Preference);
            Assert.Equal("ru-RU", controller.Culture.Name);
            Assert.Equal("ru-RU", Assert.Single(applied).Name);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void SetPreference_AppliesPersistsAndRaisesChanged()
    {
        var (controller, directory, applied) = Create(AppLanguage.System, "en-US");
        try
        {
            controller.Initialize();
            applied.Clear();
            var changes = 0;
            controller.Changed += (_, _) => changes++;

            controller.SetPreference(AppLanguage.ChineseSimplified);

            Assert.Equal(1, changes);
            Assert.Equal(AppLanguage.ChineseSimplified, new SettingsStore(directory).Load().Language);
            Assert.Equal("zh-Hans", Assert.Single(applied).Name);
            Assert.Equal("zh-Hans", controller.Culture.Name);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void SetPreference_SameChoiceDoesNotRaise()
    {
        var (controller, directory, applied) = Create(AppLanguage.Russian, "en-US");
        try
        {
            controller.Initialize();
            applied.Clear();
            var changes = 0;
            controller.Changed += (_, _) => changes++;

            controller.SetPreference(AppLanguage.Russian);

            Assert.Equal(0, changes);
            Assert.Empty(applied);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void SetPreference_BackToSystem_UsesCapturedSystemLanguage()
    {
        var (controller, directory, applied) = Create(AppLanguage.System, "ru-RU");
        try
        {
            controller.Initialize();
            controller.SetPreference(AppLanguage.English);
            applied.Clear();

            controller.SetPreference(AppLanguage.System);

            Assert.Equal("ru-RU", Assert.Single(applied).Name);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact]
    public void Options_CoverEveryLanguage()
    {
        var (controller, directory, _) = Create(AppLanguage.System, "en-US");
        try
        {
            controller.Initialize();

            Assert.Equal(Enum.GetValues<AppLanguage>().Length, controller.Options.Count);
            Assert.All(controller.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.DisplayName)));
        }
        finally
        {
            Delete(directory);
        }
    }

    private static (LanguageController Controller, string Directory, List<CultureInfo> Applied) Create(
        AppLanguage saved,
        string systemCulture)
    {
        var directory = Path.Combine(Path.GetTempPath(), "netwatcher-language-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(directory);
        store.Save(new AppSettings(Window: null, AppTheme.System, saved));

        var applied = new List<CultureInfo>();
        var controller = new LanguageController(
            store,
            () => CultureInfo.GetCultureInfo(systemCulture),
            applied.Add);

        return (controller, directory, applied);
    }

    private static void Delete(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

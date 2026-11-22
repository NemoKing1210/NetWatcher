using NetWatcher.Models;
using NetWatcher.Services.Theming;
using NetWatcher.Utils;

namespace NetWatcher.Tests;

public sealed class ThemeControllerTests
{
    private sealed class RecordingApplier : IThemeApplier
    {
        public List<bool> Calls { get; } = new();

        public void Apply(bool dark) => Calls.Add(dark);
    }

    private static (ThemeController Controller, RecordingApplier Applier, SettingsStore Store) Create(
        AppTheme saved = AppTheme.System,
        AppTheme system = AppTheme.Dark)
    {
        var directory = Path.Combine(Path.GetTempPath(), "netwatcher-theme-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(directory);
        store.Save(new AppSettings(null, saved));
        var applier = new RecordingApplier();
        return (new ThemeController(applier, store, () => system), applier, store);
    }

    [Fact]
    public void Initialize_AppliesSavedDarkPreference()
    {
        var (controller, applier, _) = Create(saved: AppTheme.Dark, system: AppTheme.Light);

        controller.Initialize();

        Assert.True(controller.IsDark);
        Assert.Equal(AppTheme.Dark, controller.Preference);
        Assert.Equal(new[] { true }, applier.Calls);
    }

    [Fact]
    public void Initialize_WithSystemPreference_UsesSystemTheme()
    {
        var (controller, applier, _) = Create(saved: AppTheme.System, system: AppTheme.Light);

        controller.Initialize();

        Assert.False(controller.IsDark);
        Assert.Equal(AppTheme.System, controller.Preference);
        Assert.Equal(new[] { false }, applier.Calls);
    }

    [Fact]
    public void SetPreference_FromDarkPersistsLightAndApplies()
    {
        var (controller, applier, store) = Create(saved: AppTheme.Dark, system: AppTheme.Dark);
        controller.Initialize();

        controller.SetPreference(AppTheme.Light);

        Assert.False(controller.IsDark);
        Assert.Equal(AppTheme.Light, store.Load().Theme);
        Assert.Equal(new[] { true, false }, applier.Calls);
    }

    [Fact]
    public void SetPreference_FromFollowSystem_AppliesExplicitChoice()
    {
        var (controller, applier, store) = Create(saved: AppTheme.System, system: AppTheme.Light);
        controller.Initialize();

        controller.SetPreference(AppTheme.Dark);

        Assert.True(controller.IsDark);
        Assert.Equal(AppTheme.Dark, store.Load().Theme);
        Assert.Equal(new[] { false, true }, applier.Calls);
    }

    [Fact]
    public void SetPreference_ToFollowSystem_ResolvesCurrentSystemTheme()
    {
        var (controller, applier, store) = Create(saved: AppTheme.Light, system: AppTheme.Dark);
        controller.Initialize();

        controller.SetPreference(AppTheme.System);

        Assert.True(controller.IsDark);
        Assert.Equal(AppTheme.System, store.Load().Theme);
        Assert.Equal(new[] { false, true }, applier.Calls);
    }

    [Fact]
    public void SetPreference_SameChoiceDoesNothing()
    {
        var (controller, applier, _) = Create(saved: AppTheme.Dark, system: AppTheme.Dark);
        controller.Initialize();

        controller.SetPreference(AppTheme.Dark);

        Assert.Equal(new[] { true }, applier.Calls);
    }

    [Fact]
    public void Initialize_FollowingSystem_ReappliesWhenSystemThemeChanges()
    {
        var (controller, applier, _) = Create(saved: AppTheme.System, system: AppTheme.Light);
        controller.Initialize();

        controller.OnSystemThemeChanged(AppTheme.Dark);

        Assert.True(controller.IsDark);
        Assert.Equal(new[] { false, true }, applier.Calls);
    }
}

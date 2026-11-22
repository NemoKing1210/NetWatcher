using NetWatcher.Models;
using NetWatcher.Utils;

namespace NetWatcher.Services.Theming;

/// <summary>
/// Owns the theme preference: resolves "follow system" to a concrete light/dark
/// choice, persists an explicit choice, and applies it. The actual theme switch
/// goes through <see cref="IThemeApplier"/> so it can be tested without a live
/// WPF application.
/// </summary>
public sealed class ThemeController(
    IThemeApplier applier,
    SettingsStore settings,
    Func<AppTheme> systemTheme)
{
    /// <summary>The persisted choice (never the resolved one).</summary>
    public AppTheme Preference { get; private set; } = AppTheme.System;

    /// <summary>The effective theme after resolving "follow system".</summary>
    public bool IsDark { get; private set; }

    /// <summary>Loads the saved preference, resolves it, and applies it once at startup.</summary>
    public void Initialize()
    {
        var saved = settings.Load().Theme;
        Preference = Enum.IsDefined(saved) ? saved : AppTheme.System;
        IsDark = Resolve(Preference, systemTheme());
        applier.Apply(IsDark);
    }

    /// <summary>Selects the theme (including "follow system") and persists it.</summary>
    public void SetPreference(AppTheme preference)
    {
        if (!Enum.IsDefined(preference) || preference == Preference)
        {
            return;
        }

        Preference = preference;
        IsDark = Resolve(preference, systemTheme());
        settings.Save(settings.Load() with { Theme = preference });
        applier.Apply(IsDark);
    }

    /// <summary>Re-resolves the theme after a Windows theme change; ignored for explicit choices.</summary>
    public void OnSystemThemeChanged(AppTheme systemThemeValue)
    {
        if (Preference != AppTheme.System)
        {
            return;
        }

        var dark = Resolve(Preference, systemThemeValue);
        if (dark == IsDark)
        {
            return;
        }

        IsDark = dark;
        applier.Apply(IsDark);
    }

    private static bool Resolve(AppTheme preference, AppTheme system) => preference switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => system == AppTheme.Dark,
    };
}

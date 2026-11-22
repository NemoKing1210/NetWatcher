using System.Globalization;
using NetWatcher.Localization;
using NetWatcher.Models;
using NetWatcher.Utils;

namespace NetWatcher.Services.Localization;

/// <summary>One selectable interface language; the name is written in that language.</summary>
public sealed record LanguageOption(AppLanguage Language, string DisplayName)
{
    /// <summary>Used by UI Automation, which reads combo items through ToString.</summary>
    public override string ToString() => DisplayName;
}

/// <summary>
/// Owns the interface language: resolves "system" to a supported Windows UI
/// language, persists explicit choices, and switches the process cultures so
/// every later XAML load and formatted value uses the new language.
/// </summary>
public sealed class LanguageController(
    SettingsStore settings,
    Func<CultureInfo>? systemCulture = null,
    Action<CultureInfo>? cultureApplier = null)
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo PortugueseBrazil = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo ChineseSimplified = CultureInfo.GetCultureInfo("zh-Hans");
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    private readonly CultureInfo _systemCulture = systemCulture?.Invoke() ?? CultureInfo.CurrentUICulture;
    private readonly Action<CultureInfo> _applyCulture = cultureApplier ?? ApplyCulture;

    /// <summary>The persisted choice (never the resolved one).</summary>
    public AppLanguage Preference { get; private set; } = AppLanguage.System;

    /// <summary>The culture currently applied to the process.</summary>
    public CultureInfo Culture { get; private set; } = English;

    /// <summary>Raised after the culture changed; the shell reloads its localized XAML.</summary>
    public event EventHandler? Changed;

    /// <summary>The choices for the settings page, with "system" first.</summary>
    public IReadOnlyList<LanguageOption> Options =>
    [
        new(AppLanguage.System, Strings.LanguageSystem),
        new(AppLanguage.English, "English"),
        new(AppLanguage.Russian, "Русский"),
        new(AppLanguage.Spanish, "Español"),
        new(AppLanguage.German, "Deutsch"),
        new(AppLanguage.French, "Français"),
        new(AppLanguage.PortugueseBrazil, "Português (Brasil)"),
        new(AppLanguage.ChineseSimplified, "简体中文"),
        new(AppLanguage.Japanese, "日本語"),
    ];

    /// <summary>Loads the saved preference and applies its culture once at startup.</summary>
    public void Initialize()
    {
        var saved = settings.Load().Language;
        Preference = Enum.IsDefined(saved) ? saved : AppLanguage.System;
        Culture = ResolveCulture(Preference, _systemCulture);
        _applyCulture(Culture);
    }

    /// <summary>Switches the language, persists it, and raises <see cref="Changed"/>.</summary>
    public void SetPreference(AppLanguage preference)
    {
        if (!Enum.IsDefined(preference) || preference == Preference)
        {
            return;
        }

        Preference = preference;
        settings.Save(settings.Load() with { Language = preference });
        Culture = ResolveCulture(preference, _systemCulture);
        _applyCulture(Culture);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>
    /// Maps a preference to the culture its resources are stored under. An
    /// unsupported Windows language falls back to English; a supported one keeps
    /// the user's regional formatting (for example <c>de-AT</c>).
    /// </summary>
    private static CultureInfo ResolveCulture(AppLanguage preference, CultureInfo system) => preference switch
    {
        AppLanguage.English => English,
        AppLanguage.Russian => Russian,
        AppLanguage.Spanish => Spanish,
        AppLanguage.German => German,
        AppLanguage.French => French,
        AppLanguage.PortugueseBrazil => PortugueseBrazil,
        AppLanguage.ChineseSimplified => ChineseSimplified,
        AppLanguage.Japanese => Japanese,
        _ => ResolveSystemCulture(system),
    };

    private static CultureInfo ResolveSystemCulture(CultureInfo system) =>
        system.TwoLetterISOLanguageName switch
        {
            "en" => system,
            "ru" => system,
            "es" => system,
            "de" => system,
            "fr" => system,
            "ja" => system,
            "pt" => PortugueseBrazil,
            "zh" => ChineseSimplified,
            _ => English,
        };
}

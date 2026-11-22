using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetWatcher.Models;

namespace NetWatcher.Utils;

/// <summary>Saved window geometry.</summary>
public sealed record WindowSettings(double Width, double Height, double X, double Y);

/// <summary>The persisted application settings.</summary>
public sealed record AppSettings(
    WindowSettings? Window,
    AppTheme Theme,
    AppLanguage Language = AppLanguage.System);

/// <summary>
/// Persists window geometry, the theme choice, and the interface language to
/// <c>%LOCALAPPDATA%\NetWatcher\settings.json</c> (or a supplied directory in
/// tests). Files written before a setting existed get that setting's default.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public SettingsStore(string? baseDirectory = null) =>
        _directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetWatcher");

    private static AppSettings Defaults => new(Window: null, AppTheme.System, AppLanguage.System);

    private string FilePath => Path.Combine(_directory, "settings.json");

    /// <summary>Returns the saved settings, or defaults when the file is missing or unreadable.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return Defaults;
            }

            var text = File.ReadAllText(FilePath);

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            // Legacy layout: geometry lived at the top level, with no theme.
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Width", out _) &&
                !root.TryGetProperty("Window", out _))
            {
                var legacy = JsonSerializer.Deserialize<WindowSettings>(text, Options);
                return legacy is null
                    ? Defaults
                    : new AppSettings(legacy, AppTheme.System, AppLanguage.System);
            }

            return JsonSerializer.Deserialize<AppSettings>(text, Options) ?? Defaults;
        }
        catch (Exception)
        {
            return Defaults;
        }
    }

    /// <summary>Writes the settings atomically, creating the directory when needed.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);

        var text = JsonSerializer.Serialize(settings, Options);
        var temporary = FilePath + ".tmp";

        try
        {
            File.WriteAllText(temporary, text);
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover temp file is harmless; the next save overwrites it.
            }
        }
    }
}

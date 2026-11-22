using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetWatcher.Models;

namespace NetWatcher.Utils;

/// <summary>Saved window geometry.</summary>
public sealed record WindowSettings(double Width, double Height, double X, double Y);

/// <summary>The persisted application settings.</summary>
public sealed record AppSettings(WindowSettings? Window, AppTheme Theme);

/// <summary>
/// Persists window geometry and the theme choice to
/// <c>%LOCALAPPDATA%\NetWatcher\settings.json</c> (or a supplied directory in
/// tests). A file written before the theme existed is migrated on load.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public SettingsStore(string? baseDirectory = null) =>
        _directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetWatcher");

    private static AppSettings Defaults => new(Window: null, AppTheme.System);

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
                return legacy is null ? Defaults : new AppSettings(legacy, AppTheme.System);
            }

            return JsonSerializer.Deserialize<AppSettings>(text, Options) ?? Defaults;
        }
        catch (Exception)
        {
            return Defaults;
        }
    }

    /// <summary>Writes the settings, creating the directory when needed.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}

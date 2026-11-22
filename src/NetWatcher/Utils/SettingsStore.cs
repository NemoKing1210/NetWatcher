using System.IO;
using System.Text.Json;

namespace NetWatcher.Utils;

/// <summary>Saved window geometry.</summary>
public sealed record WindowSettings(double Width, double Height, double X, double Y);

/// <summary>
/// Persists window geometry to <c>%LOCALAPPDATA%\NetWatcher\settings.json</c>
/// (or a supplied directory in tests).
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _directory;

    public SettingsStore(string? baseDirectory = null) =>
        _directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetWatcher");

    private string FilePath => Path.Combine(_directory, "settings.json");

    public WindowSettings? Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(FilePath))
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(WindowSettings settings)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}

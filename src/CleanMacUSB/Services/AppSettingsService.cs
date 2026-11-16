using System.IO;
using System.Text.Json;

namespace CleanMacUSB.Services;

public sealed class UserSettings
{
    public bool AutoUpdateEnabled { get; set; } = true;
    public bool RunInBackground { get; set; } = false;
    public string? Language { get; set; }
}

/// <summary>
/// Persistencia ligera en JSON (%LOCALAPPDATA%\CleanMacUSB\settings.json) para las opciones del apartado de Ajustes.
/// </summary>
public static class AppSettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CleanMacUSB",
        "settings.json");

    public static UserSettings Current { get; private set; } = Load();

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json);
                if (loaded != null)
                {
                    Current = loaded;
                    return loaded;
                }
            }
        }
        catch
        {
            // Usar valores predeterminados si el archivo no existe o está corrupto
        }

        Current = new UserSettings();
        return Current;
    }

    public static void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignorar errores de escritura en disco restringido
        }
    }
}

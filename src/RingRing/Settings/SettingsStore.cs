using System.IO;
using System.Text.Json;

namespace RingRing.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RingRing");

    private static string FilePath => Path.Combine(DataFolder, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public event Action<AppSettings>? Changed;

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                Current = new AppSettings();
                Save(Current);
            }
        }
        catch
        {
            // Archivo corrupto: se respalda y se arranca con valores por defecto.
            TryBackupCorrupt();
            Current = new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataFolder);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
        Current = settings;
        Changed?.Invoke(settings);
    }

    /// <summary>Copia profunda para editar sin tocar la configuración activa.</summary>
    public AppSettings CloneCurrent() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Current, JsonOptions), JsonOptions)!;

    private static void TryBackupCorrupt()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
        }
        catch { /* sin respaldo, no es crítico */ }
    }
}

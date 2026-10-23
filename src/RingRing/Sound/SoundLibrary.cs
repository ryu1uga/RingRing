using System.IO;
using RingRing.Settings;

namespace RingRing.Sound;

public sealed record SoundOption(string Id, string Name);

public static class SoundLibrary
{
    public const string CustomId = "custom";

    public static IReadOnlyList<SoundOption> BuiltIn { get; } =
    [
        new("campanita", "Campanita (ring ring)"),
        new("dindon", "Dindón (timbre de casa)"),
        new("digital", "Digital"),
        new("arpegio", "Arpegio de campanas"),
        new("suave", "Alarma suave"),
    ];

    public static IReadOnlyList<SoundOption> AllOptions { get; } =
        [.. BuiltIn, new(CustomId, "Archivo propio (.mp3 / .wav)")];

    private static string SoundsFolder => Path.Combine(SettingsStore.DataFolder, "sounds");

    /// <summary>Escribe los tonos incluidos en disco si faltan.</summary>
    public static void EnsureGenerated()
    {
        Directory.CreateDirectory(SoundsFolder);
        foreach (var option in BuiltIn)
        {
            var path = PathFor(option.Id);
            if (!File.Exists(path))
                File.WriteAllBytes(path, ToneSynth.Render(option.Id));
        }
    }

    public static string PathFor(string builtInId) => Path.Combine(SoundsFolder, $"{builtInId}.wav");

    /// <summary>Ruta del archivo a reproducir según la configuración; cae a la campanita si falta.</summary>
    public static string Resolve(SoundSettings sound)
    {
        if (sound.Id == CustomId && File.Exists(sound.CustomPath))
            return sound.CustomPath;

        var id = BuiltIn.Any(o => o.Id == sound.Id) ? sound.Id : "campanita";
        var path = PathFor(id);
        if (!File.Exists(path)) EnsureGenerated();
        return path;
    }
}

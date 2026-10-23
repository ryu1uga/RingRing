namespace RingRing.Settings;

public sealed class AppSettings
{
    /// <summary>Nombre que ven los demás en la página ("Llamar a ...").</summary>
    public string DisplayName { get; set; } = Environment.UserName;

    public int Port { get; set; } = 7777;

    /// <summary>PIN opcional para poder llamar. Vacío = sin PIN.</summary>
    public string Pin { get; set; } = "";

    /// <summary>Segundos mínimos entre llamadas desde la misma IP.</summary>
    public int CooldownSeconds { get; set; } = 5;

    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool CloseToTray { get; set; } = true;

    public AlertSettings Alerts { get; set; } = new();
    public SoundSettings Sound { get; set; } = new();

    public List<ReplyTemplate> Replies { get; set; } =
    [
        new() { Text = "¡Ya voy!", Hotkey = "Ctrl+Alt+1" },
        new() { Text = "Estoy ocupado", Hotkey = "Ctrl+Alt+2" },
        new() { Text = "Voy en un rato", Hotkey = "Ctrl+Alt+3" },
    ];

    /// <summary>Atajo que solo marca la llamada como vista.</summary>
    public string SeenHotkey { get; set; } = "Ctrl+Alt+0";
    public bool HotkeysEnabled { get; set; } = true;

    /// <summary>No molestar hasta esta hora (UTC). DateTime.MaxValue = indefinido.</summary>
    public DateTime? DoNotDisturbUntilUtc { get; set; }

    public bool IsDoNotDisturb =>
        DoNotDisturbUntilUtc is { } until && until > DateTime.UtcNow;
}

public sealed class AlertSettings
{
    public bool Toast { get; set; } = true;
    public bool Overlay { get; set; } = true;
    public bool Sound { get; set; } = true;
    public bool Flash { get; set; } = true;
    /// <summary>Pausa lo que esté sonando (Spotify, navegador, etc.) y lo reanuda al responder.</summary>
    public bool PauseMedia { get; set; } = true;
}

public sealed class SoundSettings
{
    /// <summary>Id de un tono incluido, o "custom" para usar CustomPath.</summary>
    public string Id { get; set; } = "campanita";
    public string CustomPath { get; set; } = "";
    /// <summary>Segundo en el que empieza el archivo propio (para saltar la intro).</summary>
    public double CustomStartSeconds { get; set; }
    /// <summary>Cuánto suena el archivo propio en cada repetición. 0 = hasta el final.</summary>
    public double CustomDurationSeconds { get; set; } = 20;
    /// <summary>0.0 a 1.0</summary>
    public double Volume { get; set; } = 0.8;
    /// <summary>Veces que se repite el tono por llamada.</summary>
    public int Repeat { get; set; } = 2;
}

public sealed class ReplyTemplate
{
    public string Text { get; set; } = "";
    public string Hotkey { get; set; } = "";
}

using System.Globalization;

namespace RingRing.Sound;

/// <summary>Convierte entre segundos y texto "m:ss" (también acepta "h:mm:ss" o solo segundos).</summary>
public static class TimeText
{
    public static string Format(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds)));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    public static bool TryParse(string? text, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text)) return true; // vacío = desde el inicio

        var parts = text.Trim().Replace(',', '.').Split(':');
        if (parts.Length > 3) return false;

        double total = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0)
                return false;
            // Minutos y segundos (salvo el primer campo) no pueden pasar de 59.
            if (i > 0 && value >= 60) return false;
            total = total * 60 + value;
        }

        seconds = total;
        return true;
    }
}

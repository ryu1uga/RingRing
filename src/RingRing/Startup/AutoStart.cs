using Microsoft.Win32;

namespace RingRing.Startup;

/// <summary>Inicio con Windows mediante HKCU\...\Run (no requiere administrador).</summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RingRing";
    public const string MinimizedArg = "--minimized";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (exe is not null)
                    key.SetValue(ValueName, $"\"{exe}\" {MinimizedArg}");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName);
            }
        }
        catch
        {
            // Política del equipo lo impide: la opción simplemente no tendrá efecto.
        }
    }
}

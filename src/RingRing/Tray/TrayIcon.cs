using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using RingRing.Core;

namespace RingRing.Tray;

/// <summary>Ícono en la bandeja del sistema con su menú.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon _normal;
    private readonly Drawing.Icon _alert;
    private readonly Forms.Timer _blinkTimer = new() { Interval = 450 };
    private readonly Forms.ToolStripMenuItem _dndItem;
    private readonly Forms.ToolStripMenuItem _dndOff;
    private bool _blinkState;

    public event Action? OpenRequested;
    public event Action? QrRequested;
    public event Action? ExitRequested;
    /// <summary>null = desactivar; TimeSpan.MaxValue = hasta reactivar.</summary>
    public event Action<TimeSpan?>? DoNotDisturbRequested;

    public TrayIcon()
    {
        _normal = LoadIcon("ringring.ico");
        _alert = LoadIcon("ringring-alert.ico");

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir RingRing", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Mostrar QR para llamarme", null, (_, _) => QrRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());

        _dndItem = new Forms.ToolStripMenuItem("No molestar");
        _dndItem.DropDownItems.Add("30 minutos", null, (_, _) => DoNotDisturbRequested?.Invoke(TimeSpan.FromMinutes(30)));
        _dndItem.DropDownItems.Add("1 hora", null, (_, _) => DoNotDisturbRequested?.Invoke(TimeSpan.FromHours(1)));
        _dndItem.DropDownItems.Add("2 horas", null, (_, _) => DoNotDisturbRequested?.Invoke(TimeSpan.FromHours(2)));
        _dndItem.DropDownItems.Add("Hasta que lo desactive", null, (_, _) => DoNotDisturbRequested?.Invoke(TimeSpan.MaxValue));
        menu.Items.Add(_dndItem);

        _dndOff = new Forms.ToolStripMenuItem("Desactivar No molestar", null, (_, _) => DoNotDisturbRequested?.Invoke(null))
        {
            Visible = false,
        };
        menu.Items.Add(_dndOff);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitRequested?.Invoke());

        _icon = new Forms.NotifyIcon
        {
            Icon = _normal,
            Text = "RingRing",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke();
        };

        _blinkTimer.Tick += (_, _) =>
        {
            _blinkState = !_blinkState;
            _icon.Icon = _blinkState ? _alert : _normal;
        };
    }

    public void SetStatus(string tooltip, DateTime? doNotDisturbUntilUtc)
    {
        // NotifyIcon.Text admite como máximo 127 caracteres.
        _icon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;

        var until = doNotDisturbUntilUtc ?? DateTime.MinValue;
        var dnd = until > DateTime.UtcNow;
        _dndOff.Visible = dnd;
        _dndOff.Text = !dnd ? "Desactivar No molestar"
            : until == DateTime.MaxValue ? "Desactivar No molestar (indefinido)"
            : $"Desactivar No molestar (hasta {DateTime.SpecifyKind(until, DateTimeKind.Utc).ToLocalTime():HH:mm})";
    }

    public void ShowRinging(RingCall call)
    {
        _icon.Text = $"{call.DisplayCaller} te está llamando";
    }

    public void StartBlink()
    {
        if (!_blinkTimer.Enabled) _blinkTimer.Start();
    }

    public void StopBlink()
    {
        _blinkTimer.Stop();
        _blinkState = false;
        _icon.Icon = _normal;
    }

    public void Dispose()
    {
        _blinkTimer.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _normal.Dispose();
        _alert.Dispose();
    }

    private static Drawing.Icon LoadIcon(string file)
    {
        var info = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{file}"));
        using var stream = info!.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}

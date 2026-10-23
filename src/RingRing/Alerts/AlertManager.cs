using System.Windows.Threading;
using RingRing.Core;
using RingRing.Media;
using RingRing.Settings;
using RingRing.Sound;
using RingRing.Tray;
using RingRing.Views;

namespace RingRing.Alerts;

/// <summary>
/// Reacciona a las llamadas: dispara las alertas activas y las apaga cuando
/// ya no queda ninguna llamada sonando.
/// </summary>
public sealed class AlertManager
{
    private readonly RingService _rings;
    private readonly SettingsStore _settings;
    private readonly TrayIcon _tray;
    private readonly Dispatcher _ui;
    private readonly RingSoundPlayer _sound = new();
    private readonly MediaPauser _media = new();
    private OverlayWindow? _overlay;

    public AlertManager(RingService rings, SettingsStore settings, TrayIcon tray, Dispatcher ui)
    {
        _rings = rings;
        _settings = settings;
        _tray = tray;
        _ui = ui;

        _rings.Ringed += call => _ui.BeginInvoke(() => OnRinged(call));
        _rings.Resolved += call => _ui.BeginInvoke(() => OnResolved(call));
    }

    /// <summary>Para probar los tonos desde la configuración.</summary>
    public RingSoundPlayer Sound => _sound;

    private async void OnRinged(RingCall call)
    {
        var s = _settings.Current;
        var alerts = s.Alerts;

        if (alerts.PauseMedia)
            await _media.PauseAllAsync();

        if (alerts.Sound)
            _sound.Play(s.Sound);

        if (alerts.Overlay)
        {
            _overlay ??= CreateOverlay();
            _overlay.ShowCall(call, s.Replies);
        }

        if (alerts.Toast)
        {
            try { ToastAlert.Show(call, s); } catch { /* notificaciones desactivadas en Windows */ }
        }

        if (alerts.Flash)
            _tray.StartBlink();

        _tray.ShowRinging(call);
    }

    private async void OnResolved(RingCall call)
    {
        ToastAlert.Remove(call);

        if (_rings.LatestRinging() is { } stillRinging)
        {
            // Quedan otras llamadas: el overlay pasa a mostrar la siguiente.
            _overlay?.ShowCall(stillRinging, _settings.Current.Replies);
            return;
        }

        _sound.Stop();
        _overlay?.Close();
        _overlay = null;
        _tray.StopBlink();
        await _media.ResumeAsync();
    }

    /// <summary>Respuesta desde cualquier origen (overlay, toast o atajo).</summary>
    public void ReplyWithTemplate(string? callId, int templateIndex)
    {
        var replies = _settings.Current.Replies;
        if (templateIndex < 0 || templateIndex >= replies.Count) return;
        var text = replies[templateIndex].Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var call = callId is null ? null : _rings.Find(callId);
        if (call is not null) _rings.Reply(call, text);
        else _rings.ReplyAllRinging(text);
    }

    public void MarkSeen(string? callId)
    {
        var call = callId is null ? null : _rings.Find(callId);
        if (call is not null) _rings.MarkSeen(call);
        else _rings.MarkAllRingingSeen();
    }

    private OverlayWindow CreateOverlay()
    {
        var overlay = new OverlayWindow();
        overlay.ReplyRequested += (id, idx) => ReplyWithTemplate(id, idx);
        overlay.SeenRequested += id => MarkSeen(id);
        overlay.Closed += (_, _) => _overlay = null;
        return overlay;
    }
}

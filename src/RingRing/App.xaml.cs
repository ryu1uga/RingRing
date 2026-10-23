using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using RingRing.Alerts;
using RingRing.Core;
using RingRing.Hotkeys;
using RingRing.Server;
using RingRing.Settings;
using RingRing.Sound;
using RingRing.Startup;
using RingRing.Tray;
using RingRing.Views;

namespace RingRing;

public partial class App : Application
{
    private const string MutexName = @"Local\RingRing.SingleInstance";
    private const string ShowEventName = @"Local\RingRing.Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private MainWindow? _main;
    private QrWindow? _qr;
    private readonly DispatcherTimer _housekeeping = new() { Interval = TimeSpan.FromSeconds(30) };

    public SettingsStore Settings { get; } = new();
    public RingService Rings { get; } = new();
    public ObservableCollection<RingCall> History { get; } = [];
    public WebServer Server { get; private set; } = null!;
    public TrayIcon Tray { get; private set; } = null!;
    public AlertManager Alerts { get; private set; } = null!;
    public HotkeyManager Hotkeys { get; private set; } = null!;
    public List<string> HotkeyErrors { get; } = [];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Una sola instancia: si ya hay otra, se le pide que muestre su ventana y salimos.
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirst);
        if (!isFirst)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        new Thread(WaitForShowRequests) { IsBackground = true, Name = "RingRing.Show" }.Start();

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "RingRing", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var firstRun = !System.IO.File.Exists(System.IO.Path.Combine(SettingsStore.DataFolder, "settings.json"));
        Settings.Load();
        SoundLibrary.EnsureGenerated();

        Tray = new TrayIcon();
        Tray.OpenRequested += ShowMain;
        Tray.QrRequested += ShowQr;
        Tray.ExitRequested += () => _ = ExitAsync();
        Tray.DoNotDisturbRequested += SetDoNotDisturb;

        Alerts = new AlertManager(Rings, Settings, Tray, Dispatcher);
        Hotkeys = new HotkeyManager();

        Rings.Ringed += call => Dispatcher.BeginInvoke(() => History.Insert(0, call));
        Rings.Ringed += _ => Dispatcher.BeginInvoke(() => { while (History.Count > 50) History.RemoveAt(History.Count - 1); });

        ToastNotificationManagerCompat.OnActivated += OnToastActivated;

        Server = new WebServer(Rings, Settings);
        AutoStart.Apply(Settings.Current.StartWithWindows);
        RegisterHotkeys();
        await StartServerAsync();

        _housekeeping.Tick += (_, _) =>
        {
            Rings.ExpireStale();
            UpdateTrayStatus();
        };
        _housekeeping.Start();

        var minimizedArg = e.Args.Contains(AutoStart.MinimizedArg, StringComparer.OrdinalIgnoreCase);
        if (firstRun || (!minimizedArg && !Settings.Current.StartMinimized))
            ShowMain();
    }

    // --- servidor ---------------------------------------------------------

    private async Task StartServerAsync()
    {
        try
        {
            await Server.StartAsync();
        }
        catch
        {
            ShowTrayMessage(Server.LastError ?? "No se pudo iniciar el servidor.");
        }
        UpdateTrayStatus();
        _main?.RefreshStatus();
    }

    public string DescribeStatus()
    {
        var s = Settings.Current;
        if (Server.RunningPort is not { } port)
            return Server.LastError ?? "Servidor detenido.";

        var url = NetworkInfo.PrimaryUrl(port) ?? $"http://localhost:{port}";
        var status = $"Escuchando en {url}";
        if (s.IsDoNotDisturb) status += "  ·  No molestar activo";
        if (HotkeyErrors.Count > 0) status += "  ·  " + string.Join(" ", HotkeyErrors);
        return status;
    }

    private void UpdateTrayStatus()
    {
        var s = Settings.Current;
        var tooltip = Server.RunningPort is { } port
            ? $"RingRing · {NetworkInfo.PrimaryUrl(port) ?? $"puerto {port}"}"
            : "RingRing · servidor detenido";
        if (s.IsDoNotDisturb) tooltip += " · No molestar";
        Tray.SetStatus(tooltip, s.DoNotDisturbUntilUtc);
    }

    // --- configuración ----------------------------------------------------

    /// <summary>Guarda y aplica. Devuelve avisos no bloqueantes (puerto ocupado, atajos tomados...).</summary>
    public async Task<List<string>> ApplySettingsAsync(AppSettings newSettings)
    {
        var problems = new List<string>();
        var portChanged = newSettings.Port != Settings.Current.Port;

        Settings.Save(newSettings);
        AutoStart.Apply(newSettings.StartWithWindows);

        RegisterHotkeys();
        problems.AddRange(HotkeyErrors);

        if (portChanged || Server.RunningPort is null)
        {
            await StartServerAsync();
            if (Server.RunningPort is null && Server.LastError is not null)
                problems.Add(Server.LastError);
        }

        UpdateTrayStatus();
        return problems;
    }

    private void RegisterHotkeys()
    {
        Hotkeys.UnregisterAll();
        HotkeyErrors.Clear();

        var s = Settings.Current;
        if (!s.HotkeysEnabled) return;

        for (var i = 0; i < s.Replies.Count; i++)
        {
            var index = i;
            var gesture = s.Replies[i].Hotkey;
            if (string.IsNullOrWhiteSpace(gesture)) continue;
            if (Hotkeys.Register(gesture, () => Alerts.ReplyWithTemplate(null, index)) is { } error)
                HotkeyErrors.Add(error);
        }

        if (!string.IsNullOrWhiteSpace(s.SeenHotkey)
            && Hotkeys.Register(s.SeenHotkey, () => Alerts.MarkSeen(null)) is { } seenError)
            HotkeyErrors.Add(seenError);
    }

    private void SetDoNotDisturb(TimeSpan? duration)
    {
        var s = Settings.CloneCurrent();
        s.DoNotDisturbUntilUtc = duration switch
        {
            null => null,
            { } d when d == TimeSpan.MaxValue => DateTime.MaxValue,
            { } d => DateTime.UtcNow + d,
        };
        Settings.Save(s);
        UpdateTrayStatus();
        _main?.RefreshStatus();
    }

    // --- notificaciones ---------------------------------------------------

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        args.TryGetValue("action", out var action);
        args.TryGetValue("id", out var id);

        Dispatcher.BeginInvoke(() =>
        {
            switch (action)
            {
                case ToastAlert.ActionReply when args.TryGetValue("idx", out var idx) && int.TryParse(idx, out var index):
                    Alerts.ReplyWithTemplate(id, index);
                    break;
                case ToastAlert.ActionSeen:
                    Alerts.MarkSeen(id);
                    break;
                default:
                    // Clic en el cuerpo de la notificación: se toma como visto y se abre la app.
                    Alerts.MarkSeen(id);
                    ShowMain();
                    break;
            }
        });
    }

    private void ShowTrayMessage(string text)
    {
        try
        {
            new ToastContentBuilder()
                .AddText("RingRing")
                .AddText(text)
                .Show();
        }
        catch { /* notificaciones desactivadas */ }
    }

    // --- ventanas ---------------------------------------------------------

    public void ShowMain()
    {
        if (_main is null)
        {
            _main = new MainWindow(this);
            _main.Closed += (_, _) =>
            {
                var userClosed = !_main!.AllowClose;
                _main = null;
                // Si "cerrar a la bandeja" está desactivado, cerrar la ventana cierra la app.
                if (userClosed && !Settings.Current.CloseToTray) _ = ExitAsync();
            };
        }

        _main.RefreshStatus();
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void ShowQr()
    {
        if (_qr is null)
        {
            _qr = new QrWindow(Server.RunningPort ?? Settings.Current.Port);
            _qr.Closed += (_, _) => _qr = null;
            _qr.Show();
        }
        _qr.Activate();
    }

    private void WaitForShowRequests()
    {
        while (_showEvent is not null)
        {
            try
            {
                _showEvent.WaitOne();
                Dispatcher.BeginInvoke(ShowMain);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    public async Task ExitAsync()
    {
        _housekeeping.Stop();
        if (_main is not null)
        {
            _main.AllowClose = true;
            _main.Close();
        }
        _qr?.Close();

        ToastAlert.RemoveAll();
        Hotkeys.Dispose();
        Tray.Dispose();
        await Server.DisposeAsync();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var showEvent = _showEvent;
        _showEvent = null;
        showEvent?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RingRing.Hotkeys;
using RingRing.Settings;
using RingRing.Sound;

namespace RingRing.Views;

/// <summary>Ventana de configuración e historial.</summary>
public partial class MainWindow : Window
{
    private readonly App _app;
    private ObservableCollection<ReplyTemplate> _replies = [];
    public bool AllowClose { get; set; }

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();

        SoundCombo.ItemsSource = SoundLibrary.AllOptions;
        RepeatCombo.ItemsSource = Enumerable.Range(1, 10).ToList();
        HistoryList.ItemsSource = _app.History;

        SoundCombo.SelectionChanged += (_, _) => UpdateCustomPanel();
        VolumeSlider.ValueChanged += (_, _) => VolumeLabel.Text = $"{VolumeSlider.Value:0} %";
        BrowseButton.Click += (_, _) => BrowseCustomSound();
        PlayButton.Click += (_, _) => PreviewSound();
        StopButton.Click += (_, _) => _app.Alerts.Sound.Stop();
        QrButton.Click += (_, _) => _app.ShowQr();
        TestButton.Click += (_, _) => _app.Rings.Ring("Prueba", "Así se ve una llamada", "127.0.0.1");
        SaveButton.Click += async (_, _) => await SaveAsync();
        RevertButton.Click += (_, _) => LoadFrom(_app.Settings.Current);

        LoadFrom(_app.Settings.Current);
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        StatusText.Text = _app.DescribeStatus();
    }

    private void LoadFrom(AppSettings s)
    {
        DisplayNameBox.Text = s.DisplayName;
        PortBox.Text = s.Port.ToString();
        PinBox.Text = s.Pin;
        CooldownBox.Text = s.CooldownSeconds.ToString();
        StartWithWindowsBox.IsChecked = s.StartWithWindows;
        StartMinimizedBox.IsChecked = s.StartMinimized;
        CloseToTrayBox.IsChecked = s.CloseToTray;

        ToastBox.IsChecked = s.Alerts.Toast;
        OverlayBox.IsChecked = s.Alerts.Overlay;
        SoundBox.IsChecked = s.Alerts.Sound;
        FlashBox.IsChecked = s.Alerts.Flash;
        PauseMediaBox.IsChecked = s.Alerts.PauseMedia;

        SoundCombo.SelectedValue = s.Sound.Id;
        if (SoundCombo.SelectedIndex < 0) SoundCombo.SelectedIndex = 0;
        CustomPathBox.Text = s.Sound.CustomPath;
        VolumeSlider.Value = Math.Round(s.Sound.Volume * 100);
        VolumeLabel.Text = $"{VolumeSlider.Value:0} %";
        RepeatCombo.SelectedItem = Math.Clamp(s.Sound.Repeat, 1, 10);
        UpdateCustomPanel();

        _replies = new ObservableCollection<ReplyTemplate>(
            s.Replies.Select(r => new ReplyTemplate { Text = r.Text, Hotkey = r.Hotkey }));
        RepliesGrid.ItemsSource = _replies;
        HotkeysEnabledBox.IsChecked = s.HotkeysEnabled;
        SeenHotkeyBox.Text = s.SeenHotkey;

        SaveMessage.Text = "";
    }

    /// <summary>Construye una configuración nueva a partir del formulario; null si hay errores.</summary>
    private AppSettings? ReadForm(out string? error)
    {
        error = null;
        var s = _app.Settings.CloneCurrent();

        if (!int.TryParse(PortBox.Text, out var port) || port is < 1024 or > 65535)
        {
            error = "El puerto debe ser un número entre 1024 y 65535.";
            return null;
        }
        if (!int.TryParse(CooldownBox.Text, out var cooldown) || cooldown is < 0 or > 600)
        {
            error = "La espera entre llamadas debe estar entre 0 y 600 segundos.";
            return null;
        }

        s.DisplayName = string.IsNullOrWhiteSpace(DisplayNameBox.Text) ? Environment.UserName : DisplayNameBox.Text.Trim();
        s.Port = port;
        s.Pin = PinBox.Text.Trim();
        s.CooldownSeconds = cooldown;
        s.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        s.StartMinimized = StartMinimizedBox.IsChecked == true;
        s.CloseToTray = CloseToTrayBox.IsChecked == true;

        s.Alerts.Toast = ToastBox.IsChecked == true;
        s.Alerts.Overlay = OverlayBox.IsChecked == true;
        s.Alerts.Sound = SoundBox.IsChecked == true;
        s.Alerts.Flash = FlashBox.IsChecked == true;
        s.Alerts.PauseMedia = PauseMediaBox.IsChecked == true;

        s.Sound.Id = SoundCombo.SelectedValue as string ?? "campanita";
        s.Sound.CustomPath = CustomPathBox.Text;
        s.Sound.Volume = VolumeSlider.Value / 100.0;
        s.Sound.Repeat = RepeatCombo.SelectedItem is int r ? r : 2;
        if (s.Sound.Id == SoundLibrary.CustomId && string.IsNullOrWhiteSpace(s.Sound.CustomPath))
        {
            error = "Elige un archivo de sonido o selecciona uno de los tonos incluidos.";
            return null;
        }

        RepliesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        s.Replies = _replies
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => new ReplyTemplate { Text = x.Text.Trim(), Hotkey = x.Hotkey?.Trim() ?? "" })
            .ToList();
        s.HotkeysEnabled = HotkeysEnabledBox.IsChecked == true;
        s.SeenHotkey = SeenHotkeyBox.Text.Trim();

        // Valida atajos: formato correcto y sin repetidos.
        var gestures = s.Replies.Select(x => x.Hotkey).Append(s.SeenHotkey).Where(g => g.Length > 0).ToList();
        foreach (var g in gestures)
        {
            if (!HotkeyManager.TryParse(g, out _, out _, out var parseError))
            {
                error = $"Atajo \"{g}\": {parseError}";
                return null;
            }
        }
        var duplicate = gestures.GroupBy(g => g, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            error = $"El atajo {duplicate.Key} está repetido.";
            return null;
        }

        return s;
    }

    private async Task SaveAsync()
    {
        var settings = ReadForm(out var error);
        if (settings is null)
        {
            SaveMessage.Foreground = System.Windows.Media.Brushes.Firebrick;
            SaveMessage.Text = error;
            return;
        }

        SaveButton.IsEnabled = false;
        var problems = await _app.ApplySettingsAsync(settings);
        SaveButton.IsEnabled = true;

        if (problems.Count == 0)
        {
            SaveMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x0B, 0x7A, 0x6C));
            SaveMessage.Text = "Guardado.";
        }
        else
        {
            SaveMessage.Foreground = System.Windows.Media.Brushes.Firebrick;
            SaveMessage.Text = "Guardado con avisos: " + string.Join(" ", problems);
        }
        RefreshStatus();
    }

    private void UpdateCustomPanel()
    {
        CustomPanel.Visibility = SoundCombo.SelectedValue as string == SoundLibrary.CustomId
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void BrowseCustomSound()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegir sonido",
            Filter = "Audio (*.mp3;*.wav;*.wma;*.m4a)|*.mp3;*.wav;*.wma;*.m4a|Todos los archivos|*.*",
        };
        if (dialog.ShowDialog(this) == true)
            CustomPathBox.Text = dialog.FileName;
    }

    private void PreviewSound()
    {
        var sound = new SoundSettings
        {
            Id = SoundCombo.SelectedValue as string ?? "campanita",
            CustomPath = CustomPathBox.Text,
            Volume = VolumeSlider.Value / 100.0,
            Repeat = 1,
        };
        _app.Alerts.Sound.Play(sound);
    }

    /// <summary>Convierte la combinación presionada en texto tipo "Ctrl+Alt+1".</summary>
    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Tab sigue sirviendo para navegar; Supr/Retroceso borran el atajo.
        if (key == Key.Tab) return;
        e.Handled = true;

        if (key is Key.Back or Key.Delete)
        {
            box.Text = "";
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return; // esperar la tecla principal

        var parts = new List<string>();
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (parts.Count == 0) return; // un atajo global necesita modificador

        var keyName = key is >= Key.D0 and <= Key.D9 ? ((int)(key - Key.D0)).ToString() : key.ToString();
        parts.Add(keyName);
        box.Text = string.Join("+", parts);
        box.CaretIndex = box.Text.Length;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose && _app.Settings.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}

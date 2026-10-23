using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RingRing.Core;
using RingRing.Settings;

namespace RingRing.Views;

/// <summary>
/// Capa semitransparente sobre toda la pantalla principal que avisa de la llamada
/// y ofrece las respuestas rápidas.
/// </summary>
public partial class OverlayWindow : Window
{
    private string? _callId;
    private bool _closingByCode;

    public event Action<string?, int>? ReplyRequested;
    public event Action<string?>? SeenRequested;

    public OverlayWindow()
    {
        InitializeComponent();

        // Cubre la pantalla principal completa, barra de tareas incluida.
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        SeenButton.Click += (_, _) => SeenRequested?.Invoke(_callId);
        PreviewKeyDown += OnKey;
    }

    public void ShowCall(RingCall call, IReadOnlyList<ReplyTemplate> replies)
    {
        _callId = call.Id;
        CallerText.Text = $"{call.DisplayCaller} te está llamando";
        MessageText.Text = call.Message;
        MessageText.Visibility = string.IsNullOrWhiteSpace(call.Message) ? Visibility.Collapsed : Visibility.Visible;
        ExtraText.Text = $"Desde {call.RemoteIp} · {call.CreatedAt:HH:mm}";

        ReplyPanel.Children.Clear();
        for (var i = 0; i < replies.Count; i++)
        {
            var reply = replies[i];
            if (string.IsNullOrWhiteSpace(reply.Text)) continue;

            var index = i;
            var label = string.IsNullOrWhiteSpace(reply.Hotkey) ? reply.Text : $"{reply.Text}   ({reply.Hotkey})";
            var button = new Button
            {
                Content = label,
                Style = (Style)FindResource("ReplyButton"),
            };
            button.Click += (_, _) => ReplyRequested?.Invoke(_callId, index);
            ReplyPanel.Children.Add(button);
        }

        HintText.Text = "Teclas 1-9 para responder · Esc para marcar visto";

        if (!IsVisible) Show();
        Activate();
        if (ReplyPanel.Children.Count > 0)
            ((Button)ReplyPanel.Children[0]).Focus();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SeenRequested?.Invoke(_callId);
            e.Handled = true;
            return;
        }

        // 1-9 (fila superior o teclado numérico) eligen plantilla.
        var digit = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => e.Key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad1,
            _ => -1,
        };
        if (digit >= 0)
        {
            ReplyRequested?.Invoke(_callId, digit);
            e.Handled = true;
        }
    }

    public new void Close()
    {
        _closingByCode = true;
        base.Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Cerrado con Alt+F4: equivale a "visto", para no dejar la llamada colgada.
        if (!_closingByCode)
            SeenRequested?.Invoke(_callId);
        base.OnClosing(e);
    }
}

using Microsoft.Toolkit.Uwp.Notifications;
using RingRing.Core;
using RingRing.Settings;

namespace RingRing.Alerts;

/// <summary>Notificación de Windows con botones de respuesta rápida.</summary>
public static class ToastAlert
{
    private const string Group = "ring";
    public const string ActionReply = "reply";
    public const string ActionSeen = "seen";
    public const string ActionOpen = "open";

    public static void Show(RingCall call, AppSettings settings)
    {
        var builder = new ToastContentBuilder()
            .AddArgument("action", ActionOpen)
            .AddArgument("id", call.Id)
            .AddText($"{call.DisplayCaller} te está llamando")
            .SetToastScenario(ToastScenario.Reminder) // se queda en pantalla hasta que respondas
            .AddAudio(new ToastAudio { Silent = true });   // el sonido lo pone RingRing

        if (!string.IsNullOrWhiteSpace(call.Message))
            builder.AddText(call.Message);

        // Windows admite hasta 5 botones: hasta 4 plantillas + "Visto".
        var templates = settings.Replies
            .Select((r, i) => (r.Text, Index: i))
            .Where(r => !string.IsNullOrWhiteSpace(r.Text))
            .Take(4);

        foreach (var (text, index) in templates)
        {
            builder.AddButton(new ToastButton()
                .SetContent(text)
                .AddArgument("action", ActionReply)
                .AddArgument("id", call.Id)
                .AddArgument("idx", index));
        }

        builder.AddButton(new ToastButton()
            .SetContent("Visto")
            .AddArgument("action", ActionSeen)
            .AddArgument("id", call.Id));

        builder.Show(toast =>
        {
            toast.Tag = call.Id;
            toast.Group = Group;
        });
    }

    public static void Remove(RingCall call)
    {
        try { ToastNotificationManagerCompat.History.Remove(call.Id, Group); }
        catch { /* el usuario pudo haberla cerrado */ }
    }

    public static void RemoveAll()
    {
        try { ToastNotificationManagerCompat.History.RemoveGroup(Group); }
        catch { }
    }
}

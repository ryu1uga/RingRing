using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RingRing.Core;

public enum RingStatus
{
    Ringing,
    Seen,
    Replied,
    Expired,
}

public sealed class RingCall : INotifyPropertyChanged
{
    private RingStatus _status = RingStatus.Ringing;
    private string? _reply;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _gate = new();

    public required string Id { get; init; }
    public required string CallerName { get; init; }
    public required string Message { get; init; }
    public required string RemoteIp { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    /// <summary>Se incrementa con cada cambio; la página lo usa para el long-polling.</summary>
    public int Version { get; private set; }

    public RingStatus Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public string? Reply
    {
        get => _reply;
        private set { _reply = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => Status switch
    {
        RingStatus.Ringing => "Sonando",
        RingStatus.Seen => "Visto",
        RingStatus.Replied => $"Respondido: {Reply}",
        RingStatus.Expired => "Sin respuesta",
        _ => "",
    };

    public string DisplayCaller => string.IsNullOrWhiteSpace(CallerName) ? "Alguien" : CallerName;

    internal bool TryUpdate(RingStatus status, string? reply)
    {
        TaskCompletionSource toComplete;
        lock (_gate)
        {
            // Una respuesta siempre gana; "visto" no pisa una respuesta ya enviada.
            if (_status == RingStatus.Replied) return false;
            if (status == RingStatus.Seen && _status != RingStatus.Ringing) return false;

            Status = status;
            if (reply is not null) Reply = reply;
            Version++;
            toComplete = _changed;
            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        toComplete.TrySetResult();
        return true;
    }

    /// <summary>Espera hasta que Version supere knownVersion o se agote el tiempo.</summary>
    public async Task WaitForChangeAsync(int knownVersion, TimeSpan timeout, CancellationToken ct)
    {
        Task wait;
        lock (_gate)
        {
            if (Version > knownVersion) return;
            wait = _changed.Task;
        }
        try
        {
            await wait.WaitAsync(timeout, ct);
        }
        catch (TimeoutException) { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

using System.Collections.Concurrent;

namespace RingRing.Core;

/// <summary>
/// Estado central de las llamadas. Es seguro llamarlo desde cualquier hilo;
/// los eventos se disparan en el hilo que produjo el cambio.
/// </summary>
public sealed class RingService
{
    private const int MaxHistory = 50;
    private static readonly TimeSpan RingingTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, RingCall> _calls = new();
    private readonly LinkedList<RingCall> _history = new();
    private readonly object _historyGate = new();

    /// <summary>Se dispara cuando entra una llamada nueva.</summary>
    public event Action<RingCall>? Ringed;

    /// <summary>Se dispara cuando una llamada se marca vista o se responde.</summary>
    public event Action<RingCall>? Resolved;

    public RingCall Ring(string callerName, string message, string remoteIp)
    {
        var call = new RingCall
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            CallerName = Clean(callerName, 40),
            Message = Clean(message, 140),
            RemoteIp = remoteIp,
        };

        _calls[call.Id] = call;
        lock (_historyGate)
        {
            _history.AddFirst(call);
            while (_history.Count > MaxHistory)
            {
                var old = _history.Last!.Value;
                _history.RemoveLast();
                _calls.TryRemove(old.Id, out _);
            }
        }

        Ringed?.Invoke(call);
        return call;
    }

    public RingCall? Find(string id) => _calls.TryGetValue(id, out var call) ? call : null;

    public IReadOnlyList<RingCall> History()
    {
        lock (_historyGate) return _history.ToList();
    }

    /// <summary>La llamada más reciente que sigue sonando (destino de los atajos).</summary>
    public RingCall? LatestRinging()
    {
        lock (_historyGate)
            return _history.FirstOrDefault(c => c.Status == RingStatus.Ringing);
    }

    public IReadOnlyList<RingCall> AllRinging()
    {
        lock (_historyGate)
            return _history.Where(c => c.Status == RingStatus.Ringing).ToList();
    }

    public bool Reply(RingCall call, string text)
    {
        var ok = call.TryUpdate(RingStatus.Replied, Clean(text, 140));
        if (ok) Resolved?.Invoke(call);
        return ok;
    }

    public bool MarkSeen(RingCall call)
    {
        var ok = call.TryUpdate(RingStatus.Seen, null);
        if (ok) Resolved?.Invoke(call);
        return ok;
    }

    /// <summary>Responde a todas las llamadas que siguen sonando (p. ej. desde un atajo).</summary>
    public void ReplyAllRinging(string text)
    {
        foreach (var call in AllRinging()) Reply(call, text);
    }

    public void MarkAllRingingSeen()
    {
        foreach (var call in AllRinging()) MarkSeen(call);
    }

    /// <summary>Marca como expiradas las llamadas que nadie atendió.</summary>
    public void ExpireStale()
    {
        var limit = DateTime.Now - RingingTtl;
        foreach (var call in AllRinging().Where(c => c.CreatedAt < limit))
        {
            if (call.TryUpdate(RingStatus.Expired, null)) Resolved?.Invoke(call);
        }
    }

    private static string Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = new string(value.Trim().Where(ch => !char.IsControl(ch)).ToArray());
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

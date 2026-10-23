using Windows.Media.Control;

namespace RingRing.Media;

/// <summary>
/// Pausa lo que esté sonando (Spotify, YouTube Music en el navegador, etc.) mediante
/// los controles multimedia de Windows, y lo reanuda después. Solo reanuda lo que pausó.
/// </summary>
public sealed class MediaPauser
{
    private readonly List<GlobalSystemMediaTransportControlsSession> _paused = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task PauseAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in manager.GetSessions())
            {
                var info = session.GetPlaybackInfo();
                if (info?.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    continue;
                if (await session.TryPauseAsync())
                    _paused.Add(session);
            }
        }
        catch
        {
            // Sin acceso a los controles multimedia: no es crítico.
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResumeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var session in _paused)
            {
                try { await session.TryPlayAsync(); } catch { /* la sesión pudo cerrarse */ }
            }
        }
        finally
        {
            _paused.Clear();
            _gate.Release();
        }
    }
}

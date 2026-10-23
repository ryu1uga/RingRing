using System.Windows.Media;
using RingRing.Settings;

namespace RingRing.Sound;

/// <summary>
/// Reproduce el tono N veces por el dispositivo de salida por defecto.
/// Debe usarse desde el hilo de UI (MediaPlayer lo requiere).
/// </summary>
public sealed class RingSoundPlayer
{
    private readonly MediaPlayer _player = new();
    private int _remaining;

    public event Action? Finished;

    public RingSoundPlayer()
    {
        _player.MediaEnded += (_, _) => OnEnded();
        _player.MediaFailed += (_, _) => Stop();
    }

    public bool IsPlaying { get; private set; }

    public void Play(SoundSettings sound) => Play(SoundLibrary.Resolve(sound), sound.Volume, sound.Repeat);

    public void Play(string path, double volume, int repeat)
    {
        Stop();
        _remaining = Math.Clamp(repeat, 1, 20);
        _player.Volume = Math.Clamp(volume, 0, 1);
        _player.Open(new Uri(path, UriKind.Absolute));
        _player.Play();
        IsPlaying = true;
    }

    public void Stop()
    {
        var wasPlaying = IsPlaying;
        _remaining = 0;
        _player.Stop();
        _player.Close();
        IsPlaying = false;
        if (wasPlaying) Finished?.Invoke();
    }

    private void OnEnded()
    {
        _remaining--;
        if (_remaining > 0)
        {
            _player.Position = TimeSpan.Zero;
            _player.Play();
        }
        else
        {
            Stop();
        }
    }
}

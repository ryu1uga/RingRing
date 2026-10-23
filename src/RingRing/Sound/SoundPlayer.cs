using System.Windows.Media;
using System.Windows.Threading;
using RingRing.Settings;

namespace RingRing.Sound;

/// <summary>
/// Reproduce el tono N veces por el dispositivo de salida por defecto.
/// Para archivos propios admite un segmento (inicio + duración) con fundido
/// de entrada y salida, para usar solo el estribillo de una canción.
/// Debe usarse desde el hilo de UI (MediaPlayer lo requiere).
/// </summary>
public sealed class RingSoundPlayer
{
    private static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(900);

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(50) };

    private int _remaining;
    private double _volume;
    private TimeSpan _start;
    private TimeSpan? _length;
    private DateTime _segmentStartedAt;
    private bool _fades;

    public event Action? Finished;

    public RingSoundPlayer()
    {
        _player.MediaOpened += (_, _) => BeginSegment();
        _player.MediaEnded += (_, _) => EndSegment();
        _player.MediaFailed += (_, _) => Stop();
        _ticker.Tick += (_, _) => OnTick();
    }

    public bool IsPlaying { get; private set; }

    public void Play(SoundSettings sound)
    {
        var path = SoundLibrary.Resolve(sound);
        var isCustom = sound.Id == SoundLibrary.CustomId && path == sound.CustomPath;

        Play(path, sound.Volume, sound.Repeat,
            start: isCustom ? TimeSpan.FromSeconds(Math.Max(0, sound.CustomStartSeconds)) : TimeSpan.Zero,
            length: isCustom && sound.CustomDurationSeconds > 0 ? TimeSpan.FromSeconds(sound.CustomDurationSeconds) : null);
    }

    public void Play(string path, double volume, int repeat, TimeSpan start = default, TimeSpan? length = null)
    {
        Stop();
        _remaining = Math.Clamp(repeat, 1, 20);
        _volume = Math.Clamp(volume, 0, 1);
        _start = start;
        _length = length;
        // Los fundidos solo tienen sentido al cortar una canción; los tonos incluidos ya vienen limpios.
        _fades = start > TimeSpan.Zero || length is not null;

        IsPlaying = true;
        _player.Volume = _fades ? 0 : _volume;
        _player.Open(new Uri(path, UriKind.Absolute));
        // La reproducción arranca en MediaOpened, cuando ya se puede posicionar.
    }

    public void Stop()
    {
        var wasPlaying = IsPlaying;
        _ticker.Stop();
        _remaining = 0;
        _player.Stop();
        _player.Close();
        IsPlaying = false;
        if (wasPlaying) Finished?.Invoke();
    }

    private void BeginSegment()
    {
        if (!IsPlaying) return;

        // Si el inicio supera la duración del archivo, se ignora y se empieza desde cero.
        var start = _start;
        if (_player.NaturalDuration.HasTimeSpan && start >= _player.NaturalDuration.TimeSpan)
            start = TimeSpan.Zero;

        _player.Position = start;
        _player.Volume = _fades ? 0 : _volume;
        _player.Play();
        _segmentStartedAt = DateTime.UtcNow;
        if (_fades || _length is not null) _ticker.Start();
    }

    private void EndSegment()
    {
        _ticker.Stop();
        _remaining--;
        if (_remaining > 0 && IsPlaying)
        {
            _player.Pause();
            BeginSegment();
        }
        else
        {
            Stop();
        }
    }

    private void OnTick()
    {
        var elapsed = DateTime.UtcNow - _segmentStartedAt;

        if (_length is { } length && elapsed >= length)
        {
            EndSegment();
            return;
        }

        if (!_fades) return;

        // Volumen = rampa de entrada × rampa de salida (si se conoce el final del segmento).
        var gain = Math.Min(1, elapsed / FadeIn);
        var end = SegmentEnd();
        if (end is { } e)
        {
            var left = e - elapsed;
            if (left < FadeOut) gain = Math.Min(gain, Math.Max(0, left / FadeOut));
        }
        _player.Volume = _volume * gain;
    }

    /// <summary>Tiempo desde el inicio del segmento hasta su final, si se conoce.</summary>
    private TimeSpan? SegmentEnd()
    {
        if (_length is { } length) return length;
        if (_player.NaturalDuration.HasTimeSpan)
            return _player.NaturalDuration.TimeSpan - _player.Position + (DateTime.UtcNow - _segmentStartedAt);
        return null;
    }
}

using System.IO;

namespace RingRing.Sound;

/// <summary>
/// Genera los tonos incluidos como WAV (PCM 16 bits, mono). Así no se reparte
/// ningún archivo de audio con licencia de terceros.
/// </summary>
internal static class ToneSynth
{
    private const int SampleRate = 44100;

    public static byte[] Render(string id) => id switch
    {
        "campanita" => ToWav(Campanita()),
        "dindon" => ToWav(Dindon()),
        "digital" => ToWav(Digital()),
        "arpegio" => ToWav(Arpegio()),
        "suave" => ToWav(Suave()),
        _ => ToWav(Campanita()),
    };

    // Campana pequeña golpeada dos veces: "ring, ring".
    private static float[] Campanita()
    {
        var buf = NewBuffer(1.7);
        Bell(buf, 0.00, 1568, 1.2, 0.9);
        Bell(buf, 0.42, 1568, 1.2, 0.9);
        return Normalize(buf);
    }

    // Timbre clásico de casa: dos notas descendentes.
    private static float[] Dindon()
    {
        var buf = NewBuffer(2.2);
        SoftBell(buf, 0.00, 659.25, 1.6, 1.0);
        SoftBell(buf, 0.60, 523.25, 1.6, 1.0);
        return Normalize(buf);
    }

    // Pitidos cortos alternados, estilo aparato electrónico.
    private static float[] Digital()
    {
        var buf = NewBuffer(1.0);
        for (var i = 0; i < 4; i++)
        {
            var freq = i % 2 == 0 ? 1046.5 : 1318.5;
            Beep(buf, i * 0.16, freq, 0.10, 0.7);
        }
        return Normalize(buf);
    }

    // Arpegio ascendente de campanitas (do mayor).
    private static float[] Arpegio()
    {
        var buf = NewBuffer(1.8);
        double[] notes = [1046.5, 1318.5, 1568.0, 2093.0];
        for (var i = 0; i < notes.Length; i++)
            Bell(buf, i * 0.13, notes[i], 0.9, 0.7);
        return Normalize(buf);
    }

    // Tres pulsos suaves que suben de tono.
    private static float[] Suave()
    {
        var buf = NewBuffer(1.9);
        double[] notes = [659.25, 783.99, 987.77];
        for (var i = 0; i < notes.Length; i++)
            Pulse(buf, i * 0.5, notes[i], 0.42, 0.8);
        return Normalize(buf);
    }

    // --- primitivas -------------------------------------------------------

    // Parciales inarmónicos típicos de una campana, cada uno con su caída.
    private static void Bell(float[] buf, double start, double f, double decay, double amp)
    {
        (double Ratio, double Gain, double Decay)[] partials =
        [
            (0.50, 0.25, 1.4), (1.00, 1.00, 1.0), (1.19, 0.45, 0.8),
            (1.56, 0.35, 0.6), (2.00, 0.30, 0.5), (2.51, 0.18, 0.35), (3.00, 0.12, 0.25),
        ];
        foreach (var p in partials)
            AddSine(buf, start, f * p.Ratio, amp * p.Gain, decay * p.Decay, attack: 0.002);
    }

    private static void SoftBell(float[] buf, double start, double f, double decay, double amp)
    {
        (double Ratio, double Gain, double Decay)[] partials =
        [
            (1.00, 1.00, 1.0), (2.00, 0.35, 0.6), (2.76, 0.18, 0.4), (3.00, 0.10, 0.3),
        ];
        foreach (var p in partials)
            AddSine(buf, start, f * p.Ratio, amp * p.Gain, decay * p.Decay, attack: 0.006);
    }

    private static void Beep(float[] buf, double start, double f, double length, double amp)
    {
        var s0 = (int)(start * SampleRate);
        var n = (int)(length * SampleRate);
        for (var i = 0; i < n && s0 + i < buf.Length; i++)
        {
            var t = (double)i / SampleRate;
            var env = Math.Min(1, Math.Min(i, n - i) / (0.005 * SampleRate));
            var wave = Math.Sin(2 * Math.PI * f * t) + Math.Sin(2 * Math.PI * f * 3 * t) / 4;
            buf[s0 + i] += (float)(amp * env * wave);
        }
    }

    private static void Pulse(float[] buf, double start, double f, double length, double amp)
    {
        var s0 = (int)(start * SampleRate);
        var n = (int)(length * SampleRate);
        for (var i = 0; i < n && s0 + i < buf.Length; i++)
        {
            var t = (double)i / SampleRate;
            var env = Math.Sin(Math.PI * i / n); // sube y baja suave
            var wave = Math.Sin(2 * Math.PI * f * t) + 0.2 * Math.Sin(2 * Math.PI * f * 2 * t);
            buf[s0 + i] += (float)(amp * env * env * wave);
        }
    }

    private static void AddSine(float[] buf, double start, double f, double amp, double decay, double attack)
    {
        var s0 = (int)(start * SampleRate);
        var atk = attack * SampleRate;
        for (var i = 0; s0 + i < buf.Length; i++)
        {
            var t = (double)i / SampleRate;
            var env = Math.Exp(-t / decay) * Math.Min(1, i / atk);
            if (env < 0.0005 && i > atk) break;
            buf[s0 + i] += (float)(amp * env * Math.Sin(2 * Math.PI * f * t));
        }
    }

    private static float[] NewBuffer(double seconds) => new float[(int)(seconds * SampleRate)];

    private static float[] Normalize(float[] buf)
    {
        var peak = buf.Max(Math.Abs);
        if (peak <= 0) return buf;
        var gain = 0.89f / peak;
        // Fundido final para evitar clics.
        var fade = (int)(0.03 * SampleRate);
        for (var i = 0; i < buf.Length; i++)
        {
            var tail = buf.Length - i;
            var f = tail < fade ? (float)tail / fade : 1f;
            buf[i] *= gain * f;
        }
        return buf;
    }

    private static byte[] ToWav(float[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var dataLen = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(SampleRate); w.Write(SampleRate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in samples)
            w.Write((short)Math.Clamp(s * short.MaxValue, short.MinValue, short.MaxValue));
        w.Flush();
        return ms.ToArray();
    }
}

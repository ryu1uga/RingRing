using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RingRing.Core;
using RingRing.Settings;

namespace RingRing.Server;

/// <summary>
/// Servidor HTTP embebido: sirve la página del botón y la API de llamadas.
/// </summary>
public sealed class WebServer : IAsyncDisposable
{
    private static readonly TimeSpan LongPollTimeout = TimeSpan.FromSeconds(25);

    private readonly RingService _rings;
    private readonly SettingsStore _settings;
    private readonly ConcurrentDictionary<string, DateTime> _lastRingByIp = new();
    private WebApplication? _app;

    public WebServer(RingService rings, SettingsStore settings)
    {
        _rings = rings;
        _settings = settings;
    }

    public int? RunningPort { get; private set; }
    public string? LastError { get; private set; }

    public async Task StartAsync()
    {
        await StopAsync();

        var port = _settings.Current.Port;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(port));

        var app = builder.Build();
        MapEndpoints(app);

        try
        {
            await app.StartAsync();
            _app = app;
            RunningPort = port;
            LastError = null;
        }
        catch (Exception ex)
        {
            await app.DisposeAsync();
            RunningPort = null;
            LastError = $"No se pudo abrir el puerto {port}: {ex.Message}";
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_app is null) return;
        try
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
        finally
        {
            _app = null;
            RunningPort = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private void MapEndpoints(WebApplication app)
    {
        // Solo red local: rechaza todo lo que no venga de una IP privada.
        app.Use(async (ctx, next) =>
        {
            if (!NetworkInfo.IsLocalNetwork(ctx.Connection.RemoteIpAddress))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            await next();
        });

        app.MapGet("/", () => Embedded("index.html", "text/html; charset=utf-8"));
        app.MapGet("/app.js", () => Embedded("app.js", "text/javascript; charset=utf-8"));
        app.MapGet("/style.css", () => Embedded("style.css", "text/css; charset=utf-8"));
        app.MapGet("/logo.svg", () => Embedded("logo.svg", "image/svg+xml"));
        app.MapGet("/favicon.svg", () => Embedded("logo.svg", "image/svg+xml"));

        app.MapGet("/api/info", () =>
        {
            var s = _settings.Current;
            return Results.Ok(new
            {
                name = s.DisplayName,
                pinRequired = !string.IsNullOrEmpty(s.Pin),
                doNotDisturb = s.IsDoNotDisturb,
                cooldownSeconds = s.CooldownSeconds,
            });
        });

        app.MapPost("/api/ring", (RingRequest req, HttpContext ctx) =>
        {
            var s = _settings.Current;
            var remote = ctx.Connection.RemoteIpAddress;
            if (remote is { IsIPv4MappedToIPv6: true }) remote = remote.MapToIPv4();
            var ip = remote?.ToString() ?? "?";

            if (!string.IsNullOrEmpty(s.Pin) && req.Pin?.Trim() != s.Pin)
                return Results.Json(new { error = "PIN incorrecto." }, statusCode: StatusCodes.Status403Forbidden);

            if (s.IsDoNotDisturb)
                return Results.Json(new { error = $"{s.DisplayName} activó No molestar." }, statusCode: StatusCodes.Status423Locked);

            var now = DateTime.UtcNow;
            var cooldown = TimeSpan.FromSeconds(Math.Max(0, s.CooldownSeconds));
            if (_lastRingByIp.TryGetValue(ip, out var last) && now - last < cooldown)
            {
                var wait = (int)Math.Ceiling((cooldown - (now - last)).TotalSeconds);
                return Results.Json(new { error = $"Espera {wait} s para volver a llamar.", retryAfter = wait },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            _lastRingByIp[ip] = now;

            var call = _rings.Ring(req.Name ?? "", req.Message ?? "", ip);
            return Results.Ok(Snapshot(call));
        });

        // Long-polling: devuelve en cuanto cambia el estado o tras ~25 s.
        app.MapGet("/api/ring/{id}", async (string id, int? since, HttpContext ctx) =>
        {
            var call = _rings.Find(id);
            if (call is null) return Results.NotFound(new { error = "Llamada no encontrada." });

            if (since is { } known)
                await call.WaitForChangeAsync(known, LongPollTimeout, ctx.RequestAborted);

            return Results.Ok(Snapshot(call));
        });
    }

    private static object Snapshot(RingCall call) => new
    {
        id = call.Id,
        version = call.Version,
        status = call.Status.ToString().ToLowerInvariant(),
        reply = call.Reply,
    };

    private static IResult Embedded(string file, string contentType)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream($"RingRing.wwwroot.{file}");
        return stream is null ? Results.NotFound() : Results.Stream(stream, contentType);
    }

    private sealed record RingRequest(string? Name, string? Message, string? Pin);
}

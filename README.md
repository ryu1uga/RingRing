# RingRing

Timbre por red local. Instalas RingRing en tu PC y cualquiera conectado a la misma red te llama desde el navegador de su celular con un botón. Sirve para enterarte aunque tengas puestos los audífonos.

## Cómo funciona

1. RingRing abre un servidor HTTP en la PC (puerto `7777` por defecto).
2. Quien quiera llamarte abre `http://<ip-de-tu-pc>:7777`, o escanea el QR desde el menú de la bandeja, y presiona **Llamar**.
3. En la PC se disparan las alertas que tengas activas.
4. Respondes con una plantilla ("¡Ya voy!", "Estoy ocupado"...) y la respuesta aparece en el celular de quien llamó.

## Alertas

Todas vienen activas y cada una se puede desactivar en **Configuración > Alertas**:

| Alerta | Detalle |
| --- | --- |
| Notificación de Windows | Se queda en pantalla e incluye botones con las 4 primeras plantillas y "Visto". |
| Aviso a pantalla completa | Capa azul que parpadea. Teclas 1-9 para responder, Esc para marcar visto. |
| Sonido | 5 tonos incluidos (generados por la app) o un archivo propio .mp3/.wav. Con archivo propio puedes elegir desde qué segundo empieza y cuánto dura (con fundido), para usar solo el estribillo. Volumen y repeticiones configurables. |
| Parpadeo en bandeja | El ícono alterna de color hasta que respondes. |
| Pausar música | Pausa lo que esté sonando (Spotify, YouTube Music en el navegador...) vía los controles multimedia de Windows y lo reanuda al responder. |

## Formas de responder

- Botones del aviso a pantalla completa.
- Botones de la notificación de Windows.
- Atajos globales configurables. Por defecto, `Ctrl+Alt+1/2/3` envían las plantillas y `Ctrl+Alt+0` solo marca visto.

## Opciones de escritorio

- Iniciar con Windows (`HKCU\...\Run`, sin administrador).
- Iniciar minimizado en la bandeja.
- Cerrar a la bandeja.
- No molestar por 30 min, 1 h, 2 h o indefinido, desde el menú de la bandeja.
- PIN opcional y espera mínima entre llamadas por IP.
- Historial de las últimas 50 llamadas.

La configuración se guarda en `%AppData%\RingRing\settings.json`.

## Seguridad

- Solo acepta conexiones de IPs privadas (192.168.x.x, 10.x.x.x, 172.16-31.x.x, enlace local, loopback).
- Límite de llamadas por IP, configurable.
- PIN opcional.

## Requisitos

- Windows 10 1809 o posterior.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) para compilar.
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) para generar el instalador.

## Desarrollo

```powershell
dotnet run --project src/RingRing
```

La primera vez, Windows pregunta si permites el acceso a la red. Acepta en **redes privadas**.

## Generar el instalador

Instala Inno Setup una vez (`winget install JRSoftware.InnoSetup`) y ejecuta:

```powershell
.\build.ps1
```

O a mano:

```powershell
dotnet publish src/RingRing -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
iscc installer\RingRing.iss
```

Resultado: `installer\Output\RingRing-Setup-0.1.0.exe`.

El instalador crea la regla del Firewall para el puerto 7777, solo en perfil privado. Si cambias el puerto en la configuración, ajusta la regla:

```powershell
netsh advfirewall firewall set rule name="RingRing" new localport=<puerto>
```

## Estructura

```
src/RingRing/
  App.xaml(.cs)      arranque, instancia única, coordinación
  Core/              llamadas y su estado (RingService, RingCall)
  Server/            Kestrel embebido, API, filtro de red local
  Alerts/            notificación, overlay y gestor de alertas
  Sound/             tonos generados y reproductor
  Media/             pausa/reanuda la música del sistema
  Hotkeys/           atajos globales (RegisterHotKey)
  Tray/              ícono y menú de bandeja
  Startup/           inicio con Windows
  Settings/          modelo y persistencia JSON
  Views/             configuración, overlay, QR
  wwwroot/           página del botón (embebida en el .exe)
installer/           script de Inno Setup
assets/              logo en SVG y PNG grande
```

## API

| Método | Ruta | Descripción |
| --- | --- | --- |
| GET | `/api/info` | Nombre, si pide PIN, si está en No molestar. |
| POST | `/api/ring` | `{ name, message, pin }` → `{ id, version, status }`. 429 si llamas muy seguido, 423 en No molestar. |
| GET | `/api/ring/{id}?since={version}` | Long-polling: responde cuando cambia el estado (`ringing`, `seen`, `replied`, `expired`) o a los 25 s. |

## Pendiente

- Integración directa con Spotify (Web API) para usar una canción como tono.
- Aviso en todos los monitores, no solo en el principal.

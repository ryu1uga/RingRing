; Instalador de RingRing (Inno Setup 6)
; 1. Publica la app:   dotnet publish src/RingRing -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
; 2. Compila esto:     iscc installer\RingRing.iss
; Resultado:           installer\Output\RingRing-Setup-<versión>.exe

#define AppName "RingRing"
#define AppVersion "0.1.0"
#define AppPublisher "ryu1uga"
#define AppExe "RingRing.exe"
#define AppPort "7777"

[Setup]
AppId={{6A3F2C1E-9B4D-4E7A-8C21-5D0F7B9E3A41}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=RingRing-Setup-{#AppVersion}
SetupIconFile=..\src\RingRing\Assets\ringring.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Se necesita administrador para crear la regla del Firewall.
PrivilegesRequired=admin
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Regla de entrada para que los celulares de la red puedan llegar al puerto (solo redes privadas).
Filename: "{sys}\netsh.exe"; \
    Parameters: "advfirewall firewall add rule name=""{#AppName}"" dir=in action=allow protocol=TCP localport={#AppPort} profile=private"; \
    Flags: runhidden; StatusMsg: "Configurando el Firewall de Windows..."
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""{#AppName}"""; \
    Flags: runhidden; RunOnceId: "RemoveFirewallRule"
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#AppName}\sounds"

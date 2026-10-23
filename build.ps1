# Publica RingRing y genera el instalador.
# Uso:  .\build.ps1
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "Publicando..." -ForegroundColor Cyan
dotnet publish src/RingRing -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló." }

# Busca el compilador de Inno Setup (PATH, instalación de usuario o de sistema).
$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw "No se encontró Inno Setup. Instálalo con:  winget install JRSoftware.InnoSetup"
}

Write-Host "Generando instalador con $iscc" -ForegroundColor Cyan
& $iscc installer\RingRing.iss
if ($LASTEXITCODE -ne 0) { throw "ISCC falló." }

Write-Host "Listo: installer\Output\" -ForegroundColor Green

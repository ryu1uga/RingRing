@echo off
rem Ejecuta build.ps1 sin depender de la política de ejecución de PowerShell.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*

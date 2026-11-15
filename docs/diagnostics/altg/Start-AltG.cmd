@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Record-AltG.ps1"
if errorlevel 1 pause

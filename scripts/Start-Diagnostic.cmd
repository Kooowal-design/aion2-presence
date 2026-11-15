@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Diagnose-Crafting.ps1"
if errorlevel 1 pause

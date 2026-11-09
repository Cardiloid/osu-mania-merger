@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath 'OsuManiaMerger.exe'" >nul 2>nul
start "" "OsuManiaMerger.exe"

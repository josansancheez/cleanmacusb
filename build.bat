@echo off
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Fallo durante la compilacion.
    pause
    exit /b %ERRORLEVEL%
)
echo.
pause

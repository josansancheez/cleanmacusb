@echo off
chcp 65001 >nul
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
start "" "%~dp0src\CleanMacUSB\bin\Release\net8.0-windows\publish\CleanMacUSB.exe"

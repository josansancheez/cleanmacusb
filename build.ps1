<#
.SYNOPSIS
    Script automatizado de compilación y empaquetado de CleanMacUSB v1.0.
    1. Genera los iconos multi-resolución (.ico) y recursos gráficos inspirados en Macintosh.
    2. Compila y publica la aplicación nativa C# (.NET 8 WPF) en modo Release.
    3. Compila el instalador profesional CleanMacUSB_Setup_v1.0.exe con Inno Setup 6.
    Desarrollado por Josan Sánchez.
#>

$ErrorActionPreference = "Stop"
$RootDir = $PSScriptRoot

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  CLEANMACUSB v1.0 - BUILD & INSTALLER PIPELINE (Josan Sánchez)   " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# 1. Generar recursos visuales e icono .ICO
Write-Host "`n[1/3] Generando identidad visual e icono Macintosh multi-resolución..." -ForegroundColor Yellow
& (Join-Path $RootDir "tools\Generate-Assets.ps1")

# 2. Localizar .NET 8 SDK y publicar aplicación en Release
Write-Host "`n[2/3] Compilando CleanMacUSB (.NET 8 WPF) en configuración Release..." -ForegroundColor Yellow
$DotNetCandidates = @(
    "$env:USERPROFILE\.dotnet\dotnet.exe",
    "$env:ProgramFiles\dotnet\dotnet.exe",
    "dotnet"
)

$DotNetExe = $null
foreach ($candidate in $DotNetCandidates) {
    try {
        if ($candidate -eq "dotnet" -or (Test-Path $candidate)) {
            $sdks = & $candidate --list-sdks 2>$null
            if ($sdks -match "^8\.") {
                $DotNetExe = $candidate
                break
            }
        }
    } catch {}
}

if (-not $DotNetExe) {
    throw "No se encontró el SDK de .NET 8. Instálalo con 'winget install Microsoft.DotNet.SDK.8'."
}

$CsprojPath = Join-Path $RootDir "src\CleanMacUSB\CleanMacUSB.csproj"
$PublishDir = Join-Path $RootDir "src\CleanMacUSB\bin\Release\net8.0-windows\publish"

Get-Process CleanMacUSB -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

if (Test-Path $PublishDir) {
    Remove-Item -Path $PublishDir -Recurse -Force
}

& $DotNetExe publish $CsprojPath `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    throw "Error al compilar CleanMacUSB.csproj"
}

Write-Host "[OK] Aplicación publicada en: $PublishDir" -ForegroundColor Green

# 3. Localizar Inno Setup 6 (ISCC.exe) y generar instalador
Write-Host "`n[3/3] Compilando instalador CleanMacUSB_Setup_v1.0.exe con Inno Setup..." -ForegroundColor Yellow
$IsccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$IsccExe = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $IsccExe) {
    throw "No se encontró Inno Setup 6 (ISCC.exe). Instálalo con 'winget install JRSoftware.InnoSetup'."
}

$IssPath = Join-Path $RootDir "installer\CleanMacUSB_Setup.iss"
& $IsccExe "/DPublishDir=$PublishDir" $IssPath

if ($LASTEXITCODE -ne 0) {
    throw "Error al compilar el instalador con Inno Setup."
}

$FinalInstaller = Join-Path $RootDir "dist\CleanMacUSB_Setup_v1.0.exe"
Write-Host "`n==================================================================" -ForegroundColor Green
Write-Host "  ¡COMPILACIÓN COMPLETADA CON ÉXITO!" -ForegroundColor Green
Write-Host "  Ejecutable App : $PublishDir\CleanMacUSB.exe" -ForegroundColor White
Write-Host "  Instalador     : $FinalInstaller" -ForegroundColor White
Write-Host "==================================================================" -ForegroundColor Green

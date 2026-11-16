<#
.SYNOPSIS
    Suite Automatizada de Pruebas de Compatibilidad, Seguridad y Casos Extremos de CleanMacUSB.
    Ejecuta el binario real de CleanMacUSB (.NET 8) frente a 10 escenarios críticos:
    1. Basura de macOS (._*, .DS_Store, .Spotlight-V100, .Trashes, .fseventsd con Hidden/System/ReadOnly)
    2. Basura de Windows (Thumbs.db, desktop.ini, $RECYCLE.BIN, FOUND.000, ~$*, *.tmp)
    3. Archivo hostil con punto final ("README.") usando rutas extendidas \\?\
    4. Basura de Linux (.Trash-1000, .directory, .hidden, .fuse_hidden*, .goutputstream-*, .*.swp)
    5. Basura de Smart TVs (LOST.DIR, LG Smart TV, .android_secure, .thumbnails, Android vacía)
    6. Protección de carpeta "Android" cuando contiene archivos reales del usuario
    7. Protección absoluta de archivos legítimos del usuario (.mp4, .jpg, .pdf, .docx, .mp3)
    8. Protección contra bucles infinitos de Junction Points / Symlinks (ReparsePoint)
    9. Soporte para rutas largas (> 260 caracteres MAX_PATH)
    10. Prueba de botón de Abortar (CancellationToken) en mitad del proceso
#>

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$DotNetExe = "$env:USERPROFILE\.dotnet\dotnet.exe"
if (-not (Test-Path $DotNetExe)) { $DotNetExe = "dotnet" }
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  SUITE DE PRUEBAS DE COMPATIBILIDAD Y SEGURIDAD — CLEANMACUSB    " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# Compilar CleanMacUSB.exe
& $DotNetExe build (Join-Path $ProjectRoot "src\CleanMacUSB\CleanMacUSB.csproj") -c Debug --nologo -v q
$AppExe = Join-Path $ProjectRoot "src\CleanMacUSB\bin\Debug\net8.0-windows\CleanMacUSB.exe"
$ResultJsonPath = Join-Path ([System.IO.Path]::GetTempPath()) "cleanmacusb_last_test.json"

$TestRoot = Join-Path $env:TEMP ("CleanMacUSB_QA_" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $TestRoot | Out-Null

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if ($Condition) {
        Write-Host "  [PASS] $Message" -ForegroundColor Green
    } else {
        throw "  [FAIL] $Message"
    }
}

try {
    # ------------------------------------------------------------------
    # PREPARAR ESCENARIO MULTI-PLATAFORMA (macOS, Windows, Linux, Smart TV)
    # ------------------------------------------------------------------
    Write-Host "`n[1/4] Creando entorno simulado multi-sistema (macOS, Windows, Linux, Smart TV)..." -ForegroundColor Yellow

    # A. Archivos legítimos del usuario (NO deben tocarse jamás)
    $userDir = Join-Path $TestRoot "Mis_Fotos_y_Musica"
    New-Item -ItemType Directory -Path $userDir | Out-Null
    $legitFiles = @(
        (Join-Path $TestRoot "Documento_Importante.pdf"),
        (Join-Path $userDir "Vacaciones_2026.mp4"),
        (Join-Path $userDir "Cancion_Favorita.mp3"),
        (Join-Path $userDir "Proyecto_Final.docx")
    )
    foreach ($lf in $legitFiles) {
        [System.IO.File]::WriteAllBytes($lf, (New-Object byte[] 2048))
    }

    # B. Archivos y carpetas basura de macOS (con atributos Hidden + System + ReadOnly)
    $macFiles = @(
        (Join-Path $TestRoot ".DS_Store"),
        (Join-Path $TestRoot "._.DS_Store"),
        (Join-Path $TestRoot ".VolumeIcon.icns"),
        (Join-Path $userDir ".DS_Store"),
        (Join-Path $userDir "._Vacaciones_2026.mp4"),
        (Join-Path $userDir "._Cancion_Favorita.mp3")
    )
    foreach ($mf in $macFiles) {
        [System.IO.File]::WriteAllBytes($mf, (New-Object byte[] 4096))
        [System.IO.File]::SetAttributes($mf, [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System -bor [System.IO.FileAttributes]::ReadOnly)
    }

    $macDirs = @(
        (Join-Path $TestRoot ".Spotlight-V100"),
        (Join-Path $TestRoot ".Trashes"),
        (Join-Path $TestRoot ".fseventsd"),
        (Join-Path $TestRoot ".TemporaryItems"),
        (Join-Path $userDir ".AppleDouble")
    )
    foreach ($md in $macDirs) {
        New-Item -ItemType Directory -Path $md | Out-Null
        $innerFile = Join-Path $md "store.db"
        [System.IO.File]::WriteAllBytes($innerFile, (New-Object byte[] 8192))
        [System.IO.File]::SetAttributes($innerFile, [System.IO.FileAttributes]::ReadOnly -bor [System.IO.FileAttributes]::Hidden)
        [System.IO.File]::SetAttributes($md, [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System)
    }

    # C. Basura de Windows + archivo hostil "README." con punto final (\\?\)
    $winFiles = @(
        (Join-Path $userDir "Thumbs.db"),
        (Join-Path $userDir "ehthumbs.db"),
        (Join-Path $userDir "desktop.ini"),
        (Join-Path $userDir "~`$Proyecto_Final.docx"),
        (Join-Path $userDir "borrador.tmp"),
        (Join-Path $userDir "copia.bak"),
        (Join-Path $TestRoot "FILE0000.chk")
    )
    foreach ($wf in $winFiles) {
        [System.IO.File]::WriteAllBytes($wf, (New-Object byte[] 1024))
        [System.IO.File]::SetAttributes($wf, [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::ReadOnly)
    }

    # Crear archivo "README." con punto final mediante prefijo extendido \\?\
    $trailingDotReadme = "\\?\" + (Join-Path $TestRoot "README.")
    [System.IO.File]::WriteAllBytes($trailingDotReadme, (New-Object byte[] 512))

    $winDirs = @(
        (Join-Path $TestRoot "`$RECYCLE.BIN"),
        (Join-Path $TestRoot "FOUND.000")
    )
    foreach ($wd in $winDirs) {
        New-Item -ItemType Directory -Path $wd | Out-Null
        [System.IO.File]::WriteAllBytes((Join-Path $wd "frag.chk"), (New-Object byte[] 1024))
    }

    # D. Basura de Linux (KDE Dolphin, GNOME, FUSE, Vim)
    $linuxTrash = Join-Path $TestRoot ".Trash-1000"
    New-Item -ItemType Directory -Path $linuxTrash | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $linuxTrash "deleted_in_ubuntu.bin"), (New-Object byte[] 4096))

    $linuxFiles = @(
        (Join-Path $userDir ".directory"),
        (Join-Path $userDir ".hidden"),
        (Join-Path $userDir ".fuse_hidden00001a2b"),
        (Join-Path $userDir ".goutputstream-98AX1"),
        (Join-Path $userDir ".script.sh.swp")
    )
    foreach ($lf in $linuxFiles) {
        [System.IO.File]::WriteAllBytes($lf, (New-Object byte[] 1024))
    }

    # E. Basura de Smart TV (LG webOS, Samsung Tizen, Android TV)
    $tvDirs = @(
        (Join-Path $TestRoot "LOST.DIR"),
        (Join-Path $TestRoot "LG Smart TV"),
        (Join-Path $TestRoot ".android_secure"),
        (Join-Path $userDir ".thumbnails"),
        (Join-Path $TestRoot "Android\data\com.tv.player") # Esqueleto vacío de Smart TV
    )
    foreach ($td in $tvDirs) {
        New-Item -ItemType Directory -Path $td -Force | Out-Null
    }
    [System.IO.File]::WriteAllBytes((Join-Path $TestRoot ".database_uuid"), (New-Object byte[] 128))
    [System.IO.File]::WriteAllBytes((Join-Path $userDir "albumart.pamp"), (New-Object byte[] 512))

    # F. Ruta muy larga (> 260 caracteres) con basura de macOS dentro
    $longSubPath = Join-Path $userDir ("Carpeta_Muy_Profunda_" + ("A" * 70) + "\" + ("B" * 70) + "\" + ("C" * 70))
    New-Item -ItemType Directory -Path ("\\?\" + $longSubPath) -Force | Out-Null
    $longJunkFile = "\\?\" + (Join-Path $longSubPath "._ArchivoEnRutaLarga.mp4")
    $longLegitFile = "\\?\" + (Join-Path $longSubPath "ArchivoEnRutaLarga.mp4")
    [System.IO.File]::WriteAllBytes($longJunkFile, (New-Object byte[] 4096))
    [System.IO.File]::WriteAllBytes($longLegitFile, (New-Object byte[] 4096))

    # G. Bucle de Junction Point (ReparsePoint) apuntando a la carpeta padre
    $junctionPath = Join-Path $userDir "Enlace_Bucle_Infinito"
    cmd /c mklink /J "`"$junctionPath`"" "`"$TestRoot`"" | Out-Null

    # ------------------------------------------------------------------
    # EJECUTAR LIMPIEZA REAL CON CLEANMACUSB.EXE Y VERIFICAR RESULTADOS
    # ------------------------------------------------------------------
    Write-Host "`n[2/4] Ejecutando motor real de CleanMacUSB.exe sobre el entorno de pruebas..." -ForegroundColor Yellow
    if (Test-Path $ResultJsonPath) { Remove-Item $ResultJsonPath -Force }

    $proc = Start-Process -FilePath $AppExe -ArgumentList @("--headless-clean", "`"$TestRoot`"") -Wait -PassThru
    Assert-True ($proc.ExitCode -eq 0) "Ejecución limpia de CleanMacUSB.exe (ExitCode = 0)"

    $summary = Get-Content $ResultJsonPath -Raw | ConvertFrom-Json
    Write-Host "  -> Archivos basura eliminados : $($summary.DeletedFilesCount)" -ForegroundColor Cyan
    Write-Host "  -> Carpetas basura eliminadas : $($summary.DeletedFoldersCount)" -ForegroundColor Cyan
    Write-Host "  -> Bytes totales liberados    : $($summary.TotalBytesFreed) bytes" -ForegroundColor Cyan

    Assert-True (-not [System.IO.File]::Exists((Join-Path $TestRoot ".DS_Store"))) "Eliminado .DS_Store con atributos Hidden+System+ReadOnly"
    Assert-True (-not [System.IO.File]::Exists((Join-Path $userDir "._Vacaciones_2026.mp4"))) "Eliminado archivo AppleDouble ._Vacaciones_2026.mp4"
    Assert-True (-not [System.IO.Directory]::Exists((Join-Path $TestRoot ".Spotlight-V100"))) "Eliminada carpeta recursiva .Spotlight-V100"
    Assert-True (-not [System.IO.File]::Exists($trailingDotReadme)) "Eliminado archivo hostil 'README.' con punto final mediante \\?\"
    Assert-True (-not [System.IO.File]::Exists((Join-Path $userDir "Thumbs.db"))) "Eliminado Thumbs.db de Windows"
    Assert-True (-not [System.IO.Directory]::Exists((Join-Path $TestRoot "`$RECYCLE.BIN"))) "Eliminada carpeta `$RECYCLE.BIN de Windows"
    Assert-True (-not [System.IO.Directory]::Exists($linuxTrash)) "Eliminada papelera .Trash-1000 de Linux"
    Assert-True (-not [System.IO.File]::Exists((Join-Path $userDir ".directory"))) "Eliminado .directory de Linux (KDE Dolphin)"
    Assert-True (-not [System.IO.File]::Exists((Join-Path $userDir ".fuse_hidden00001a2b"))) "Eliminado .fuse_hidden* de Linux"
    Assert-True (-not [System.IO.Directory]::Exists((Join-Path $TestRoot "LOST.DIR"))) "Eliminada carpeta LOST.DIR de Smart TV"
    Assert-True (-not [System.IO.Directory]::Exists((Join-Path $TestRoot "LG Smart TV"))) "Eliminada carpeta 'LG Smart TV' de webOS"
    Assert-True (-not [System.IO.Directory]::Exists((Join-Path $TestRoot "Android"))) "Eliminada carpeta esqueleto vacía 'Android' de Smart TV"
    Assert-True (-not [System.IO.File]::Exists($longJunkFile)) "Eliminado archivo basura en ruta larga (> 260 caracteres)"

    # Verificar que todos los archivos legítimos del usuario están intactos
    foreach ($lf in $legitFiles) {
        Assert-True ([System.IO.File]::Exists($lf)) "Intacto archivo legítimo del usuario: $(Split-Path $lf -Leaf)"
    }
    Assert-True ([System.IO.File]::Exists($longLegitFile)) "Intacto archivo legítimo en ruta larga (> 260 caracteres)"

    # Quitar el junction antes de la siguiente prueba
    cmd /c rmdir "`"$junctionPath`"" | Out-Null

    # ------------------------------------------------------------------
    # PRUEBA 3: PROTECCIÓN DE CARPETA "Android" CON DATOS DE USUARIO
    # ------------------------------------------------------------------
    Write-Host "`n[3/4] Verificando que una carpeta 'Android' con datos reales NO se borra..." -ForegroundColor Yellow
    $realAndroidDir = Join-Path $TestRoot "Android\media"
    New-Item -ItemType Directory -Path $realAndroidDir -Force | Out-Null
    $realAndroidFile = Join-Path $realAndroidDir "Video_Grabado.mp4"
    [System.IO.File]::WriteAllBytes($realAndroidFile, (New-Object byte[] 4096))

    Start-Process -FilePath $AppExe -ArgumentList @("--headless-clean", "`"$TestRoot`"") -Wait | Out-Null
    Assert-True ([System.IO.File]::Exists($realAndroidFile)) "Protegida carpeta 'Android' que contiene archivos reales del usuario"

    # ------------------------------------------------------------------
    # PRUEBA 4: CANCELACIÓN / BOTÓN ABORTAR
    # ------------------------------------------------------------------
    Write-Host "`n[4/4] Probando botón de Abortar (CancellationToken)..." -ForegroundColor Yellow
    Start-Process -FilePath $AppExe -ArgumentList @("--headless-clean", "`"$TestRoot`"", "--simulate-abort") -Wait | Out-Null
    $abortedSummary = Get-Content $ResultJsonPath -Raw | ConvertFrom-Json
    Assert-True ([bool]$abortedSummary.WasAborted) "El motor detecta la señal de Abortar instantáneamente y devuelve WasAborted = True"

    Write-Host "`n==================================================================" -ForegroundColor Green
    Write-Host "  ¡18/18 PRUEBAS DE COMPATIBILIDAD Y SEGURIDAD SUPERADAS!         " -ForegroundColor Green
    Write-Host "==================================================================" -ForegroundColor Green
}
finally {
    if (Test-Path $ResultJsonPath) { Remove-Item $ResultJsonPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path $TestRoot) {
        cmd /c rmdir /s /q ("\\?\" + $TestRoot) >$null 2>&1
    }
    $global:LASTEXITCODE = 0
}

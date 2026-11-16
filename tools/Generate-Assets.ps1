<#
.SYNOPSIS
    Generador de Identidad Visual e Icono Multi-resolución de CleanMacUSB.
    Inspirado en el Macintosh clásico (1984) con pantalla azulada, escoba brillante y conector USB.
    Desarrollado para CleanMacUSB por Josan Sánchez.
#>

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$AssetsDir = Join-Path $ProjectRoot "assets"
if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Path $AssetsDir | Out-Null
}

function New-RoundedRectPath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2
    if ($d -gt $Width) { $d = $Width }
    if ($d -gt $Height) { $d = $Height }
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-CleanMacUSBLogo {
    param([int]$Size = 512)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # trabajamos en coordenadas virtuales 512x512 escaladas a $Size
    $scale = $Size / 512.0
    $g.ScaleTransform($scale, $scale)

    # 1. Sombra suave inferior
    $shadowPath = New-RoundedRectPath -X 88 -Y 46 -Width 336 -Height 384 -Radius 38
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(55, 15, 23, 42))
    $g.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()

    # 2. Conector USB Integrado en la base inferior
    $usbNeckPath = New-RoundedRectPath -X 204 -Y 404 -Width 104 -Height 28 -Radius 8
    $usbNeckBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 59))
    $g.FillPath($usbNeckBrush, $usbNeckPath)

    $usbPlugPath = New-RoundedRectPath -X 216 -Y 426 -Width 80 -Height 66 -Radius 7
    $usbPlugRect = New-Object System.Drawing.RectangleF(216, 426, 80, 66)
    $usbMetalBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $usbPlugRect,
        [System.Drawing.Color]::FromArgb(255, 226, 232, 240),
        [System.Drawing.Color]::FromArgb(255, 148, 163, 184),
        [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal
    )
    $usbPlugPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 71, 85, 105), 4)
    $g.FillPath($usbMetalBrush, $usbPlugPath)
    $g.DrawPath($usbPlugPen, $usbPlugPath)

    # Contactos USB dorados/oscuros
    $holeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 59))
    $goldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 250, 204, 21))
    $g.FillRectangle($holeBrush, 230, 444, 18, 26)
    $g.FillRectangle($holeBrush, 264, 444, 18, 26)
    $g.FillRectangle($goldBrush, 233, 452, 12, 10)
    $g.FillRectangle($goldBrush, 267, 452, 12, 10)

    # 3. Base escalonada del Macintosh 1984
    $basePath = New-RoundedRectPath -X 104 -Y 356 -Width 304 -Height 60 -Radius 18
    $baseRect = New-Object System.Drawing.RectangleF(104, 356, 304, 60)
    $baseBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $baseRect,
        [System.Drawing.Color]::FromArgb(255, 216, 208, 184),
        [System.Drawing.Color]::FromArgb(255, 184, 175, 150),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
    )
    $basePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 148, 138, 114), 4)
    $g.FillPath($baseBrush, $basePath)
    $g.DrawPath($basePen, $basePath)

    # 4. Carcasa Principal del Monitor Macintosh (Beige/Gris cálido clásico)
    $bodyPath = New-RoundedRectPath -X 88 -Y 34 -Width 336 -Height 348 -Radius 36
    $bodyRect = New-Object System.Drawing.RectangleF(88, 34, 336, 348)
    $bodyBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $bodyRect,
        [System.Drawing.Color]::FromArgb(255, 247, 242, 228),
        [System.Drawing.Color]::FromArgb(255, 214, 205, 182),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $bodyPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 168, 158, 134), 5)
    $g.FillPath($bodyBrush, $bodyPath)
    $g.DrawPath($bodyPen, $bodyPath)

    # 5. Marco Interior Hundido del CRT
    $bezelPath = New-RoundedRectPath -X 116 -Y 62 -Width 280 -Height 218 -Radius 24
    $bezelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 194, 184, 158))
    $bezelPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 150, 140, 116), 3)
    $g.FillPath($bezelBrush, $bezelPath)
    $g.DrawPath($bezelPen, $bezelPath)

    # 6. Pantalla CRT Azulada Brillante
    $screenPath = New-RoundedRectPath -X 126 -Y 72 -Width 260 -Height 198 -Radius 18
    $screenRect = New-Object System.Drawing.RectangleF(126, 72, 260, 198)
    $screenBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $screenRect,
        [System.Drawing.Color]::FromArgb(255, 56, 189, 248),
        [System.Drawing.Color]::FromArgb(255, 3, 105, 161),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $screenPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 7, 89, 133), 4)
    $g.FillPath($screenBrush, $screenPath)
    $g.DrawPath($screenPen, $screenPath)

    # Brillo diagonal de cristal en la pantalla CRT
    $glarePoints = @(
        (New-Object System.Drawing.PointF(138, 84)),
        (New-Object System.Drawing.PointF(336, 84)),
        (New-Object System.Drawing.PointF(138, 224))
    )
    $glareBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 255, 255, 255))
    $g.FillPolygon($glareBrush, $glarePoints)

    # 7. Ranura de Disquete 3.5" e Insignia Retro en el Frontal
    $floppyOuter = New-RoundedRectPath -X 266 -Y 316 -Width 118 -Height 15 -Radius 6
    $floppyOuterBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 51, 65, 85))
    $g.FillPath($floppyOuterBrush, $floppyOuter)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 23, 42))), 292, 319, 68, 9)

    # Insignia multicolor inspirada en los 80
    $badgeColors = @(
        [System.Drawing.Color]::FromArgb(255, 97, 187, 70),
        [System.Drawing.Color]::FromArgb(255, 253, 184, 39),
        [System.Drawing.Color]::FromArgb(255, 245, 130, 31),
        [System.Drawing.Color]::FromArgb(255, 224, 58, 62),
        [System.Drawing.Color]::FromArgb(255, 150, 61, 151),
        [System.Drawing.Color]::FromArgb(255, 0, 157, 220)
    )
    for ($i = 0; $i -lt $badgeColors.Count; $i++) {
        $bBrush = New-Object System.Drawing.SolidBrush($badgeColors[$i])
        $g.FillRectangle($bBrush, 134, (310 + ($i * 4)), 26, 4)
        $bBrush.Dispose()
    }

    # 8. Escoba de Limpieza Brillante dentro de la Pantalla CRT
    $state = $g.Save()
    $g.TranslateTransform(256, 172)
    $g.RotateTransform(-28)

    # Mango de la escoba
    $handlePath = New-RoundedRectPath -X -8 -Y -76 -Width 16 -Height 78 -Radius 7
    $handleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 253, 224, 71))
    $handlePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 161, 98, 7), 3)
    $g.FillPath($handleBrush, $handlePath)
    $g.DrawPath($handlePen, $handlePath)

    # Cerdas de la escoba
    $bristlePoints = @(
        (New-Object System.Drawing.PointF(-28, 4)),
        (New-Object System.Drawing.PointF(28, 4)),
        (New-Object System.Drawing.PointF(44, 62)),
        (New-Object System.Drawing.PointF(-44, 62))
    )
    $bristleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 250, 204, 21))
    $g.FillPolygon($bristleBrush, $bristlePoints)
    $g.DrawPolygon($handlePen, $bristlePoints)

    # Líneas de las cerdas
    $g.DrawLine($handlePen, -14, 18, -20, 60)
    $g.DrawLine($handlePen, 0, 18, 0, 60)
    $g.DrawLine($handlePen, 14, 18, 20, 60)

    # Collarín de la escoba
    $collarPath = New-RoundedRectPath -X -28 -Y -8 -Width 56 -Height 15 -Radius 6
    $collarBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $collarPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 2, 132, 199), 3)
    $g.FillPath($collarBrush, $collarPath)
    $g.DrawPath($collarPen, $collarPath)

    $g.Restore($state)

    # 9. Destellos de limpieza (Sparkles) en la pantalla
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $sparkle1 = @(
        (New-Object System.Drawing.PointF(176, 102)),
        (New-Object System.Drawing.PointF(182, 120)),
        (New-Object System.Drawing.PointF(200, 126)),
        (New-Object System.Drawing.PointF(182, 132)),
        (New-Object System.Drawing.PointF(176, 150)),
        (New-Object System.Drawing.PointF(170, 132)),
        (New-Object System.Drawing.PointF(152, 126)),
        (New-Object System.Drawing.PointF(170, 120))
    )
    $g.FillPolygon($whiteBrush, $sparkle1)

    $sparkle2 = @(
        (New-Object System.Drawing.PointF(336, 106)),
        (New-Object System.Drawing.PointF(341, 119)),
        (New-Object System.Drawing.PointF(354, 124)),
        (New-Object System.Drawing.PointF(341, 129)),
        (New-Object System.Drawing.PointF(336, 142)),
        (New-Object System.Drawing.PointF(331, 129)),
        (New-Object System.Drawing.PointF(318, 124)),
        (New-Object System.Drawing.PointF(331, 119))
    )
    $g.FillPolygon($whiteBrush, $sparkle2)
    $g.FillEllipse($whiteBrush, 190, 214, 12, 12)
    $g.FillEllipse($whiteBrush, 332, 206, 10, 10)

    $g.Dispose()
    return $bmp
}

# 1. Guardar PNG principal de alta resolución (512x512)
$masterBmp = Draw-CleanMacUSBLogo -Size 512
$logoPngPath = Join-Path $AssetsDir "logo.png"
$masterBmp.Save($logoPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$masterBmp.Dispose()
Write-Host "[OK] Generado: $logoPngPath"

# 2. Generar archivo .ICO multi-resolución real (16x16, 32x32, 48x48, 64x64, 256x256)
$sizes = @(16, 32, 48, 64, 256)
$pngBuffers = New-Object System.Collections.Generic.List[byte[]]

foreach ($sz in $sizes) {
    $bmp = Draw-CleanMacUSBLogo -Size $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBuffers.Add($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$icoPath = Join-Path $AssetsDir "CleanMacUSB.ico"
$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

# Cabecera ICONDIR (6 bytes)
$bw.Write([uint16]0)              # Reserved
$bw.Write([uint16]1)              # Type: 1 = Icon
$bw.Write([uint16]$sizes.Count)   # Number of images

# Calcular offset inicial después de las entradas ICONDIRENTRY (16 bytes cada una)
$dataOffset = 6 + (16 * $sizes.Count)

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $buf = $pngBuffers[$i]
    $dimByte = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }

    $bw.Write($dimByte)                 # Width
    $bw.Write($dimByte)                 # Height
    $bw.Write([byte]0)                  # ColorCount
    $bw.Write([byte]0)                  # Reserved
    $bw.Write([uint16]1)                # Planes
    $bw.Write([uint16]32)               # BitCount (32bpp ARGB)
    $bw.Write([uint32]$buf.Length)      # BytesInRes
    $bw.Write([uint32]$dataOffset)      # ImageOffset

    $dataOffset += $buf.Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bw.Write($pngBuffers[$i])
}

$bw.Flush()
$bw.Dispose()
$fs.Dispose()
Write-Host "[OK] Generado ICO multi-resolución (16, 32, 48, 64, 256): $icoPath"

# 3. Generar imágenes para el asistente Inno Setup (InstallShield Wizard Style)
# Banner lateral: 164x314 BMP
$wizBmp = New-Object System.Drawing.Bitmap(164, 314, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$wg = [System.Drawing.Graphics]::FromImage($wizBmp)
$wg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$wg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

$wizRect = New-Object System.Drawing.Rectangle(0, 0, 164, 314)
$wizGrad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $wizRect,
    [System.Drawing.Color]::FromArgb(255, 15, 23, 42),
    [System.Drawing.Color]::FromArgb(255, 3, 105, 161),
    [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
)
$wg.FillRectangle($wizGrad, $wizRect)

$logoForWiz = Draw-CleanMacUSBLogo -Size 132
$wg.DrawImage($logoForWiz, 16, 48, 132, 132)
$logoForWiz.Dispose()

$titleFont = New-Object System.Drawing.Font("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)
$subFont = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Regular)
$isFont = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Bold)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$wg.DrawString("CleanMacUSB", $titleFont, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF(0, 195, 164, 30)), $sf)
$wg.DrawString("by Josan Sánchez", $subFont, [System.Drawing.Brushes]::LightSkyBlue, (New-Object System.Drawing.RectangleF(0, 222, 164, 24)), $sf)
$wg.DrawString("InstallShield Wizard", $isFont, [System.Drawing.Brushes]::Silver, (New-Object System.Drawing.RectangleF(0, 286, 164, 20)), $sf)

$wizPath = Join-Path $AssetsDir "wizard_image.bmp"
$wizBmp.Save($wizPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$wg.Dispose()
$wizBmp.Dispose()

# Icono superior derecho del asistente: 55x58 BMP
$smallBmp = New-Object System.Drawing.Bitmap(55, 58, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$sg = [System.Drawing.Graphics]::FromImage($smallBmp)
$sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.Clear([System.Drawing.Color]::White)
$logoSmall = Draw-CleanMacUSBLogo -Size 50
$sg.DrawImage($logoSmall, 2, 4, 50, 50)
$logoSmall.Dispose()

$smallPath = Join-Path $AssetsDir "wizard_small.bmp"
$smallBmp.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$sg.Dispose()
$smallBmp.Dispose()
Write-Host "[OK] Generados recursos de Inno Setup Wizard: $wizPath y $smallPath"

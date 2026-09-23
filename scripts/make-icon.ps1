# Dessine l'icône de l'application (assets\customnotch.ico et .png) : un carré arrondi noir,
# et en blanc la pilule et ses cellules - un anneau puis deux barres - le « symbole » que
# l'icône de la zone de notification reprend. Noir et blanc : lisible sur toute barre des
# tâches, et sans couleur imposée à côté de l'accent choisi. Aucune dépendance : GDI+ seulement.
#   powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1 [-Base "#16171D"] [-Mark "#FFFFFF"]
param([string]$Base = "#16171D", [string]$Mark = "#FFFFFF")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assets = Join-Path $root "assets"
New-Item -ItemType Directory -Force $assets | Out-Null

function Draw-Mark([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [double]$size
    $base = [System.Drawing.ColorTranslator]::FromHtml($Base)
    $light = [System.Drawing.Color]::FromArgb(255, [Math]::Min(255, $base.R + 26), [Math]::Min(255, $base.G + 26), [Math]::Min(255, $base.B + 28))
    $dark = [System.Drawing.Color]::FromArgb(255, [int]($base.R * 0.6), [int]($base.G * 0.6), [int]($base.B * 0.6))
    $ink = [System.Drawing.ColorTranslator]::FromHtml($Mark)
    # Le carré arrondi, dégradé du plus clair (haut gauche) au plus foncé (bas droite).
    $body = [System.Drawing.RectangleF]::new($s * 0.06, $s * 0.06, $s * 0.88, $s * 0.88)
    $radius = $s * 0.26; $d = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($body.X, $body.Y, $d, $d, 180, 90)
    $path.AddArc($body.Right - $d, $body.Y, $d, $d, 270, 90)
    $path.AddArc($body.Right - $d, $body.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($body.X, $body.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($body, $light, $dark, 45)
    $g.FillPath($brush, $path)
    $edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(56, 255, 255, 255), [Math]::Max(1, $s * 0.02))
    $g.DrawPath($edge, $path)
    # Le symbole : la pilule (capsule blanche horizontale) et ses cellules - un anneau puis deux barres.
    $pillW = $s * 0.62; $pillH = $s * 0.24
    $pillRect = [System.Drawing.RectangleF]::new($s * 0.5 - $pillW / 2, $s * 0.5 - $pillH / 2, $pillW, $pillH)
    $pillD = $pillH
    $pillPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pillPath.AddArc($pillRect.X, $pillRect.Y, $pillD, $pillD, 90, 180)
    $pillPath.AddArc($pillRect.Right - $pillD, $pillRect.Y, $pillD, $pillD, 270, 180)
    $pillPath.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush($ink)), $pillPath)
    $ringD = $s * 0.13
    $ringPen = New-Object System.Drawing.Pen($base, ($s * 0.035))
    $g.DrawEllipse($ringPen, $s * 0.34 - $ringD / 2, $s * 0.5 - $ringD / 2, $ringD, $ringD)
    $barPen = New-Object System.Drawing.Pen($base, ($s * 0.035))
    $barPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $barPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($barPen, $s * 0.46, $s * 0.455, $s * 0.66, $s * 0.455)
    $g.DrawLine($barPen, $s * 0.46, $s * 0.545, $s * 0.66, $s * 0.545)
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    $bmp = Draw-Mark $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += [pscustomobject]@{ Size = $size; Bytes = $ms.ToArray() }
    if ($size -eq 256) { $bmp.Save((Join-Path $assets "customnotch.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose(); $ms.Dispose()
}

# Conteneur ICO : en-tête, une entrée par image, puis les PNG bout à bout.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$f.Bytes.Length); $w.Write([uint32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $w.Write($f.Bytes) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $assets "customnotch.ico"), $out.ToArray())
"Icône : $(Join-Path $assets 'customnotch.ico') ($($frames.Count) tailles) et customnotch.png (256 px)"

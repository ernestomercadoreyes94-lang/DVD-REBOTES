# Genera los iconos de la app (logo DVD) en res/
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot

function New-DvdPath([float]$x, [float]$y, [float]$w, [float]$h) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $letters = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ff = New-Object System.Drawing.FontFamily "Arial Black"
    $letters.AddString("DVD", $ff, [int][System.Drawing.FontStyle]::Bold, 100, (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic)
    $lb = $letters.GetBounds()
    $th = $h * 0.64
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate($x, $y)
    $m.Shear(-0.18, 0)
    $m.Translate($th * 0.18, 0)
    $m.Scale(($w * 0.88) / $lb.Width, $th / $lb.Height)
    $m.Translate(-$lb.X, -$lb.Y)
    $letters.Transform($m)
    $p.AddPath($letters, $false)
    $p.AddEllipse($x + $w * 0.03, $y + $h * 0.71, $w * 0.94, $h * 0.27)
    return $p
}

function Save-Icon([int]$size, [string]$file, [bool]$notif) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    if (-not $notif) {
        $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 18, 18, 26))
        $r = $size * 0.22; $d = $r * 2
        $rr = New-Object System.Drawing.Drawing2D.GraphicsPath
        $rr.AddArc(0, 0, $d, $d, 180, 90); $rr.AddArc($size - $d - 1, 0, $d, $d, 270, 90)
        $rr.AddArc($size - $d - 1, $size - $d - 1, $d, $d, 0, 90); $rr.AddArc(0, $size - $d - 1, $d, $d, 90, 90)
        $rr.CloseFigure()
        $g.FillPath($bg, $rr)
    }
    $w = $size * 0.80; $h = $w * 0.5
    $path = New-DvdPath (($size - $w) / 2) (($size - $h) / 2) $w $h
    $color = if ($notif) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(255, 0, 191, 255) }
    $g.FillPath((New-Object System.Drawing.SolidBrush $color), $path)
    $g.Dispose()
    New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Save-Icon 192 "$here\res\mipmap-xxxhdpi\ic_launcher.png" $false
Save-Icon 144 "$here\res\mipmap-xxhdpi\ic_launcher.png" $false
Save-Icon 96  "$here\res\mipmap-xhdpi\ic_launcher.png" $false
Save-Icon 48  "$here\res\mipmap-mdpi\ic_launcher.png" $false
Save-Icon 96  "$here\res\drawable-xxxhdpi\ic_notif.png" $true
Save-Icon 48  "$here\res\drawable-xhdpi\ic_notif.png" $true
"iconos listos"

Add-Type -AssemblyName System.Drawing

$src = Join-Path $PSScriptRoot "..\src\NickeltownFinance\Assets\AppIcon.png"
$outDir = Join-Path $PSScriptRoot "..\src\NickeltownFinance\Assets"
$bg = [System.Drawing.Color]::FromArgb(15, 23, 42)

function Resize-Image {
    param([string]$FileName, [int]$Width, [int]$Height)

    $img = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($bg)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $scale = [Math]::Min($Width / $img.Width, $Height / $img.Height) * 0.88
    $nw = [int]($img.Width * $scale)
    $nh = [int]($img.Height * $scale)
    $x = ($Width - $nw) / 2
    $y = ($Height - $nh) / 2
    $g.DrawImage($img, $x, $y, $nw, $nh)
    $g.Dispose()
    $img.Dispose()
    $bmp.Save((Join-Path $outDir $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Resize-Image "StoreLogo.png" 50 50
Resize-Image "Square44x44Logo.png" 44 44
Resize-Image "Square150x150Logo.png" 150 150
Resize-Image "Wide310x150Logo.png" 310 150
Resize-Image "SplashScreen.png" 620 300

Write-Host "MSIX assets generated in $outDir"

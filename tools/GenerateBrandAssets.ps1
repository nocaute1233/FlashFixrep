Add-Type -AssemblyName System.Drawing

$assetDir = Join-Path $PSScriptRoot '..\src\FlashFix.Desktop\Assets'
$assetDir = [System.IO.Path]::GetFullPath($assetDir)

function New-BrandBitmap([int]$width, [int]$height, [bool]$wide) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(11, 11, 12))

    $size = [Math]::Min($width, $height) * $(if ($wide) { 0.48 } else { 0.78 })
    $x = $(if ($wide) { $width * 0.15 } else { ($width - $size) / 2 })
    $y = ($height - $size) / 2
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, [float]($size * 0.09))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square

    # An angular F made from three deliberate strokes.
    $graphics.DrawLine($pen, [float]($x + $size * 0.20), [float]($y + $size * 0.12), [float]($x + $size * 0.20), [float]($y + $size * 0.88))
    $graphics.DrawLine($pen, [float]($x + $size * 0.20), [float]($y + $size * 0.12), [float]($x + $size * 0.79), [float]($y + $size * 0.12))
    $graphics.DrawLine($pen, [float]($x + $size * 0.20), [float]($y + $size * 0.50), [float]($x + $size * 0.65), [float]($y + $size * 0.50))

    if ($wide) {
        $font = [System.Drawing.Font]::new('Segoe UI', [float]($height * 0.15), [System.Drawing.FontStyle]::Bold)
        $graphics.DrawString('FLASHFIX', $font, [System.Drawing.Brushes]::White,
            [float]($x + $size * 1.07), [float]($height * 0.39))
        $font.Dispose()
    }

    $pen.Dispose()
    $graphics.Dispose()
    return $bitmap
}

$squareNames = @{
    'StoreLogo.png' = 256
    'Square44x44Logo.scale-200.png' = 88
    'Square44x44Logo.targetsize-24_altform-unplated.png' = 24
    'Square44x44Logo.targetsize-48_altform-lightunplated.png' = 48
    'Square150x150Logo.scale-200.png' = 300
    'LockScreenLogo.scale-200.png' = 48
}

foreach ($entry in $squareNames.GetEnumerator()) {
    $image = New-BrandBitmap $entry.Value $entry.Value $false
    $image.Save((Join-Path $assetDir $entry.Key), [System.Drawing.Imaging.ImageFormat]::Png)
    $image.Dispose()
}

foreach ($entry in @(@('Wide310x150Logo.scale-200.png', 620, 300), @('SplashScreen.scale-200.png', 620, 300))) {
    $image = New-BrandBitmap $entry[1] $entry[2] $true
    $image.Save((Join-Path $assetDir $entry[0]), [System.Drawing.Imaging.ImageFormat]::Png)
    $image.Dispose()
}

$iconImage = New-BrandBitmap 256 256 $false
$stream = [System.IO.MemoryStream]::new()
$iconImage.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$iconImage.Dispose()
$png = $stream.ToArray()
$stream.Dispose()

$iconPath = Join-Path $assetDir 'AppIcon.ico'
$file = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]1)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([uint16]1)
$writer.Write([uint16]32)
$writer.Write([uint32]$png.Length)
$writer.Write([uint32]22)
$writer.Write($png)
$writer.Dispose()

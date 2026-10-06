param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\FlashFix.Desktop\Assets\AppIcon.ico')
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 256)
$frames = [System.Collections.Generic.List[byte[]]]::new()
$ink = [System.Drawing.Color]::FromArgb(255, 17, 17, 19)
$paper = [System.Drawing.Color]::FromArgb(255, 247, 247, 244)

function Draw-FlashMark($graphics, [single]$left, [single]$top, [single]$size) {
    $state = $graphics.Save()
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TranslateTransform($left, $top)
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)

    $background = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $background.AddArc(8, 8, 72, 72, 180, 90)
    $background.AddArc(176, 8, 72, 72, 270, 90)
    $background.AddArc(176, 176, 72, 72, 0, 90)
    $background.AddArc(8, 176, 72, 72, 90, 90)
    $background.CloseFigure()
    $inkBrush = [System.Drawing.SolidBrush]::new($ink)
    $paperBrush = [System.Drawing.SolidBrush]::new($paper)
    $graphics.FillPath($inkBrush, $background)

    # A single bold bolt stays legible in the 16 px taskbar icon.
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(146, 33),
        [System.Drawing.PointF]::new(71, 142),
        [System.Drawing.PointF]::new(119, 142),
        [System.Drawing.PointF]::new(103, 223),
        [System.Drawing.PointF]::new(187, 107),
        [System.Drawing.PointF]::new(139, 107)
    )
    $graphics.FillPolygon($paperBrush, $points)
    $paperBrush.Dispose()
    $inkBrush.Dispose()
    $background.Dispose()
    $graphics.Restore($state)
}

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    Draw-FlashMark $graphics 0 0 $size
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames.Add($stream.ToArray())
    $stream.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$target = [System.IO.Path]::GetFullPath($OutputPath)
$file = [System.IO.File]::Create($target)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $writer.Write([byte]($size % 256))
        $writer.Write([byte]($size % 256))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write($frame) }
}
finally {
    $writer.Dispose()
}

Write-Output $target

$assetDirectory = [System.IO.Path]::GetDirectoryName($target)
$squareAssets = @{
    'LockScreenLogo.scale-200.png' = 48
    'Square150x150Logo.scale-200.png' = 300
    'Square44x44Logo.scale-200.png' = 88
    'Square44x44Logo.targetsize-24_altform-unplated.png' = 24
    'Square44x44Logo.targetsize-48_altform-lightunplated.png' = 48
    'StoreLogo.png' = 256
}
foreach ($entry in $squareAssets.GetEnumerator()) {
    $size = [int]$entry.Value
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    Draw-FlashMark $graphics 0 0 $size
    $bitmap.Save((Join-Path $assetDirectory $entry.Key), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

foreach ($name in @('SplashScreen.scale-200.png', 'Wide310x150Logo.scale-200.png')) {
    $bitmap = [System.Drawing.Bitmap]::new(620, 300, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($ink)
    Draw-FlashMark $graphics 155 108 84
    $font = [System.Drawing.Font]::new('Segoe UI', 31, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new($paper)
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.DrawString('FlashFix', $font, $brush, 249, 126)
    $bitmap.Save((Join-Path $assetDirectory $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $brush.Dispose()
    $font.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Copy-Item -LiteralPath $target -Destination (Join-Path $PSScriptRoot '..\src\FlashFix.KeyManager\AppIcon.ico') -Force

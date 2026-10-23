$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assetPath = Join-Path $root 'src/RvzConverter.App/Assets'
[IO.Directory]::CreateDirectory($assetPath) | Out-Null
$layers = [Collections.Generic.List[byte[]]]::new()
$sizes = @(16, 32, 48, 64, 128, 256)
foreach ($size in $sizes) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 48.0), ($size / 48.0))
    $background = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#2865BB'))
    $white = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F4F8FF'))
    $rounded = [Drawing.Drawing2D.GraphicsPath]::new()
    $rounded.AddArc(0,0,16,16,180,90)
    $rounded.AddArc(32,0,16,16,270,90)
    $rounded.AddArc(32,32,16,16,0,90)
    $rounded.AddArc(0,32,16,16,90,90)
    $rounded.CloseFigure()
    $graphics.FillPath($background,$rounded)
    $shape1 = [Drawing.PointF[]]@([Drawing.PointF]::new(11,13),[Drawing.PointF]::new(29,13),[Drawing.PointF]::new(36,20),[Drawing.PointF]::new(31,25),[Drawing.PointF]::new(25,19),[Drawing.PointF]::new(17,19),[Drawing.PointF]::new(17,34),[Drawing.PointF]::new(11,34))
    $shape2 = [Drawing.PointF[]]@([Drawing.PointF]::new(22,29),[Drawing.PointF]::new(31,29),[Drawing.PointF]::new(31,23),[Drawing.PointF]::new(39,31),[Drawing.PointF]::new(31,39),[Drawing.PointF]::new(31,34),[Drawing.PointF]::new(22,34))
    $graphics.FillPolygon($white,$shape1)
    $graphics.FillPolygon($white,$shape2)
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $layers.Add($stream.ToArray())
    $stream.Dispose()
    $rounded.Dispose()
    $white.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $assetPath 'App.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$layers[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $layers[$i].Length
    }
    foreach ($layer in $layers) { $writer.Write($layer) }
} finally { $writer.Dispose() }
Write-Host 'Original application icon generated (Windows authoring tool).'

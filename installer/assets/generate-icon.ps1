[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $PSScriptRoot 'CodexCue.ico' }

function New-RoundedPath([System.Drawing.RectangleF]$Rectangle, [float]$Radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $arc = [System.Drawing.RectangleF]::new($Rectangle.X, $Rectangle.Y, $diameter, $diameter)
    $path.AddArc($arc, 180, 90)
    $arc.X = $Rectangle.Right - $diameter
    $path.AddArc($arc, 270, 90)
    $arc.Y = $Rectangle.Bottom - $diameter
    $path.AddArc($arc, 0, 90)
    $arc.X = $Rectangle.X
    $path.AddArc($arc, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng([int]$Size) {
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $inset = [Math]::Max(1.0, $Size * 0.11)
            $rect = [System.Drawing.RectangleF]::new($inset, $inset, ($Size - (2 * $inset)), ($Size - (2 * $inset)))
            $path = New-RoundedPath $rect ([Math]::Max(2.0, $Size * 0.17))
            try {
                $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
                $outline = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#2563EB'), [Math]::Max(1.2, $Size * 0.075))
                try {
                    $outline.Alignment = [System.Drawing.Drawing2D.PenAlignment]::Inset
                    $graphics.FillPath($white, $path)
                    $graphics.DrawPath($outline, $path)
                } finally {
                    $white.Dispose()
                    $outline.Dispose()
                }
            } finally { $path.Dispose() }

            $check = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#2563EB'), [Math]::Max(1.5, $Size * 0.105))
            try {
                $check.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
                $check.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                $check.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                $graphics.DrawLines($check, @(
                    ([System.Drawing.PointF]::new($Size * 0.28, $Size * 0.52)),
                    ([System.Drawing.PointF]::new($Size * 0.44, $Size * 0.68)),
                    ([System.Drawing.PointF]::new($Size * 0.73, $Size * 0.36))
                ))
            } finally { $check.Dispose() }
        } finally { $graphics.Dispose() }

        $stream = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        } finally { $stream.Dispose() }
    } finally { $bitmap.Dispose() }
}

$sizes = @(16, 32, 48, 256)
$images = @()
foreach ($size in $sizes) { $images += ,(New-IconPng $size) }

$directory = Split-Path ([IO.Path]::GetFullPath($OutputPath)) -Parent
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$file = [IO.File]::Open($OutputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $writer = [IO.BinaryWriter]::new($file)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)
        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $sizeByte = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([Byte]$sizeByte)
            $writer.Write([Byte]$sizeByte)
            $writer.Write([Byte]0)
            $writer.Write([Byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$images[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($image in $images) { $writer.Write([byte[]]$image) }
    } finally { $writer.Dispose() }
} finally { $file.Dispose() }

$length = (Get-Item -LiteralPath $OutputPath).Length
if ($length -ge 25000) { throw "Generated icon is too large: $length bytes" }
Write-Output "Generated icon: $OutputPath ($length bytes)"

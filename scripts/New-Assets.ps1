<#
.SYNOPSIS
    Draws the package logos, widget icons and widget picker screenshots.

.DESCRIPTION
    The images are committed to the repository; run this script only to change them.
    Screenshots are 300 x 304 pixels with transparent rounded corners, as the widget picker requires.
    All port names and device names in the screenshots are made up.

.EXAMPLE
    ./scripts/New-Assets.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\WhichCOM.WidgetProvider'
$assets = Join-Path $project 'Assets'
$providerAssets = Join-Path $project 'ProviderAssets'
New-Item -ItemType Directory -Force $assets, $providerAssets | Out-Null

$accent = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)

function New-Canvas([int]$width, [int]$height) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.TextRenderingHint = 'AntiAliasGridFit'
    $graphics.Clear([System.Drawing.Color]::Transparent)
    return $bitmap, $graphics
}

function New-RoundedRectangle([single]$x, [single]$y, [single]$width, [single]$height, [single]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $width - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $width - $d, $y + $height - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Save-Png($bitmap, $graphics, [string]$path) {
    $graphics.Dispose()
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Write-Host "  $([System.IO.Path]::GetFileName($path))"
}

function Add-Tile($graphics, [single]$x, [single]$y, [single]$size) {
    $background = New-RoundedRectangle $x $y $size $size ($size * 0.22)
    $brush = [System.Drawing.SolidBrush]::new($accent)
    $graphics.FillPath($brush, $background)
    $brush.Dispose()
}

# Signal bars on a rounded square.
function Add-Bars($graphics, [single]$x, [single]$y, [single]$size) {
    Add-Tile $graphics $x $y $size

    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $width = $size * 0.13
    foreach ($i in 0..3) {
        $height = $size * (0.16 + $i * 0.13)
        $bar = New-RoundedRectangle ($x + $size * (0.2 + $i * 0.165)) ($y + $size * 0.76 - $height) $width $height ($width * 0.3)
        $graphics.FillPath($white, $bar)
    }
    $white.Dispose()
}

# A 9 pin serial connector on a rounded square.
function Add-Connector($graphics, [single]$x, [single]$y, [single]$size) {
    Add-Tile $graphics $x $y $size

    $top = $y + $size * 0.34
    $bottom = $y + $size * 0.66
    $shell = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $shell.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($x + $size * 0.14, $top),
        [System.Drawing.PointF]::new($x + $size * 0.86, $top),
        [System.Drawing.PointF]::new($x + $size * 0.76, $bottom),
        [System.Drawing.PointF]::new($x + $size * 0.24, $bottom)))

    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, [Math]::Max(1.5, $size * 0.06))
    $pen.LineJoin = 'Round'
    $graphics.DrawPath($pen, $shell)
    $pen.Dispose()

    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $pin = $size * 0.07
    foreach ($i in 0..4) {
        $graphics.FillEllipse($white, $x + $size * (0.27 + $i * 0.115) - $pin / 2, $y + $size * 0.44 - $pin / 2, $pin, $pin)
    }
    foreach ($i in 0..3) {
        $graphics.FillEllipse($white, $x + $size * (0.3275 + $i * 0.115) - $pin / 2, $y + $size * 0.56 - $pin / 2, $pin, $pin)
    }
    $white.Dispose()
}

function Add-Glyph($graphics, [string]$glyph, [single]$x, [single]$y, [single]$size) {
    if ($glyph -eq 'Bars') { Add-Bars $graphics $x $y $size } else { Add-Connector $graphics $x $y $size }
}

function New-Logo([int]$size, [string]$path, [single]$margin = 0, [string]$glyph = 'Connector') {
    $bitmap, $graphics = New-Canvas $size $size
    $inner = $size * (1 - 2 * $margin)
    Add-Glyph $graphics $glyph ($size * $margin) ($size * $margin) $inner
    Save-Png $bitmap $graphics $path
}

function Add-Text($graphics, [string]$text, [single]$x, [single]$y, [single]$size, $color, [switch]$Bold, [switch]$Right) {
    $style = if ($Bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    $font = [System.Drawing.Font]::new('Segoe UI', $size, $style, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new($color)
    if ($Right) {
        $x -= $graphics.MeasureString($text, $font).Width
    }
    $graphics.DrawString($text, $font, $brush, $x, $y)
    $brush.Dispose()
    $font.Dispose()
}

function New-Theme([bool]$dark) {
    if ($dark) {
        return @{
            Background = [System.Drawing.Color]::FromArgb(255, 44, 44, 44)
            Text       = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
            Subtle     = [System.Drawing.Color]::FromArgb(255, 170, 170, 170)
            Line       = [System.Drawing.Color]::FromArgb(255, 70, 70, 70)
            Accent     = [System.Drawing.Color]::FromArgb(255, 96, 165, 250)
        }
    }

    return @{
        Background = [System.Drawing.Color]::FromArgb(255, 249, 249, 249)
        Text       = [System.Drawing.Color]::FromArgb(255, 26, 26, 26)
        Subtle     = [System.Drawing.Color]::FromArgb(255, 96, 96, 96)
        Line       = [System.Drawing.Color]::FromArgb(255, 224, 224, 224)
        Accent     = $accent
    }
}

function New-Screenshot([bool]$dark, [string]$path, [string]$title, [scriptblock]$body, [string]$glyph = 'Connector') {
    $theme = New-Theme $dark
    $bitmap, $graphics = New-Canvas 300 304

    $card = New-RoundedRectangle 0 0 300 304 8
    $brush = [System.Drawing.SolidBrush]::new($theme.Background)
    $graphics.FillPath($brush, $card)
    $brush.Dispose()

    Add-Glyph $graphics $glyph 16 14 18
    Add-Text $graphics $title 40 14 13 $theme.Subtle

    & $body $graphics $theme
    Save-Png $bitmap $graphics $path
}

function Add-Button($graphics, $theme, [string]$text, [single]$right, [single]$y) {
    $width = 58
    $shape = New-RoundedRectangle ($right - $width) $y $width 26 4
    $pen = [System.Drawing.Pen]::new($theme.Line, 1)
    $graphics.DrawPath($pen, $shape)
    $pen.Dispose()
    Add-Text $graphics $text ($right - $width + 13) ($y + 5) 12 $theme.Text
}

$serialPorts = {
    param($graphics, $theme)

    $rows = @(
        @{ Port = 'COM3'; Label = 'CH340'; New = $false },
        @{ Port = 'COM7'; Label = 'Sensor board #2'; New = $true },
        @{ Port = 'COM12'; Label = 'CP210x'; New = $false }
    )

    $y = 52
    foreach ($row in $rows) {
        Add-Text $graphics $row.Port 16 $y 24 $theme.Text -Bold
        Add-Text $graphics $row.Label 17 ($y + 32) 13 $theme.Subtle
        if ($row.New) {
            Add-Text $graphics 'new' 100 ($y + 9) 12 $theme.Accent -Bold
        }
        Add-Button $graphics $theme 'Copy' 284 ($y + 12)

        $pen = [System.Drawing.Pen]::new($theme.Line, 1)
        $graphics.DrawLine($pen, 16, $y + 62, 284, $y + 62)
        $pen.Dispose()
        $y += 76
    }
}

$systemStatus = {
    param($graphics, $theme)

    $good = [System.Drawing.Color]::FromArgb(255, 34, 160, 90)
    $rows = @(
        @{ Title = 'TestNetwork'; Detail = '72% · 192.0.2.10' },
        @{ Title = 'Ethernet'; Detail = '1 Gbps · 192.0.2.11' },
        @{ Title = 'Internet access'; Detail = ''; Color = $good },
        @{ Title = 'Speakers'; Detail = 'Output' },
        @{ Title = 'Microphone'; Detail = 'Input' }
    )

    $y = 50
    foreach ($row in $rows) {
        if ($row.Detail) {
            $dot = [System.Drawing.SolidBrush]::new($theme.Accent)
            $graphics.FillEllipse($dot, 18, $y + 12, 12, 12)
            $dot.Dispose()
            Add-Text $graphics $row.Title 42 $y 15 $theme.Text -Bold
            Add-Text $graphics $row.Detail 42 ($y + 20) 12 $theme.Subtle
            $y += 52
        }
        else {
            Add-Text $graphics $row.Title 17 ($y - 2) 12 $row.Color -Bold
            $y += 28
        }
    }
}

Write-Host 'Package logos'
New-Logo 50 (Join-Path $assets 'StoreLogo.png')
New-Logo 44 (Join-Path $assets 'Square44x44Logo.png')
New-Logo 150 (Join-Path $assets 'Square150x150Logo.png') 0.2

Write-Host 'Widget assets'
New-Logo 64 (Join-Path $providerAssets 'SerialPorts_Icon.png')
New-Screenshot $false (Join-Path $providerAssets 'SerialPorts_Screenshot_Light.png') 'Serial Ports' $serialPorts
New-Screenshot $true (Join-Path $providerAssets 'SerialPorts_Screenshot_Dark.png') 'Serial Ports' $serialPorts

New-Logo 64 (Join-Path $providerAssets 'SystemStatus_Icon.png') 0 'Bars'
New-Screenshot $false (Join-Path $providerAssets 'SystemStatus_Screenshot_Light.png') 'System Status' $systemStatus 'Bars'
New-Screenshot $true (Join-Path $providerAssets 'SystemStatus_Screenshot_Dark.png') 'System Status' $systemStatus 'Bars'

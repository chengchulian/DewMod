$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$modRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $modRoot 'assets/icons'
$null = New-Item -ItemType Directory -Path $output -Force
$scale = 4
$white = [Drawing.Brushes]::White

function Polygon([float[]]$Coordinates) {
    $points = for ($i = 0; $i -lt $Coordinates.Length; $i += 2) {
        [Drawing.PointF]::new($Coordinates[$i], $Coordinates[$i + 1])
    }
    $script:g.FillPolygon($white, [Drawing.PointF[]]$points)
}
function Line([float]$Width, [float[]]$Coordinates) {
    $pen = [Drawing.Pen]::new([Drawing.Color]::White, $Width)
    $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $points = for ($i = 0; $i -lt $Coordinates.Length; $i += 2) {
        [Drawing.PointF]::new($Coordinates[$i], $Coordinates[$i + 1])
    }
    $script:g.DrawLines($pen, [Drawing.PointF[]]$points)
    $pen.Dispose()
}
function Arc([float]$Width, [float]$X, [float]$Y, [float]$W, [float]$H, [float]$Start, [float]$Sweep) {
    $pen = [Drawing.Pen]::new([Drawing.Color]::White, $Width)
    $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $script:g.DrawArc($pen, $X, $Y, $W, $H, $Start, $Sweep)
    $pen.Dispose()
}
function Disc([float]$X, [float]$Y, [float]$Radius) {
    $script:g.FillEllipse($white, ($X - $Radius), ($Y - $Radius), (2 * $Radius), (2 * $Radius))
}
function CutEllipse([float]$X, [float]$Y, [float]$W, [float]$H) {
    $script:g.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $script:g.FillEllipse([Drawing.Brushes]::Transparent, $X, $Y, $W, $H)
    $script:g.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceOver
}
function Star([float]$X, [float]$Y, [float]$Radius, [float]$Inner = 0.4, [int]$Tips = 4) {
    $coordinates = for ($i = 0; $i -lt (2 * $Tips); $i++) {
        $angle = -[Math]::PI / 2 + $i * [Math]::PI / $Tips
        $r = if ($i % 2 -eq 0) { $Radius } else { $Radius * $Inner }
        [float]($X + [Math]::Cos($angle) * $r)
        [float]($Y + [Math]::Sin($angle) * $r)
    }
    Polygon $coordinates
}
function Curve([float]$Width, [float[]]$Coordinates) {
    $pen = [Drawing.Pen]::new([Drawing.Color]::White, $Width)
    $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $script:g.DrawBezier($pen, $Coordinates[0], $Coordinates[1], $Coordinates[2], $Coordinates[3],
        $Coordinates[4], $Coordinates[5], $Coordinates[6], $Coordinates[7])
    $pen.Dispose()
}

$designs = [ordered]@{
    LucidDream_AstronomyMaster = {
        Arc 5 16 16 96 96 18 104
        Arc 5 16 16 96 96 141 109
        Arc 5 16 16 96 96 269 89
        Line 4 @(34, 43, 66, 27, 92, 51, 76, 83, 38, 89, 34, 43, 76, 83)
        Disc 34 43 7; Disc 66 27 6; Disc 92 51 7; Disc 76 83 6; Disc 38 89 7
        Star 62 59 15 0.28
        Star 99 99 13 0.3
    }
    LucidDream_Telescope = {
        # 镜身向右上倾斜，断开的镜箍保留小尺寸识别度。
        Polygon @(30, 51, 77, 24, 92, 48, 44, 75)
        Polygon @(77, 19, 87, 13, 106, 44, 96, 50)
        Polygon @(19, 62, 27, 57, 37, 73, 29, 79)
        Line 6 @(56, 73, 56, 86)
        Disc 56 86 7
        Line 6 @(56, 86, 31, 112)
        Line 6 @(56, 86, 82, 112)
        Line 6 @(56, 86, 56, 114)
        Star 105 73 13 0.28
        Star 29 25 8 0.3
    }
    LucidDream_ResourceInversion = {
        Arc 7 19 14 89 95 205 100
        Polygon @(91, 14, 108, 34, 82, 34)
        Arc 7 19 19 89 95 25 100
        Polygon @(37, 114, 20, 94, 46, 94)
        Disc 45 57 21
        CutEllipse 31 43 28 28
        Line 5 @(45, 49, 45, 65)
        Star 85 73 23 0.45
        CutEllipse 80 68 10 10
    }
    LucidDream_PrimordialCurse = {
        # 外部尖角与中心眼印构成原始咒符。
        Polygon @(64, 8, 72, 30, 64, 26, 56, 30)
        Curve 7 @(43, 27, 20, 28, 17, 46, 30, 60)
        Curve 7 @(85, 27, 108, 28, 111, 46, 98, 60)
        Polygon @(23, 32, 16, 17, 37, 28)
        Polygon @(105, 32, 112, 17, 91, 28)
        $path = [Drawing.Drawing2D.GraphicsPath]::new()
        $path.AddBezier(24, 59, 47, 29, 81, 29, 104, 59)
        $path.AddBezier(104, 59, 81, 89, 47, 89, 24, 59)
        $script:g.FillPath($white, $path); $path.Dispose()
        CutEllipse 47 42 34 34
        Polygon @(64, 44, 69, 59, 64, 74, 59, 59)
        Line 7 @(64, 85, 64, 110)
        Line 6 @(43, 83, 39, 98, 28, 108)
        Line 6 @(85, 83, 89, 98, 100, 108)
        Polygon @(64, 120, 53, 108, 75, 108)
    }
    LucidDream_FallenStarGod = {
        Arc 5 24 12 80 29 189 161
        Arc 5 24 12 80 29 10 65
        Line 5 @(24, 41, 33, 54)
        Line 5 @(104, 41, 95, 54)
        Star 64 66 40 0.45 5
        # 透明裂痕贯穿星冠。
        $script:g.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $pen = [Drawing.Pen]::new([Drawing.Color]::Transparent, 6)
        $script:g.DrawLines($pen, [Drawing.PointF[]]@(
            [Drawing.PointF]::new(68, 30), [Drawing.PointF]::new(57, 59),
            [Drawing.PointF]::new(70, 73), [Drawing.PointF]::new(59, 106)))
        $pen.Dispose()
        $script:g.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceOver
        Polygon @(28, 91, 33, 104, 25, 110, 20, 99)
        Polygon @(96, 94, 106, 104, 99, 116, 93, 106)
        Star 64 117 7 0.35
    }
    LucidDream_HeavyFootsteps = {
        $script:g.FillEllipse($white, 30, 17, 27, 43)
        $script:g.FillEllipse($white, 33, 59, 23, 21)
        $script:g.FillEllipse($white, 74, 43, 27, 43)
        $script:g.FillEllipse($white, 76, 85, 23, 21)
        CutEllipse 25 45 14 17
        CutEllipse 92 71 14 17
        Line 6 @(22, 88, 63, 88)
        Polygon @(32, 88, 26, 109, 60, 109, 54, 88)
        Line 6 @(68, 114, 107, 114)
        Polygon @(76, 114, 73, 123, 103, 123, 100, 114)
        Line 4 @(16, 58, 16, 72)
        Line 4 @(112, 81, 112, 95)
    }
}

foreach ($name in $designs.Keys) {
    $large = [Drawing.Bitmap]::new((128 * $scale), (128 * $scale), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $script:g = [Drawing.Graphics]::FromImage($large)
    $script:g.Clear([Drawing.Color]::Transparent)
    $script:g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $script:g.ScaleTransform($scale, $scale)
    & $designs[$name]
    $script:g.Dispose()
    $icon = [Drawing.Bitmap]::new(128, 128, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $resize = [Drawing.Graphics]::FromImage($icon)
    $resize.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $resize.DrawImage($large, 0, 0, 128, 128)
    $resize.Dispose()
    # 统一为白色 RGB，只用 Alpha 表示抗锯齿，防止 Unity 着色后产生暗边。
    for ($y = 0; $y -lt 128; $y++) {
        for ($x = 0; $x -lt 128; $x++) {
            $alpha = $icon.GetPixel($x, $y).A
            $icon.SetPixel($x, $y, [Drawing.Color]::FromArgb($alpha, 255, 255, 255))
        }
    }
    $icon.Save((Join-Path $output ($name + '.png')), [Drawing.Imaging.ImageFormat]::Png)
    $icon.Dispose(); $large.Dispose()
}

# 深底预览同时展示游戏中常见的 48 像素尺寸。
$sheet = [Drawing.Bitmap]::new(900, 230)
$preview = [Drawing.Graphics]::FromImage($sheet)
$preview.Clear([Drawing.Color]::FromArgb(32, 35, 38))
$labels = @('天文大师', '望远镜', '资源反转', '最原始的诅咒', '星神的堕落', '沉足')
$font = [Drawing.Font]::new('Microsoft YaHei', 11)
$format = [Drawing.StringFormat]::new()
$format.Alignment = [Drawing.StringAlignment]::Center
$i = 0
foreach ($name in $designs.Keys) {
    $source = [Drawing.Image]::FromFile((Join-Path $output ($name + '.png')))
    $preview.DrawImage($source, [Drawing.Rectangle]::new(($i * 150 + 19), 12, 112, 112))
    $preview.DrawString($labels[$i], $font, $white, [Drawing.RectangleF]::new(($i * 150), 136, 150, 26), $format)
    $preview.DrawImage($source, [Drawing.Rectangle]::new(($i * 150 + 51), 173, 48, 48))
    $source.Dispose(); $i++
}
$sheet.Save((Join-Path $modRoot 'assets/icons-preview.png'), [Drawing.Imaging.ImageFormat]::Png)
$format.Dispose(); $font.Dispose(); $preview.Dispose(); $sheet.Dispose()
Write-Output 'Rendered six 128x128 transparent icons and preview.'

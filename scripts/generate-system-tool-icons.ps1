param(
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not $OutDir) {
    $OutDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'DevEnv.Host\Resources\icons\win'
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

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
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-FilledRect {
    param(
        $Graphics,
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [System.Drawing.Brush]$Brush,
        [float]$Radius = 2
    )

    $path = New-RoundedRectPath -X $X -Y $Y -Width $Width -Height $Height -Radius $Radius
    $Graphics.FillPath($Brush, $path)
    $path.Dispose()
}

function Draw-ToolGlyph {
    param(
        $Graphics,
        [string]$Glyph,
        [System.Drawing.Color]$TileColor
    )

    $white = [System.Drawing.Brushes]::White
    $tileBrush = New-Object System.Drawing.SolidBrush $TileColor
    $penWhite3 = New-Object System.Drawing.Pen ([System.Drawing.Color]::White, 3)
    $penWhite2 = New-Object System.Drawing.Pen ([System.Drawing.Color]::White, 2)

    switch ($Glyph) {
        'computer' {
            # 显示器
            New-FilledRect -Graphics $Graphics -X 12 -Y 8 -Width 40 -Height 32 -Brush $white -Radius 4
            New-FilledRect -Graphics $Graphics -X 16 -Y 12 -Width 32 -Height 24 -Brush $tileBrush -Radius 2
            # 屏幕中的两个窗口
            New-FilledRect -Graphics $Graphics -X 20 -Y 16 -Width 11 -Height 7 -Brush $white -Radius 1
            New-FilledRect -Graphics $Graphics -X 34 -Y 16 -Width 10 -Height 15 -Brush $white -Radius 1
            # 支架
            New-FilledRect -Graphics $Graphics -X 28 -Y 40 -Width 8 -Height 7 -Brush $white -Radius 1
            New-FilledRect -Graphics $Graphics -X 23 -Y 47 -Width 18 -Height 4 -Brush $white -Radius 2
        }
        'server' {
            # 三台堆叠服务器
            New-FilledRect -Graphics $Graphics -X 12 -Y 8 -Width 40 -Height 12 -Brush $white -Radius 3
            New-FilledRect -Graphics $Graphics -X 12 -Y 24 -Width 40 -Height 12 -Brush $white -Radius 3
            New-FilledRect -Graphics $Graphics -X 12 -Y 40 -Width 40 -Height 12 -Brush $white -Radius 3
            # 指示灯
            $Graphics.FillEllipse($tileBrush, 17, 12.5, 5, 5)
            $Graphics.FillEllipse($tileBrush, 17, 28.5, 5, 5)
            $Graphics.FillEllipse($tileBrush, 17, 44.5, 5, 5)
        }
        'config' {
            # 三条滑杆
            $Graphics.DrawLine($penWhite3, 12, 16, 52, 16)
            $Graphics.DrawLine($penWhite3, 12, 32, 52, 32)
            $Graphics.DrawLine($penWhite3, 12, 48, 52, 48)
            $Graphics.FillEllipse($white, 30, 10, 12, 12)
            $Graphics.FillEllipse($white, 16, 26, 12, 12)
            $Graphics.FillEllipse($white, 36, 42, 12, 12)
        }
        'regedit' {
            # 注册表项网格
            foreach ($x in @(16, 27, 38)) {
                foreach ($y in @(16, 27, 38)) {
                    New-FilledRect -Graphics $Graphics -X $x -Y $y -Width 10 -Height 10 -Brush $white -Radius 2
                    New-FilledRect -Graphics $Graphics -X ($x + 3) -Y ($y + 3) -Width 4 -Height 4 -Brush $tileBrush -Radius 1
                }
            }
        }
        'env' {
            $font = New-Object System.Drawing.Font('Segoe UI', 28, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $format = New-Object System.Drawing.StringFormat
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            $rect = New-Object System.Drawing.RectangleF(0, 0, 64, 64)
            $Graphics.DrawString('%', $font, $white, $rect, $format)
            $font.Dispose()
            $format.Dispose()
        }
        'device' {
            # 芯片与引脚
            foreach ($pin in @(26, 34)) {
                New-FilledRect -Graphics $Graphics -X $pin -Y 9 -Width 4 -Height 6 -Brush $white -Radius 1
                New-FilledRect -Graphics $Graphics -X $pin -Y 49 -Width 4 -Height 6 -Brush $white -Radius 1
                New-FilledRect -Graphics $Graphics -X 9 -Y $pin -Width 6 -Height 4 -Brush $white -Radius 1
                New-FilledRect -Graphics $Graphics -X 49 -Y $pin -Width 6 -Height 4 -Brush $white -Radius 1
            }
            New-FilledRect -Graphics $Graphics -X 18 -Y 18 -Width 28 -Height 28 -Brush $white -Radius 4
            New-FilledRect -Graphics $Graphics -X 24 -Y 24 -Width 16 -Height 16 -Brush $tileBrush -Radius 2
        }
        'disk' {
            # 硬盘
            New-FilledRect -Graphics $Graphics -X 12 -Y 16 -Width 40 -Height 32 -Brush $white -Radius 3
            New-FilledRect -Graphics $Graphics -X 12 -Y 16 -Width 40 -Height 10 -Brush $tileBrush -Radius 3
            $Graphics.FillEllipse($white, 17, 20, 5, 5)
            $Graphics.DrawLine($penWhite2, 17, 36, 47, 36)
        }
        'sound' {
            # 扬声器
            $speaker = New-Object System.Drawing.Drawing2D.GraphicsPath
            $speaker.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 12, 25),
                (New-Object System.Drawing.PointF 20, 25),
                (New-Object System.Drawing.PointF 20, 39),
                (New-Object System.Drawing.PointF 12, 39)
            ))
            $speaker.CloseFigure()
            $speaker.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 20, 25),
                (New-Object System.Drawing.PointF 33, 18),
                (New-Object System.Drawing.PointF 33, 46),
                (New-Object System.Drawing.PointF 20, 39)
            ))
            $speaker.CloseFigure()
            $Graphics.FillPath($white, $speaker)
            $speaker.Dispose()
            # 声波
            $Graphics.DrawArc($penWhite2, 31, 23, 18, 18, -55, 110)
            $Graphics.DrawArc($penWhite2, 38, 16, 32, 32, -55, 110)
        }
        'display' {
            # 显示器（带桌面壁纸）
            New-FilledRect -Graphics $Graphics -X 12 -Y 8 -Width 40 -Height 32 -Brush $white -Radius 4
            New-FilledRect -Graphics $Graphics -X 16 -Y 12 -Width 32 -Height 24 -Brush $tileBrush -Radius 2
            # 太阳与山
            $Graphics.FillEllipse($white, 20, 17, 6, 6)
            $mountain = New-Object System.Drawing.Drawing2D.GraphicsPath
            $mountain.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 27, 34),
                (New-Object System.Drawing.PointF 36, 24),
                (New-Object System.Drawing.PointF 45, 34)
            ))
            $mountain.CloseFigure()
            $Graphics.FillPath($white, $mountain)
            $mountain.Dispose()
            New-FilledRect -Graphics $Graphics -X 28 -Y 40 -Width 8 -Height 7 -Brush $white -Radius 1
            New-FilledRect -Graphics $Graphics -X 23 -Y 47 -Width 18 -Height 4 -Brush $white -Radius 2
        }
        'power' {
            # 电池与闪电
            New-FilledRect -Graphics $Graphics -X 11 -Y 21 -Width 39 -Height 22 -Brush $white -Radius 3
            New-FilledRect -Graphics $Graphics -X 50 -Y 27 -Width 5 -Height 10 -Brush $white -Radius 1
            $bolt = New-Object System.Drawing.Drawing2D.GraphicsPath
            $bolt.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 29, 24),
                (New-Object System.Drawing.PointF 21, 34),
                (New-Object System.Drawing.PointF 27, 34),
                (New-Object System.Drawing.PointF 24, 44),
                (New-Object System.Drawing.PointF 36, 33),
                (New-Object System.Drawing.PointF 29, 33),
                (New-Object System.Drawing.PointF 33, 24)
            ))
            $bolt.CloseFigure()
            $Graphics.FillPath($tileBrush, $bolt)
            $bolt.Dispose()
        }
        'firewall' {
            # 盾牌
            $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
            $shield.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 18, 14),
                (New-Object System.Drawing.PointF 46, 14),
                (New-Object System.Drawing.PointF 46, 29),
                (New-Object System.Drawing.PointF 32, 50),
                (New-Object System.Drawing.PointF 18, 29)
            ))
            $shield.CloseFigure()
            $Graphics.FillPath($white, $shield)
            $shield.Dispose()
        }
        'security' {
            # 盾牌轮廓与挂锁
            $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
            $shield.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 18, 14),
                (New-Object System.Drawing.PointF 46, 14),
                (New-Object System.Drawing.PointF 46, 29),
                (New-Object System.Drawing.PointF 32, 50),
                (New-Object System.Drawing.PointF 18, 29)
            ))
            $shield.CloseFigure()
            $Graphics.DrawPath($penWhite3, $shield)
            $shield.Dispose()
            New-FilledRect -Graphics $Graphics -X 26 -Y 29 -Width 12 -Height 12 -Brush $white -Radius 2
            $Graphics.DrawArc($penWhite3, 27, 21, 10, 10, 180, 180)
            $Graphics.FillEllipse($tileBrush, 30, 33, 4, 4)
        }
        'network' {
            # 地球
            $Graphics.DrawEllipse($penWhite3, 14, 14, 36, 36)
            $Graphics.DrawArc($penWhite2, 14, 27, 36, 10, 0, 360)
            $Graphics.DrawArc($penWhite2, 27, 14, 10, 36, 0, 360)
        }
        'uac' {
            # 用户
            $Graphics.FillEllipse($white, 23, 12, 18, 18)
            $shoulder = New-Object System.Drawing.Drawing2D.GraphicsPath
            $shoulder.AddEllipse(16, 38, 32, 17)
            $shoulder.CloseFigure()
            $Graphics.FillPath($white, $shoulder)
            $shoulder.Dispose()
        }
        'task' {
            # 柱状图
            New-FilledRect -Graphics $Graphics -X 16 -Y 30 -Width 8 -Height 22 -Brush $white -Radius 2
            New-FilledRect -Graphics $Graphics -X 28 -Y 20 -Width 8 -Height 32 -Brush $white -Radius 2
            New-FilledRect -Graphics $Graphics -X 40 -Y 24 -Width 8 -Height 28 -Brush $white -Radius 2
        }
        'res' {
            # 仪表盘
            $Graphics.DrawArc($penWhite3, 15, 14, 34, 34, 205, 130)
            $Graphics.DrawLine($penWhite3, 32, 31, 43, 21)
            $Graphics.FillEllipse($white, 29.5, 28.5, 5, 5)
        }
        'event' {
            # 文档与日志
            New-FilledRect -Graphics $Graphics -X 22 -Y 8 -Width 20 -Height 48 -Brush $white -Radius 3
            $Graphics.DrawLine($penWhite2, 26, 17, 38, 17)
            $Graphics.DrawLine($penWhite2, 26, 24, 38, 24)
            $Graphics.DrawLine($penWhite2, 26, 31, 38, 31)
            $Graphics.DrawLine($penWhite2, 26, 38, 32, 38)
        }
        'info' {
            $Graphics.FillEllipse($white, 12, 12, 40, 40)
            $Graphics.FillRectangle($tileBrush, 29.5, 27, 5, 15)
            $Graphics.FillEllipse($tileBrush, 29.5, 20, 5, 5)
        }
        'planned_task' {
            # 时钟
            $Graphics.DrawEllipse($penWhite3, 14, 14, 36, 36)
            $Graphics.DrawLine($penWhite3, 32, 32, 32, 21)
            $Graphics.DrawLine($penWhite3, 32, 32, 42, 32)
            $Graphics.FillEllipse($white, 29.5, 29.5, 5, 5)
        }
        'startup' {
            # 火箭式开机：竖线 + 箭头
            $Graphics.DrawLine($penWhite3, 32, 46, 32, 20)
            $path = New-Object System.Drawing.Drawing2D.GraphicsPath
            $path.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 32, 12),
                (New-Object System.Drawing.PointF 42, 26),
                (New-Object System.Drawing.PointF 22, 26)
            ))
            $Graphics.FillPath($white, $path)
            $path.Dispose()
            New-FilledRect -Graphics $Graphics -X 24 -Y 44 -Width 16 -Height 6 -Brush $white -Radius 2
        }
        'control' {
            # 控制面板网格
            New-FilledRect -Graphics $Graphics -X 17 -Y 17 -Width 12 -Height 12 -Brush $white -Radius 3
            New-FilledRect -Graphics $Graphics -X 35 -Y 17 -Width 12 -Height 12 -Brush $white -Radius 3
            New-FilledRect -Graphics $Graphics -X 17 -Y 35 -Width 12 -Height 12 -Brush $white -Radius 3
            New-FilledRect -Graphics $Graphics -X 35 -Y 35 -Width 12 -Height 12 -Brush $white -Radius 3
        }
        'default' {
            # 工具箱
            $Graphics.DrawArc($penWhite3, 19, 10, 26, 18, 180, 180)
            New-FilledRect -Graphics $Graphics -X 13 -Y 26 -Width 38 -Height 19 -Brush $white -Radius 3
        }
        default {
            # 通用圆角方块
            New-FilledRect -Graphics $Graphics -X 18 -Y 18 -Width 28 -Height 28 -Brush $white -Radius 6
        }
    }

    $tileBrush.Dispose()
    $penWhite3.Dispose()
    $penWhite2.Dispose()
}

function New-ToolIcon {
    param(
        [string]$Name,
        [string]$HexColor,
        [string]$Glyph
    )

    $bmp = New-Object System.Drawing.Bitmap 128, 128
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform(2, 2)

    $tileColor = [System.Drawing.ColorTranslator]::FromHtml($HexColor)
    $tileBrush = New-Object System.Drawing.SolidBrush $tileColor
    $tilePath = New-RoundedRectPath -X 0 -Y 0 -Width 64 -Height 64 -Radius 14
    $g.FillPath($tileBrush, $tilePath)

    Draw-ToolGlyph -Graphics $g -Glyph $Glyph -TileColor $tileColor

    $outPath = Join-Path $OutDir "$Name.png"
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $tilePath.Dispose()
    $tileBrush.Dispose()
    $g.Dispose()
    $bmp.Dispose()

    Write-Output "已生成: $outPath"
}

$icons = @(
    @{ Name = 'computer';     Color = '#1565C0'; Glyph = 'computer' },
    @{ Name = 'server';       Color = '#5E35B1'; Glyph = 'server' },
    @{ Name = 'config';       Color = '#F57F17'; Glyph = 'config' },
    @{ Name = 'regedit';      Color = '#6D4C41'; Glyph = 'regedit' },
    @{ Name = 'env';          Color = '#00897B'; Glyph = 'env' },
    @{ Name = 'device';       Color = '#3949AB'; Glyph = 'device' },
    @{ Name = 'disk';         Color = '#0277BD'; Glyph = 'disk' },
    @{ Name = 'sound';        Color = '#F4511E'; Glyph = 'sound' },
    @{ Name = 'display';      Color = '#7CB342'; Glyph = 'display' },
    @{ Name = 'power';        Color = '#43A047'; Glyph = 'power' },
    @{ Name = 'firewall';     Color = '#E53935'; Glyph = 'firewall' },
    @{ Name = 'security';     Color = '#D81B60'; Glyph = 'security' },
    @{ Name = 'network';      Color = '#039BE5'; Glyph = 'network' },
    @{ Name = 'uac';          Color = '#8E24AA'; Glyph = 'uac' },
    @{ Name = 'task';         Color = '#00ACC1'; Glyph = 'task' },
    @{ Name = 'res';          Color = '#FB8C00'; Glyph = 'res' },
    @{ Name = 'event';        Color = '#455A64'; Glyph = 'event' },
    @{ Name = 'info';         Color = '#1E88E5'; Glyph = 'info' },
    @{ Name = 'planned_task'; Color = '#26A69A'; Glyph = 'planned_task' },
    @{ Name = 'startup';      Color = '#00897B'; Glyph = 'startup' },
    @{ Name = 'control';      Color = '#546E7A'; Glyph = 'control' },
    @{ Name = 'default';      Color = '#78909C'; Glyph = 'default' }
)

foreach ($icon in $icons) {
    New-ToolIcon -Name $icon.Name -HexColor $icon.Color -Glyph $icon.Glyph
}

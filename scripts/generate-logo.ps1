# 生成平面品牌 Logo：SVG、各尺寸 PNG 与多尺寸 ICO。
# 全部图形由同一套 64 单位坐标系绘制，SVG 与位图输出保持一致。
# 用法: .\scripts\generate-logo.ps1            # 仅输出 assets\brand\
#       .\scripts\generate-logo.ps1 -Apply    # 同时写入程序图标与站点 logo
# 输出: assets\brand\

param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outDir = Join-Path $root 'assets\brand'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# ---- 调色板（与 website/src/styles/main.css 的品牌色一致）----
$tealDeep = [System.Drawing.ColorTranslator]::FromHtml('#0F766E') # 盒体
$amber    = [System.Drawing.ColorTranslator]::FromHtml('#FBBF24') # 提示符下划线
$paper    = [System.Drawing.ColorTranslator]::FromHtml('#F0FDFA') # 提示符箭头

function New-RoundedRectPath([float]$X, [float]$Y, [float]$W, [float]$H, [float]$R) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($R, [Math]::Min($W, $H) / 2) * 2
    if ($d -le 0) {
        $path.AddRectangle([System.Drawing.RectangleF]::new($X, $Y, $W, $H))
        return $path
    }
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-RoundedRect([System.Drawing.Graphics]$G, [float]$X, [float]$Y, [float]$W, [float]$H, [float]$R, [System.Drawing.Color]$Color) {
    $path = New-RoundedRectPath $X $Y $W $H $R
    $brush = New-Object System.Drawing.SolidBrush $Color
    $G.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
}

function New-RoundPen([System.Drawing.Color]$Color, [single]$Width) {
    $pen = New-Object System.Drawing.Pen $Color, $Width
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

# 64 单位坐标系：盒体铺满画布（无底板、无描边），提示符 >_ 居中
function Draw-Logo([System.Drawing.Graphics]$G, [int]$Size, [bool]$Compact) {
    $G.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $G.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $G.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $G.Clear([System.Drawing.Color]::Transparent)
    $G.ScaleTransform($Size / 64.0, $Size / 64.0)

    # 盒体：直接作为图标外形，避免出现深色包边
    Fill-RoundedRect $G 0 0 64 64 15 $tealDeep

    if ($Compact) {
        # 小尺寸：放大提示符、加粗描边，保证 16px 下仍可辨认
        $arrowPen = New-RoundPen $paper 7.5
        $G.DrawLines($arrowPen, [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(14, 19),
            [System.Drawing.PointF]::new(30, 32),
            [System.Drawing.PointF]::new(14, 45)
        ))
        $arrowPen.Dispose()

        $underscorePen = New-RoundPen $amber 7.5
        $G.DrawLine($underscorePen, 36, 45, 50, 45)
        $underscorePen.Dispose()
        return
    }

    # > 箭头
    $arrowPen = New-RoundPen $paper 6.2
    $G.DrawLines($arrowPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(16, 20),
        [System.Drawing.PointF]::new(30, 32),
        [System.Drawing.PointF]::new(16, 44)
    ))
    $arrowPen.Dispose()

    # _ 下划线
    $underscorePen = New-RoundPen $amber 6.2
    $G.DrawLine($underscorePen, 36, 44, 48, 44)
    $underscorePen.Dispose()
}

function New-LogoBitmap([int]$Size, [bool]$Compact) {
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Draw-Logo -G $g -Size $Size -Compact $Compact
    $g.Dispose()
    return $bmp
}

function Write-IcoFile([object[]]$Frames, [string]$Dest) {
    $fs = [System.IO.File]::Open($Dest, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$Frames.Count)

    $offset = 6 + (16 * $Frames.Count)
    foreach ($f in $Frames) {
        $s = $f.Size
        $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
        $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$f.Bytes.Length)
        $bw.Write([uint32]$offset)
        $offset += $f.Bytes.Length
    }
    foreach ($f in $Frames) { $bw.Write($f.Bytes) }
    $bw.Flush()
    $fs.Dispose()
}

# ---- SVG：与上面的绘制共用同一套坐标 ----
$svgLogo = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" fill="none">
  <title>FireNeb DevEnv DevEnv</title>
  <rect width="64" height="64" rx="15" fill="#0F766E"/>
  <path d="M16 20 30 32 16 44" stroke="#F0FDFA" stroke-width="6.2" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M36 44h12" stroke="#FBBF24" stroke-width="6.2" stroke-linecap="round"/>
</svg>
'@

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $outDir 'logo-flat.svg'), $svgLogo, $utf8)

# ---- PNG：16/32/64/128/256/512，24 及以下用简化几何 ----
foreach ($size in @(16, 32, 64, 128, 256, 512)) {
    $bmp = New-LogoBitmap -Size $size -Compact ($size -le 24)
    $bmp.Save((Join-Path $outDir "logo-flat-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# ---- ICO：含 Win10/11 DPI 常用尺寸 ----
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bmp = New-LogoBitmap -Size $size -Compact ($size -le 24)
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += [pscustomobject]@{ Size = $size; Bytes = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}
Write-IcoFile -Frames $frames -Dest (Join-Path $outDir 'logo-flat.ico')

# ---- 应用到程序与站点 ----
if ($Apply) {
    $appRes = Join-Path $root 'DevEnv\Resources'
    $sitePublic = Join-Path $root 'website\public'

    Copy-Item (Join-Path $outDir 'logo-flat.ico') (Join-Path $appRes 'icon.ico') -Force
    Copy-Item (Join-Path $outDir 'logo-flat-512.png') (Join-Path $appRes 'icon.png') -Force
    Copy-Item (Join-Path $outDir 'logo-flat.svg') (Join-Path $sitePublic 'logo.svg') -Force

    Write-Host "应用图标: $appRes\icon.ico, $appRes\icon.png"
    Write-Host "站点图标: $sitePublic\logo.svg"
}

Write-Host "OK: $outDir"

# 生成清晰多尺寸应用图标（小尺寸用简化几何，避免 AI 大图缩放下发糊）
# 用法: .\scripts\generate-app-icon.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resDir = Join-Path $root 'DevEnv.Host\Resources'
$docsLogo = Join-Path $root 'website\public\logo.svg'
New-Item -ItemType Directory -Force -Path $resDir | Out-Null

$bg = [System.Drawing.Color]::FromArgb(255, 11, 18, 32)
$tealTop = [System.Drawing.Color]::FromArgb(255, 45, 212, 191)
$tealMid = [System.Drawing.Color]::FromArgb(255, 20, 184, 166)
$tealDark = [System.Drawing.Color]::FromArgb(255, 15, 118, 110)
$tealDeep = [System.Drawing.Color]::FromArgb(255, 17, 94, 89)
$amber = [System.Drawing.Color]::FromArgb(255, 245, 158, 11)
$white = [System.Drawing.Color]::FromArgb(255, 236, 254, 255)

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

function Draw-Icon([System.Drawing.Graphics]$g, [int]$size, [bool]$simplified) {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $radius = $size * 0.22
    $bgPath = New-RoundedRectPath 0 0 $size $size $radius
    $bgBrush = New-Object System.Drawing.SolidBrush $bg
    $g.FillPath($bgBrush, $bgPath)
    $bgBrush.Dispose(); $bgPath.Dispose()

    # 统一坐标系：内容区约占 62%
    $s = $size
    $ox = $s * 0.19
    $oy = $s * 0.22
    $cw = $s * 0.62
    $ch = $s * 0.56

    # 等距盒子关键点
    $cx = $ox + $cw * 0.5
    $topY = $oy + $ch * 0.08
    $midY = $oy + $ch * 0.38
    $botY = $oy + $ch * 0.92
    $leftX = $ox + $cw * 0.08
    $rightX = $ox + $cw * 0.92
    $lidPeakY = $oy - $ch * 0.02

    $leftBrush = New-Object System.Drawing.SolidBrush $tealDeep
    $rightBrush = New-Object System.Drawing.SolidBrush $tealMid
    $topBrush = New-Object System.Drawing.SolidBrush $tealTop
    $darkBrush = New-Object System.Drawing.SolidBrush $tealDark
    $amberBrush = New-Object System.Drawing.SolidBrush $amber
    $penWidth = [single]([Math]::Max(1.5, $s / 18.0))
    $pen = [System.Drawing.Pen]::new($white, $penWidth)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    # 箱体左/右面
    $leftFace = @(
        [System.Drawing.PointF]::new($leftX, $midY),
        [System.Drawing.PointF]::new($cx, $midY + $ch * 0.12),
        [System.Drawing.PointF]::new($cx, $botY),
        [System.Drawing.PointF]::new($leftX, $botY - $ch * 0.12)
    )
    $rightFace = @(
        [System.Drawing.PointF]::new($cx, $midY + $ch * 0.12),
        [System.Drawing.PointF]::new($rightX, $midY),
        [System.Drawing.PointF]::new($rightX, $botY - $ch * 0.12),
        [System.Drawing.PointF]::new($cx, $botY)
    )
    $g.FillPolygon($leftBrush, $leftFace)
    $g.FillPolygon($rightBrush, $rightFace)

    # 打开的盖子
    $lidTop = @(
        [System.Drawing.PointF]::new($leftX, $midY),
        [System.Drawing.PointF]::new($cx, $lidPeakY),
        [System.Drawing.PointF]::new($rightX, $midY),
        [System.Drawing.PointF]::new($cx, $midY + $ch * 0.12)
    )
    $g.FillPolygon($topBrush, $lidTop)

    if (-not $simplified) {
        # 盖内侧略加深
        $lidInner = @(
            [System.Drawing.PointF]::new($leftX + $cw * 0.08, $midY - $ch * 0.02),
            [System.Drawing.PointF]::new($cx, $lidPeakY + $ch * 0.08),
            [System.Drawing.PointF]::new($rightX - $cw * 0.08, $midY - $ch * 0.02),
            [System.Drawing.PointF]::new($cx, $midY + $ch * 0.06)
        )
        $innerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 13, 148, 136))
        $g.FillPolygon($innerBrush, $lidInner)
        $innerBrush.Dispose()
    }

    # 锁扣 / 状态点
    $dotR = [Math]::Max(1.2, $s * 0.045)
    $g.FillEllipse($amberBrush, $cx + $cw * 0.12, $midY + $ch * 0.28, $dotR * 2, $dotR * 2)

    # <> 括号（盖内侧）
    $by = $lidPeakY + $ch * 0.22
    $bx1 = $cx - $cw * 0.16
    $bx2 = $cx + $cw * 0.16
    $bh = $ch * 0.16
    $g.DrawLines($pen, @(
        [System.Drawing.PointF]::new($bx1 + $cw * 0.06, $by),
        [System.Drawing.PointF]::new($bx1, $by + $bh * 0.5),
        [System.Drawing.PointF]::new($bx1 + $cw * 0.06, $by + $bh)
    ))
    $g.DrawLines($pen, @(
        [System.Drawing.PointF]::new($bx2 - $cw * 0.06, $by),
        [System.Drawing.PointF]::new($bx2, $by + $bh * 0.5),
        [System.Drawing.PointF]::new($bx2 - $cw * 0.06, $by + $bh)
    ))

    $leftBrush.Dispose(); $rightBrush.Dispose(); $topBrush.Dispose()
    $darkBrush.Dispose(); $amberBrush.Dispose(); $pen.Dispose()
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    # 16/20/24/32 用简化几何，大尺寸保留更多层次
    Draw-Icon $g $size ($size -le 48)
    $g.Dispose()
    return $bmp
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-IconFile([System.Drawing.Bitmap[]]$bitmaps, [string]$destIco) {
    $frames = @()
    foreach ($bmp in $bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames += [pscustomobject]@{ Size = $bmp.Width; Bytes = $ms.ToArray(); Stream = $ms }
    }

    $fs = [System.IO.File]::Open($destIco, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter $fs
    $count = $frames.Count
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$count)
    $offset = 6 + (16 * $count)
    foreach ($f in $frames) {
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
    foreach ($f in $frames) { $bw.Write($f.Bytes) }
    $bw.Flush(); $fs.Dispose()
    foreach ($f in $frames) { $f.Stream.Dispose() }
}

# 含 Win10/11 DPI 常用尺寸
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$bitmaps = @()
foreach ($size in $sizes) {
    $bitmaps += New-IconBitmap $size
}

$pngPath = Join-Path $resDir 'icon.png'
$icoPath = Join-Path $resDir 'icon.ico'
Save-Png ($bitmaps | Where-Object { $_.Width -eq 256 } | Select-Object -First 1) $pngPath
# 另存一份 512 给标题栏/高清显示
$bmp512 = New-IconBitmap 512
Save-Png $bmp512 $pngPath
$bmp512.Dispose()

New-IconFile $bitmaps $icoPath
foreach ($b in $bitmaps) { $b.Dispose() }

# 同步文档站矢量 logo（保持与程序化图标一致）
@'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" fill="none">
  <rect width="64" height="64" rx="14" fill="#0B1220"/>
  <path d="M14 30 L32 20 L50 30 L32 38 Z" fill="#2DD4BF"/>
  <path d="M14 30 L32 38 L32 52 L14 44 Z" fill="#115E59"/>
  <path d="M50 30 L32 38 L32 52 L50 44 Z" fill="#14B8A6"/>
  <circle cx="40" cy="42" r="2.4" fill="#F59E0B"/>
  <path d="M24 26 L20 29.5 L24 33" stroke="#ECFEFF" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M40 26 L44 29.5 L40 33" stroke="#ECFEFF" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
'@ | Set-Content -Path $docsLogo -Encoding utf8

Write-Host "OK: $pngPath"
Write-Host "OK: $icoPath"
Write-Host "OK: $docsLogo"

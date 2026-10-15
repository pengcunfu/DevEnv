# 从 Assets/icon.jpg 生成多尺寸（16/32/48/64/128/256）PNG 压缩 ICO。
# 用法: .\scripts\create-icon.ps1
# 输出: Translator/Assets/icon.ico
#   同时用作：exe ApplicationIcon / 窗口图标 / vpk pack -i 安装器图标
#   （256px 条目满足 Velopack 图标要求；PNG 压缩条目 Windows Vista+ 均支持）

Add-Type -AssemblyName System.Drawing

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$src = Join-Path $root "Translator\Assets\icon.jpg"
$dest = Join-Path $root "Translator\Assets\icon.ico"

if (-not (Test-Path $src)) {
    throw "源图片不存在: $src"
}

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path $src))
$sizes = @(16, 32, 48, 64, 128, 256)

$pngData = @{}
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData[$size] = $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}
$source.Dispose()

$count = $sizes.Count
$headerSize = 6 + (16 * $count)
$body = New-Object System.Collections.Generic.List[byte]
$offset = $headerSize

# ICONDIR（6 字节）：reserved=0, type=1(icon), count
$header = New-Object byte[] 6
$header[2] = 1
$header[4] = [byte]$count

# ICONDIRENTRY（每项 16 字节）
$entries = New-Object System.Collections.Generic.List[byte]
foreach ($size in $sizes) {
    $data = $pngData[$size]
    $entry = New-Object byte[] 16
    $entry[0] = if ($size -eq 256) { 0 } else { [byte]$size }  # bWidth（0=256）
    $entry[1] = if ($size -eq 256) { 0 } else { [byte]$size }  # bHeight（0=256）
    $entry[2] = 0                                              # bColorCount
    $entry[3] = 0                                              # bReserved
    $entry[4] = 1                                              # wPlanes=1（小端低字节）
    $entry[6] = 32                                             # wBitCount=32（小端低字节）
    $entry[8] = [byte]($data.Length -band 0xFF)                # dwBytesInRes（小端）
    $entry[9] = [byte](($data.Length -shr 8) -band 0xFF)
    $entry[10] = [byte](($data.Length -shr 16) -band 0xFF)
    $entry[11] = [byte](($data.Length -shr 24) -band 0xFF)
    $entry[12] = [byte]($offset -band 0xFF)                    # dwImageOffset（小端）
    $entry[13] = [byte](($offset -shr 8) -band 0xFF)
    $entry[14] = [byte](($offset -shr 16) -band 0xFF)
    $entry[15] = [byte](($offset -shr 24) -band 0xFF)
    $entries.AddRange($entry)
    $body.AddRange($data)
    $offset += $data.Length
}

$result = New-Object System.Collections.Generic.List[byte]
$result.AddRange($header)
$result.AddRange($entries)
$result.AddRange($body)

[System.IO.File]::WriteAllBytes($dest, $result.ToArray())

$mb = [math]::Round((Get-Item $dest).Length / 1KB, 1)
Write-Host "OK: $dest ($mb KB)"
Write-Host "Sizes: $($sizes -join ', ') px (PNG compressed)"

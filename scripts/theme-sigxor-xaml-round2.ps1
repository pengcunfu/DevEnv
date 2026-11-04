#
# SigXor 模块 XAML 主题化（第二轮）：处理命名色（White / Gray / Silver 等）。
#
# 第一轮 theme-sigxor-xaml.ps1 处理的是十六进制色（#RRGGBB），
# 这一轮处理 WPF 内置命名色。
#
# 保持不变的：
#   Transparent            —— 无色，不是主题色
#   截图 / 取色 / 选区 / 语音浮窗里的 Black 与 White —— 功能性高对比配色
#
# 用法：.\scripts\theme-sigxor-xaml-round2.ps1 [-DryRun]
#
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$xamlDir = Join-Path $repoRoot 'DevEnv\SigXor'

# 功能性配色文件：盖在截图 / 语音内容之上，必须保持不透明与高对比
$functionalFiles = @(
    'Toolkit\Capture\RegionSelectorWindow.xaml',
    'Toolkit\Capture\ScreenshotPreviewWindow.xaml',
    'Toolkit\Capture\ScreenshotToolbarWindow.xaml',
    'Toolkit\Ocr\OcrResultWindow.xaml',
    'Toolkit\Voice\VoiceOverlayWindow.xaml'
)

# 命名色 -> 主题资源
$namedMap = @(
    @{ name = 'White';     key = 'SurfaceBrush' }
    @{ name = 'Gray';      key = 'TextMutedBrush' }
    @{ name = 'LightGray'; key = 'CardBorderBrush' }
    @{ name = 'DarkGray';  key = 'TextSecondaryBrush' }
    @{ name = 'Silver';    key = 'CardBorderBrush' }
    @{ name = 'Red';       key = 'DangerBrush' }
    @{ name = 'Green';     key = 'SuccessBrush' }
    @{ name = 'Blue';      key = 'AccentBrush' }
)

$attrRules = @(
    @{ attr = 'Foreground'; allow = @('TextPrimaryBrush','TextSecondaryBrush','TextMutedBrush','AccentBrush','SuccessBrush','DangerBrush','WarningBrush') }
    @{ attr = 'Background'; allow = @('SurfaceBrush','SurfaceAltBrush','HoverBrush','AccentBrush','ChromeBrush','AppBackgroundBrush','SelectedBrush') }
    @{ attr = 'BorderBrush'; allow = @('CardBorderBrush','AccentBrush','DangerBrush','ChromeBorderBrush') }
    @{ attr = 'Fill'; allow = @('AccentBrush','SurfaceBrush','TextPrimaryBrush','DangerBrush','SuccessBrush') }
)

$changedFiles = 0
$changedSpots = 0
$unresolved = @()

$files = Get-ChildItem $xamlDir -Recurse -File -Filter *.xaml

foreach ($file in $files) {
    $rel = $file.FullName.Substring($xamlDir.Length).TrimStart('\')
    if ($rel -in $functionalFiles) {
        Write-Host "跳过（功能性配色）: $rel"
        continue
    }

    $text = [System.IO.File]::ReadAllText($file.FullName)
    $original = $text
    $fileSpots = 0

    foreach ($rule in $attrRules) {
        foreach ($m in $namedMap) {
            $pattern = '(\b' + $rule.attr + '\s*=\s*")' + $m.name + '(")'
            $evaluator = {
                param($mm)
                if ($m.key -in $rule.allow) {
                    $script:fileSpots++
                    return $mm.Groups[1].Value + '{DynamicResource ' + $m.key + '}' + $mm.Groups[2].Value
                }
                $script:unresolved += "$rel : $($rule.attr)=$($m.name)"
                return $mm.Value
            }
            $text = [regex]::Replace($text, $pattern, $evaluator)
        }
    }

    if ($text -ne $original) {
        if (-not $DryRun) {
            $utf8 = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($file.FullName, $text, $utf8)
        }
        $changedFiles++
        $changedSpots += $fileSpots
        Write-Host ("{0,-40} 替换 {1} 处" -f $rel, $fileSpots)
    }
}

Write-Host ""
Write-Host "=== SigXor XAML 主题化（命名色） ==="
Write-Host "修改文件: $changedFiles 个，替换 $changedSpots 处（DryRun=$DryRun）"

if ($unresolved.Count -gt 0) {
    Write-Host ""
    Write-Host "--- 未映射 ---"
    $unresolved | Group-Object | ForEach-Object { Write-Host ("  {0}  ({1} 处)" -f $_.Name, $_.Count) }
}

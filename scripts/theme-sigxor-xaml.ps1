#
# SigXor 模块 XAML 主题化：把硬编码颜色替换为 DynamicResource 主题资源。
#
# 为什么必须做：AGENTS.md 第 11 条要求「颜色一律走主题资源」。
# SigXor 独立应用只有浅色主题，写死了 121 处颜色；并入 DevEnv 后
# 深色主题下这些地方会露白（浅色文字配深底、白卡片配深背景）。
#
# 映射依据（与 Themes/Palette.*.xaml 的 key 一一对应）：
#   #FFFFFF / #FFFFFF-ish  -> SurfaceBrush      （卡片 / 面板底）
#   #F9FAFB / #F3F4F6 ... -> SurfaceAltBrush    （次级底色）
#   #374151 / #111827 ... -> TextPrimaryBrush   （正文）
#   #6B7280 / #9CA3AF ... -> TextSecondaryBrush / TextMutedBrush
#   #D1D5DB / #E5E7EB ... -> CardBorderBrush / HoverBrush
#   #2563EB / #3B82F6 ... -> AccentBrush
#
# 例外（功能性颜色，必须保持不透明与高对比，不随主题变化）：
#   截图 / 取色 / 选区 / 语音浮窗里盖在内容之上的黑白配色
#
# 用法：.\scripts\theme-sigxor-xaml.ps1 [-DryRun]
#
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$xamlDir = Join-Path $repoRoot 'DevEnv\SigXor'

# 这些文件里的颜色是功能性配色（覆盖在截图 / 语音内容之上），不主题化
$functionalFiles = @(
    'Toolkit\Capture\RegionSelectorWindow.xaml',
    'Toolkit\Capture\ScreenshotPreviewWindow.xaml',
    'Toolkit\Capture\ScreenshotToolbarWindow.xaml',
    'Toolkit\Ocr\OcrResultWindow.xaml',
    'Toolkit\Voice\VoiceOverlayWindow.xaml'
)

# 颜色 -> 主题资源 的映射表。按「色相 + 明度」判定，越靠前的规则优先匹配。
$colorMap = @(
    # 强调蓝 / 靛蓝（按钮、选中态）
    @{ hex = '#2563EB'; key = 'AccentBrush' }
    @{ hex = '#2563eb'; key = 'AccentBrush' }
    @{ hex = '#3B82F6'; key = 'AccentBrush' }
    @{ hex = '#3b82f6'; key = 'AccentBrush' }
    @{ hex = '#4F46E5'; key = 'AccentBrush' }
    @{ hex = '#4f46e5'; key = 'AccentBrush' }
    @{ hex = '#6366F1'; key = 'AccentBrush' }
    @{ hex = '#6366f1'; key = 'AccentBrush' }
    @{ hex = '#1D4ED8'; key = 'AccentPressedBrush' }
    @{ hex = '#1d4ed8'; key = 'AccentPressedBrush' }
    @{ hex = '#1E40AF'; key = 'AccentPressedBrush' }
    @{ hex = '#1e40af'; key = 'AccentPressedBrush' }

    # 成功 / 危险 / 警告
    @{ hex = '#16A34A'; key = 'SuccessBrush' }
    @{ hex = '#16a34a'; key = 'SuccessBrush' }
    @{ hex = '#22C55E'; key = 'SuccessBrush' }
    @{ hex = '#22c55e'; key = 'SuccessBrush' }
    @{ hex = '#DC2626'; key = 'DangerBrush' }
    @{ hex = '#dc2626'; key = 'DangerBrush' }
    @{ hex = '#EF4444'; key = 'DangerBrush' }
    @{ hex = '#ef4444'; key = 'DangerBrush' }
    @{ hex = '#D97706'; key = 'WarningBrush' }
    @{ hex = '#d97706'; key = 'WarningBrush' }
    @{ hex = '#F59E0B'; key = 'WarningBrush' }
    @{ hex = '#f59e0b'; key = 'WarningBrush' }

    # 正文 / 主文字
    @{ hex = '#111827'; key = 'TextPrimaryBrush' }
    @{ hex = '#111827'; key = 'TextPrimaryBrush' }
    @{ hex = '#1F2937'; key = 'TextPrimaryBrush' }
    @{ hex = '#1f2937'; key = 'TextPrimaryBrush' }
    @{ hex = '#374151'; key = 'TextPrimaryBrush' }
    @{ hex = '#374151'; key = 'TextPrimaryBrush' }
    @{ hex = '#0F172A'; key = 'TextPrimaryBrush' }
    @{ hex = '#0f172a'; key = 'TextPrimaryBrush' }

    # 次级文字
    @{ hex = '#4B5563'; key = 'TextSecondaryBrush' }
    @{ hex = '#4b5563'; key = 'TextSecondaryBrush' }
    @{ hex = '#6B7280'; key = 'TextSecondaryBrush' }
    @{ hex = '#6b7280'; key = 'TextSecondaryBrush' }
    @{ hex = '#4B5563'; key = 'TextSecondaryBrush' }

    # 弱化文字
    @{ hex = '#9CA3AF'; key = 'TextMutedBrush' }
    @{ hex = '#9ca3af'; key = 'TextMutedBrush' }
    @{ hex = '#9CA3AF'; key = 'TextMutedBrush' }
    @{ hex = '#6B7280'; key = 'TextMutedBrush' }

    # 边框
    @{ hex = '#D1D5DB'; key = 'CardBorderBrush' }
    @{ hex = '#d1d5db'; key = 'CardBorderBrush' }
    @{ hex = '#E5E7EB'; key = 'CardBorderBrush' }
    @{ hex = '#e5e7eb'; key = 'CardBorderBrush' }
    @{ hex = '#D1D5DB'; key = 'CardBorderBrush' }

    # 悬停底色
    @{ hex = '#E5E7EB'; key = 'HoverBrush' }
    @{ hex = '#e5e7eb'; key = 'HoverBrush' }
    @{ hex = '#F3F4F6'; key = 'HoverBrush' }
    @{ hex = '#f3f4f6'; key = 'HoverBrush' }
    @{ hex = '#E5E7EB'; key = 'HoverBrush' }

    # 面板底色
    @{ hex = '#F9FAFB'; key = 'SurfaceAltBrush' }
    @{ hex = '#f9fafb'; key = 'SurfaceAltBrush' }
    @{ hex = '#F3F4F6'; key = 'SurfaceAltBrush' }
    @{ hex = '#f3f4f6'; key = 'SurfaceAltBrush' }
    @{ hex = '#F8FAFC'; key = 'SurfaceAltBrush' }
    @{ hex = '#f8fafc'; key = 'SurfaceAltBrush' }
    @{ hex = '#F1F5F9'; key = 'SurfaceAltBrush' }
    @{ hex = '#f1f5f9'; key = 'SurfaceAltBrush' }

    # 卡片底色
    @{ hex = '#FFFFFF'; key = 'SurfaceBrush' }
    @{ hex = '#ffffff'; key = 'SurfaceBrush' }

    # ---- Material / Tailwind 风格的语义色 ----

    # 错误提示块（红底 + 红字 + 红边）：背景/边框用 Danger 的浅色调，
    # 主题里没有浅红底，退化为 DangerBrush 会太刺眼，故这几个保留在下方人工确认清单，
    # 改为使用 Success/Danger 的常规 Brush 只对 Foreground 生效。
    @{ hex = '#B91C1C'; key = 'DangerBrush' }
    @{ hex = '#b91c1c'; key = 'DangerBrush' }
    @{ hex = '#991B1B'; key = 'DangerBrush' }
    @{ hex = '#991b1b'; key = 'DangerBrush' }
    @{ hex = '#DC2626'; key = 'DangerBrush' }
    @{ hex = '#dc2626'; key = 'DangerBrush' }
    @{ hex = '#DC2626'; key = 'DangerBrush' }

    # 警告 / 提示块（琥珀色）：文字用 WarningBrush
    @{ hex = '#92400E'; key = 'WarningBrush' }
    @{ hex = '#92400e'; key = 'WarningBrush' }
    @{ hex = '#B45309'; key = 'WarningBrush' }
    @{ hex = '#b45309'; key = 'WarningBrush' }

    # 强调靛蓝（OfficeView 的选中态文字）
    @{ hex = '#4338CA'; key = 'AccentBrush' }
    @{ hex = '#4338ca'; key = 'AccentBrush' }

    # 蓝色高亮（LauncherWindow 的系统蓝）
    @{ hex = '#0078D4'; key = 'AccentBrush' }
    @{ hex = '#0078d4'; key = 'AccentBrush' }
    @{ hex = '#1565C0'; key = 'AccentBrush' }
    @{ hex = '#1565c0'; key = 'AccentBrush' }
    @{ hex = '#90A4AE'; key = 'TextMutedBrush' }
    @{ hex = '#90a4ae'; key = 'TextMutedBrush' }
    @{ hex = '#78909C'; key = 'TextMutedBrush' }
    @{ hex = '#78909c'; key = 'TextMutedBrush' }

    # 中性灰（各种 #F5F5F5 / #F7F7F7 / #FAFAFA / #F8F8F8 面板底）
    @{ hex = '#F5F5F5'; key = 'SurfaceAltBrush' }
    @{ hex = '#f5f5f5'; key = 'SurfaceAltBrush' }
    @{ hex = '#F7F7F7'; key = 'SurfaceAltBrush' }
    @{ hex = '#f7f7f7'; key = 'SurfaceAltBrush' }
    @{ hex = '#FAFAFA'; key = 'SurfaceAltBrush' }
    @{ hex = '#fafafa'; key = 'SurfaceAltBrush' }
    @{ hex = '#F8F8F8'; key = 'SurfaceAltBrush' }
    @{ hex = '#f8f8f8'; key = 'SurfaceAltBrush' }
    @{ hex = '#F2F2F2'; key = 'SurfaceAltBrush' }
    @{ hex = '#f2f2f2'; key = 'SurfaceAltBrush' }

    # 浅蓝选中底（LauncherWindow 的搜索结果高亮）
    @{ hex = '#E3F0FC'; key = 'SelectedBrush' }
    @{ hex = '#e3f0fc'; key = 'SelectedBrush' }
    @{ hex = '#EEF2FF'; key = 'SelectedBrush' }
    @{ hex = '#eef2ff'; key = 'SelectedBrush' }

    # 浅色边框
    @{ hex = '#E0E0E0'; key = 'CardBorderBrush' }
    @{ hex = '#e0e0e0'; key = 'CardBorderBrush' }
    @{ hex = '#E8E8E8'; key = 'CardBorderBrush' }
    @{ hex = '#e8e8e8'; key = 'CardBorderBrush' }
    @{ hex = '#D0D0D0'; key = 'CardBorderBrush' }
    @{ hex = '#d0d0d0'; key = 'CardBorderBrush' }
    @{ hex = '#C0C0C0'; key = 'CardBorderBrush' }
    @{ hex = '#c0c0c0'; key = 'CardBorderBrush' }
    @{ hex = '#404040'; key = 'CardBorderBrush' }

    # 滚动条滑块（灰）
    @{ hex = '#C0C0C0'; key = 'CardBorderBrush' }

    # 提示块的浅色底（错误红底 / 警告琥珀底）：
    # 主题里没有「浅红 / 浅琥珀底」资源，用 SurfaceAltBrush 承接，
    # 底色跟随主题，红字红边仍能表达语义。
    @{ hex = '#FEF2F2'; key = 'SurfaceAltBrush' }
    @{ hex = '#fef2f2'; key = 'SurfaceAltBrush' }
    @{ hex = '#FECACA'; key = 'CardBorderBrush' }
    @{ hex = '#fecaca'; key = 'CardBorderBrush' }
    @{ hex = '#FEF3C7'; key = 'SurfaceAltBrush' }
    @{ hex = '#fef3c7'; key = 'SurfaceAltBrush' }
    @{ hex = '#FCD34D'; key = 'CardBorderBrush' }
    @{ hex = '#fcd34d'; key = 'CardBorderBrush' }
    @{ hex = '#FFFBEB'; key = 'SurfaceAltBrush' }
    @{ hex = '#fffbeb'; key = 'SurfaceAltBrush' }

    # 次级 / 弱化文字（各种灰）
    @{ hex = '#666666'; key = 'TextSecondaryBrush' }
    @{ hex = '#666666'; key = 'TextSecondaryBrush' }
    @{ hex = '#888888'; key = 'TextMutedBrush' }
    @{ hex = '#888888'; key = 'TextMutedBrush' }
    @{ hex = '#444444'; key = 'TextPrimaryBrush' }
    @{ hex = '#444444'; key = 'TextPrimaryBrush' }
)

# 属性 -> 允许使用的资源类别。
# 有些属性不该被替换成 SurfaceBrush（例如 BorderBrush 不该变白）。
$attrRules = @(
    @{ attr = 'Foreground'; allow = @('TextPrimaryBrush','TextSecondaryBrush','TextMutedBrush','AccentBrush','SuccessBrush','DangerBrush','WarningBrush','OnAccentBrush') }
    @{ attr = 'Background'; allow = @('SurfaceBrush','SurfaceAltBrush','HoverBrush','AccentBrush','AccentHoverBrush','AccentPressedBrush','ChromeBrush','AppBackgroundBrush','SelectedBrush','SuccessBrush','DangerBrush','WarningBrush','CardBorderBrush') }
    @{ attr = 'BorderBrush'; allow = @('CardBorderBrush','AccentBrush','DangerBrush','SuccessBrush','ChromeBorderBrush','TextMutedBrush','WarningBrush') }
    @{ attr = 'Fill'; allow = @('AccentBrush','SurfaceBrush','SurfaceAltBrush','TextPrimaryBrush','TextSecondaryBrush','TextMutedBrush','DangerBrush','SuccessBrush','WarningBrush','ChromeBrush') }
    @{ attr = 'Stroke'; allow = @('AccentBrush','CardBorderBrush','TextPrimaryBrush','TextSecondaryBrush','DangerBrush') }
)

# 建立「hex -> key」查找表（取第一个匹配的属性允许项）
function Resolve-Key([string]$hex, [string]$attr) {
    $rule = $attrRules | Where-Object { $_.attr -eq $attr } | Select-Object -First 1
    if (-not $rule) { return $null }
    foreach ($m in $colorMap) {
        if ($m.hex -eq $hex) {
            if ($m.key -in $rule.allow) { return $m.key }
        }
    }
    return $null
}

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

    # 形如 Property="#AABBCC" 的替换
    foreach ($rule in $attrRules) {
        $pattern = '(\b' + $rule.attr + '\s*=\s*")(#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8}))(")'
        $evaluator = {
            param($m)
            $key = Resolve-Key $m.Groups[2].Value $rule.attr
            if ($key) {
                $script:fileSpots++
                return $m.Groups[1].Value + '{DynamicResource ' + $key + '}' + $m.Groups[3].Value
            }
            $script:unresolved += "$rel : $($rule.attr)=$($m.Groups[2].Value)"
            return $m.Value
        }
        $text = [regex]::Replace($text, $pattern, $evaluator)
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
Write-Host "=== SigXor XAML 主题化 ==="
Write-Host "修改文件: $changedFiles 个，替换 $changedSpots 处（DryRun=$DryRun）"

if ($unresolved.Count -gt 0) {
    Write-Host ""
    Write-Host "--- 未映射的颜色（需人工确认）---"
    $unresolved | Group-Object | ForEach-Object { Write-Host ("  {0}  ({1} 处)" -f $_.Name, $_.Count) }
}

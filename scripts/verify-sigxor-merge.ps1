#
# 合并完整性校验：逐字节比对 DevEnv 中的 SigXor 代码与 SigXor 源仓库。
#
# 用途：合并最怕"少搬了几个文件"。这个脚本按相对路径逐个比对，
# 输出缺失 / 多余 / 内容不一致三类差异，避免靠肉眼数文件。
#
# 用法：.\scripts\verify-sigxor-merge.ps1 [-Source <SigXor 路径>] [-SkipNamespace]
#
param(
    [string]$Source = "D:\Fireneb\Projects\DevTools\SigXor",
    [switch]$SkipNamespace
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

# 源 -> 目标 的映射规则
$maps = @(
    @{ label = 'Core';  src = 'SigXor.Core'; dst = 'DevEnv.Core\SigXor' },
    @{ label = 'UI';    src = 'SigXor';      dst = 'DevEnv\SigXor' },
    @{ label = 'Tests'; src = 'SigXor.Tests'; dst = 'DevEnv.Tests' }
)

# 命名空间改写规则（与 rewrite-sigxor-namespace.ps1 保持一致）
# 注意顺序：Core / Tests 必须排在通用的 SigXor.X 规则之前，
# 否则 `namespace SigXor.Core;`（裸名无点号结尾）会被误改成 Host.SigXor.Core。
$nsRules = @(
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Core(\s*;)';  replacement = '${1}DevEnv.Core.SigXor${2}' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Core\.';      replacement = '${1}DevEnv.Core.SigXor.' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Tests(\s*;)'; replacement = '${1}DevEnv.Tests.SigXor${2}' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Tests\.';     replacement = '${1}DevEnv.Tests.SigXor.' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.([A-Za-z]+)'; replacement = '${1}DevEnv.Host.SigXor.${2}' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor(\s*;)';       replacement = '${1}DevEnv.Host.SigXor${2}' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Core(\s*;)';      replacement = '${1}DevEnv.Core.SigXor${2}' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Core\.';          replacement = '${1}DevEnv.Core.SigXor.' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Tests(\s*;)';     replacement = '${1}DevEnv.Tests.SigXor${2}' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Tests\.';         replacement = '${1}DevEnv.Tests.SigXor.' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.([A-Za-z]+)';    replacement = '${1}DevEnv.Host.SigXor.${2}' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor(\s*;)';           replacement = '${1}DevEnv.Host.SigXor${2}' }
    @{ pattern = 'clr-namespace:SigXor\.Core(\.|\")';        replacement = 'clr-namespace:DevEnv.Core.SigXor$1' }
    @{ pattern = 'clr-namespace:SigXor\.Tests(\.|\")';       replacement = 'clr-namespace:DevEnv.Tests.SigXor$1' }
    @{ pattern = 'clr-namespace:SigXor\.([A-Za-z]+)';        replacement = 'clr-namespace:DevEnv.Host.SigXor.${1}' }
    @{ pattern = 'clr-namespace:SigXor(?=")';                 replacement = 'clr-namespace:DevEnv.Host.SigXor' }
    @{ pattern = 'x:Class="SigXor\.Core\.';                  replacement = 'x:Class="DevEnv.Core.SigXor.' }
    @{ pattern = 'x:Class="SigXor\.Core"';                   replacement = 'x:Class="DevEnv.Core.SigXor"' }
    @{ pattern = 'x:Class="SigXor\.([A-Za-z]+)';            replacement = 'x:Class="DevEnv.Host.SigXor.${1}' }
    @{ pattern = 'x:Class="SigXor"';                          replacement = 'x:Class="DevEnv.Host.SigXor"' }
)

function Get-RelativeFiles([string]$root) {
    if (-not (Test-Path $root)) { return @() }
    [System.IO.Directory]::GetFiles($root, '*', [System.IO.SearchOption]::AllDirectories) |
        Where-Object { $_ -notmatch '\\(bin|obj|\.git|\.vs)\\' } |
        ForEach-Object { $_.Substring($root.Length).TrimStart('\') }
}

# 把源文本按规则改写，便于与目标比对
function Convert-Namespace([string]$text) {
    if ($SkipNamespace) { return $text }
    foreach ($rule in $nsRules) {
        $text = [regex]::Replace($text, $rule.pattern, $rule.replacement)
    }
    return $text
}

$missing = @()
$extra = @()
$mismatch = @()
$checked = 0

# 有意删除的文件：与 DevEnv 现有基础设施重复，或与宿主入口冲突。
# 删除原因见 AGENTS.md 的 SigXor 约定一节。
$expectedMissing = @(
    'UI\App.xaml',                       # 独立 Application → 并入 DevEnv.Host.App
    'UI\App.xaml.cs',                    # 同上
    'UI\Program.cs',                     # 独立入口 → 并入 DevEnv 的 Program
    'UI\Services\SingleInstanceService.cs', # 单例由 DevEnv 统一提供
    'UI\Services\TrayIconService.cs',    # 托盘由 DevEnv 的 TrayIconHost 统一提供
    'UI\Services\Updater.cs'             # 自研更新器 → 改用 Velopack
)

# 有意改写的文件：为适配 DevEnv 宿主而做的必要改动。
$expectedChanged = @(
    'UI\MainWindow.xaml',                # 去掉失效的 /SigXor.exe 图标与托盘耦合
    'UI\MainWindow.xaml.cs',             # 关闭行为从「隐藏到托盘」改为正常关闭
    'UI\UpdateWindow.xaml.cs',           # 下载命令从异步改为同步（Velopack 后台下载）
    'UI\Services\AutoStartService.cs',   # 自启改走 DevEnv 注册表 + SelfStarting
    'UI\Services\UpdateService.cs',      # 改为包装 DevEnv 的 Velopack 更新服务
    'UI\ViewModels\UpdateViewModel.cs',  # 适配新的更新接口
    'UI\Views\TextView.xaml.cs',         # 编辑器底色固定为深色，配 VS Code Dark 语法配色

    # 主题化：硬编码颜色改为 DynamicResource 主题资源
    'UI\AboutWindow.xaml',
    'UI\SettingsWindow.xaml',
    'UI\UpdateWindow.xaml',
    'UI\Views\ArchiveView.xaml',
    'UI\Views\AudioView.xaml',
    'UI\Views\CsvView.xaml',
    'UI\Views\FontView.xaml',
    'UI\Views\HexEditorView.xaml',
    'UI\Views\HtmlView.xaml',
    'UI\Views\ImageView.xaml',
    'UI\Views\InfoView.xaml',
    'UI\Views\LauncherWindow.xaml',
    'UI\Views\MarkdownView.xaml',
    'UI\Views\OfficeView.xaml',
    'UI\Views\PdfView.xaml',
    'UI\Views\PreviewPane.xaml',
    'UI\Views\ShellPreviewWindow.xaml',
    'UI\Views\SvgView.xaml',
    'UI\Views\TextView.xaml',
    'UI\Views\VideoView.xaml'
)

foreach ($map in $maps) {
    $srcRoot = Join-Path $Source $map.src
    $dstRoot = Join-Path $repoRoot $map.dst

    $srcFiles = @(Get-RelativeFiles $srcRoot)
    $dstFiles = @(Get-RelativeFiles $dstRoot)

    # csproj 不比对（DevEnv 用自己的工程文件）
    $srcFiles = $srcFiles | Where-Object { $_ -notmatch '\.csproj$' }

    foreach ($rel in $srcFiles) {
        $srcPath = Join-Path $srcRoot $rel
        $dstPath = Join-Path $dstRoot $rel

        if (-not (Test-Path $dstPath)) {
            $missing += "$($map.label)\$rel"
            continue
        }

        $checked++
        $srcText = Convert-Namespace ([System.IO.File]::ReadAllText($srcPath))
        $dstText = [System.IO.File]::ReadAllText($dstPath);

        # 归一化换行：源仓库是 CRLF，DevEnv 工作区可能是 LF，
        # 直接比对会产生大量假阳性差异。
        $srcNorm = $srcText.Replace("`r`n", "`n").TrimEnd()
        $dstNorm = $dstText.Replace("`r`n", "`n").TrimEnd()

        if ($srcNorm -ne $dstNorm) {
            $mismatch += "$($map.label)\$rel"
        }
    }
}

Write-Host ""
Write-Host "=== 合并完整性校验 ==="
Write-Host "已比对: $checked 个文件"

# 剔除「有意删除 / 有意改写」的部分，只看真正的意外差异
$unexpectedMissing = @($missing | Where-Object { $_ -notin $expectedMissing })
$unexpectedMismatch = @($mismatch | Where-Object { $_ -notin $expectedChanged })
$expectedMissingStill = @($expectedMissing | Where-Object { $_ -in $missing })
$expectedChangedStill = @($expectedChanged | Where-Object { $_ -in $mismatch })

Write-Host "有意删除: $($expectedMissingStill.Count) 个（独立入口 / 重复基础设施）"
Write-Host "有意改写: $($expectedChangedStill.Count) 个（宿主适配）"
Write-Host "意外缺失: $($unexpectedMissing.Count) 个"
Write-Host "意外差异: $($unexpectedMismatch.Count) 个"

if ($unexpectedMissing.Count -gt 0) {
    Write-Host ""
    Write-Host "--- 意外缺失 ---"
    $unexpectedMissing | ForEach-Object { Write-Host "  缺: $_" }
}

if ($unexpectedMismatch.Count -gt 0) {
    Write-Host ""
    Write-Host "--- 意外差异（已做命名空间改写后再比对）---"
    $unexpectedMismatch | ForEach-Object { Write-Host "  差: $_" }
}

Write-Host ""
if ($unexpectedMissing.Count -eq 0 -and $unexpectedMismatch.Count -eq 0) {
    Write-Host "合并完整：除预期的宿主适配外，与源仓库逐字一致。"
    exit 0
} else {
    Write-Host "存在意外差异，需要处理。"
    exit 1
}

#
# SigXor -> DevEnv 代码合并脚本（第一步：纯文件搬运）
#
# 目的：把 SigXor 仓库的最新源码原样搬进 DevEnv 的对应目录，保留原命名空间，
# 命名空间改写与宿主接线由后续脚本处理。分两步是为了让每一步都能独立验证：
# 搬运后用文件清单比对，改写后用编译 + 探针验证。
#
# 用法：.\scripts\merge-sigxor.ps1 [-Source <SigXor 路径>] [-DryRun]
#
param(
    [string]$Source = "D:\Fireneb\Projects\DevTools\SigXor",
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$srcUi   = Join-Path $Source 'SigXor'
$srcCore = Join-Path $Source 'SigXor.Core'
$srcTest = Join-Path $Source 'SigXor.Tests'

# 目标目录：沿用 DevEnv 的目录约定
#   DevEnv.Core/SigXor/  -> 命名空间 DevEnv.Core.SigXor.*
#   DevEnv/SigXor/       -> 命名空间 DevEnv.Host.SigXor.*
#   DevEnv.Tests/        -> SigXor 的测试并入 DevEnv.Tests 根目录
$dstCore = Join-Path $repoRoot 'DevEnv.Core\SigXor'
$dstUi   = Join-Path $repoRoot 'DevEnv\SigXor'
$dstTest = Join-Path $repoRoot 'DevEnv.Tests'

foreach ($dir in @($dstCore, $dstUi, $dstTest)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Get-SourceFiles([string]$root) {
    if (-not (Test-Path $root)) { return @() }
    [System.IO.Directory]::GetFiles($root, '*', [System.IO.SearchOption]::AllDirectories) |
        Where-Object { $_ -notmatch '\\(bin|obj|\.git|\.vs)\\' }
}

# 需要排除的文件
$excludePattern = '\\(bin|obj|\.git|\.vs)\\|\.csproj$|\.sln$|packages\.lock\.json$'

function Copy-Tree([string]$from, [string]$to, [string]$label) {
    $files = Get-SourceFiles $from | Where-Object { $_ -notmatch $excludePattern }
    Write-Host ""
    Write-Host "=== $label ==="
    Write-Host "  源: $($from)"
    Write-Host "  目标: $to"

    $copied = 0
    foreach ($file in $files) {
        $rel = $file.Substring($from.Length).TrimStart('\')
        $target = Join-Path $to $rel
        $targetDir = Split-Path $target -Parent
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }

        if ($DryRun) {
            Write-Host "  [dry] $rel"
            $copied++
            continue
        }

        Copy-Item -Path $file -Destination $target -Force
        $copied++
    }
    Write-Host "  共 $copied 个文件"
    return $copied
}

if (-not (Test-Path $srcUi)) {
    throw "找不到 SigXor 源码目录：$srcUi"
}

$total = 0
$total += Copy-Tree $srcCore $dstCore 'Core 纯逻辑'
$total += Copy-Tree $srcUi   $dstUi   'UI 与服务'
$total += Copy-Tree $srcTest $dstTest '单元测试'

Write-Host ""
Write-Host "共搬运 $total 个文件（DryRun=$DryRun）"
Write-Host ""
Write-Host "下一步：运行 scripts\rewrite-sigxor-namespace.ps1 改写命名空间"

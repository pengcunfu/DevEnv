#
# SigXor -> DevEnv 命名空间改写
#
# 映射：
#   SigXor.Core.*   -> DevEnv.Core.SigXor.*
#   SigXor.Tests.*  -> DevEnv.Tests.SigXor.*
#   SigXor.*        -> DevEnv.Host.SigXor.*
#
# 需要连带改写的地方：
#   - C# 的 namespace 声明与 using
#   - XAML 的 xmlns:xxx="clr-namespace:SigXor..."
#   - XAML 的 x:Class="SigXor..."
#
# 注意：不能全局无脑替换字符串字面量（例如界面文案里的 "SigXor"），
# 因此按上下文匹配：namespace 声明、using 指令、clr-namespace:、x:Class=。
#
# 用法：.\scripts\rewrite-sigxor-namespace.ps1 [-DryRun]
#
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

$targets = @(
    (Join-Path $repoRoot 'DevEnv\SigXor'),
    (Join-Path $repoRoot 'DevEnv.Core\SigXor')
)

# 需要改写的文本文件
$textExtensions = @('.cs', '.xaml')

# 单条替换规则：正则 -> 替换文本
$rules = @(
    # C#: namespace 声明（行首或空白后的 namespace 关键字）
    # 注意顺序：Core / Tests 必须排在通用的 SigXor.X 规则之前，
    # 否则 `namespace SigXor.Core;`（裸名无点号结尾）会被误改成 Host.SigXor.Core。
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Core(\s*;)';  replacement = '${1}DevEnv.Core.SigXor${2}' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Core\.';      replacement = '${1}DevEnv.Core.SigXor.' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Tests(\s*;)'; replacement = '${1}DevEnv.Tests.SigXor${2}' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.Tests\.';     replacement = '${1}DevEnv.Tests.SigXor.' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor\.';             replacement = '${1}DevEnv.Host.SigXor.' }
    @{ pattern = '(?m)^(\s*namespace\s+)SigXor(\s*;)';        replacement = '${1}DevEnv.Host.SigXor${2}' }

    # C#: using 指令
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Core(\s*;)';      replacement = '${1}DevEnv.Core.SigXor${2}' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Core\.';          replacement = '${1}DevEnv.Core.SigXor.' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Tests(\s*;)';     replacement = '${1}DevEnv.Tests.SigXor${2}' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.Tests\.';         replacement = '${1}DevEnv.Tests.SigXor.' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor\.';                replacement = '${1}DevEnv.Host.SigXor.' }
    @{ pattern = '(?m)^(\s*using\s+)SigXor(\s*;)';           replacement = '${1}DevEnv.Host.SigXor${2}' }
    @{ pattern = '(?m)^(\s*using\s+static\s+)SigXor\.';      replacement = '${1}DevEnv.Host.SigXor.' }

    # XAML: xmlns 声明
    @{ pattern = 'clr-namespace:SigXor\.Core(\.|\")';        replacement = 'clr-namespace:DevEnv.Core.SigXor$1' }
    @{ pattern = 'clr-namespace:SigXor\.Tests(\.|\")';       replacement = 'clr-namespace:DevEnv.Tests.SigXor$1' }
    @{ pattern = 'clr-namespace:SigXor\.';                   replacement = 'clr-namespace:DevEnv.Host.SigXor.' }
    @{ pattern = 'clr-namespace:SigXor(?=")';                 replacement = 'clr-namespace:DevEnv.Host.SigXor' }

    # XAML: x:Class
    @{ pattern = 'x:Class="SigXor\.Core\.';                  replacement = 'x:Class="DevEnv.Core.SigXor.' }
    @{ pattern = 'x:Class="SigXor\.Core"';                   replacement = 'x:Class="DevEnv.Core.SigXor"' }
    @{ pattern = 'x:Class="SigXor\.';                        replacement = 'x:Class="DevEnv.Host.SigXor.' }
    @{ pattern = 'x:Class="SigXor"';                          replacement = 'x:Class="DevEnv.Host.SigXor"' }
)

# 测试文件单独处理（位于 DevEnv.Tests 根目录）
$testTargets = @(Get-ChildItem (Join-Path $repoRoot 'DevEnv.Tests') -File -Filter '*Tests.cs' -ErrorAction SilentlyContinue)

$changed = 0
$total = 0

function Apply-Rules([string]$text) {
    foreach ($rule in $rules) {
        $text = [regex]::Replace($text, $rule.pattern, $rule.replacement)
    }
    return $text
}

function Process-Files($files) {
    $script:changed = 0
    $script:total = 0
    foreach ($file in $files) {
        if ($file.Extension -notin $textExtensions) { continue }
        $script:total++
        $text = [System.IO.File]::ReadAllText($file.FullName)
        $new = Apply-Rules $text
        if ($new -ne $text) {
            if (-not $DryRun) {
                $utf8 = New-Object System.Text.UTF8Encoding($false)
                [System.IO.File]::WriteAllText($file.FullName, $new, $utf8)
            }
            $script:changed++
        }
    }
}

$allFiles = @()
foreach ($dir in $targets) {
    if (Test-Path $dir) {
        $allFiles += Get-ChildItem $dir -Recurse -File
    }
}
$allFiles += $testTargets

Process-Files $allFiles

Write-Host ""
Write-Host "=== 命名空间改写 ==="
Write-Host "扫描 $($total) 个文件，修改 $changed 个（DryRun=$DryRun）"

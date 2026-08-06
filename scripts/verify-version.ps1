# 本地发版前校验：VERSION、DevBox/DevBox.csproj、CHANGELOG、可选 Git 标签
# 用法: .\scripts\verify-version.ps1
#       .\scripts\verify-version.ps1 -Tag v1.0.0

param(
    [string]$Tag = ''
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Fail($msg) { Write-Error $msg; exit 1 }

$versionPath = Join-Path $root 'VERSION'
$csprojPath = Join-Path $root 'DevBox\DevBox.csproj'
$changelogPath = Join-Path $root 'CHANGELOG.md'

if (-not (Test-Path $versionPath)) { Fail 'Missing VERSION' }
$semver = (Get-Content $versionPath -Raw).Trim()

$csproj = Get-Content $csprojPath -Raw
if ($csproj -notmatch '<Version>\s*([^\s<]+)\s*</Version>') { Fail 'Missing <Version> in csproj' }
if ($Matches[1] -ne $semver) { Fail "csproj Version ($($Matches[1])) != VERSION ($semver)" }

$changelog = Get-Content $changelogPath -Raw
if ($changelog -notmatch "## \[$([regex]::Escape($semver))\]") {
    Fail "CHANGELOG missing ## [$semver]"
}

if ($Tag) {
    if ($Tag -notmatch '^v') { $Tag = "v$Tag" }
    $tagSemver = $Tag.Substring(1)
    if ($tagSemver -ne $semver) { Fail "Tag $Tag ($tagSemver) != VERSION ($semver)" }
}

Write-Host "OK: version $semver is consistent (VERSION, csproj, CHANGELOG$(if ($Tag) { ", tag $Tag" }))."

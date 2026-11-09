# Build the portable release: slim single-file app zip + component zips + latest.json.
# Usage:
#   .\scripts\package-release.ps1
#   .\scripts\package-release.ps1 -OutputDir .\artifacts\release
# Output layout:
#   <OutputDir>\app\DevEnv-<ver>-win-x64\  (staging)
#   <OutputDir>\DevEnv-<ver>-win-x64.zip
#   <OutputDir>\DevEnv-<ver>-win-x64.zip.sha256
#   <OutputDir>\DevEnv-component-<office|ocr|voice>-<ver>.zip
#   <OutputDir>\components.json
#   <OutputDir>\latest.json

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$OutputDir = '',

    [string]$Repository = 'pengcunfu/DevEnv'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'DevEnv\DevEnv.csproj'
$versionPath = Join-Path $root 'VERSION'

if (-not (Test-Path $versionPath)) {
    Write-Error "VERSION file missing: $versionPath"
    exit 1
}
$semver = (Get-Content $versionPath -Raw).Trim()

if (-not $OutputDir) {
    $OutputDir = Join-Path $root 'publish\release'
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$appPublish = Join-Path $OutputDir 'app-publish'
$fullPublish = Join-Path $OutputDir 'full'

Write-Host "== 1/4 发布瘦身单文件 =="
& (Join-Path $PSScriptRoot 'publish.ps1') -Configuration $Configuration -Runtime $Runtime -OutputDir $appPublish
if ($LASTEXITCODE -ne 0) { throw "publish.ps1 failed" }

Write-Host "== 2/4 完整发布（组件源） =="
& dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --no-self-contained `
    -p:PublishSingleFile=false `
    -p:DevEnvIncludeOptionalComponents=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$semver `
    -p:InformationalVersion=$semver `
    -o $fullPublish `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (full) failed" }

Get-ChildItem -Path $fullPublish -Filter '*.pdb' -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force
Get-ChildItem -Path $fullPublish -Filter '*.xml' -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

Write-Host "== 3/4 打包组件 =="
& (Join-Path $PSScriptRoot 'package-components.ps1') `
    -FullPublishDir $fullPublish `
    -Version $semver `
    -OutputDir $OutputDir `
    -Repository $Repository
if ($LASTEXITCODE -ne 0) { throw "package-components.ps1 failed" }

Write-Host "== 4/4 打包便携程序 =="
$appStage = Join-Path $OutputDir "app\DevEnv-$semver-$Runtime"
New-Item -ItemType Directory -Force -Path $appStage | Out-Null
Copy-Item -LiteralPath (Join-Path $appPublish 'DevEnv.exe') -Destination $appStage -Force
foreach ($item in @('Resources', 'LICENSE', 'NOTICE')) {
    $source = Join-Path $appPublish $item
    if (Test-Path $source) {
        Copy-Item -LiteralPath $source -Destination $appStage -Recurse -Force
    }
}

$appZip = Join-Path $OutputDir "DevEnv-$semver-$Runtime.zip"
Compress-Archive -Path (Join-Path $appStage '*') -DestinationPath $appZip -Force
$appHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $appZip).Hash.ToLower()
$appSize = (Get-Item -LiteralPath $appZip).Length
"$appHash  $(Split-Path -Leaf $appZip)" | Set-Content -LiteralPath "$appZip.sha256" -Encoding ASCII

$latest = [ordered]@{
    version = $semver
    url     = "https://github.com/$Repository/releases/download/v$semver/$(Split-Path -Leaf $appZip)"
    sha256  = $appHash
    size    = $appSize
}
$latestJson = $latest | ConvertTo-Json
[System.IO.File]::WriteAllText(
    (Join-Path $OutputDir 'latest.json'),
    $latestJson,
    (New-Object System.Text.UTF8Encoding($false)))

Write-Host ""
Write-Host "OK: release -> $OutputDir"
Get-ChildItem -Path $OutputDir -File |
    Select-Object Name, @{N = 'SizeMB'; E = { [math]::Round($_.Length / 1MB, 2) } } |
    Format-Table -AutoSize

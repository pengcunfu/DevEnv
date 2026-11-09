# Package optional components (Office / OCR / Voice) from a full publish directory.
# Usage:
#   .\scripts\package-components.ps1 -FullPublishDir .\artifacts\full -Version 1.3.0 -OutputDir .\artifacts\components
# The full publish must be produced with -p:DevEnvIncludeOptionalComponents=true and
# -p:PublishSingleFile=false so the optional managed/native files are present.

param(
    [Parameter(Mandatory = $true)]
    [string]$FullPublishDir,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$OutputDir = '',
    [string]$Repository = 'pengcunfu/DevEnv',
    [string]$MirrorBase = $env:COMPONENT_MIRROR_BASE
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not $OutputDir) {
    $OutputDir = Join-Path $root 'publish\components'
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

$source = (Resolve-Path $FullPublishDir).Path
if (-not (Test-Path (Join-Path $source 'DevEnv.exe'))) {
    Write-Error "DevEnv.exe not found in full publish directory: $source"
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# 只清理本脚本生成的产物；输出目录可能同时存放 app-publish / full 暂存目录。
$stagingRoot = Join-Path $OutputDir 'staging'
if (Test-Path $stagingRoot) { Remove-Item -Path $stagingRoot -Recurse -Force }
Get-ChildItem -Path $OutputDir -Filter 'DevEnv-component-*.zip' -File -ErrorAction SilentlyContinue |
    Remove-Item -Force
$oldManifest = Join-Path $OutputDir 'components.json'
if (Test-Path $oldManifest) { Remove-Item -Path $oldManifest -Force }

$ortFiles = @(
    'Microsoft.ML.OnnxRuntime.dll',
    'onnxruntime.dll',
    'onnxruntime_providers_shared.dll'
)

$ocrFiles = @(
    'RapidOcrNet.dll',
    'SkiaSharp.dll',
    'libSkiaSharp.dll',
    'Clipper2Lib.dll',
    'System.Numerics.Tensors.dll'
) + $ortFiles
$voiceFiles = @('sherpa-onnx.dll', 'sherpa-onnx-c-api.dll', 'System.Numerics.Tensors.dll') + $ortFiles

$components = @(
    @{
        Id = 'office'
        Name = 'Office 预览'
        Files = @('DocumentFormat.OpenXml.dll', 'DocumentFormat.OpenXml.Framework.dll')
        Models = @()
    },
    @{
        Id = 'ocr'
        Name = 'OCR 引擎'
        Files = $ocrFiles
        Models = @('models\v5\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx')
    },
    @{
        Id = 'voice'
        Name = '语音引擎'
        Files = $voiceFiles
        Models = @()
    }
)

function Copy-ComponentFile {
    param([string]$RelativePath, [string]$DestinationRoot)
    $sourcePath = Join-Path $source $RelativePath
    if (-not (Test-Path $sourcePath)) {
        throw "组件文件缺失: $RelativePath"
    }
    $target = Join-Path $DestinationRoot $RelativePath
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }
    Copy-Item -LiteralPath $sourcePath -Destination $target -Force
}

$manifestComponents = @()
foreach ($component in $components) {
    $id = $component.Id
    $stage = Join-Path $OutputDir "staging\$id"
    New-Item -ItemType Directory -Force -Path $stage | Out-Null

    foreach ($file in $component.Files) {
        Copy-ComponentFile -RelativePath $file -DestinationRoot $stage
    }
    foreach ($model in $component.Models) {
        Copy-ComponentFile -RelativePath $model -DestinationRoot $stage
    }

    $assetName = "DevEnv-component-$id-$Version.zip"
    $zipPath = Join-Path $OutputDir $assetName
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -Force

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLower()
    $size = (Get-Item -LiteralPath $zipPath).Length
    $urls = @("https://github.com/$Repository/releases/download/v$Version/$assetName")
    if ($MirrorBase) {
        $urls += ($MirrorBase.TrimEnd('/') + "/$assetName")
    }

    $manifestComponents += [ordered]@{
        id    = $id
        name  = $component.Name
        version = $Version
        size  = $size
        sha256 = $hash
        urls  = $urls
    }
}

# OCR 与语音必须使用同一份 onnxruntime；从同一份完整发布复制后哈希应完全一致。
foreach ($file in $ortFiles) {
    $ocrCopy = Join-Path $OutputDir "staging\ocr\$file"
    $voiceCopy = Join-Path $OutputDir "staging\voice\$file"
    $ocrHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $ocrCopy).Hash
    $voiceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $voiceCopy).Hash
    if ($ocrHash -ne $voiceHash) {
        throw "onnxruntime 文件哈希不一致: $file"
    }
}

$manifest = [ordered]@{
    version = 1
    appVersion = $Version
    components = $manifestComponents
}
$manifestPath = Join-Path $OutputDir 'components.json'
$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "OK: components -> $OutputDir"
Get-ChildItem -Path $OutputDir -Filter '*.zip' -File |
    Select-Object Name, @{N = 'SizeMB'; E = { [math]::Round($_.Length / 1MB, 2) } } |
    Format-Table -AutoSize
Write-Host "OK: manifest -> $manifestPath"

# Framework-dependent publish (does not bundle .NET runtime). Target needs matching Desktop Runtime.
# Usage: .\scripts\publish.ps1
#        .\scripts\publish.ps1 -Zip
#        .\scripts\publish.ps1 -Runtime win-x64 -Configuration Release

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$OutputDir = '',

    [switch]$Zip,

    [switch]$NoSingleFile,

    [switch]$SkipUi
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'DevBox.Host\DevBox.Host.csproj'
$versionPath = Join-Path $root 'VERSION'
$uiDir = Join-Path $root 'ui'

if (-not (Test-Path $project)) {
    Write-Error "Project not found: $project"
    exit 1
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $root "publish\$Runtime"
}

$semver = '0.0.0'
if (Test-Path $versionPath) {
    $semver = (Get-Content $versionPath -Raw).Trim()
}

if (Test-Path $OutputDir) {
    try {
        Remove-Item -Path $OutputDir -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Error @"
Unable to clean output directory (files may be in use): $OutputDir
Close any running DevBox.exe, then retry.
$($_.Exception.Message)
"@
        exit 1
    }
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Build React UI into wwwroot next to the published exe (API serves static files from there).
if (-not $SkipUi) {
    if (-not (Test-Path (Join-Path $uiDir 'package.json'))) {
        Write-Error "UI project not found: $uiDir"
        exit 1
    }
    Push-Location $uiDir
    try {
        if (-not (Test-Path 'node_modules')) {
            Write-Host 'ui deps missing - running npm install'
            npm install
            if ($LASTEXITCODE -ne 0) { throw "npm install failed with exit code $LASTEXITCODE" }
        }
        Write-Host 'Building UI (npm run build)...'
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit code $LASTEXITCODE" }
    }
    finally {
        Pop-Location
    }
}

$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', 'false',
    '-o', $OutputDir,
    "-p:Version=$semver",
    "-p:InformationalVersion=$semver"
)

if (-not $NoSingleFile) {
    $publishArgs += @(
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true'
    )
}

Write-Host "Publishing DevBox $semver ($Configuration, $Runtime, framework-dependent)..."
Write-Host "dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$exe = Join-Path $OutputDir 'DevBox.exe'
if (-not (Test-Path $exe)) {
    Write-Error "DevBox.exe not found at $exe"
    exit 1
}

# Copy Vite dist -> wwwroot beside the exe (single-file publish keeps loose content next to exe).
if (-not $SkipUi) {
    $dist = Join-Path $uiDir 'dist'
    $wwwroot = Join-Path $OutputDir 'wwwroot'
    if (-not (Test-Path (Join-Path $dist 'index.html'))) {
        Write-Error "UI build missing index.html at $dist"
        exit 1
    }
    if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
    Copy-Item -Path $dist -Destination $wwwroot -Recurse
    Write-Host "OK: wwwroot copied from ui/dist"
}

# Strip PDBs to shrink the distribution package
Get-ChildItem -Path $OutputDir -Filter '*.pdb' -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "OK: $exe ($sizeMb MB)"
Write-Host 'Note: target machine must have the matching .NET Desktop Runtime installed.'

if ($Zip) {
    $zipName = "DevBox-$semver-$Runtime.zip"
    $zipPath = Join-Path $root $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $OutputDir '*') -DestinationPath $zipPath
    Write-Host "OK: $zipPath"
}

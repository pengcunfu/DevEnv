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

    [switch]$NoSingleFile
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'DevEnv\DevEnv.csproj'
$versionPath = Join-Path $root 'VERSION'

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
Close any running DevEnv.exe, then retry.
$($_.Exception.Message)
"@
        exit 1
    }
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

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

Write-Host "Publishing DevEnv $semver ($Configuration, $Runtime, framework-dependent)..."
Write-Host "dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$exe = Join-Path $OutputDir 'DevEnv.exe'
if (-not (Test-Path $exe)) {
    Write-Error "DevEnv.exe not found at $exe"
    exit 1
}

# Strip PDBs to shrink the distribution package
Get-ChildItem -Path $OutputDir -Filter '*.pdb' -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "OK: $exe ($sizeMb MB)"
Write-Host 'Note: target machine must have the matching .NET Desktop Runtime installed.'

if ($Zip) {
    $zipName = "DevEnv-$semver-$Runtime.zip"
    $zipPath = Join-Path $root $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $OutputDir '*') -DestinationPath $zipPath
    Write-Host "OK: $zipPath"
}

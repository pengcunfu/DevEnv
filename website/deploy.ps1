# ============================================================
# Cloudflare Pages deploy (DevEnv site)
#
# Usage:
#   .\deploy.ps1
#   .\deploy.ps1 -SkipBuild
#   .\deploy.ps1 -ProjectName xxx -Branch main
#
# Auth: wrangler login (browser) or CLOUDFLARE_API_TOKEN (+ CLOUDFLARE_ACCOUNT_ID)
# ============================================================

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [string]$ProjectName = "devbox-docs",
    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked([string]$Message, [scriptblock]$Action) {
    Write-Step $Message
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Failed: $Message"
    }
}

Set-Location -LiteralPath $PSScriptRoot

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm not found. Install Node.js from https://nodejs.org"
}

if (-not (Test-Path -LiteralPath "node_modules")) {
    Invoke-Checked "npm install" { npm install }
}
else {
    Write-Host "node_modules exists, skip npm install" -ForegroundColor DarkGray
}

if ($SkipBuild) {
    Write-Host "-SkipBuild: skip npm run build" -ForegroundColor DarkGray
}
else {
    Invoke-Checked "npm run build" { npm run build }
}

if (-not (Test-Path -LiteralPath "dist\index.html")) {
    throw "dist\index.html missing. Run npm run build first."
}

Write-Step "wrangler whoami"
$whoamiOutput = (& npx wrangler whoami 2>&1 | Out-String)
$whoamiExit = $LASTEXITCODE
if ($whoamiExit -ne 0 -or $whoamiOutput -match "(?i)not logged in|not authenticated|no token found") {
    Write-Host "Not logged in. Opening browser for wrangler login..." -ForegroundColor Yellow
    Write-Step "wrangler login"
    & npx wrangler login
    if ($LASTEXITCODE -ne 0) {
        Write-Host "wrangler login failed" -ForegroundColor Yellow
        $whoamiExit = 1
    }
    else {
        $whoamiOutput = (& npx wrangler whoami 2>&1 | Out-String)
        $whoamiExit = $LASTEXITCODE
    }

    if ($whoamiExit -ne 0 -or $whoamiOutput -match "(?i)not logged in|not authenticated|no token found") {
        Write-Host ""
        Write-Host "Login incomplete. Use API Token instead:" -ForegroundColor Yellow
        Write-Host "  1) Create API Token with Cloudflare Pages:Edit"
        Write-Host "  2) Set CLOUDFLARE_API_TOKEN (and CLOUDFLARE_ACCOUNT_ID)"
        Write-Host "  3) Re-run this script"
        exit 1
    }
}
Write-Host $whoamiOutput.Trim()

Write-Step "Check Pages project $ProjectName"
$projectList = (& npx wrangler pages project list --json 2>&1 | Out-String)
$projectExists = $false
try {
    $projects = $projectList | ConvertFrom-Json
    $projectExists = @($projects | Where-Object { $_.'Project Name' -eq $ProjectName }).Count -gt 0
}
catch {
    $projectExists = $projectList -match [regex]::Escape($ProjectName)
}
if ($projectExists) {
    Write-Host "Project $ProjectName exists" -ForegroundColor Green
}
else {
    Write-Host "Creating project $ProjectName ..." -ForegroundColor Yellow
    Invoke-Checked "pages project create $ProjectName" {
        npx wrangler pages project create $ProjectName --production-branch $Branch
    }
}

Invoke-Checked "pages deploy (branch: $Branch)" {
    npx wrangler pages deploy dist --project-name $ProjectName --branch $Branch
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host "URL: https://devenv.cn"
Write-Host "Fallback: https://$ProjectName.pages.dev"
Write-Host "Custom domain: Cloudflare Dashboard -> Workers & Pages -> $ProjectName -> Custom domains" -ForegroundColor DarkGray
# ============================================================
# Cloudflare Pages 部署脚本
#
# 用法:
#   .\deploy.ps1               # 安装依赖 + 构建 + 部署
#   .\deploy.ps1 -SkipBuild    # 跳过构建，直接部署 dist/
#   .\deploy.ps1 -ProjectName xxx -Branch main
#
# 前提:
#   未登录时脚本会自动打开浏览器完成登录（wrangler login）；
#   也可自行设置环境变量 CLOUDFLARE_API_TOKEN（以及 CLOUDFLARE_ACCOUNT_ID）
# ============================================================

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [string]$ProjectName = "devenv",
    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
# PowerShell 7 默认会把原生命令的 stderr 当作错误，这里关闭以免干扰输出捕获
$PSNativeCommandUseErrorActionPreference = $false
# 统一以 UTF-8 解码/输出原生命令文本，避免 wrangler 的中文与 emoji 在 GBK 控制台下乱码
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
        throw "步骤失败: $Message"
    }
}

# 始终在仓库根目录执行
Set-Location -LiteralPath $PSScriptRoot

# 0. 环境检查
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "未找到 npm，请先安装 Node.js (https://nodejs.org)"
}

# 1. 安装依赖
if (-not (Test-Path -LiteralPath "node_modules")) {
    Invoke-Checked "安装依赖 npm install" { npm install }
}
else {
    Write-Host "node_modules 已存在，跳过安装（如需更新依赖请手动运行 npm install）" -ForegroundColor DarkGray
}

# 2. 构建
if ($SkipBuild) {
    Write-Host "已指定 -SkipBuild，跳过 npm run build" -ForegroundColor DarkGray
}
else {
    Invoke-Checked "构建站点 npm run build" { npm run build }
}

if (-not (Test-Path -LiteralPath "dist\index.html")) {
    throw "构建产物 dist\index.html 不存在，请先执行 npm run build"
}

# 3. 登录检查
Write-Step "检查 Cloudflare 登录状态 (wrangler whoami)"
$whoamiOutput = (& npx wrangler whoami 2>&1 | Out-String)
$whoamiExit = $LASTEXITCODE
if ($whoamiExit -ne 0 -or $whoamiOutput -match "(?i)not logged in|not authenticated|no token found") {
    Write-Host "尚未登录 Cloudflare，正在自动打开浏览器完成登录..." -ForegroundColor Yellow
    Write-Step "打开浏览器完成 Cloudflare 登录 (wrangler login)"
    & npx wrangler login
    if ($LASTEXITCODE -ne 0) {
        Write-Host "wrangler login 未成功完成" -ForegroundColor Yellow
        $whoamiExit = 1
    }
    else {
        $whoamiOutput = (& npx wrangler whoami 2>&1 | Out-String)
        $whoamiExit = $LASTEXITCODE
    }

    if ($whoamiExit -ne 0 -or $whoamiOutput -match "(?i)not logged in|not authenticated|no token found") {
        Write-Host ""
        Write-Host "登录未完成，也可以改用 API Token 方式：" -ForegroundColor Yellow
        Write-Host "  1) 在 Cloudflare 控制台创建 API Token（权限需包含 Cloudflare Pages: Edit）"
        Write-Host "  2) 设置环境变量 CLOUDFLARE_API_TOKEN（建议同时设置 CLOUDFLARE_ACCOUNT_ID）"
        Write-Host "  3) 重新运行本脚本"
        exit 1
    }
}
Write-Host $whoamiOutput.Trim()

# 4. 确保 Pages 项目存在
Write-Step "检查 Pages 项目 $ProjectName 是否存在"
$projectList = (& npx wrangler pages project list --json 2>&1 | Out-String)
$projectExists = $false
try {
    $projects = $projectList | ConvertFrom-Json
    $projectExists = @($projects | Where-Object { $_.'Project Name' -eq $ProjectName }).Count -gt 0
}
catch {
    # JSON 解析失败时回退到文本匹配（表格输出中包含项目名）
    $projectExists = $projectList -match [regex]::Escape($ProjectName)
}
if ($projectExists) {
    Write-Host "项目 $ProjectName 已存在，直接部署" -ForegroundColor Green
}
else {
    Write-Host "项目 $ProjectName 不存在，正在创建..." -ForegroundColor Yellow
    Invoke-Checked "创建 Pages 项目 $ProjectName" {
        npx wrangler pages project create $ProjectName --production-branch $Branch
    }
}

# 5. 部署
Invoke-Checked "部署到 Cloudflare Pages (分支: $Branch)" {
    npx wrangler pages deploy dist --project-name $ProjectName --branch $Branch
}

Write-Host ""
Write-Host "部署完成!" -ForegroundColor Green
Write-Host "访问 https://$ProjectName.pages.dev 查看效果"
Write-Host "绑定自定义域名：Cloudflare 控制台 -> Workers 和 Pages -> $ProjectName -> 自定义域" -ForegroundColor DarkGray

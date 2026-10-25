# 生成并应用应用图标（保留旧入口，实际逻辑在 generate-logo.ps1）。
# 输出 assets\brand\，并同步：
#   DevEnv\Resources\icon.ico、DevEnv\Resources\icon.png
#   website\public\logo.svg
# 用法: .\scripts\generate-app-icon.ps1

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'generate-logo.ps1') -Apply

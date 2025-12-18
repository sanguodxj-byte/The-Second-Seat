#!/usr/bin/env pwsh
# TSS v2.0.2 推送验证脚本
# 推送后验证远程仓库状态

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  TSS v2.0.2 - 推送验证脚本                                ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# 1. 检查远程仓库连接
Write-Host "?? [1/5] 检查远程仓库连接..." -ForegroundColor Yellow
$remoteUrl = git config --get remote.origin.url

if ($remoteUrl) {
    Write-Host "? 远程仓库: $remoteUrl" -ForegroundColor Green
} else {
    Write-Host "? 未找到远程仓库配置" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 2. 获取远程最新提交
Write-Host "?? [2/5] 获取远程最新提交信息..." -ForegroundColor Yellow
git fetch origin main

Write-Host ""

# 3. 比较本地和远程分支
Write-Host "?? [3/5] 比较本地和远程分支..." -ForegroundColor Yellow

$localCommit = git rev-parse HEAD
$remoteCommit = git rev-parse origin/main

Write-Host "本地提交: $localCommit" -ForegroundColor Gray
Write-Host "远程提交: $remoteCommit" -ForegroundColor Gray

if ($localCommit -eq $remoteCommit) {
    Write-Host "? 本地和远程分支一致" -ForegroundColor Green
} else {
    Write-Host "??  本地和远程分支不一致" -ForegroundColor Yellow
    Write-Host "可能原因：" -ForegroundColor Yellow
    Write-Host "  1. 推送尚未完成" -ForegroundColor Yellow
    Write-Host "  2. 远程仓库有新的提交" -ForegroundColor Yellow
}

Write-Host ""

# 4. 查看远程最新提交详情
Write-Host "?? [4/5] 查看远程最新提交详情..." -ForegroundColor Yellow
Write-Host ""

git log origin/main -1 --pretty=format:"%C(yellow)Commit: %H%n%C(cyan)Author: %an <%ae>%n%C(green)Date:   %ad%n%C(white)%n%s%n%b" --date=format:"%Y-%m-%d %H:%M:%S"

Write-Host ""
Write-Host ""

# 5. 验证关键文件
Write-Host "?? [5/5] 验证关键文件..." -ForegroundColor Yellow
Write-Host ""

$keyFiles = @(
    "Source/TheSecondSeat/TTS/TTSService.cs",
    "TSS实体功能与TTS升级-完成报告-v2.0.2.md",
    "TSS实体功能与TTS-快速参考-v2.0.2.md",
    "Git推送报告-v2.0.2.md"
)

$allFilesExist = $true

foreach ($file in $keyFiles) {
    if (Test-Path $file) {
        Write-Host "? $file" -ForegroundColor Green
    } else {
        Write-Host "? $file (未找到)" -ForegroundColor Red
        $allFilesExist = $false
    }
}

Write-Host ""

# 总结
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                  验证结果总结                              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if ($localCommit -eq $remoteCommit -and $allFilesExist) {
    Write-Host "? 推送验证成功！" -ForegroundColor Green
    Write-Host ""
    Write-Host "所有关键文件已就绪" -ForegroundColor Green
    Write-Host "本地和远程分支一致" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? v2.0.2 已成功推送到 GitHub！" -ForegroundColor Green
} else {
    Write-Host "??  验证未完全通过" -ForegroundColor Yellow
    Write-Host ""
    if ($localCommit -ne $remoteCommit) {
        Write-Host "??  本地和远程分支不一致" -ForegroundColor Yellow
    }
    if (-not $allFilesExist) {
        Write-Host "??  某些关键文件缺失" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "?? GitHub 链接：" -ForegroundColor Cyan
Write-Host "   https://github.com/sanguodxj-byte/the-second-seat/commits/main" -ForegroundColor Blue
Write-Host ""

Write-Host "?? 查看提交历史：" -ForegroundColor Cyan
Write-Host "   git log origin/main --oneline -10" -ForegroundColor Gray
Write-Host ""

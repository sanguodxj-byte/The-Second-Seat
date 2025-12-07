# Git 仓库推送脚本
# 用于初始化 Git 仓库并推送到远程

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Git 仓库初始化和推送" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 0. 检查 Git 是否安装
Write-Host "0. 检查 Git 是否安装..." -ForegroundColor Yellow
try {
    $gitVersion = git --version
    Write-Host "   ? Git 已安装: $gitVersion" -ForegroundColor Green
} catch {
    Write-Host "   ? Git 未安装！请先安装 Git" -ForegroundColor Red
    Write-Host "   下载地址: https://git-scm.com/download/win" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# 1. 检查是否已经是 Git 仓库
Write-Host "1. 检查 Git 仓库状态..." -ForegroundColor Yellow
if (Test-Path ".git") {
    Write-Host "   ?? 已经是 Git 仓库" -ForegroundColor Yellow
    $continue = Read-Host "   是否继续？(y/n)"
    if ($continue -ne "y") {
        Write-Host "   操作已取消" -ForegroundColor Yellow
        exit 0
    }
} else {
    Write-Host "   ?? 不是 Git 仓库，将进行初始化" -ForegroundColor Cyan
}

Write-Host ""

# 2. 初始化 Git 仓库
Write-Host "2. 初始化 Git 仓库..." -ForegroundColor Yellow
if (-not (Test-Path ".git")) {
    git init
    Write-Host "   ? Git 仓库初始化完成" -ForegroundColor Green
} else {
    Write-Host "   ? Git 仓库已存在" -ForegroundColor Green
}

Write-Host ""

# 3. 创建 .gitignore 文件
Write-Host "3. 创建 .gitignore 文件..." -ForegroundColor Yellow
$gitignoreContent = @"
# RimWorld Mod - Git Ignore

## Build Results
Source/**/bin/
Source/**/obj/
Source/**/Release/
Source/**/Debug/

## Visual Studio
.vs/
*.suo
*.user
*.userosscache
*.sln.docstates
*.userprefs

## RimWorld Specific
*.log
Player.log
Player-prev.log

## OS Files
.DS_Store
Thumbs.db
Desktop.ini

## Temporary Files
*.tmp
*.bak
*.swp
*~

## NuGet
packages/
*.nupkg
*.snupkg

## Test Results
TestResults/

## PowerShell
*.ps1.bak

## Editor
*.code-workspace
.vscode/
.idea/

## Large Binary Files (可选)
# Textures/**/*.psd
# Textures/**/*.xcf

## Personal Files
*.personal.xml
*_BACKUP_*.xml
"@

$gitignoreContent | Out-File -FilePath ".gitignore" -Encoding UTF8 -Force
Write-Host "   ? .gitignore 文件已创建" -ForegroundColor Green

Write-Host ""

# 4. 添加所有文件到暂存区
Write-Host "4. 添加文件到暂存区..." -ForegroundColor Yellow
git add .
Write-Host "   ? 所有文件已添加到暂存区" -ForegroundColor Green

Write-Host ""

# 5. 显示待提交的文件
Write-Host "5. 待提交的文件：" -ForegroundColor Yellow
git status --short | ForEach-Object {
    Write-Host "   $_" -ForegroundColor Gray
}

Write-Host ""

# 6. 创建第一次提交
Write-Host "6. 创建初始提交..." -ForegroundColor Yellow
$commitMessage = Read-Host "   请输入提交消息 (默认: Initial commit - The Second Seat Mod)"
if ([string]::IsNullOrWhiteSpace($commitMessage)) {
    $commitMessage = "Initial commit - The Second Seat Mod"
}

git commit -m $commitMessage
Write-Host "   ? 初始提交完成" -ForegroundColor Green

Write-Host ""

# 7. 配置远程仓库
Write-Host "7. 配置远程仓库..." -ForegroundColor Yellow
Write-Host "   请选择远程仓库平台：" -ForegroundColor Cyan
Write-Host "   1. GitHub" -ForegroundColor White
Write-Host "   2. GitLab" -ForegroundColor White
Write-Host "   3. Gitee (码云)" -ForegroundColor White
Write-Host "   4. 自定义" -ForegroundColor White
Write-Host ""

$platform = Read-Host "   请选择 (1-4)"

switch ($platform) {
    "1" {
        Write-Host "   ?? GitHub 配置" -ForegroundColor Cyan
        $username = Read-Host "   GitHub 用户名"
        $repoName = Read-Host "   仓库名称 (默认: the-second-seat)"
        if ([string]::IsNullOrWhiteSpace($repoName)) {
            $repoName = "the-second-seat"
        }
        $remoteUrl = "https://github.com/$username/$repoName.git"
    }
    "2" {
        Write-Host "   ?? GitLab 配置" -ForegroundColor Cyan
        $username = Read-Host "   GitLab 用户名"
        $repoName = Read-Host "   仓库名称 (默认: the-second-seat)"
        if ([string]::IsNullOrWhiteSpace($repoName)) {
            $repoName = "the-second-seat"
        }
        $remoteUrl = "https://gitlab.com/$username/$repoName.git"
    }
    "3" {
        Write-Host "   ?? Gitee 配置" -ForegroundColor Cyan
        $username = Read-Host "   Gitee 用户名"
        $repoName = Read-Host "   仓库名称 (默认: the-second-seat)"
        if ([string]::IsNullOrWhiteSpace($repoName)) {
            $repoName = "the-second-seat"
        }
        $remoteUrl = "https://gitee.com/$username/$repoName.git"
    }
    "4" {
        Write-Host "   ?? 自定义配置" -ForegroundColor Cyan
        $remoteUrl = Read-Host "   远程仓库 URL"
    }
    default {
        Write-Host "   ? 无效选择" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "   远程仓库 URL: $remoteUrl" -ForegroundColor Cyan

# 检查是否已有 origin
$existingRemote = git remote get-url origin 2>$null
if ($existingRemote) {
    Write-Host "   ?? 已存在 origin 远程仓库: $existingRemote" -ForegroundColor Yellow
    $overwrite = Read-Host "   是否覆盖？(y/n)"
    if ($overwrite -eq "y") {
        git remote remove origin
        git remote add origin $remoteUrl
        Write-Host "   ? 远程仓库已更新" -ForegroundColor Green
    } else {
        Write-Host "   ?? 保持现有远程仓库" -ForegroundColor Yellow
    }
} else {
    git remote add origin $remoteUrl
    Write-Host "   ? 远程仓库已添加" -ForegroundColor Green
}

Write-Host ""

# 8. 推送到远程仓库
Write-Host "8. 推送到远程仓库..." -ForegroundColor Yellow
Write-Host "   ?? 请确保远程仓库已创建！" -ForegroundColor Yellow
Write-Host ""

$push = Read-Host "   是否立即推送？(y/n)"
if ($push -eq "y") {
    Write-Host "   正在推送..." -ForegroundColor Cyan
    
    # 设置默认分支为 main
    git branch -M main
    
    # 推送到远程
    try {
        git push -u origin main
        Write-Host "   ? 推送成功！" -ForegroundColor Green
    } catch {
        Write-Host "   ? 推送失败" -ForegroundColor Red
        Write-Host "   可能的原因：" -ForegroundColor Yellow
        Write-Host "   1. 远程仓库不存在" -ForegroundColor Gray
        Write-Host "   2. 没有推送权限" -ForegroundColor Gray
        Write-Host "   3. 网络连接问题" -ForegroundColor Gray
        Write-Host ""
        Write-Host "   请手动推送：" -ForegroundColor Cyan
        Write-Host "   git push -u origin main" -ForegroundColor White
    }
} else {
    Write-Host "   ?? 跳过推送" -ForegroundColor Yellow
    Write-Host "   稍后可手动推送：" -ForegroundColor Cyan
    Write-Host "   git push -u origin main" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 9. 显示后续操作建议
Write-Host "后续操作建议：" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. 在远程平台创建仓库（如果还未创建）：" -ForegroundColor Cyan
Write-Host "   - GitHub: https://github.com/new" -ForegroundColor Gray
Write-Host "   - GitLab: https://gitlab.com/projects/new" -ForegroundColor Gray
Write-Host "   - Gitee: https://gitee.com/projects/new" -ForegroundColor Gray
Write-Host ""
Write-Host "2. 配置 SSH 密钥（推荐）：" -ForegroundColor Cyan
Write-Host "   ssh-keygen -t ed25519 -C 'your_email@example.com'" -ForegroundColor White
Write-Host "   将公钥添加到远程平台的 SSH Keys 设置" -ForegroundColor Gray
Write-Host ""
Write-Host "3. 日常操作命令：" -ForegroundColor Cyan
Write-Host "   git status              # 查看状态" -ForegroundColor White
Write-Host "   git add .               # 添加所有更改" -ForegroundColor White
Write-Host "   git commit -m 'message' # 提交更改" -ForegroundColor White
Write-Host "   git push                # 推送到远程" -ForegroundColor White
Write-Host "   git pull                # 拉取远程更新" -ForegroundColor White
Write-Host ""
Write-Host "4. 查看远程仓库信息：" -ForegroundColor Cyan
Write-Host "   git remote -v           # 查看远程仓库" -ForegroundColor White
Write-Host "   git log --oneline       # 查看提交历史" -ForegroundColor White
Write-Host ""

Write-Host "按任意键退出..." -ForegroundColor Yellow
$null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

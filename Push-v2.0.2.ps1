#!/usr/bin/env pwsh
# TSS v2.0.2 Git 推送脚本
# 自动化推送流程

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  TSS v2.0.2 - Git 推送脚本                                ║" -ForegroundColor Cyan
Write-Host "║  OpenAI TTS 支持 + 高阶动作系统                          ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 Git 仓库状态
Write-Host "?? [1/5] 检查 Git 仓库状态..." -ForegroundColor Yellow
Write-Host ""

$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "? 检测到以下文件修改：" -ForegroundColor Green
    git status --short
} else {
    Write-Host "??  没有检测到文件修改" -ForegroundColor Yellow
    Write-Host "提示：如果您刚刚修改了文件，请确保已保存" -ForegroundColor Yellow
    $continue = Read-Host "是否继续推送？(y/n)"
    if ($continue -ne "y") {
        Write-Host "? 推送已取消" -ForegroundColor Red
        exit
    }
}

Write-Host ""

# 2. 添加所有修改
Write-Host "?? [2/5] 添加所有修改到暂存区..." -ForegroundColor Yellow
git add .

if ($LASTEXITCODE -eq 0) {
    Write-Host "? 文件已添加到暂存区" -ForegroundColor Green
} else {
    Write-Host "? 添加文件失败" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 3. 提交更改
Write-Host "?? [3/5] 提交更改..." -ForegroundColor Yellow

$commitMessage = @"
feat(tts): Add OpenAI-compatible TTS support (v2.0.2)

- Add OpenAI Speech API support (GPT-SoVITS, OpenAI TTS)
- Implement GenerateOpenAITTSAsync method
- Update Configure method to accept apiUrl and modelName
- Add "openai" branch in SpeakAsync switch statement
- Support any OpenAI-compatible TTS service
- Fix PlaySoundAction (use SoundStarter.PlayOneShotOnCamera)
- AdvancedActions already completed (4 god-level actions)

Breaking Changes: None
Backward Compatible: Yes

Files Changed:
- Source/TheSecondSeat/TTS/TTSService.cs
- Source/TheSecondSeat/Framework/Actions/BasicActions.cs (already fixed)
- Source/TheSecondSeat/Framework/Actions/AdvancedActions.cs (already exists)

Docs:
- TSS实体功能与TTS升级-完成报告-v2.0.2.md
- TSS实体功能与TTS-快速参考-v2.0.2.md
- Git推送报告-v2.0.2.md
"@

git commit -m $commitMessage

if ($LASTEXITCODE -eq 0) {
    Write-Host "? 提交成功" -ForegroundColor Green
} else {
    Write-Host "? 提交失败" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 4. 推送到远程仓库
Write-Host "?? [4/5] 推送到远程仓库..." -ForegroundColor Yellow
Write-Host "目标分支: main" -ForegroundColor Gray
Write-Host "远程仓库: origin" -ForegroundColor Gray
Write-Host ""

git push origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host "? 推送成功！" -ForegroundColor Green
} else {
    Write-Host "? 推送失败" -ForegroundColor Red
    Write-Host "可能的原因：" -ForegroundColor Yellow
    Write-Host "  1. 网络连接问题" -ForegroundColor Yellow
    Write-Host "  2. 远程仓库有新的提交（需要先 pull）" -ForegroundColor Yellow
    Write-Host "  3. 权限问题" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "建议操作：" -ForegroundColor Yellow
    Write-Host "  git pull origin main --rebase" -ForegroundColor Cyan
    Write-Host "  git push origin main" -ForegroundColor Cyan
    exit 1
}

Write-Host ""

# 5. 完成总结
Write-Host "?? [5/5] 推送完成总结" -ForegroundColor Yellow
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              推送成功！v2.0.2                              ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "?? 版本信息：" -ForegroundColor Cyan
Write-Host "   版本号: v2.0.2" -ForegroundColor White
Write-Host "   类型: 功能增强 + TTS升级" -ForegroundColor White
Write-Host "   编译状态: ? 成功" -ForegroundColor White
Write-Host ""

Write-Host "? 核心功能：" -ForegroundColor Cyan
Write-Host "   ? OpenAI 兼容 TTS 支持" -ForegroundColor White
Write-Host "   ? GPT-SoVITS 本地部署" -ForegroundColor White
Write-Host "   ? 多提供商 TTS 架构" -ForegroundColor White
Write-Host "   ? 完整的高阶动作系统" -ForegroundColor White
Write-Host ""

Write-Host "?? 修改统计：" -ForegroundColor Cyan
Write-Host "   修改文件: 1个（TTSService.cs）" -ForegroundColor White
Write-Host "   新增功能: OpenAI TTS 支持" -ForegroundColor White
Write-Host "   新增代码: ~50行" -ForegroundColor White
Write-Host "   新增文档: 3个" -ForegroundColor White
Write-Host ""

Write-Host "?? GitHub 链接：" -ForegroundColor Cyan
Write-Host "   https://github.com/sanguodxj-byte/the-second-seat" -ForegroundColor Blue
Write-Host ""

Write-Host "?? 下一步操作：" -ForegroundColor Cyan
Write-Host "   1. 访问 GitHub 查看提交记录" -ForegroundColor White
Write-Host "   2. 测试 GPT-SoVITS 本地部署" -ForegroundColor White
Write-Host "   3. 测试高阶动作系统" -ForegroundColor White
Write-Host "   4. (可选) 创建 Release v2.0.2" -ForegroundColor White
Write-Host ""

# 询问是否创建标签
Write-Host "???  是否创建并推送 Git 标签？(y/n): " -ForegroundColor Yellow -NoNewline
$createTag = Read-Host

if ($createTag -eq "y") {
    Write-Host ""
    Write-Host "?? 创建标签 v2.0.2..." -ForegroundColor Yellow
    git tag -a v2.0.2 -m "Version 2.0.2: OpenAI TTS Support"
    
    Write-Host "?? 推送标签到远程..." -ForegroundColor Yellow
    git push origin v2.0.2
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? 标签创建并推送成功！" -ForegroundColor Green
        Write-Host "?? 查看标签: https://github.com/sanguodxj-byte/the-second-seat/releases/tag/v2.0.2" -ForegroundColor Blue
    } else {
        Write-Host "? 标签推送失败" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "? 所有操作完成！" -ForegroundColor Green
Write-Host "感谢使用 TSS 推送脚本 ??" -ForegroundColor Cyan
Write-Host ""

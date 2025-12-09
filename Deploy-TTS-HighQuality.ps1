# TTS 高质量音频更新部署脚本

$ErrorActionPreference = "Stop"

Write-Host "?? 部署 TTS 高质量音频更新..." -ForegroundColor Cyan

# 复制 DLL 文件
Copy-Item "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\" -Force
Copy-Item "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\" -Force

Write-Host "? 部署成功！" -ForegroundColor Green
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  ?? TTS 高质量音频更新" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 更新详情:" -ForegroundColor White
Write-Host "  ? 采样率: 24kHz → 48kHz" -ForegroundColor Green
Write-Host "  ? 音频格式: riff-48khz-16bit-mono-pcm" -ForegroundColor Green
Write-Host "  ? 位深度: 16-bit (保持不变)" -ForegroundColor Gray
Write-Host "  ? 声道: Mono (保持不变)" -ForegroundColor Gray
Write-Host ""
Write-Host "? 好处:" -ForegroundColor White
Write-Host "  ? 更平滑的语音过渡" -ForegroundColor Cyan
Write-Host "  ? 更自然的人声效果" -ForegroundColor Cyan
Write-Host "  ? 更高保真度的神经网络语音" -ForegroundColor Cyan
Write-Host "  ? 更好的情感表达细节" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 使用方法:" -ForegroundColor Yellow
Write-Host "  1??  重启 RimWorld" -ForegroundColor White
Write-Host "  2??  进入 Mod 设置 → The Second Seat → TTS 设置" -ForegroundColor White
Write-Host "  3??  启用 Azure TTS 并配置 API 密钥" -ForegroundColor White
Write-Host "  4??  选择喜欢的语音（推荐：zh-CN-XiaoxiaoNeural）" -ForegroundColor White
Write-Host "  5??  点击 '测试 TTS' 按钮" -ForegroundColor White
Write-Host "  6??  享受高质量语音输出！" -ForegroundColor White
Write-Host ""
Write-Host "?? 技术细节:" -ForegroundColor White
Write-Host "  原始格式: 24,000 Hz @ 16-bit = 384 kbps" -ForegroundColor Gray
Write-Host "  新格式:   48,000 Hz @ 16-bit = 768 kbps" -ForegroundColor Gray
Write-Host "  质量提升: 2x 采样率 = 显著提高细节还原" -ForegroundColor Green
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

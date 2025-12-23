# 通用姿态系统 - 自动重构脚本 v1.6.63
# 用途：自动修改 FullBodyPortraitPanel.cs 以支持姿态系统

$ErrorActionPreference = "Stop"
$filePath = "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  通用姿态系统 - 自动重构脚本 v1.6.63" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 备份原文件
Write-Host "[1/6] 备份原文件..." -ForegroundColor Yellow
$backupPath = "$filePath.backup_v1.6.63"
Copy-Item $filePath $backupPath -Force
Write-Host "  ? 备份完成: $backupPath" -ForegroundColor Green

# 2. 读取文件
Write-Host "[2/6] 读取文件内容..." -ForegroundColor Yellow
$content = Get-Content $filePath -Raw -Encoding UTF8

# 3. 检查是否已经修改过
if ($content -match "通用姿态系统字段") {
    Write-Host "  ?? 文件已经包含姿态系统，跳过修改" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "如需重新应用，请先运行：" -ForegroundColor Cyan
    Write-Host "  git checkout HEAD -- $filePath" -ForegroundColor White
    exit 0
}

Write-Host "  ? 文件未修改，继续..." -ForegroundColor Green

# 4. 添加 using System;
Write-Host "[3/6] 添加 using System;..." -ForegroundColor Yellow
$content = $content -replace '(using System\.Collections\.Generic;)', '$1' + "`r`nusing System; // ? 添加"

# 5. 添加姿态系统字段
Write-Host "[4/6] 添加姿态系统字段..." -ForegroundColor Yellow
$postureFields = @"

        // ==================== ? 通用姿态系统字段 ====================
        
        /// <summary>
        /// 当前覆盖姿态的纹理名称（如 "body_arrival"）
        /// 非空时：替代默认身体层 (Layer 1)
        /// </summary>
        private string overridePosture = null;
        
        /// <summary>
        /// 特效纹理名称（如 "glitch_circle"）
        /// 绘制在最顶层，使用 Alpha 混合
        /// </summary>
        private string activeEffect = null;
        
        /// <summary>
        /// 动画结束回调
        /// </summary>
        private Action onAnimationComplete = null;
        
        /// <summary>
        /// 动画计时器（秒）
        /// </summary>
        private float animationTimer = 0f;
        
        /// <summary>
        /// 动画总时长（秒）
        /// </summary>
        private float animationDuration = 0f;
        
        /// <summary>
        /// 动画状态标志
        /// </summary>
        private bool isPlayingAnimation = false;
"@

# 在常量定义后添加
$content = $content -replace '(private const float FAST_FLASH_INTERVAL = 0\.05f;)', "`$1$postureFields"

# 6. 添加公共接口方法
Write-Host "[5/6] 添加公共接口方法..." -ForegroundColor Yellow
$publicMethods = @"

        
        // ==================== ? 通用姿态系统公共接口 ====================
        
        /// <summary>
        /// ? 触发姿态动画
        /// </summary>
        /// <param name="postureName">姿态纹理名称（如 "body_arrival"）</param>
        /// <param name="effectName">特效纹理名称（如 "glitch_circle"），可为 null</param>
        /// <param name="duration">动画时长（秒）</param>
        /// <param name="callback">动画结束回调，可为 null</param>
        public void TriggerPostureAnimation(string postureName, string effectName, float duration, Action callback = null)
        {
            // 初始化动画状态
            overridePosture = postureName;
            activeEffect = effectName;
            animationDuration = duration;
            animationTimer = 0f;
            onAnimationComplete = callback;
            isPlayingAnimation = true;
            
            Log.Message(`$"[FullBodyPortraitPanel] ? 开始姿态动画: {postureName}, 特效: {effectName ?? `"无`"}, 时长: {duration}秒");
        }
        
        /// <summary>
        /// ? 停止当前动画并恢复默认状态
        /// </summary>
        public void StopAnimation()
        {
            if (!isPlayingAnimation) return;
            
            // 触发回调（如果存在）
            try
            {
                onAnimationComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(`$"[FullBodyPortraitPanel] 动画回调异常: {ex}");
            }
            
            // 清除动画状态
            overridePosture = null;
            activeEffect = null;
            animationTimer = 0f;
            animationDuration = 0f;
            onAnimationComplete = null;
            isPlayingAnimation = false;
            
            Log.Message("[FullBodyPortraitPanel] ? 动画已停止");
        }
"@

# 在构造函数后添加
$content = $content -replace '(drawRect = new Rect\(x, y, displayWidth, displayHeight\);[\r\n]+        \})', "`$1$publicMethods"

Write-Host "  ? 字段和方法已添加" -ForegroundColor Green

# 7. 保存修改后的文件
Write-Host "[6/6] 保存修改..." -ForegroundColor Yellow
Set-Content $filePath $content -Encoding UTF8 -NoNewline
Write-Host "  ? 文件已保存" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ? 重构完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "下一步：" -ForegroundColor Cyan
Write-Host "  1. 运行编译脚本验证：.\编译并部署到游戏.ps1" -ForegroundColor White
Write-Host "  2. 查看完整重构指南：通用姿态系统-重构指南-v1.6.63.md" -ForegroundColor White
Write-Host ""
Write-Host "如遇问题，可恢复备份：" -ForegroundColor Yellow
Write-Host "  Copy-Item '$backupPath' '$filePath' -Force" -ForegroundColor White

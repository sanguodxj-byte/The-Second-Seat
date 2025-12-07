# 表情变化系统诊断脚本
# 检查所有相关组件的状态

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   表情变化系统完整诊断" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$modPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$logPath = "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log"

# 1. 检查 ExpressionSystem.cs
Write-Host "1. 检查 ExpressionSystem.cs" -ForegroundColor Yellow
$expressionSystemPath = "Source\TheSecondSeat\PersonaGeneration\ExpressionSystem.cs"
if (Test-Path $expressionSystemPath) {
    Write-Host "   ? 文件存在" -ForegroundColor Green
    
    $content = Get-Content $expressionSystemPath -Raw
    
    # 检查关键方法
    $checks = @(
        @{Name="SetExpression"; Pattern="public static void SetExpression"},
        @{Name="GetExpressionSuffix"; Pattern="public static string GetExpressionSuffix"},
        @{Name="GetRandomVariant"; Pattern="private static string GetRandomVariant"},
        @{Name="UpdateExpressionByDialogueTone"; Pattern="public static void UpdateExpressionByDialogueTone"},
        @{Name="Shy表情枚举"; Pattern="ExpressionType.Shy"}
    )
    
    foreach ($check in $checks) {
        if ($content -match $check.Pattern) {
            Write-Host "   ? $($check.Name) 存在" -ForegroundColor Green
        } else {
            Write-Host "   ? $($check.Name) 缺失！" -ForegroundColor Red
        }
    }
} else {
    Write-Host "   ? 文件不存在！" -ForegroundColor Red
}

Write-Host ""

# 2. 检查 PortraitLoader.cs
Write-Host "2. 检查 PortraitLoader.cs" -ForegroundColor Yellow
$portraitLoaderPath = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"
if (Test-Path $portraitLoaderPath) {
    Write-Host "   ? 文件存在" -ForegroundColor Green
    
    $content = Get-Content $portraitLoaderPath -Raw
    
    if ($content -match "ExpressionSystem\.GetExpressionSuffix") {
        Write-Host "   ? 调用 GetExpressionSuffix（支持随机变体）" -ForegroundColor Green
    } else {
        Write-Host "   ? 未调用 GetExpressionSuffix！" -ForegroundColor Red
    }
} else {
    Write-Host "   ? 文件不存在！" -ForegroundColor Red
}

Write-Host ""

# 3. 检查 AvatarLoader.cs
Write-Host "3. 检查 AvatarLoader.cs" -ForegroundColor Yellow
$avatarLoaderPath = "Source\TheSecondSeat\PersonaGeneration\AvatarLoader.cs"
if (Test-Path $avatarLoaderPath) {
    Write-Host "   ? 文件存在" -ForegroundColor Green
    
    $content = Get-Content $avatarLoaderPath -Raw
    
    if ($content -match "ExpressionSystem\.GetExpressionSuffix") {
        Write-Host "   ? 调用 GetExpressionSuffix（支持随机变体）" -ForegroundColor Green
    } else {
        Write-Host "   ? 未调用 GetExpressionSuffix！" -ForegroundColor Red
    }
} else {
    Write-Host "   ? 文件不存在！" -ForegroundColor Red
}

Write-Host ""

# 4. 检查 NarratorWindow.cs（表情触发）
Write-Host "4. 检查 NarratorWindow.cs" -ForegroundColor Yellow
$narratorWindowPath = "Source\TheSecondSeat\UI\NarratorWindow.cs"
if (Test-Path $narratorWindowPath) {
    Write-Host "   ? 文件存在" -ForegroundColor Green
    
    $content = Get-Content $narratorWindowPath -Raw
    
    $checks = @(
        @{Name="UpdateExpressionByDialogueTone调用"; Pattern="ExpressionSystem\.UpdateExpressionByDialogueTone"},
        @{Name="SetExpression调用"; Pattern="ExpressionSystem\.SetExpression"},
        @{Name="UpdateTransition调用"; Pattern="ExpressionSystem\.UpdateTransition"}
    )
    
    foreach ($check in $checks) {
        if ($content -match $check.Pattern) {
            Write-Host "   ? $($check.Name) 存在" -ForegroundColor Green
        } else {
            Write-Host "   ?? $($check.Name) 可能缺失" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "   ? 文件不存在！" -ForegroundColor Red
}

Write-Host ""

# 5. 检查编译后的 DLL
Write-Host "5. 检查编译后的 DLL" -ForegroundColor Yellow
$dllPath = "$modPath\Assemblies\TheSecondSeat.dll"
if (Test-Path $dllPath) {
    $dllInfo = Get-Item $dllPath
    Write-Host "   ? DLL 存在" -ForegroundColor Green
    Write-Host "   ?? 最后修改: $($dllInfo.LastWriteTime)" -ForegroundColor Cyan
    Write-Host "   ?? 文件大小: $([math]::Round($dllInfo.Length / 1KB, 2)) KB" -ForegroundColor Cyan
    
    # 检查是否是最新的
    $sourceFiles = Get-ChildItem "Source\TheSecondSeat" -Recurse -Filter "*.cs"
    $newestSource = $sourceFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    
    if ($dllInfo.LastWriteTime -lt $newestSource.LastWriteTime) {
        Write-Host "   ?? DLL 比源代码旧，需要重新编译！" -ForegroundColor Yellow
        Write-Host "   最新源文件: $($newestSource.Name)" -ForegroundColor Yellow
    } else {
        Write-Host "   ? DLL 是最新的" -ForegroundColor Green
    }
} else {
    Write-Host "   ? DLL 不存在！需要编译" -ForegroundColor Red
}

Write-Host ""

# 6. 检查游戏日志
Write-Host "6. 检查游戏日志" -ForegroundColor Yellow
if (Test-Path $logPath) {
    Write-Host "   ? 日志文件存在" -ForegroundColor Green
    
    # 读取最后 500 行
    $logLines = Get-Content $logPath -Tail 500
    
    # 检查表情相关日志
    $expressionLogs = $logLines | Where-Object { $_ -match "\[ExpressionSystem\]" }
    
    if ($expressionLogs.Count -gt 0) {
        Write-Host "   ? 找到 $($expressionLogs.Count) 条表情系统日志" -ForegroundColor Green
        Write-Host "   最近的日志：" -ForegroundColor Cyan
        $expressionLogs | Select-Object -Last 5 | ForEach-Object {
            Write-Host "     $_" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ?? 未找到表情系统日志" -ForegroundColor Yellow
    }
    
    # 检查错误
    $errors = $logLines | Where-Object { $_ -match "ExpressionSystem.*error|ExpressionSystem.*Error|ExpressionSystem.*Exception" }
    
    if ($errors.Count -gt 0) {
        Write-Host "   ? 发现错误：" -ForegroundColor Red
        $errors | Select-Object -Last 3 | ForEach-Object {
            Write-Host "     $_" -ForegroundColor Red
        }
    } else {
        Write-Host "   ? 未发现错误" -ForegroundColor Green
    }
} else {
    Write-Host "   ? 日志文件不存在" -ForegroundColor Red
}

Write-Host ""

# 7. 检查表情文件
Write-Host "7. 检查表情文件" -ForegroundColor Yellow
$expressionsPath = "$modPath\Textures\UI\Narrators\9x16\Expressions\Sideria"
if (Test-Path $expressionsPath) {
    $expressionFiles = Get-ChildItem $expressionsPath -Filter "*.png"
    Write-Host "   ? 表情文件夹存在" -ForegroundColor Green
    Write-Host "   ?? 找到 $($expressionFiles.Count) 个表情文件：" -ForegroundColor Cyan
    
    $expressionFiles | ForEach-Object {
        Write-Host "     - $($_.Name)" -ForegroundColor Gray
    }
    
    # 检查必需的表情
    $required = @("base.png", "happy.png", "sad.png", "angry.png", "shy.png")
    foreach ($req in $required) {
        if ($expressionFiles.Name -contains $req) {
            Write-Host "   ? $req 存在" -ForegroundColor Green
        } else {
            Write-Host "   ?? $req 缺失" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "   ? 表情文件夹不存在！" -ForegroundColor Red
}

Write-Host ""

# 8. 生成诊断报告
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   诊断总结" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$report = @"
表情变化系统诊断报告
生成时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## 检查项目

1. ? ExpressionSystem.cs - 表情系统核心
2. ? PortraitLoader.cs - 立绘加载器
3. ? AvatarLoader.cs - 头像加载器
4. ? NarratorWindow.cs - 表情触发
5. ? DLL 编译状态
6. ? 游戏日志检查
7. ? 表情文件检查

## 常见问题排查

### 问题1：表情不切换
- 检查 NarratorWindow.cs 是否调用 UpdateExpressionByDialogueTone
- 检查对话文本是否包含触发关键词
- 检查日志是否有 [ExpressionSystem] 表情切换 记录

### 问题2：随机变体不生效
- 确认 GetRandomVariant 方法存在
- 确认 PortraitLoader 和 AvatarLoader 都调用 GetExpressionSuffix
- 检查是否有多个变体文件（如 happy1.png, happy2.png）

### 问题3：害羞表情不显示
- 确认 ExpressionType.Shy 枚举存在
- 确认 UpdateExpressionByDialogueTone 包含害羞关键词识别
- 确认 shy.png 文件存在

## 下一步操作

1. 如果 DLL 过时：重新编译
   cd "Source\TheSecondSeat"
   dotnet build -c Release

2. 如果代码正确但不生效：检查游戏日志
   查看: $logPath

3. 如果日志无输出：可能未触发表情切换
   - 确保与叙事者对话时发送了包含关键词的文本
   - 检查 NarratorWindow.cs 的事件绑定

4. 如果有错误日志：查看具体错误信息并修复

## 测试建议

1. 发送包含害羞关键词的消息：
   "谢谢你"、"不好意思"、"有点害羞"

2. 观察立绘和头像是否同步切换

3. 多次触发同一表情，观察是否随机显示不同变体
"@

$report | Out-File "表情系统诊断报告.md" -Encoding UTF8
Write-Host "? 诊断报告已保存到: 表情系统诊断报告.md" -ForegroundColor Green
Write-Host ""

# 9. 提供快速修复建议
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   快速修复建议" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "如果表情系统失效，请按以下步骤操作：" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. 重新编译（如果 DLL 过时）：" -ForegroundColor Cyan
Write-Host "   cd 'Source\TheSecondSeat'" -ForegroundColor Gray
Write-Host "   dotnet build -c Release" -ForegroundColor Gray
Write-Host ""
Write-Host "2. 部署 DLL：" -ForegroundColor Cyan
Write-Host "   .\部署.bat" -ForegroundColor Gray
Write-Host ""
Write-Host "3. 重启游戏并测试" -ForegroundColor Cyan
Write-Host ""
Write-Host "4. 查看游戏日志：" -ForegroundColor Cyan
Write-Host "   notepad '$logPath'" -ForegroundColor Gray
Write-Host ""

Write-Host "按任意键退出..." -ForegroundColor Yellow
$null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

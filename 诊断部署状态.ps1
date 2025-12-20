#!/usr/bin/env pwsh
# TSS 部署诊断脚本 - 检查为什么找不到事件

param(
    [string]$GameModPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
)

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  TSS 部署诊断 v2.0.3                                       ║" -ForegroundColor Cyan
Write-Host "║  Diagnostic & Troubleshooting                             ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# 1. 检查游戏目录
# ============================================================
Write-Host "?? [1/5] 检查游戏目录..." -ForegroundColor Yellow
Write-Host ""

if (!(Test-Path $GameModPath)) {
    Write-Host "? 游戏目录不存在: $GameModPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "建议：" -ForegroundColor Yellow
    Write-Host "1. 确认游戏安装路径" -ForegroundColor White
    Write-Host "2. 检查是否有 RimWorld\Mods 文件夹" -ForegroundColor White
    exit 1
}

Write-Host "? 游戏目录存在" -ForegroundColor Green
Write-Host "   $GameModPath" -ForegroundColor Gray
Write-Host ""

# ============================================================
# 2. 检查关键文件
# ============================================================
Write-Host "?? [2/5] 检查关键文件..." -ForegroundColor Yellow
Write-Host ""

$criticalFiles = @{
    "Assemblies\TheSecondSeat.dll" = "核心 DLL"
    "About\About.xml" = "Mod 描述"
    "Defs\GameComponentDefs.xml" = "游戏组件"
    "Defs\TSS_Custom_Events.xml" = "自定义事件定义"
}

$missingFiles = @()

foreach ($file in $criticalFiles.Keys) {
    $fullPath = Join-Path $GameModPath $file
    $desc = $criticalFiles[$file]
    
    if (Test-Path $fullPath) {
        $size = (Get-Item $fullPath).Length
        $lastWrite = (Get-Item $fullPath).LastWriteTime
        Write-Host "? $file" -ForegroundColor Green
        Write-Host "   描述: $desc" -ForegroundColor Gray
        Write-Host "   大小: $([math]::Round($size/1KB, 2)) KB" -ForegroundColor Gray
        Write-Host "   修改: $($lastWrite.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    } else {
        Write-Host "? $file (缺失)" -ForegroundColor Red
        $missingFiles += $file
    }
}

Write-Host ""

if ($missingFiles.Count -gt 0) {
    Write-Host "??  发现 $($missingFiles.Count) 个文件缺失！" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "解决方法：" -ForegroundColor Cyan
    Write-Host "运行完整部署脚本：" -ForegroundColor White
    Write-Host "  .\编译并部署到游戏.ps1 -FullDeploy" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================
# 3. 检查 DLL 内容
# ============================================================
Write-Host "?? [3/5] 检查 DLL 内容..." -ForegroundColor Yellow
Write-Host ""

$dllPath = Join-Path $GameModPath "Assemblies\TheSecondSeat.dll"

if (!(Test-Path $dllPath)) {
    Write-Host "? DLL 文件不存在" -ForegroundColor Red
} else {
    try {
        $dll = [System.Reflection.Assembly]::LoadFile($dllPath)
        $version = $dll.GetName().Version
        Write-Host "? DLL 版本: $version" -ForegroundColor Green
        Write-Host ""
        
        # 检查关键类
        Write-Host "检查关键类..." -ForegroundColor Cyan
        
        $keyClasses = @{
            "TheSecondSeat.Testing.EventTester" = "事件测试工具"
            "TheSecondSeat.Framework.NarratorEventManager" = "事件管理器"
            "TheSecondSeat.Framework.NarratorEventDef" = "事件定义"
        }
        
        $missingClasses = @()
        
        foreach ($className in $keyClasses.Keys) {
            $desc = $keyClasses[$className]
            $type = $dll.GetType($className)
            
            if ($type) {
                Write-Host "? $className" -ForegroundColor Green
                Write-Host "   ($desc)" -ForegroundColor Gray
                
                # 如果是 EventTester，检查方法
                if ($className -eq "TheSecondSeat.Testing.EventTester") {
                    $methods = $type.GetMethods([System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::NonPublic)
                    $debugMethods = $methods | Where-Object { 
                        $_.GetCustomAttributes($false) | Where-Object { $_.GetType().Name -eq "DebugActionAttribute" }
                    }
                    
                    if ($debugMethods) {
                        Write-Host "   找到 $($debugMethods.Count) 个测试按钮:" -ForegroundColor Gray
                        foreach ($method in $debugMethods) {
                            Write-Host "     - $($method.Name)" -ForegroundColor Gray
                        }
                    } else {
                        Write-Host "   ??  未找到 DebugAction 方法" -ForegroundColor Yellow
                        $missingClasses += "DebugAction 方法"
                    }
                }
            } else {
                Write-Host "? $className (未找到)" -ForegroundColor Red
                $missingClasses += $className
            }
        }
        
        Write-Host ""
        
        if ($missingClasses.Count -gt 0) {
            Write-Host "??  发现 $($missingClasses.Count) 个类/方法缺失" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "原因可能是：" -ForegroundColor Cyan
            Write-Host "1. EventTester.cs 未添加到项目" -ForegroundColor White
            Write-Host "2. 编译时出错但未注意" -ForegroundColor White
            Write-Host "3. DLL 版本过旧" -ForegroundColor White
            Write-Host ""
            Write-Host "解决方法：" -ForegroundColor Cyan
            Write-Host "1. 重新编译项目" -ForegroundColor White
            Write-Host "2. 检查编译输出是否有错误" -ForegroundColor White
            Write-Host "3. 运行: .\编译并部署到游戏.ps1" -ForegroundColor Gray
            Write-Host ""
        }
        
    } catch {
        Write-Host "? 无法读取 DLL: $_" -ForegroundColor Red
    }
}

# ============================================================
# 4. 检查事件定义文件
# ============================================================
Write-Host "?? [4/5] 检查事件定义..." -ForegroundColor Yellow
Write-Host ""

$eventsFile = Join-Path $GameModPath "Defs\TSS_Custom_Events.xml"

if (!(Test-Path $eventsFile)) {
    Write-Host "? 事件定义文件不存在" -ForegroundColor Red
} else {
    try {
        [xml]$xml = Get-Content $eventsFile
        $events = $xml.Defs.ChildNodes | Where-Object { $_.Name -ne "#comment" }
        
        Write-Host "? 找到 $($events.Count) 个事件定义:" -ForegroundColor Green
        
        foreach ($event in $events) {
            if ($event.defName) {
                Write-Host "   - $($event.defName)" -ForegroundColor Gray
                if ($event.eventLabel) {
                    Write-Host "     标签: $($event.eventLabel)" -ForegroundColor DarkGray
                }
            }
        }
        
        Write-Host ""
        
        if ($events.Count -eq 0) {
            Write-Host "??  事件定义文件为空" -ForegroundColor Yellow
        } elseif ($events.Count -ne 3) {
            Write-Host "??  预期 3 个事件，实际 $($events.Count) 个" -ForegroundColor Yellow
        }
        
    } catch {
        Write-Host "? XML 格式错误: $_" -ForegroundColor Red
    }
}

Write-Host ""

# ============================================================
# 5. 游戏内检查指南
# ============================================================
Write-Host "?? [5/5] 游戏内检查指南" -ForegroundColor Yellow
Write-Host ""

Write-Host "步骤1: 确认 Mod 已加载" -ForegroundColor Cyan
Write-Host "   1. 启动 RimWorld" -ForegroundColor White
Write-Host "   2. 主菜单 → 选项 → Mod" -ForegroundColor White
Write-Host "   3. 查找 'The Second Seat'" -ForegroundColor White
Write-Host "   4. 确保已勾选" -ForegroundColor White
Write-Host ""

Write-Host "步骤2: 检查是否启用开发者模式" -ForegroundColor Cyan
Write-Host "   1. 在游戏中按 F12" -ForegroundColor White
Write-Host "   2. 屏幕顶部应出现一排白色按钮" -ForegroundColor White
Write-Host "   3. 右上角应有 ?? 齿轮图标" -ForegroundColor White
Write-Host ""

Write-Host "步骤3: 查找 TSS Events" -ForegroundColor Cyan
Write-Host "   1. 点击 ?? 齿轮图标" -ForegroundColor White
Write-Host "   2. 在搜索框输入: TSS Events" -ForegroundColor White
Write-Host "   3. 应该看到:" -ForegroundColor White
Write-Host "      - ?? 触发见面礼" -ForegroundColor Gray
Write-Host "      - ? 触发神罚" -ForegroundColor Gray
Write-Host "      - ?? 触发敌袭警报" -ForegroundColor Gray
Write-Host "      - ?? 列出所有事件" -ForegroundColor Gray
Write-Host "      - ?? 检查事件系统" -ForegroundColor Gray
Write-Host ""

Write-Host "如果找不到 'TSS Events':" -ForegroundColor Yellow
Write-Host "   → EventTester 类未编译进 DLL" -ForegroundColor White
Write-Host "   → 需要重新编译部署" -ForegroundColor White
Write-Host ""

# ============================================================
# 诊断总结
# ============================================================
Write-Host "?? 诊断总结" -ForegroundColor Yellow
Write-Host ""

$allGood = ($missingFiles.Count -eq 0) -and ($missingClasses.Count -eq 0)

if ($allGood) {
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║              ? 部署状态正常                               ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "如果游戏内仍找不到 TSS Events：" -ForegroundColor Cyan
    Write-Host "1. 确认 Mod 已在 Mod 管理器中启用" -ForegroundColor White
    Write-Host "2. 重启游戏" -ForegroundColor White
    Write-Host "3. 按 F12 开启开发者模式" -ForegroundColor White
    Write-Host "4. 点击 ?? → 搜索 'TSS Events'" -ForegroundColor White
} else {
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║              ? 发现部署问题                               ║" -ForegroundColor Red
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    Write-Host "问题汇总：" -ForegroundColor Yellow
    if ($missingFiles.Count -gt 0) {
        Write-Host "  - $($missingFiles.Count) 个文件缺失" -ForegroundColor White
    }
    if ($missingClasses.Count -gt 0) {
        Write-Host "  - $($missingClasses.Count) 个类/方法缺失" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "解决方法：" -ForegroundColor Cyan
    Write-Host "运行完整编译+部署：" -ForegroundColor White
    Write-Host "  .\编译并部署到游戏.ps1 -FullDeploy" -ForegroundColor Gray
}

Write-Host ""
Write-Host "?? 相关文档：" -ForegroundColor Cyan
Write-Host "   - 图解指南: TSS自定义事件-图解测试指南-v1.0.md" -ForegroundColor White
Write-Host "   - 测试方法: TSS自定义事件-正确的游戏内测试方法-v1.0.md" -ForegroundColor White
Write-Host ""

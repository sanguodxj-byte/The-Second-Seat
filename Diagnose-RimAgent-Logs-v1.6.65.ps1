# ================================================================
# Diagnose-RimAgent-Logs-v1.6.65.ps1
# ?? RimAgent 日志缺失诊断脚本
# ================================================================

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ?? RimAgent v1.6.65 日志缺失诊断" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

$gamePath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$dllPath = "$gamePath\Assemblies\TheSecondSeat.dll"
$sourcePath = "Source\TheSecondSeat\TheSecondSeatMod.cs"

# ================================================================
# 1. 检查源文件
# ================================================================
Write-Host "?? 1. 检查源文件..." -ForegroundColor Yellow

if (Test-Path $sourcePath) {
    Write-Host "  ? 源文件存在: $sourcePath" -ForegroundColor Green
    
    # 检查关键标记
    $content = Get-Content $sourcePath -Raw
    
    if ($content -match "\[Verse\.StaticConstructorOnStartup\]") {
        Write-Host "  ? StaticConstructorOnStartup 标记存在" -ForegroundColor Green
    } else {
        Write-Host "  ? StaticConstructorOnStartup 标记缺失" -ForegroundColor Red
    }
    
    if ($content -match "public static class TheSecondSeatInit") {
        Write-Host "  ? TheSecondSeatInit 类定义正确" -ForegroundColor Green
    } else {
        Write-Host "  ? TheSecondSeatInit 类定义错误" -ForegroundColor Red
    }
    
    if ($content -match "LLMProviderFactory\.Initialize") {
        Write-Host "  ? LLMProviderFactory.Initialize() 调用存在" -ForegroundColor Green
    } else {
        Write-Host "  ? LLMProviderFactory.Initialize() 调用缺失" -ForegroundColor Red
    }
    
    if ($content -match "RimAgentTools\.RegisterTool") {
        Write-Host "  ? RimAgentTools.RegisterTool() 调用存在" -ForegroundColor Green
    } else {
        Write-Host "  ? RimAgentTools.RegisterTool() 调用缺失" -ForegroundColor Red
    }
} else {
    Write-Host "  ? 源文件不存在: $sourcePath" -ForegroundColor Red
}

Write-Host ""

# ================================================================
# 2. 检查编译输出
# ================================================================
Write-Host "?? 2. 检查编译输出..." -ForegroundColor Yellow

$localDll = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
if (Test-Path $localDll) {
    $localInfo = Get-Item $localDll
    Write-Host "  ? 本地 DLL 存在" -ForegroundColor Green
    Write-Host "     路径: $localDll" -ForegroundColor White
    Write-Host "     大小: $($localInfo.Length) bytes" -ForegroundColor White
    Write-Host "     时间: $($localInfo.LastWriteTime)" -ForegroundColor White
} else {
    Write-Host "  ? 本地 DLL 不存在: $localDll" -ForegroundColor Red
}

Write-Host ""

# ================================================================
# 3. 检查游戏目录
# ================================================================
Write-Host "?? 3. 检查游戏目录..." -ForegroundColor Yellow

if (Test-Path $dllPath) {
    $gameInfo = Get-Item $dllPath
    Write-Host "  ? 游戏 DLL 存在" -ForegroundColor Green
    Write-Host "     路径: $dllPath" -ForegroundColor White
    Write-Host "     大小: $($gameInfo.Length) bytes" -ForegroundColor White
    Write-Host "     时间: $($gameInfo.LastWriteTime)" -ForegroundColor White
    
    # 比较时间戳
    if (Test-Path $localDll) {
        $localInfo = Get-Item $localDll
        if ($gameInfo.LastWriteTime -lt $localInfo.LastWriteTime) {
            Write-Host "  ?? 警告: 游戏 DLL 比本地 DLL 旧，需要重新部署" -ForegroundColor Yellow
        } else {
            Write-Host "  ? DLL 时间戳正常" -ForegroundColor Green
        }
    }
} else {
    Write-Host "  ? 游戏 DLL 不存在: $dllPath" -ForegroundColor Red
}

Write-Host ""

# ================================================================
# 4. 检查 LoadFolders.xml
# ================================================================
Write-Host "?? 4. 检查 LoadFolders.xml..." -ForegroundColor Yellow

$loadFoldersPath = "$gamePath\LoadFolders.xml"
if (Test-Path $loadFoldersPath) {
    Write-Host "  ? LoadFolders.xml 存在" -ForegroundColor Green
    $loadFoldersContent = Get-Content $loadFoldersPath -Raw
    
    if ($loadFoldersContent -match "<v1.5>") {
        Write-Host "  ? v1.5 文件夹配置存在" -ForegroundColor Green
    } else {
        Write-Host "  ?? 警告: v1.5 文件夹配置可能缺失" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ?? LoadFolders.xml 不存在（可能使用默认配置）" -ForegroundColor Yellow
}

Write-Host ""

# ================================================================
# 5. 检查命名空间冲突
# ================================================================
Write-Host "?? 5. 检查命名空间冲突..." -ForegroundColor Yellow

$duplicates = Get-ChildItem -Path "Source\TheSecondSeat" -Recurse -Filter "*.cs" | 
    Select-String "class TheSecondSeatInit" | 
    Select-Object -ExpandProperty Path

if ($duplicates.Count -gt 1) {
    Write-Host "  ?? 警告: 发现多个 TheSecondSeatInit 类定义" -ForegroundColor Yellow
    foreach ($dup in $duplicates) {
        Write-Host "     - $dup" -ForegroundColor White
    }
} elseif ($duplicates.Count -eq 1) {
    Write-Host "  ? 只有一个 TheSecondSeatInit 类定义" -ForegroundColor Green
} else {
    Write-Host "  ? 未找到 TheSecondSeatInit 类定义" -ForegroundColor Red
}

Write-Host ""

# ================================================================
# 6. 检查最近的编译日志
# ================================================================
Write-Host "?? 6. 检查最近的编译日志..." -ForegroundColor Yellow

$logFiles = Get-ChildItem -Filter "TSS-编译部署报告-*.txt" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if ($logFiles) {
    Write-Host "  ? 找到最近的编译日志: $($logFiles.Name)" -ForegroundColor Green
    Write-Host "     时间: $($logFiles.LastWriteTime)" -ForegroundColor White
    
    $logContent = Get-Content $logFiles.FullName -Tail 20
    Write-Host ""
    Write-Host "  ?? 最后 20 行日志:" -ForegroundColor Cyan
    $logContent | ForEach-Object { Write-Host "     $_" -ForegroundColor White }
} else {
    Write-Host "  ?? 未找到编译日志" -ForegroundColor Yellow
}

Write-Host ""

# ================================================================
# 7. 生成修复建议
# ================================================================
Write-Host "?? 7. 修复建议..." -ForegroundColor Yellow
Write-Host ""

$issues = @()

if (-not (Test-Path $dllPath)) {
    $issues += "DLL 未部署到游戏目录"
}

if (Test-Path $localDll) {
    $localInfo = Get-Item $localDll
    if (Test-Path $dllPath) {
        $gameInfo = Get-Item $dllPath
        if ($gameInfo.LastWriteTime -lt $localInfo.LastWriteTime) {
            $issues += "游戏 DLL 版本过旧"
        }
    }
}

if ($issues.Count -gt 0) {
    Write-Host "?? 发现以下问题：" -ForegroundColor Yellow
    foreach ($issue in $issues) {
        Write-Host "  ? $issue" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "?? 建议执行以下操作：" -ForegroundColor Cyan
    Write-Host "  1. 重新编译项目" -ForegroundColor White
    Write-Host "     dotnet build Source\TheSecondSeat\TheSecondSeat.csproj -c Release" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. 重新部署到游戏" -ForegroundColor White
    Write-Host "     .\编译并部署到游戏.ps1" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. 重启游戏" -ForegroundColor White
} else {
    Write-Host "? 未发现明显问题" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? 如果日志仍然缺失，请尝试：" -ForegroundColor Cyan
    Write-Host "  1. 完全关闭游戏" -ForegroundColor White
    Write-Host "  2. 删除游戏 Assemblies 文件夹" -ForegroundColor White
    Write-Host "     Remove-Item '$gamePath\Assemblies' -Recurse -Force" -ForegroundColor Gray
    Write-Host "  3. 重新部署" -ForegroundColor White
    Write-Host "     .\编译并部署到游戏.ps1" -ForegroundColor Gray
    Write-Host "  4. 启动游戏并查看日志" -ForegroundColor White
}

Write-Host ""

# ================================================================
# 8. 创建手动验证脚本
# ================================================================
Write-Host "?? 8. 创建游戏内验证脚本..." -ForegroundColor Yellow

$verifyScript = @'
// 在游戏控制台（~键）中执行以下命令来验证 RimAgent 是否加载

// 1. 检查类是否存在
var types = System.Reflection.Assembly.GetExecutingAssembly().GetTypes();
var initClass = types.FirstOrDefault(t => t.Name == "TheSecondSeatInit");
Log.Message("TheSecondSeatInit 类存在: " + (initClass != null));

// 2. 检查 RimAgentTools 是否存在
var toolsClass = types.FirstOrDefault(t => t.Name == "RimAgentTools");
Log.Message("RimAgentTools 类存在: " + (toolsClass != null));

// 3. 检查工具注册状态
if (toolsClass != null) {
    var getToolsMethod = toolsClass.GetMethod("GetRegisteredToolNames", 
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    if (getToolsMethod != null) {
        var tools = getToolsMethod.Invoke(null, null) as System.Collections.Generic.List<string>;
        Log.Message("已注册的工具: " + string.Join(", ", tools));
    }
}

// 4. 检查 NarratorManager
var manager = Current.Game?.GetComponent<TheSecondSeat.Narrator.NarratorManager>();
if (manager != null) {
    Log.Message("NarratorManager 存在");
    // 尝试获取 Agent 统计
    try {
        var stats = manager.GetAgentStats();
        Log.Message(stats);
    } catch (System.Exception ex) {
        Log.Error("获取 Agent 统计失败: " + ex.Message);
    }
}
'@

Set-Content -Path "Verify-RimAgent-InGame.txt" -Value $verifyScript
Write-Host "  ? 已创建验证脚本: Verify-RimAgent-InGame.txt" -ForegroundColor Green
Write-Host "     在游戏中按 ~ 键打开控制台，复制粘贴脚本内容执行" -ForegroundColor White

Write-Host ""

# ================================================================
# 完成
# ================================================================
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ?? 诊断完成" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 诊断报告已保存: RimAgent-日志缺失诊断-v1.6.65.md" -ForegroundColor Cyan
Write-Host "?? 游戏内验证脚本: Verify-RimAgent-InGame.txt" -ForegroundColor Cyan
Write-Host ""

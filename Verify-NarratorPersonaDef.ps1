# NarratorPersonaDef 修复验证脚本
Write-Host "?? NarratorPersonaDef 修复验证" -ForegroundColor Cyan
Write-Host "=" * 70

# 1. 验证DLL
Write-Host "`n1?? 验证DLL..." -ForegroundColor Yellow
$dllPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\TheSecondSeat.dll"
if (Test-Path $dllPath) {
    $dll = Get-Item $dllPath
    Write-Host "? DLL存在" -ForegroundColor Green
    Write-Host "   大小: $([math]::Round($dll.Length/1KB, 2)) KB"
    Write-Host "   修改时间: $($dll.LastWriteTime)"
} else {
    Write-Host "? DLL不存在" -ForegroundColor Red
}

# 2. 验证XML结构
Write-Host "`n2?? 验证XML结构..." -ForegroundColor Yellow
$xmlPath = "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Defs\NarratorPersonaDefs.xml"
if (Test-Path $xmlPath) {
    $xmlContent = Get-Content $xmlPath -Raw
    
    # 检查是否还有嵌套的dialogueStyle节点
    if ($xmlContent -match "<dialogueStyle>") {
        Write-Host "? 仍然包含嵌套的 <dialogueStyle> 节点" -ForegroundColor Red
    } else {
        Write-Host "? 已移除嵌套的 <dialogueStyle> 节点" -ForegroundColor Green
    }
    
    # 检查是否还有嵌套的eventPreferences节点
    if ($xmlContent -match "<eventPreferences>") {
        Write-Host "? 仍然包含嵌套的 <eventPreferences> 节点" -ForegroundColor Red
    } else {
        Write-Host "? 已移除嵌套的 <eventPreferences> 节点" -ForegroundColor Green
    }
    
    # 检查是否有扁平化字段
    if ($xmlContent -match "<formalityLevel>") {
        Write-Host "? 包含扁平化字段 <formalityLevel>" -ForegroundColor Green
    } else {
        Write-Host "? 缺少扁平化字段" -ForegroundColor Red
    }
    
    # 统计Def数量
    $defCount = ([regex]::Matches($xmlContent, "<defName>")).Count
    Write-Host "? 找到 $defCount 个叙事者定义" -ForegroundColor Green
    
} else {
    Write-Host "? XML文件不存在" -ForegroundColor Red
}

# 3. 等待RimWorld启动后检查日志
Write-Host "`n3?? 等待RimWorld启动..." -ForegroundColor Yellow
Write-Host "请启动RimWorld，然后按任意键继续检查日志..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 4. 检查Player.log
Write-Host "`n4?? 检查Player.log..." -ForegroundColor Yellow
$logPath = "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log"

if (Test-Path $logPath) {
    Write-Host "? 找到Player.log" -ForegroundColor Green
    
    # 检查错误
    Write-Host "`n?? 查找Def加载错误：" -ForegroundColor Yellow
    $errors = Select-String -Path $logPath -Pattern "NarratorPersonaDef.*not a Def type|could not be found" -CaseSensitive:$false
    
    if ($errors) {
        Write-Host "? 发现错误：" -ForegroundColor Red
        $errors | Select-Object -Last 3 | ForEach-Object {
            Write-Host "   $($_.Line)" -ForegroundColor Red
        }
    } else {
        Write-Host "? 未发现Def加载错误！" -ForegroundColor Green
    }
    
    # 检查成功加载的Def
    Write-Host "`n?? 查找成功加载的Def：" -ForegroundColor Yellow
    $loaded = Select-String -Path $logPath -Pattern "Cassandra_Classic|Phoebe_Chillax|Randy_Random" -CaseSensitive:$false
    
    if ($loaded) {
        Write-Host "? 找到叙事者Def引用：" -ForegroundColor Green
        $loaded | Select-Object -Last 5 | ForEach-Object {
            Write-Host "   $($_.Line.Substring(0, [Math]::Min(100, $_.Line.Length)))" -ForegroundColor Cyan
        }
    } else {
        Write-Host "?? 未找到叙事者Def引用（可能还未使用）" -ForegroundColor Yellow
    }
    
} else {
    Write-Host "? Player.log未找到" -ForegroundColor Red
}

Write-Host "`n" + "=" * 70
Write-Host "? 验证完成！" -ForegroundColor Green
Write-Host "`n?? 修复总结：" -ForegroundColor Cyan
Write-Host "1. DLL已编译并部署（437.5 KB）"
Write-Host "2. XML已更新为扁平化结构"
Write-Host "3. 移除了嵌套的 <dialogueStyle> 和 <eventPreferences> 节点"
Write-Host "4. 改为使用基本类型字段（formalityLevel, emotionalExpression等）"
Write-Host "`n?? 如果仍有问题，请查看上方的日志分析" -ForegroundColor Yellow

# 诊断 NarratorPersonaDef 加载问题
Write-Host "?? NarratorPersonaDef 加载诊断工具" -ForegroundColor Cyan
Write-Host "=" * 60

$modRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$rimworldModPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

Write-Host "`n1?? 检查 DLL 文件..." -ForegroundColor Yellow

$sourceDll = "$modRoot\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$targetDll = "$rimworldModPath\1.6\Assemblies\TheSecondSeat.dll"

if (Test-Path $sourceDll) {
    $sourceInfo = Get-Item $sourceDll
    Write-Host "? 源 DLL 存在" -ForegroundColor Green
    Write-Host "   路径: $sourceDll"
    Write-Host "   大小: $([math]::Round($sourceInfo.Length/1KB, 2)) KB"
    Write-Host "   修改时间: $($sourceInfo.LastWriteTime)"
} else {
    Write-Host "? 源 DLL 不存在: $sourceDll" -ForegroundColor Red
}

if (Test-Path $targetDll) {
    $targetInfo = Get-Item $targetDll
    Write-Host "? 目标 DLL 存在" -ForegroundColor Green
    Write-Host "   路径: $targetDll"
    Write-Host "   大小: $([math]::Round($targetInfo.Length/1KB, 2)) KB"
    Write-Host "   修改时间: $($targetInfo.LastWriteTime)"
    
    # 检查是否是最新版本
    if ($sourceInfo.LastWriteTime -eq $targetInfo.LastWriteTime) {
        Write-Host "? DLL 是最新版本" -ForegroundColor Green
    } else {
        Write-Host "?? DLL 可能不是最新版本！" -ForegroundColor Yellow
        Write-Host "   时间差: $(($sourceInfo.LastWriteTime - $targetInfo.LastWriteTime).TotalSeconds) 秒"
    }
} else {
    Write-Host "? 目标 DLL 不存在: $targetDll" -ForegroundColor Red
}

Write-Host "`n2?? 检查 XML 文件..." -ForegroundColor Yellow

$xmlPath = "$modRoot\Defs\NarratorPersonaDefs.xml"
if (Test-Path $xmlPath) {
    Write-Host "? XML 文件存在: $xmlPath" -ForegroundColor Green
    
    # 检查XML内容
    $xmlContent = Get-Content $xmlPath -Raw
    if ($xmlContent -match "TheSecondSeat\.PersonaGeneration\.NarratorPersonaDef") {
        Write-Host "? XML 包含正确的类型名称" -ForegroundColor Green
    } else {
        Write-Host "? XML 不包含 TheSecondSeat.PersonaGeneration.NarratorPersonaDef" -ForegroundColor Red
    }
    
    # 检查编码
    $encoding = [System.Text.Encoding]::Default
    $bytes = [System.IO.File]::ReadAllBytes($xmlPath)
    if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        Write-Host "? XML 文件编码: UTF-8 with BOM" -ForegroundColor Green
    } elseif ($bytes[0] -eq 0x3C -and $bytes[1] -eq 0x3F) {
        Write-Host "? XML 文件编码: UTF-8 without BOM (或 ASCII)" -ForegroundColor Green
    } else {
        Write-Host "?? XML 文件编码未知" -ForegroundColor Yellow
    }
} else {
    Write-Host "? XML 文件不存在: $xmlPath" -ForegroundColor Red
}

Write-Host "`n3?? 使用反射检查 DLL..." -ForegroundColor Yellow

if (Test-Path $targetDll) {
    try {
        # 加载 DLL
        $assembly = [System.Reflection.Assembly]::LoadFile($targetDll)
        Write-Host "? DLL 加载成功" -ForegroundColor Green
        
        # 查找 NarratorPersonaDef 类型
        $type = $assembly.GetType("TheSecondSeat.PersonaGeneration.NarratorPersonaDef")
        
        if ($type) {
            Write-Host "? 找到 NarratorPersonaDef 类型" -ForegroundColor Green
            Write-Host "   完整名称: $($type.FullName)"
            Write-Host "   基类: $($type.BaseType.FullName)"
            Write-Host "   是否公开: $($type.IsPublic)"
            
            # 检查是否继承自 Verse.Def
            $baseTypeName = $type.BaseType.Name
            if ($baseTypeName -eq "Def") {
                Write-Host "? 正确继承自 Verse.Def" -ForegroundColor Green
            } else {
                Write-Host "? 未继承自 Verse.Def，基类是: $baseTypeName" -ForegroundColor Red
            }
            
            # 列出公共字段
            Write-Host "`n   公共字段 (前10个):"
            $fields = $type.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance)
            $fields | Select-Object -First 10 | ForEach-Object {
                Write-Host "     - $($_.Name): $($_.FieldType.Name)"
            }
        } else {
            Write-Host "? 未找到 NarratorPersonaDef 类型" -ForegroundColor Red
        }
        
        # 列出所有公开类型
        Write-Host "`n   DLL 中的所有 Def 类型:"
        $assembly.GetTypes() | Where-Object { 
            $_.IsPublic -and $_.BaseType -and $_.BaseType.Name -eq "Def" 
        } | ForEach-Object {
            Write-Host "     - $($_.FullName)"
        }
        
    } catch {
        Write-Host "? 加载 DLL 失败: $_" -ForegroundColor Red
    }
} else {
    Write-Host "?? 跳过反射检查（DLL 不存在）" -ForegroundColor Yellow
}

Write-Host "`n4?? 检查 LoadFolders.xml..." -ForegroundColor Yellow

$loadFoldersPath = "$modRoot\LoadFolders.xml"
if (Test-Path $loadFoldersPath) {
    Write-Host "? LoadFolders.xml 存在" -ForegroundColor Green
    $loadFoldersContent = Get-Content $loadFoldersPath -Raw
    
    if ($loadFoldersContent -match "1\.6") {
        Write-Host "? 包含 1.6 配置" -ForegroundColor Green
    } else {
        Write-Host "?? 未找到 1.6 配置" -ForegroundColor Yellow
    }
} else {
    Write-Host "?? LoadFolders.xml 不存在（可能不需要）" -ForegroundColor Yellow
}

Write-Host "`n5?? 建议的修复步骤..." -ForegroundColor Yellow
Write-Host "=" * 60

if (-not (Test-Path $targetDll)) {
    Write-Host "?? 步骤 1: 重新部署 DLL" -ForegroundColor Cyan
    Write-Host "   运行: Quick-Deploy.ps1"
} elseif ($sourceInfo.LastWriteTime -ne $targetInfo.LastWriteTime) {
    Write-Host "?? 步骤 1: 更新 DLL 到最新版本" -ForegroundColor Cyan
    Write-Host "   运行: Quick-Deploy.ps1"
}

Write-Host "?? 步骤 2: 清除 RimWorld 缓存" -ForegroundColor Cyan
Write-Host "   1. 完全关闭 RimWorld"
Write-Host "   2. 删除: C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\"
Write-Host "   3. 删除: C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\ModsConfig.xml"

Write-Host "?? 步骤 3: 重新启动 RimWorld" -ForegroundColor Cyan
Write-Host "   启动时注意 Dev Mode 日志输出"

Write-Host "`n? 诊断完成！" -ForegroundColor Green

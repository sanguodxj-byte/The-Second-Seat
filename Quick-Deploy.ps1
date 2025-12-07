# 快速部署脚本
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Local", "Workshop", "GitHub", "All")]
    [string]$Target = "Local",
    
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "  The Second Seat - 快速部署工具" -ForegroundColor Yellow
Write-Host "  目标: $Target" -ForegroundColor Cyan
Write-Host "="*60 + "`n" -ForegroundColor Cyan

# 设置路径
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDll = "$projectRoot\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$targetDll = "$projectRoot\Assemblies\TheSecondSeat.dll"
$rimworldMods = "D:\steam\steamapps\common\RimWorld\Mods"

# 步骤 1: 编译 DLL
Write-Host "?? 步骤 1: 编译 DLL..." -ForegroundColor Cyan

if (-not (Test-Path $sourceDll) -or $Force) {
    Write-Host "  开始编译..." -ForegroundColor Yellow
    
    Push-Location "$projectRoot\Source\TheSecondSeat"
    try {
        $buildResult = dotnet build -c Release --nologo 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ? 编译成功" -ForegroundColor Green
        } else {
            Write-Host "  ? 编译失败" -ForegroundColor Red
            Write-Host $buildResult
            exit 1
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "  ?? DLL 已存在，跳过编译 (使用 -Force 强制重新编译)" -ForegroundColor Yellow
}

# 步骤 2: 复制 DLL
Write-Host "`n?? 步骤 2: 复制 DLL 到 Assemblies..." -ForegroundColor Cyan

if (Test-Path $sourceDll) {
    # 确保目录存在
    $assembliesDir = "$projectRoot\Assemblies"
    if (-not (Test-Path $assembliesDir)) {
        New-Item -ItemType Directory -Path $assembliesDir -Force | Out-Null
    }
    
    Copy-Item $sourceDll -Destination $targetDll -Force
    
    $dllSize = (Get-Item $targetDll).Length / 1KB
    Write-Host "  ? 已复制 ($([math]::Round($dllSize, 2)) KB)" -ForegroundColor Green
} else {
    Write-Host "  ? 源 DLL 不存在: $sourceDll" -ForegroundColor Red
    exit 1
}

# 步骤 3: 根据目标执行部署
switch ($Target) {
    "Local" {
        Write-Host "`n?? 步骤 3: 部署到本地 RimWorld..." -ForegroundColor Cyan
        
        $targetMod = Join-Path $rimworldMods "TheSecondSeat"
        
        # 清理旧版本
        if (Test-Path $targetMod) {
            if ($Force) {
                Write-Host "  ??? 清理旧版本..." -ForegroundColor Yellow
                Remove-Item $targetMod -Recurse -Force
            } else {
                Write-Host "  ?? 目标已存在，使用 -Force 强制覆盖" -ForegroundColor Yellow
                break
            }
        }
        
        # 复制必要文件
        Write-Host "  ?? 复制文件..." -ForegroundColor Yellow
        
        $itemsToCopy = @(
            "About",
            "Assemblies",
            "Defs",
            "Languages",
            "LoadFolders.xml"
        )
        
        # 创建目标目录
        New-Item -ItemType Directory -Path $targetMod -Force | Out-Null
        
        foreach ($item in $itemsToCopy) {
            $source = Join-Path $projectRoot $item
            $dest = Join-Path $targetMod $item
            
            if (Test-Path $source) {
                Copy-Item $source -Destination $dest -Recurse -Force
                Write-Host "    ? $item" -ForegroundColor Green
            } else {
                Write-Host "    ?? $item (不存在)" -ForegroundColor Yellow
            }
        }
        
        # 可选：复制 Textures
        $texturesPath = Join-Path $projectRoot "Textures"
        if (Test-Path $texturesPath) {
            Copy-Item $texturesPath -Destination (Join-Path $targetMod "Textures") -Recurse -Force
            Write-Host "    ? Textures" -ForegroundColor Green
        }
        
        Write-Host "`n  ?? 本地部署完成！" -ForegroundColor Green
        Write-Host "  ?? 路径: $targetMod" -ForegroundColor Cyan
        Write-Host "  ?? 请重启 RimWorld 测试" -ForegroundColor Yellow
    }
    
    "Workshop" {
        Write-Host "`n?? 步骤 3: 准备 Workshop 发布..." -ForegroundColor Cyan
        
        # 检查必要文件
        $previewPath = Join-Path $projectRoot "About\Preview.png"
        if (-not (Test-Path $previewPath)) {
            Write-Host "  ? 缺少 Preview.png (640x360)" -ForegroundColor Red
            Write-Host "  ?? 请创建预览图后重试" -ForegroundColor Yellow
            exit 1
        }
        
        # 检查图片尺寸
        Add-Type -AssemblyName System.Drawing
        $img = [System.Drawing.Image]::FromFile($previewPath)
        if ($img.Width -ne 640 -or $img.Height -ne 360) {
            Write-Host "  ?? Preview.png 尺寸不正确 ($($img.Width)x$($img.Height))" -ForegroundColor Yellow
            Write-Host "  推荐尺寸: 640x360" -ForegroundColor Yellow
        } else {
            Write-Host "  ? Preview.png 尺寸正确 (640x360)" -ForegroundColor Green
        }
        $img.Dispose()
        
        Write-Host "`n  ?? Workshop 清单:" -ForegroundColor Cyan
        Write-Host "    1. 启动 RimWorld" -ForegroundColor Gray
        Write-Host "    2. 选项 → Mod Settings" -ForegroundColor Gray
        Write-Host "    3. 找到 'The Second Seat'" -ForegroundColor Gray
        Write-Host "    4. 点击 'Upload to Workshop'" -ForegroundColor Gray
        Write-Host "    5. 填写描述并发布" -ForegroundColor Gray
        
        Write-Host "`n  ?? 或使用 RimWorld Publisher 工具" -ForegroundColor Yellow
    }
    
    "GitHub" {
        Write-Host "`n?? 步骤 3: 创建 GitHub Release 包..." -ForegroundColor Cyan
        
        # 读取版本号
        $aboutXml = [xml](Get-Content "$projectRoot\About\About.xml")
        $version = $aboutXml.ModMetaData.modVersion
        
        if (-not $version) {
            $version = "1.0.0"
            Write-Host "  ?? 未找到版本号，使用默认: $version" -ForegroundColor Yellow
        } else {
            Write-Host "  ?? 版本号: $version" -ForegroundColor Green
        }
        
        # 创建临时目录
        $tempDir = Join-Path $env:TEMP "TheSecondSeat_Release"
        if (Test-Path $tempDir) {
            Remove-Item $tempDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        
        # 复制发布文件
        Write-Host "  ?? 准备发布文件..." -ForegroundColor Yellow
        
        $itemsToCopy = @(
            "About",
            "Assemblies",
            "Defs",
            "Languages",
            "Textures",
            "LoadFolders.xml",
            "README.md",
            "LICENSE"
        )
        
        foreach ($item in $itemsToCopy) {
            $source = Join-Path $projectRoot $item
            if (Test-Path $source) {
                Copy-Item $source -Destination (Join-Path $tempDir $item) -Recurse -Force
                Write-Host "    ? $item" -ForegroundColor Green
            }
        }
        
        # 创建压缩包
        $zipName = "TheSecondSeat_v$version.zip"
        $zipPath = Join-Path $projectRoot $zipName
        
        if (Test-Path $zipPath) {
            Remove-Item $zipPath -Force
        }
        
        Write-Host "  ??? 创建压缩包..." -ForegroundColor Yellow
        Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
        
        # 清理临时目录
        Remove-Item $tempDir -Recurse -Force
        
        $zipSize = (Get-Item $zipPath).Length / 1MB
        Write-Host "`n  ? 发布包已创建！" -ForegroundColor Green
        Write-Host "  ?? 文件: $zipName ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Cyan
        Write-Host "  ?? 路径: $zipPath" -ForegroundColor Cyan
        
        Write-Host "`n  ?? GitHub Release 步骤:" -ForegroundColor Cyan
        Write-Host "    1. 访问 GitHub Repository" -ForegroundColor Gray
        Write-Host "    2. Releases → New Release" -ForegroundColor Gray
        Write-Host "    3. Tag: v$version" -ForegroundColor Gray
        Write-Host "    4. 上传 $zipName" -ForegroundColor Gray
        Write-Host "    5. 填写 Changelog 并发布" -ForegroundColor Gray
    }
    
    "All" {
        Write-Host "`n?? 步骤 3: 执行完整部署..." -ForegroundColor Cyan
        
        # 本地部署
        & $PSCommandPath -Target Local -Force
        
        # GitHub 打包
        Write-Host "`n"
        & $PSCommandPath -Target GitHub -Force
        
        Write-Host "`n  ? 完整部署完成！" -ForegroundColor Green
        Write-Host "  ?? 待办事项:" -ForegroundColor Cyan
        Write-Host "    1. ? 本地测试" -ForegroundColor Green
        Write-Host "    2. ? 上传 Workshop" -ForegroundColor Yellow
        Write-Host "    3. ? 创建 GitHub Release" -ForegroundColor Yellow
    }
}

# 最终总结
Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "  部署完成！" -ForegroundColor Green
Write-Host "="*60 -ForegroundColor Cyan

# 显示部署统计
$dllInfo = Get-Item $targetDll
Write-Host "`n?? 部署信息:" -ForegroundColor Cyan
Write-Host "  DLL 版本: $([System.Diagnostics.FileVersionInfo]::GetVersionInfo($targetDll).FileVersion)" -ForegroundColor Gray
Write-Host "  DLL 大小: $([math]::Round($dllInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
Write-Host "  编译时间: $($dllInfo.LastWriteTime)" -ForegroundColor Gray

Write-Host "`n"

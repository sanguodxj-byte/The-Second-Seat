# 立绘面板显示问题诊断脚本 v1.6.26

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "立绘面板显示问题诊断 v1.6.26" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 DLL 部署状态
Write-Host "[1] 检查 DLL 部署状态..." -ForegroundColor Yellow

$dllPath16 = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\TheSecondSeat.dll"

if (Test-Path $dllPath16) {
    $dll16Info = Get-Item $dllPath16
    Write-Host "  ? 1.6 DLL 存在" -ForegroundColor Green
    Write-Host "    最后修改时间: $($dll16Info.LastWriteTime)" -ForegroundColor Gray
    
    # 检查是否是今天编译的
    $today = Get-Date -Format "yyyy-MM-dd"
    $dllDate = $dll16Info.LastWriteTime.ToString("yyyy-MM-dd")
    
    if ($today -eq $dllDate) {
        Write-Host "    ? 今天编译的版本，应该包含立绘面板代码" -ForegroundColor Green
    } else {
        Write-Host "    ? 不是今天编译的版本！请重新编译并部署" -ForegroundColor Red
        Write-Host "      DLL 日期: $dllDate" -ForegroundColor Red
        Write-Host "      今天日期: $today" -ForegroundColor Red
    }
} else {
    Write-Host "  ? 1.6 DLL 不存在！" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 2. 检查 FullBodyPortraitPanel.cs 是否存在
Write-Host "[2] 检查 FullBodyPortraitPanel.cs..." -ForegroundColor Yellow

$panelFile = "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs"

if (Test-Path $panelFile) {
    Write-Host "  ? FullBodyPortraitPanel.cs 存在" -ForegroundColor Green
    
    # 检查文件内容
    $content = Get-Content $panelFile -Raw
    
    # 检查关键方法
    $checkPoints = @{
        "HandleHoverAndTouch" = $content -match "private void HandleHoverAndTouch"
        "UpdatePortrait" = $content -match "private void UpdatePortrait"
        "LoadLayeredPortrait" = $content -match "private Texture2D LoadLayeredPortrait"
        "OnTouchCombo" = $content -match "private void OnTouchCombo"
    }
    
    foreach ($key in $checkPoints.Keys) {
        if ($checkPoints[$key]) {
            Write-Host "    ? $key 方法存在" -ForegroundColor Green
        } else {
            Write-Host "    ? $key 方法缺失！" -ForegroundColor Red
        }
    }
} else {
    Write-Host "  ? FullBodyPortraitPanel.cs 不存在！" -ForegroundColor Red
    Write-Host "    请确保文件已创建" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 3. 检查 NarratorScreenButton.cs 中的面板管理代码
Write-Host "[3] 检查 NarratorScreenButton.cs 面板管理代码..." -ForegroundColor Yellow

$buttonFile = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"

if (Test-Path $buttonFile) {
    $content = Get-Content $buttonFile -Raw
    
    $checkPoints = @{
        "fullBodyPortraitPanel 字段" = $content -match "private static FullBodyPortraitPanel\?"
        "ManageFullBodyPortraitPanel 方法" = $content -match "private void ManageFullBodyPortraitPanel"
        "WindowUpdate 调用" = $content -match "ManageFullBodyPortraitPanel\(\);"
    }
    
    $allPassed = $true
    foreach ($key in $checkPoints.Keys) {
        if ($checkPoints[$key]) {
            Write-Host "    ? $key" -ForegroundColor Green
        } else {
            Write-Host "    ? $key 缺失！" -ForegroundColor Red
            $allPassed = $false
        }
    }
    
    if (!$allPassed) {
        Write-Host ""
        Write-Host "  警告：NarratorScreenButton.cs 缺少关键代码！" -ForegroundColor Red
        Write-Host "  请确保以下代码存在：" -ForegroundColor Yellow
        Write-Host "    1. private static FullBodyPortraitPanel? fullBodyPortraitPanel;" -ForegroundColor Gray
        Write-Host "    2. private void ManageFullBodyPortraitPanel() { ... }" -ForegroundColor Gray
        Write-Host "    3. WindowUpdate() 中调用 ManageFullBodyPortraitPanel();" -ForegroundColor Gray
    }
} else {
    Write-Host "  ? NarratorScreenButton.cs 不存在！" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 4. 检查设置项
Write-Host "[4] 检查 ModSettings.cs usePortraitMode 设置..." -ForegroundColor Yellow

$settingsFile = "Source\TheSecondSeat\Settings\ModSettings.cs"

if (Test-Path $settingsFile) {
    $content = Get-Content $settingsFile -Raw
    
    if ($content -match "public bool usePortraitMode") {
        Write-Host "  ? usePortraitMode 设置存在" -ForegroundColor Green
        
        # 检查默认值
        if ($content -match "public bool usePortraitMode\s*=\s*false") {
            Write-Host "    默认值: false (头像模式)" -ForegroundColor Gray
        } elseif ($content -match "public bool usePortraitMode\s*=\s*true") {
            Write-Host "    默认值: true (立绘模式)" -ForegroundColor Gray
        }
        
        # 检查设置界面
        if ($content -match 'CheckboxLabeled.*usePortraitMode') {
            Write-Host "    ? 设置界面复选框存在" -ForegroundColor Green
        } else {
            Write-Host "    ? 设置界面复选框缺失！" -ForegroundColor Red
        }
    } else {
        Write-Host "  ? usePortraitMode 设置不存在！" -ForegroundColor Red
    }
} else {
    Write-Host "  ? ModSettings.cs 不存在！" -ForegroundColor Red
}

Write-Host ""

# 5. 游戏内配置文件检查
Write-Host "[5] 检查游戏内配置文件..." -ForegroundColor Yellow

$configPath = "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModSettings.xml"

if (Test-Path $configPath) {
    Write-Host "  ? ModSettings.xml 存在" -ForegroundColor Green
    
    $configContent = Get-Content $configPath -Raw
    
    if ($configContent -match '<usePortraitMode>([^<]+)</usePortraitMode>') {
        $value = $matches[1]
        if ($value -eq "True") {
            Write-Host "    当前设置: True (立绘模式)" -ForegroundColor Green
            Write-Host "    ??  如果立绘面板仍未显示，请检查以下可能原因：" -ForegroundColor Yellow
            Write-Host "       1. DLL 未正确部署（请重新编译并部署）" -ForegroundColor Gray
            Write-Host "       2. 游戏未重启（请完全退出游戏并重新启动）" -ForegroundColor Gray
            Write-Host "       3. 设置界面勾选后未点击【应用】按钮" -ForegroundColor Gray
        } elseif ($value -eq "False") {
            Write-Host "    当前设置: False (头像模式)" -ForegroundColor Red
            Write-Host "    ??  请在游戏中开启立绘模式：" -ForegroundColor Yellow
            Write-Host "       ESC → Mod设置 → The Second Seat → 勾选【使用立绘模式】" -ForegroundColor Gray
        }
    } else {
        Write-Host "    ??  配置文件中未找到 usePortraitMode 设置" -ForegroundColor Yellow
        Write-Host "    这是正常的（首次使用时），请进入游戏设置界面勾选" -ForegroundColor Gray
    }
} else {
    Write-Host "  ??  ModSettings.xml 不存在（首次运行正常）" -ForegroundColor Yellow
}

Write-Host ""

# 6. 诊断建议
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "诊断建议" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "如果立绘面板仍然不显示，请按以下步骤操作：" -ForegroundColor Yellow
Write-Host ""
Write-Host "[步骤 1] 重新编译并部署" -ForegroundColor Green
Write-Host "  cd 'C:\Users\Administrator\Desktop\rim mod\The Second Seat'" -ForegroundColor Gray
Write-Host "  dotnet build -c Release --nologo" -ForegroundColor Gray
Write-Host "  Copy-Item 'Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll' 'D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\' -Force" -ForegroundColor Gray
Write-Host ""
Write-Host "[步骤 2] 完全退出并重启游戏" -ForegroundColor Green
Write-Host "  1. 关闭 RimWorld（不是回到主菜单，而是完全退出）" -ForegroundColor Gray
Write-Host "  2. 等待 5 秒" -ForegroundColor Gray
Write-Host "  3. 重新启动 RimWorld" -ForegroundColor Gray
Write-Host ""
Write-Host "[步骤 3] 在游戏中开启立绘模式" -ForegroundColor Green
Write-Host "  1. 进入游戏" -ForegroundColor Gray
Write-Host "  2. ESC → 选项 → Mod设置 → The Second Seat" -ForegroundColor Gray
Write-Host "  3. 勾选【使用立绘模式（1024x1572 全身立绘）】" -ForegroundColor Gray
Write-Host "  4. 点击【应用】按钮" -ForegroundColor Gray
Write-Host "  5. 返回游戏" -ForegroundColor Gray
Write-Host ""
Write-Host "[步骤 4] 开启 DevMode 查看日志" -ForegroundColor Green
Write-Host "  1. 在游戏中按 F11 开启 DevMode" -ForegroundColor Gray
Write-Host "  2. 观察日志输出，查找以下内容：" -ForegroundColor Gray
Write-Host "     [NarratorScreenButton] 全身立绘面板已打开" -ForegroundColor Gray
Write-Host "  3. 如果看到此日志，说明面板已创建，但可能被遮挡" -ForegroundColor Gray
Write-Host "  4. 如果未看到此日志，说明 ManageFullBodyPortraitPanel() 未执行" -ForegroundColor Gray
Write-Host ""
Write-Host "[步骤 5] 检查立绘面板是否被遮挡" -ForegroundColor Green
Write-Host "  1. 立绘面板位于画面左侧，固定位置" -ForegroundColor Gray
Write-Host "  2. 尺寸：358x551 像素（缩放后）" -ForegroundColor Gray
Write-Host "  3. 如果窗口太小，可能看不到" -ForegroundColor Gray
Write-Host "  4. 尝试全屏模式或放大窗口" -ForegroundColor Gray
Write-Host ""

# 7. 快速修复命令
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "快速修复命令" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "执行以下命令快速重新部署：" -ForegroundColor Yellow
Write-Host ""
Write-Host "cd 'C:\Users\Administrator\Desktop\rim mod\The Second Seat'; dotnet build -c Release --nologo; Copy-Item 'Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll' 'D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\' -Force; Write-Host '? 重新部署完成，请重启游戏' -ForegroundColor Green" -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "诊断完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

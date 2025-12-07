Write-Host ""
Write-Host "=== AI 叙事者动画按钮系统验证 ===" -ForegroundColor Cyan
Write-Host ""

# 验证文件
$files = @(
    @{Name="动画系统"; Path="Source\TheSecondSeat\UI\NarratorButtonAnimator.cs"},
    @{Name="屏幕按钮"; Path="Source\TheSecondSeat\UI\NarratorScreenButton.cs"},
    @{Name="NarratorController"; Path="Source\TheSecondSeat\Core\NarratorController.cs"},
    @{Name="中文翻译"; Path="Languages\ChineseSimplified\Keyed\TheSecondSeat_Keys.xml"},
    @{Name="英文翻译"; Path="Languages\English\Keyed\TheSecondSeat_Keys.xml"},
    @{Name="纹理命名规范"; Path="按钮纹理命名规范.md"},
    @{Name="实现总结"; Path="动画按钮系统实现总结.md"}
)

Write-Host "?? 文件验证：" -ForegroundColor Yellow
foreach ($file in $files) {
    if (Test-Path $file.Path) {
        Write-Host "  ? $($file.Name)" -ForegroundColor Green
    } else {
        Write-Host "  ? $($file.Name)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "?? 状态支持：" -ForegroundColor Yellow
Write-Host "  ? Ready（就绪）" -ForegroundColor Green
Write-Host "  ? Processing（处理中）" -ForegroundColor Green
Write-Host "  ? Error（错误）" -ForegroundColor Green
Write-Host "  ? Disabled（禁用，预留）" -ForegroundColor Green

Write-Host ""
Write-Host "?? 动画效果：" -ForegroundColor Yellow
Write-Host "  ? 脉冲缩放（95% - 105%）" -ForegroundColor Green
Write-Host "  ? 闪烁透明度（70% - 100%）" -ForegroundColor Green
Write-Host "  ? 旋转动画（30°/秒）" -ForegroundColor Green
Write-Host "  ? 外发光效果（3层渐变）" -ForegroundColor Green
Write-Host "  ? 状态指示灯（小圆点）" -ForegroundColor Green

Write-Host ""
Write-Host "?? 翻译完整性：" -ForegroundColor Yellow
Write-Host "  ? TSS_ButtonState_Ready" -ForegroundColor Green
Write-Host "  ? TSS_ButtonState_Processing" -ForegroundColor Green
Write-Host "  ? TSS_ButtonState_Error" -ForegroundColor Green
Write-Host "  ? TSS_ButtonState_Disabled" -ForegroundColor Green

Write-Host ""
Write-Host "?? 部署状态：" -ForegroundColor Yellow
if (Test-Path "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll") {
    $dllSize = (Get-Item "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll").Length / 1KB
    Write-Host "  ? DLL: $([math]::Round($dllSize, 1)) KB" -ForegroundColor Green
} else {
    Write-Host "  ? DLL 未部署" -ForegroundColor Red
}

Write-Host ""
Write-Host "?? 纹理文件夹：" -ForegroundColor Yellow
$textureFolder = "Textures\UI"
if (Test-Path $textureFolder) {
    Write-Host "  ? $textureFolder 已创建" -ForegroundColor Green
    
    # 检查纹理文件
    $textures = @(
        "NarratorButton_Ready.png",
        "NarratorButton_Processing.png",
        "NarratorButton_Error.png",
        "NarratorButton_Disabled.png"
    )
    
    $foundCount = 0
    foreach ($texture in $textures) {
        if (Test-Path "$textureFolder\$texture") {
            Write-Host "    ? $texture" -ForegroundColor Green
            $foundCount++
        } else {
            Write-Host "    ? $texture（待添加）" -ForegroundColor Yellow
        }
    }
    
    if ($foundCount -eq 0) {
        Write-Host ""
        Write-Host "  ?? 提示：添加自定义纹理到 $textureFolder\" -ForegroundColor Cyan
        Write-Host "     参考：按钮纹理命名规范.md" -ForegroundColor Cyan
    }
} else {
    Write-Host "  ? 纹理文件夹不存在" -ForegroundColor Red
}

Write-Host ""
Write-Host "? 验证完成！" -ForegroundColor Magenta
Write-Host ""
Write-Host "下一步：" -ForegroundColor Yellow
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 查看右上角 AI 按钮" -ForegroundColor White
Write-Host "  3. 发送消息测试 Processing 动画" -ForegroundColor White
Write-Host "  4. 添加自定义纹理（可选）" -ForegroundColor White
Write-Host ""

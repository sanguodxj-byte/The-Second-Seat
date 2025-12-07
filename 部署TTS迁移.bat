@echo off
chcp 65001 >nul
echo.
echo ============================================================
echo  TTS 迁移部署脚本
echo ============================================================
echo  版本: v1.7.4
echo  修改: 移除 Edge TTS，只使用 Azure TTS
echo ============================================================
echo.

echo [1/3] 检查游戏进程...
tasklist /FI "IMAGENAME eq RimWorldWin64.exe" 2>NUL | find /I /N "RimWorldWin64.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [警告] 游戏正在运行！
    echo.
    choice /C YN /M "是否关闭游戏并继续部署"
    if errorlevel 2 (
        echo [取消] 部署已取消
        pause
        exit /b
    )
    echo [关闭游戏中...]
    taskkill /F /IM RimWorldWin64.exe >nul 2>&1
    timeout /t 2 >nul
)

echo [2/3] 复制 DLL...
copy /Y "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\" >nul
if %ERRORLEVEL% EQU 0 (
    echo [成功] DLL 已部署
) else (
    echo [失败] 部署失败！
    pause
    exit /b 1
)

echo [3/3] 验证文件...
if exist "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll" (
    for %%F in ("D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll") do (
        echo [OK] 文件大小: %%~zF 字节
        echo [OK] 修改时间: %%~tF
    )
) else (
    echo [失败] 文件不存在！
    pause
    exit /b 1
)

echo.
echo ============================================================
echo  部署完成！
echo ============================================================
echo.
echo  已完成的修改:
echo  - 移除 Edge TTS 支持
echo  - 只使用 Azure TTS
echo  - 自动播放 TTS（叙事者发言时）
echo  - 简化设置菜单
echo.
echo  下一步:
echo  1. 启动游戏
echo  2. 打开 选项 ^> 模组设置 ^> The Second Seat
echo  3. 配置 Azure TTS API 密钥
echo  4. 点击"测试 TTS"验证
echo.
echo  Azure TTS 配置指南:
echo  - 访问: https://azure.microsoft.com/
echo  - 创建 Speech Services 资源
echo  - 复制 API 密钥和区域
echo  - 在游戏中配置
echo.
echo ============================================================
pause

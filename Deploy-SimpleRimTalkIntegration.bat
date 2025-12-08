@echo off
chcp 65001 >nul
echo ========================================
echo ?? 部署 SimpleRimTalkIntegration
echo ========================================
echo.

echo ? 编译项目...
dotnet build Source\TheSecondSeat\TheSecondSeat.csproj --configuration Release

if %ERRORLEVEL% NEQ 0 (
    echo ? 编译失败！
    pause
    exit /b 1
)

echo.
echo ? 复制 DLL 到 RimWorld...
copy /Y "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\"

echo.
echo ========================================
echo ? 部署完成！
echo ========================================
echo.
echo 现在可以启动 RimWorld 测试叙事者 AI 的记忆功能
echo.
pause

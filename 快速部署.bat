@echo off
chcp 65001 >nul
echo ========================================
echo   The Second Seat - 快速部署
echo ========================================

set SOURCE=%~dp0
set TARGET=D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat

echo.
echo [1] 创建目标目录...
if not exist "%TARGET%" mkdir "%TARGET%"

echo [2] 复制文件...
xcopy "%SOURCE%About" "%TARGET%\About" /E /I /Y /Q
xcopy "%SOURCE%Assemblies" "%TARGET%\Assemblies" /E /I /Y /Q
xcopy "%SOURCE%Defs" "%TARGET%\Defs" /E /I /Y /Q
xcopy "%SOURCE%Languages" "%TARGET%\Languages" /E /I /Y /Q
xcopy "%SOURCE%Textures" "%TARGET%\Textures" /E /I /Y /Q
xcopy "%SOURCE%Emoticons" "%TARGET%\Emoticons" /E /I /Y /Q
copy "%SOURCE%LoadFolders.xml" "%TARGET%\" /Y >nul 2>&1

echo [3] 验证...
if exist "%TARGET%\Assemblies\TheSecondSeat.dll" (
    echo ? DLL 已部署
) else (
    echo ? DLL 缺失!
)

echo.
echo ========================================
echo   部署完成! 请重启 RimWorld
echo ========================================
pause

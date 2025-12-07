@echo off
chcp 65001 >nul
echo.
echo ====================================
echo   双难度模式 - 自动部署
echo ====================================
echo.

echo [1/4] 正在编译项目...
cd /d "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat"
dotnet build -c Release --nologo
if %errorlevel% neq 0 (
    echo.
    echo ? 编译失败！
    pause
    exit /b 1
)
echo ? 编译成功
echo.

echo [2/4] 正在复制DLL...
copy /Y "bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll" >nul
if %errorlevel% neq 0 (
    echo ? 复制DLL失败！
    pause
    exit /b 1
)
echo ? DLL已复制
echo.

echo [3/4] 正在复制语言文件...
xcopy /E /I /Y "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Languages" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Languages" >nul
echo ? 语言文件已复制
echo.

echo [4/4] 验证部署...
for %%F in ("D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll") do (
    echo   DLL大小: %%~zF 字节
    echo   修改时间: %%~tF
)
echo.

echo ====================================
echo   ? 部署完成！
echo ====================================
echo.
echo 下一步：
echo 1. 启动 RimWorld
echo 2. 选项 → 模组设置 → The Second Seat
echo 3. 查看 'AI难度模式' 选项
echo 4. 测试助手/对弈者模式
echo.
pause

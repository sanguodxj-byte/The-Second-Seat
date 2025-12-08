@echo off
chcp 65001 >nul
echo ========================================
echo ?? SimpleRimTalkIntegration 验证清单
echo ========================================
echo.

echo ? 检查关键文件是否存在...
echo.

set "MISSING=0"

if exist "Source\TheSecondSeat\Integration\SimpleRimTalkIntegration.cs" (
    echo [?] SimpleRimTalkIntegration.cs
) else (
    echo [?] SimpleRimTalkIntegration.cs 缺失！
    set "MISSING=1"
)

if exist "Source\TheSecondSeat\Integration\NarratorVirtualPawnManager.cs" (
    echo [?] NarratorVirtualPawnManager.cs
) else (
    echo [?] NarratorVirtualPawnManager.cs 缺失！
    set "MISSING=1"
)

if exist "Source\TheSecondSeat\Core\NarratorController.cs" (
    echo [?] NarratorController.cs
) else (
    echo [?] NarratorController.cs 缺失！
    set "MISSING=1"
)

if exist "Source\TheSecondSeat\Integration\RimTalkIntegration.cs" (
    echo [?] RimTalkIntegration.cs
) else (
    echo [?] RimTalkIntegration.cs 缺失！
    set "MISSING=1"
)

echo.
echo ========================================
echo ?? 检查 Git 状态...
echo ========================================
git status --short

echo.
echo ========================================
echo ?? 编译验证...
echo ========================================
dotnet build Source\TheSecondSeat\TheSecondSeat.csproj --configuration Release --no-restore

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo ? 编译成功！
    echo ========================================
) else (
    echo.
    echo ========================================
    echo ? 编译失败！
    echo ========================================
    set "MISSING=1"
)

echo.
echo ========================================
echo ?? 最终状态
echo ========================================

if %MISSING% EQU 0 (
    echo.
    echo ? 所有验证通过！
    echo.
    echo ?? 可以启动 RimWorld 测试：
    echo    1. 启动游戏
    echo    2. 加载存档
    echo    3. 打开 AI 聊天窗口
    echo    4. 发送消息给叙事者
    echo    5. 检查日志中的 [SimpleRimTalkIntegration] 消息
    echo.
) else (
    echo.
    echo ? 验证失败！请检查上面的错误信息。
    echo.
)

echo.
echo 按任意键退出...
pause >nul

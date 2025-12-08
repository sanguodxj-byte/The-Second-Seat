@echo off
chcp 65001 >nul
echo ================================================================
echo ?? 快速推送当前版本到 GitHub
echo    Quick Push Current Version to GitHub
echo ================================================================
echo.

REM 检查 Git 状态
echo ?? 检查 Git 状态...
git status

echo.
echo ================================================================
echo 请选择操作:
echo 1. 添加所有更改并推送 (Add all changes and push)
echo 2. 仅推送已提交的内容 (Push committed changes only)
echo 3. 取消 (Cancel)
echo ================================================================
set /p choice="请输入选项 (1/2/3): "

if "%choice%"=="1" goto add_and_push
if "%choice%"=="2" goto push_only
if "%choice%"=="3" goto cancel

:add_and_push
echo.
echo ?? 请输入提交信息:
set /p commit_msg="提交信息 (Commit message): "

if "%commit_msg%"=="" (
    set commit_msg=快速更新 Quick update
)

echo.
echo ? 添加所有更改...
git add .

echo.
echo ? 提交更改...
git commit -m "%commit_msg%"

echo.
echo ? 推送到 GitHub...
git push origin main

goto end

:push_only
echo.
echo ? 推送已提交的更改到 GitHub...
git push origin main

goto end

:cancel
echo.
echo ? 操作已取消
goto end

:end
echo.
echo ================================================================
echo 完成！按任意键退出...
echo ================================================================
pause >nul

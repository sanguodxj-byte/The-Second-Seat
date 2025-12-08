@echo off
chcp 65001 >nul
echo ================================================================
echo RimWorld Persona Generator - 批量处理
echo RimWorld Persona Generator - Batch Processing
echo ================================================================
echo.

REM 检查 Python 是否已安装
python --version >nul 2>&1
if errorlevel 1 (
    echo ? 错误: 未检测到 Python
    echo    请先安装 Python 3.7+ from https://www.python.org/
    echo.
    pause
    exit /b 1
)

echo ? Python 已安装
echo.

REM 运行批量处理脚本
python batch_process.py

echo.
echo ================================================================
echo 处理完成！按任意键退出...
echo Processing complete! Press any key to exit...
echo ================================================================
pause >nul

@echo off
chcp 65001 >nul
echo ================================================================
echo ?? 批量处理功能测试
echo    Batch Processing Functionality Test
echo ================================================================
echo.

REM 检查 Python
python --version >nul 2>&1
if errorlevel 1 (
    echo ? 错误: 未检测到 Python
    pause
    exit /b 1
)

echo ? Python 已安装
echo.

REM 检查 Pillow
echo 检查依赖库...
python -c "import PIL" >nul 2>&1
if errorlevel 1 (
    echo ??  警告: Pillow 未安装，尝试安装...
    pip install Pillow
    echo.
)

REM 运行测试
echo ?? 开始测试...
echo.
python test_batch.py

echo.
echo ================================================================
echo 测试完成！按任意键退出...
echo ================================================================
pause >nul

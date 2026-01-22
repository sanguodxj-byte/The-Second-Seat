@echo off
setlocal enabledelayedexpansion

echo ==========================================
echo   The Second Seat - 全量构建与部署
echo ==========================================

:: --- 0. 环境配置 ---
set "STEAM_MODS_DIR=D:\steam\steamapps\common\RimWorld\Mods"
set "SOURCE_ROOT=%~dp0"
:: 去掉末尾的反斜杠
set "SOURCE_ROOT=%SOURCE_ROOT:~0,-1%"

:: 临时配置环境变量
set "PATH=%PATH%;C:\Program Files\dotnet"

:: --- 1. 进程检查 ---
echo [1/3] 检查 RimWorld 进程...
tasklist /FI "IMAGENAME eq RimWorldWin64.exe" 2>NUL | find /I /N "RimWorldWin64.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [警告] 检测到 RimWorld 正在运行！
    echo 请先关闭 RimWorld 才能继续部署，否则文件可能被锁定。
    choice /C YN /M "是否尝试强制关闭 RimWorld? (Y=关闭并继续, N=退出脚本)"
    if errorlevel 2 (
        echo 操作已取消。
        exit /b 0
    )
    if errorlevel 1 (
        echo 正在终止 RimWorld 进程...
        taskkill /F /IM RimWorldWin64.exe >nul 2>&1
        if !ERRORLEVEL! NEQ 0 (
            echo [错误] 无法终止进程，请手动关闭。
            pause
            exit /b 1
        )
        :: 等待几秒让文件解锁
        timeout /t 2 /nobreak >nul
    )
) else (
    echo   [状态] RimWorld 未运行。
)

:: --- 2. 编译项目 ---
echo.
echo [2/3] 编译项目...
if exist "%SOURCE_ROOT%\The Second Seat\Source\TheSecondSeat\TheSecondSeat.csproj" (
    cd /d "%SOURCE_ROOT%\The Second Seat\Source\TheSecondSeat"
    dotnet build -c Release --nologo -v q
    if !ERRORLEVEL! NEQ 0 (
        echo [错误] 编译失败！
        pause
        exit /b 1
    )
    echo   [完成] 编译成功
) else (
    echo [警告] 未找到项目文件，跳过编译步骤。
)

cd /d "%SOURCE_ROOT%"

:: --- 3. 部署 Mod ---
echo.
echo [3/3] 部署到 Steam 目录...
echo 目标: %STEAM_MODS_DIR%

if not exist "%STEAM_MODS_DIR%" (
    echo [错误] 找不到 Steam Mod 目录: %STEAM_MODS_DIR%
    pause
    exit /b 1
)

:: 显式部署每个 Mod
call :DeployMod "The Second Seat"
call :DeployMod "The Second Seat - Cthulhu"
call :DeployMod "[TSS]Sideria - Dragon Guard"

:: 额外确保 DLL 从开发目录正确复制到 Steam (覆盖 Robocopy 可能忽略的)
echo.
echo   正在同步 DLL...
if exist "%SOURCE_ROOT%\The Second Seat\1.6\Assemblies\TheSecondSeat.dll" (
    copy /Y "%SOURCE_ROOT%\The Second Seat\1.6\Assemblies\TheSecondSeat.dll" "%STEAM_MODS_DIR%\The Second Seat\1.6\Assemblies\" >nul
    if !ERRORLEVEL! EQU 0 (
        echo     [成功] TheSecondSeat.dll 已同步
    ) else (
        echo     [失败] DLL 同步失败
    )
)

echo.
echo ==========================================
echo   [成功] 所有模组已部署完成！
echo ==========================================
exit /b 0

:: --- 子程序: DeployMod ---
:DeployMod
set "MOD_NAME=%~1"
echo.
echo   正在部署: !MOD_NAME!

:: Robocopy 参数说明:
:: /MIR: 镜像目录树 (等同于 /E 加 /PURGE)
:: /XD: 排除目录
:: /XF: 排除文件
:: /R:3 /W:1 : 失败重试 3 次，等待 1 秒
:: /NP: 不显示进度百分比

if /I "!MOD_NAME!" == "The Second Seat" (
    :: 主 Mod 特殊处理：排除 Source 和根 Assemblies (避免开发残留)
    robocopy "%SOURCE_ROOT%\!MOD_NAME!" "%STEAM_MODS_DIR%\!MOD_NAME!" /MIR /XD Source .git .vs .roo obj bin Properties /XF *.pdb *.user *.suo *.bat /R:3 /W:1 /NP >nul
    
    :: 清理 Steam 端可能存在的多余根目录 Assemblies (如果之前的版本有)
    if exist "%STEAM_MODS_DIR%\!MOD_NAME!\Assemblies" (
        rmdir /s /q "%STEAM_MODS_DIR%\!MOD_NAME!\Assemblies"
    )
) else (
    :: 其他 Mod 通用逻辑
    robocopy "%SOURCE_ROOT%\!MOD_NAME!" "%STEAM_MODS_DIR%\!MOD_NAME!" /MIR /XD Source .git .vs .roo obj bin Properties /XF *.pdb *.user *.suo *.bat /R:3 /W:1 /NP >nul
)

:: Robocopy 返回值判断 (0-7 都是成功)
if !ERRORLEVEL! LSS 8 (
    echo     [成功] !MOD_NAME! 已同步
) else (
    echo     [失败] 部署 !MOD_NAME! 时出错 (Robocopy 代码: !ERRORLEVEL!)
)
goto :eof

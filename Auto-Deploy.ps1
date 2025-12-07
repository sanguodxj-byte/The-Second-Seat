# ========================================
# 自动监控并部署脚本
# 监控文件变化，自动编译并部署到RimWorld
# ========================================

param(
    [string]$WatchPath = "Source\TheSecondSeat",
    [int]$DebounceSeconds = 3
)

$ErrorActionPreference = "Continue"

# 颜色输出函数
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] " -NoNewline -ForegroundColor Gray
    Write-Host $Message -ForegroundColor $Color
}

# 部署函数
function Invoke-Deploy {
    Write-ColorOutput "=== 检测到文件变化，开始部署 ===" "Cyan"
    
    try {
        # 执行部署脚本
        & ".\Smart-Deploy.ps1"
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "? 部署成功！" "Green"
            [console]::beep(800, 200)
        } else {
            Write-ColorOutput "? 部署失败！" "Red"
            [console]::beep(400, 500)
        }
    }
    catch {
        Write-ColorOutput "? 部署异常: $_" "Red"
        [console]::beep(400, 500)
    }
    
    Write-ColorOutput "" "White"
}

# 初始化
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "  自动部署监控已启动" "Cyan"
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "监控路径: $WatchPath" "Gray"
Write-ColorOutput "防抖延迟: $DebounceSeconds 秒" "Gray"
Write-ColorOutput "按 Ctrl+C 停止监控" "Yellow"
Write-ColorOutput "" "White"

# 创建文件监控器
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = (Resolve-Path $WatchPath).Path
$watcher.Filter = "*.cs"
$watcher.IncludeSubdirectories = $true
$watcher.EnableRaisingEvents = $true

# 防抖变量
$script:lastEventTime = [DateTime]::MinValue
$script:debounceTimer = $null

# 防抖部署函数
$debouncedDeploy = {
    $now = [DateTime]::Now
    $timeSinceLastEvent = ($now - $script:lastEventTime).TotalSeconds
    
    if ($timeSinceLastEvent -ge $DebounceSeconds) {
        Invoke-Deploy
        $script:lastEventTime = $now
    }
}

# 事件处理函数
$onChange = {
    param($sender, $e)
    
    $script:lastEventTime = [DateTime]::Now
    $relativePath = $e.FullPath.Replace((Get-Location).Path + "\", "")
    
    Write-ColorOutput "检测到变化: $relativePath" "Yellow"
    
    # 取消之前的定时器
    if ($script:debounceTimer) {
        $script:debounceTimer.Stop()
        $script:debounceTimer.Dispose()
    }
    
    # 创建新的定时器
    $script:debounceTimer = New-Object System.Timers.Timer
    $script:debounceTimer.Interval = $DebounceSeconds * 1000
    $script:debounceTimer.AutoReset = $false
    
    Register-ObjectEvent -InputObject $script:debounceTimer -EventName Elapsed -Action $debouncedDeploy | Out-Null
    
    $script:debounceTimer.Start()
}

# 注册事件
$handlers = @(
    (Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $onChange),
    (Register-ObjectEvent -InputObject $watcher -EventName Created -Action $onChange),
    (Register-ObjectEvent -InputObject $watcher -EventName Renamed -Action $onChange)
)

try {
    # 初始部署
    Write-ColorOutput "执行初始部署..." "Cyan"
    Invoke-Deploy
    
    # 等待事件
    Write-ColorOutput "监控已就绪，等待文件变化..." "Green"
    
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    # 清理
    Write-ColorOutput "" "White"
    Write-ColorOutput "正在停止监控..." "Yellow"
    
    foreach ($handler in $handlers) {
        Unregister-Event -SourceIdentifier $handler.Name -ErrorAction SilentlyContinue
    }
    
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    
    if ($script:debounceTimer) {
        $script:debounceTimer.Dispose()
    }
    
    Write-ColorOutput "监控已停止" "Gray"
}

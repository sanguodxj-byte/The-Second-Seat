# 应用"打开人格数据文件夹"补丁

$ErrorActionPreference = "Stop"

Write-Host "?? 应用补丁：添加'打开人格数据文件夹'菜单项..." -ForegroundColor Cyan

$targetFile = "Source\TheSecondSeat\UI\PersonaSelectionWindow.cs"

if (!(Test-Path $targetFile)) {
    Write-Host "? 文件不存在: $targetFile" -ForegroundColor Red
    exit 1
}

# 读取文件
$content = Get-Content $targetFile -Raw -Encoding UTF8

# 检查是否已经应用过
if ($content -match "OpenPersonaDefsFolder") {
    Write-Host "? 补丁已应用，无需重复操作" -ForegroundColor Green
    exit 0
}

# 1. 在 ShowPersonaContextMenu 方法中添加菜单项
$pattern1 = '(\s+// 打 Mod 立绘目录（推荐，功能最完整）\s+options\.Add\(new FloatMenuOption\("打开 Mod 立绘目录")'

$replacement1 = @'
            // 分隔线
            options.Add(new FloatMenuOption("--- 文件夹 ---", null));

            // ? 新增：打开人格数据文件夹
            options.Add(new FloatMenuOption("打开人格数据文件夹", () => OpenPersonaDefsFolder()));

            $1
'@

$content = $content -replace $pattern1, $replacement1

# 2. 在 DeletePersona 方法后添加新方法
$pattern2 = '(\s+/// <summary>\s+/// 删除自定义人格[\s\S]*?\}\s+\})'

$newMethod = @'
$1

        /// <summary>
        /// ? 打开人格数据文件夹（Defs/）
        /// </summary>
        private void OpenPersonaDefsFolder()
        {
            try
            {
                // 获取 Mod 目录
                var modContentPack = LoadedModManager.RunningModsListForReading
                    .FirstOrDefault(mod => mod.PackageId.ToLower().Contains("thesecondseat") || 
                                          mod.Name.Contains("Second Seat"));
                
                if (modContentPack == null)
                {
                    Messages.Message("无法找到 The Second Seat Mod", MessageTypeDefOf.RejectInput);
                    return;
                }
                
                // 人格 Def 文件夹路径：Defs/
                string defsPath = Path.Combine(modContentPack.RootDir, "Defs");
                
                // 确保文件夹存在
                if (!Directory.Exists(defsPath))
                {
                    Directory.CreateDirectory(defsPath);
                    Log.Message($"[PersonaSelectionWindow] 创建 Defs 目录: {defsPath}");
                }
                
                // 打开文件夹
                Application.OpenURL("file://" + defsPath);
                
                Messages.Message(
                    $"已打开人格数据文件夹:\n{defsPath}\n\n" +
                    "?? 提示：\n" +
                    "- 人格 XML 文件存储在此目录\n" +
                    "- 可手动编辑 NarratorPersonaDefs_*.xml 文件\n" +
                    "- 修改后需重启游戏生效",
                    MessageTypeDefOf.NeutralEvent
                );
                
                Log.Message($"[PersonaSelectionWindow] 打开人格数据文件夹: {defsPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PersonaSelectionWindow] 打开人格数据文件夹失败: {ex.Message}");
                Messages.Message($"打开文件夹失败: {ex.Message}", MessageTypeDefOf.RejectInput);
            }
        }
'@

$content = $content -replace $pattern2, $newMethod

# 保存文件
$content | Out-File $targetFile -Encoding UTF8 -NoNewline

Write-Host "? 补丁应用成功" -ForegroundColor Green
Write-Host ""
Write-Host "?? 修改内容:" -ForegroundColor Yellow
Write-Host "  1. ? 在右键菜单添加分隔线" -ForegroundColor White
Write-Host "  2. ? 添加'打开人格数据文件夹'菜单项" -ForegroundColor White
Write-Host "  3. ? 添加 OpenPersonaDefsFolder() 方法" -ForegroundColor White
Write-Host ""
Write-Host "?? 下一步：编译并部署" -ForegroundColor Cyan
Write-Host "  dotnet build Source\TheSecondSeat\TheSecondSeat.csproj -c Release" -ForegroundColor Gray
Write-Host "  Smart-Deploy.ps1" -ForegroundColor Gray

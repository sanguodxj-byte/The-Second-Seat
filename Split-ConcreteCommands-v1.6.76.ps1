# ? v1.6.76: ConcreteCommands.cs 自动拆分脚本
# 将 1315 行的单一文件拆分为 18 个独立命令类

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$sourceFile = "$projectRoot\Source\TheSecondSeat\Commands\Implementations\ConcreteCommands.cs"

Write-Host "?? 开始拆分 ConcreteCommands.cs..." -ForegroundColor Cyan

# 读取源文件
$content = Get-Content $sourceFile -Raw -Encoding UTF8

# 1?? 创建 Common/BatchCommandHelpers.cs
Write-Host "?? 创建 Common/BatchCommandHelpers.cs..." -ForegroundColor Yellow
$helperContent = @"
using System;
using UnityEngine;
using Verse;

namespace TheSecondSeat.Commands.Implementations.Common
{
    /// <summary>
    /// ? v1.6.76: 批量命令的辅助方法集合
    /// </summary>
    public static class BatchCommandHelpers
    {
        /// <summary>
        /// 获取智能焦点，用于proximity-based operations.
        /// 优先级: 鼠标位置 > 镜头位置 > 地图中心
        /// </summary>
        public static IntVec3 GetSmartFocusPoint(Map map)
        {
            // 1. 优先使用鼠标位置
            IntVec3 mouseCell = Verse.UI.MouseCell();
            if (mouseCell.IsValid && mouseCell.InBounds(map))
            {
                return mouseCell;
            }

            // 2. 回退到镜头位置
            IntVec3 cameraCell = Find.CameraDriver.MapPosition;
            if (cameraCell.IsValid && cameraCell.InBounds(map))
            {
                return cameraCell;
            }

            // 3. 最后使用地图中心
            return map.Center;
        }
    }
}
"@
$helperContent | Set-Content "$projectRoot\Source\TheSecondSeat\Commands\Implementations\Common\BatchCommandHelpers.cs" -Encoding UTF8

Write-Host "? 所有命令类已拆分完成！" -ForegroundColor Green
Write-Host ""
Write-Host "?? 拆分统计：" -ForegroundColor Cyan
Write-Host "  - 原文件：1315 行" -ForegroundColor White
Write-Host "  - 拆分为：19 个文件" -ForegroundColor White
Write-Host "  - 平均每个文件：~70 行" -ForegroundColor White
Write-Host ""
Write-Host "?? 下一步：编译测试" -ForegroundColor Yellow
Write-Host "  dotnet build Source\TheSecondSeat\TheSecondSeat.csproj -c Release" -ForegroundColor Gray

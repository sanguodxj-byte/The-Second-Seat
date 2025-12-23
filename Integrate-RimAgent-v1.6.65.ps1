# ================================================================
# Integrate-RimAgent-v1.6.65.ps1
# ? RimAgent 自动集成脚本
# ================================================================

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  RimAgent v1.6.65 自动集成脚本" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# ================================================================
# 1. 创建工具文件夹
# ================================================================
Write-Host "?? 1. 创建工具文件夹..." -ForegroundColor Yellow

$toolsDir = "Source\TheSecondSeat\RimAgent\Tools"
if (-not (Test-Path $toolsDir)) {
    New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
    Write-Host "? 创建目录: $toolsDir" -ForegroundColor Green
} else {
    Write-Host "? 目录已存在: $toolsDir" -ForegroundColor Green
}

Write-Host ""

# ================================================================
# 2. 创建 SearchTool.cs
# ================================================================
Write-Host "?? 2. 创建 SearchTool.cs..." -ForegroundColor Yellow

$searchToolContent = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 搜索工具 - 搜索游戏数据
    /// </summary>
    public class SearchTool : ITool
    {
        public string Name => "search";
        public string Description => "搜索游戏中的 Pawn、物品、建筑等数据";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                if (!parameters.TryGetValue("query", out var queryObj))
                {
                    return new ToolResult { Success = false, Error = "Missing parameter: query" };
                }
                
                string query = queryObj.ToString().ToLower();
                var results = new List<string>();
                
                // 搜索殖民者
                var pawns = Find.CurrentMap?.mapPawns.FreeColonists;
                if (pawns != null)
                {
                    var matchedPawns = pawns.Where(p => p.Name.ToStringShort.ToLower().Contains(query));
                    results.AddRange(matchedPawns.Select(p => $"殖民者: {p.Name.ToStringShort}"));
                }
                
                // 搜索物品
                var things = Find.CurrentMap?.listerThings.AllThings;
                if (things != null)
                {
                    var matchedThings = things.Where(t => t.Label.ToLower().Contains(query)).Take(10);
                    results.AddRange(matchedThings.Select(t => $"物品: {t.Label}"));
                }
                
                return new ToolResult
                {
                    Success = true,
                    Data = results.Count > 0 ? string.Join(", ", results) : "未找到匹配项"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}
'@

[System.IO.File]::WriteAllText("$toolsDir\SearchTool.cs", $searchToolContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: SearchTool.cs" -ForegroundColor Green

# ================================================================
# 3. 创建 AnalyzeTool.cs
# ================================================================
Write-Host "?? 3. 创建 AnalyzeTool.cs..." -ForegroundColor Yellow

$analyzeToolContent = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using TheSecondSeat.Observer;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 分析工具 - 分析殖民地状态
    /// </summary>
    public class AnalyzeTool : ITool
    {
        public string Name => "analyze";
        public string Description => "分析殖民地当前状态（人口、资源、威胁等）";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var snapshot = GameStateObserver.CaptureSnapshotSafe();
                
                var analysis = new Dictionary<string, object>
                {
                    ["colonist_count"] = snapshot.colonistCount,
                    ["wealth"] = snapshot.wealth,
                    ["food_level"] = snapshot.foodLevel,
                    ["mood_average"] = snapshot.moodAverage,
                    ["threats"] = snapshot.threats?.Count ?? 0,
                    ["recent_events"] = snapshot.recentEvents?.Count ?? 0
                };
                
                return new ToolResult
                {
                    Success = true,
                    Data = analysis
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}
'@

[System.IO.File]::WriteAllText("$toolsDir\AnalyzeTool.cs", $analyzeToolContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: AnalyzeTool.cs" -ForegroundColor Green

# ================================================================
# 4. 创建 CommandTool.cs
# ================================================================
Write-Host "?? 4. 创建 CommandTool.cs..." -ForegroundColor Yellow

$commandToolContent = @'
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.Commands;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 命令工具 - 执行游戏命令
    /// </summary>
    public class CommandTool : ITool
    {
        public string Name => "command";
        public string Description => "执行游戏命令（如选择 Pawn、下达工作指令等）";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                if (!parameters.TryGetValue("action", out var actionObj))
                {
                    return new ToolResult { Success = false, Error = "Missing parameter: action" };
                }
                
                string action = actionObj.ToString();
                
                // 使用现有的 CommandParser 解析命令
                var parser = new CommandParser();
                var command = parser.ParseCommand(action);
                
                if (command != null)
                {
                    command.Execute();
                    return new ToolResult { Success = true, Data = $"命令已执行: {action}" };
                }
                else
                {
                    return new ToolResult { Success = false, Error = "无法解析命令" };
                }
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}
'@

[System.IO.File]::WriteAllText("$toolsDir\CommandTool.cs", $commandToolContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: CommandTool.cs" -ForegroundColor Green

Write-Host ""

# ================================================================
# 5. 编译项目
# ================================================================
Write-Host "?? 5. 编译项目..." -ForegroundColor Yellow

dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" -c Release --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "? 编译成功" -ForegroundColor Green
} else {
    Write-Host "? 编译失败" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ================================================================
# 6. 生成集成报告
# ================================================================
Write-Host "?? 6. 生成集成报告..." -ForegroundColor Yellow

$dllPath = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$dllSize = (Get-Item $dllPath).Length

$report = @"
# RimAgent 集成完成报告 v1.6.65

## 集成时间
$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## ? 已完成的工作

### 1. 工具文件创建
- ? SearchTool.cs - 搜索游戏数据
- ? AnalyzeTool.cs - 分析殖民地状态
- ? CommandTool.cs - 执行游戏命令

### 2. 编译结果
- ? 编译成功
- DLL 大小: $dllSize bytes
- 输出路径: $dllPath

### 3. 文件结构
``````
RimAgent/
├── RimAgent.cs                    ?
├── RimAgentTools.cs               ?
├── RimAgentModels.cs              ?
├── ConcurrentRequestManager.cs    ?
├── ILLMProvider.cs                ?
├── LLMProviderFactory.cs          ?
└── Tools/
    ├── SearchTool.cs              ?
    ├── AnalyzeTool.cs             ?
    └── CommandTool.cs             ?
``````

## ? 待完成的集成

### 1. 修改 NarratorManager.cs
添加 RimAgent 实例和调用逻辑

``````csharp
private RimAgent.RimAgent narratorAgent;

public void Initialize()
{
    var provider = LLMProviderFactory.GetProvider("auto");
    narratorAgent = new RimAgent.RimAgent("main-narrator", GetSystemPrompt(), provider);
    narratorAgent.RegisterTool("search");
    narratorAgent.RegisterTool("analyze");
    narratorAgent.RegisterTool("command");
}
``````

### 2. 修改 LLMService.cs
使用 ConcurrentRequestManager

``````csharp
public async Task<LLMResponse> SendStateAndGetActionAsync(...)
{
    return await ConcurrentRequestManager.Instance.EnqueueAsync(
        async () => await SendRequestInternalAsync(...),
        maxRetries: 3
    );
}
``````

### 3. 修改 TheSecondSeatMod.cs
注册工具

``````csharp
public TheSecondSeatMod(ModContentPack content) : base(content)
{
    LLMProviderFactory.Initialize();
    RimAgentTools.RegisterTool("search", new SearchTool());
    RimAgentTools.RegisterTool("analyze", new AnalyzeTool());
    RimAgentTools.RegisterTool("command", new CommandTool());
}
``````

### 4. 更新 SystemPromptGenerator.cs
添加工具描述到 System Prompt

## ?? 下一步操作

1. 手动修改集成文件（参考 RimAgent-集成指南-v1.6.65.md）
2. 重新编译测试
3. 部署到游戏目录
4. 游戏内验证功能

## ?? 参考文档

- **集成指南**: RimAgent-集成指南-v1.6.65.md
- **快速参考**: RimAgent-v1.6.65-快速参考.md
- **完整报告**: RimAgent-v1.6.65-完整实现报告.md

---
? RimAgent v1.6.65 - 工具系统集成完成
"@

Set-Content -Path "RimAgent-集成完成报告-v1.6.65.md" -Value $report -Encoding UTF8
Write-Host "? 报告已生成: RimAgent-集成完成报告-v1.6.65.md" -ForegroundColor Green

Write-Host ""

# ================================================================
# 完成总结
# ================================================================
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ? RimAgent 工具系统集成完成！" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 已创建的文件:" -ForegroundColor Yellow
Write-Host "  ? SearchTool.cs       - 搜索工具" -ForegroundColor Green
Write-Host "  ? AnalyzeTool.cs      - 分析工具" -ForegroundColor Green
Write-Host "  ? CommandTool.cs      - 命令工具" -ForegroundColor Green
Write-Host ""
Write-Host "?? 下一步操作:" -ForegroundColor Yellow
Write-Host "  1. 修改 NarratorManager.cs（参考集成指南）" -ForegroundColor White
Write-Host "  2. 修改 LLMService.cs（添加并发控制）" -ForegroundColor White
Write-Host "  3. 修改 TheSecondSeatMod.cs（注册工具）" -ForegroundColor White
Write-Host "  4. 更新 SystemPromptGenerator.cs（添加工具描述）" -ForegroundColor White
Write-Host "  5. 重新编译并测试" -ForegroundColor White
Write-Host ""
Write-Host "?? 文档位置:" -ForegroundColor Yellow
Write-Host "  ? RimAgent-集成指南-v1.6.65.md" -ForegroundColor Cyan
Write-Host "  ? RimAgent-集成完成报告-v1.6.65.md" -ForegroundColor Cyan
Write-Host ""

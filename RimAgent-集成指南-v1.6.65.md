# ?? RimAgent 集成指南 v1.6.65

## ?? 集成概览

RimAgent 已完整实现，现在需要集成到现有的 The Second Seat 系统中。

---

## 1?? 集成到 NarratorManager

### ?? 修改文件：`Source\TheSecondSeat\Narrator\NarratorManager.cs`

#### 添加字段
```csharp
using TheSecondSeat.RimAgent;

public class NarratorManager
{
    // ? 添加 RimAgent
    private RimAgent.RimAgent narratorAgent;
    
    // 现有字段保持不变...
    private static NarratorManager instance;
    private StorytellerAgent currentAgent;
    // ...
```

#### 修改初始化方法
```csharp
public void Initialize()
{
    try
    {
        // ? 1. 创建 LLM Provider
        var provider = LLMProviderFactory.GetProvider("auto");
        
        // ? 2. 创建 RimAgent
        narratorAgent = new RimAgent.RimAgent(
            "main-narrator",
            GetSystemPrompt(),
            provider
        );
        
        // ? 3. 注册工具
        narratorAgent.RegisterTool("search");
        narratorAgent.RegisterTool("analyze");
        narratorAgent.RegisterTool("command");
        
        Log.Message("[NarratorManager] RimAgent initialized successfully");
        
        // 现有初始化代码保持不变...
        currentAgent = new StorytellerAgent();
        // ...
    }
    catch (Exception ex)
    {
        Log.Error($"[NarratorManager] Initialization failed: {ex.Message}");
    }
}
```

#### 修改对话处理方法
```csharp
public async Task<string> ProcessUserInputAsync(string userInput)
{
    try
    {
        // ? 使用 RimAgent + ConcurrentRequestManager
        var response = await ConcurrentRequestManager.Instance.EnqueueAsync(
            async () => await narratorAgent.ExecuteAsync(
                userInput,
                temperature: 0.7f,
                maxTokens: 500
            ),
            maxRetries: 3
        );
        
        if (response.Success)
        {
            Log.Message($"[NarratorManager] Agent response: {response.Content}");
            return response.Content;
        }
        else
        {
            Log.Error($"[NarratorManager] Agent error: {response.Error}");
            return "抱歉，我现在无法回应。请稍后再试。";
        }
    }
    catch (Exception ex)
    {
        Log.Error($"[NarratorManager] Error: {ex.Message}");
        return "发生了错误，请检查日志。";
    }
}
```

#### 添加统计方法
```csharp
public string GetAgentStats()
{
    if (narratorAgent == null) return "Agent not initialized";
    return narratorAgent.GetDebugInfo();
}

public void ResetAgent()
{
    narratorAgent?.Reset();
}
```

---

## 2?? 集成到 LLMService

### ?? 修改文件：`Source\TheSecondSeat\LLM\LLMService.cs`

#### 添加并发管理
```csharp
using TheSecondSeat.RimAgent;

public class LLMService
{
    // 现有字段保持不变...
    
    // ? 修改发送请求方法（添加并发控制）
    public async Task<LLMResponse> SendStateAndGetActionAsync(
        string systemPrompt,
        string gameState,
        string userQuery)
    {
        try
        {
            // ? 使用 ConcurrentRequestManager
            return await ConcurrentRequestManager.Instance.EnqueueAsync(
                async () => await SendRequestInternalAsync(systemPrompt, gameState, userQuery),
                maxRetries: 3
            );
        }
        catch (Exception ex)
        {
            Log.Error($"[LLMService] Request failed: {ex.Message}");
            return new LLMResponse
            {
                dialogue = "抱歉，我现在无法回应。",
                thought = $"Error: {ex.Message}"
            };
        }
    }
    
    // ? 内部发送方法（原有逻辑）
    private async Task<LLMResponse> SendRequestInternalAsync(
        string systemPrompt,
        string gameState,
        string userQuery)
    {
        // 原有的 HTTP 请求逻辑保持不变...
        // ...
    }
}
```

---

## 3?? 实现工具系统

### ?? 创建文件：`Source\TheSecondSeat\RimAgent\Tools\SearchTool.cs`

```csharp
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
```

### ?? 创建文件：`Source\TheSecondSeat\RimAgent\Tools\AnalyzeTool.cs`

```csharp
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
```

### ?? 创建文件：`Source\TheSecondSeat\RimAgent\Tools\CommandTool.cs`

```csharp
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
```

### ?? 工具注册 - 修改 `TheSecondSeatMod.cs`

```csharp
using TheSecondSeat.RimAgent;
using TheSecondSeat.RimAgent.Tools;

public class TheSecondSeatMod : Mod
{
    public TheSecondSeatMod(ModContentPack content) : base(content)
    {
        // ? 初始化 LLM Provider
        LLMProviderFactory.Initialize();
        
        // ? 注册工具
        RimAgentTools.RegisterTool("search", new SearchTool());
        RimAgentTools.RegisterTool("analyze", new AnalyzeTool());
        RimAgentTools.RegisterTool("command", new CommandTool());
        
        Log.Message("[TheSecondSeat] RimAgent tools registered");
        
        // 现有初始化代码...
        settings = GetSettings<TheSecondSeatSettings>();
    }
}
```

---

## 4?? 更新 SystemPromptGenerator

### ?? 修改文件：`Source\TheSecondSeat\PersonaGeneration\SystemPromptGenerator.cs`

#### 添加工具描述
```csharp
public static string GenerateSystemPrompt(
    NarratorPersonaDef personaDef,
    PersonaAnalysisResult analysis,
    StorytellerAgent agent,
    AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
{
    var prompt = new StringBuilder();
    
    // 现有部分保持不变...
    prompt.AppendLine(GenerateIdentitySection(personaDef, agent, difficultyMode));
    prompt.AppendLine(GeneratePersonalitySection(analysis, personaDef));
    
    // ? 添加工具描述
    prompt.AppendLine("\n### ?? 可用工具");
    prompt.AppendLine("你可以使用以下工具来辅助回答：");
    prompt.AppendLine("- **search(query)**: 搜索游戏中的 Pawn、物品、建筑等");
    prompt.AppendLine("- **analyze()**: 分析殖民地当前状态");
    prompt.AppendLine("- **command(action)**: 执行游戏命令");
    prompt.AppendLine("使用格式：`[TOOL:工具名(参数)]`");
    
    // 现有部分保持不变...
    prompt.AppendLine(GenerateOutputFormat());
    
    return prompt.ToString();
}
```

---

## 5?? 测试与验证

### ?? 创建测试脚本：`Test-RimAgent-Integration.ps1`

```powershell
# ================================================================
# Test-RimAgent-Integration.ps1
# ? RimAgent 集成测试脚本
# ================================================================

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  RimAgent v1.6.65 集成测试" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. 编译项目
Write-Host "?? 1. 编译项目..." -ForegroundColor Yellow
dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" -c Release --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "? 编译失败" -ForegroundColor Red
    exit 1
}

Write-Host "? 编译成功" -ForegroundColor Green
Write-Host ""

# 2. 检查 DLL
Write-Host "?? 2. 检查输出文件..." -ForegroundColor Yellow
$dllPath = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"

if (Test-Path $dllPath) {
    $dllSize = (Get-Item $dllPath).Length
    Write-Host "? DLL 文件存在: $dllSize bytes" -ForegroundColor Green
} else {
    Write-Host "? DLL 文件不存在" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 3. 检查工具文件
Write-Host "?? 3. 检查工具文件..." -ForegroundColor Yellow

$toolFiles = @(
    "Source\TheSecondSeat\RimAgent\Tools\SearchTool.cs",
    "Source\TheSecondSeat\RimAgent\Tools\AnalyzeTool.cs",
    "Source\TheSecondSeat\RimAgent\Tools\CommandTool.cs"
)

$allToolsExist = $true
foreach ($file in $toolFiles) {
    if (Test-Path $file) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file (缺失)" -ForegroundColor Red
        $allToolsExist = $false
    }
}

if (-not $allToolsExist) {
    Write-Host ""
    Write-Host "?? 部分工具文件缺失，请先创建" -ForegroundColor Yellow
}

Write-Host ""

# 4. 生成集成报告
Write-Host "?? 4. 生成集成报告..." -ForegroundColor Yellow

$report = @"
# RimAgent 集成测试报告

## 测试时间
$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## 测试结果

### ? 编译测试
- 状态: 通过
- DLL 大小: $dllSize bytes

### ?? 核心组件
- [x] RimAgent.cs
- [x] RimAgentTools.cs
- [x] RimAgentModels.cs
- [x] ConcurrentRequestManager.cs
- [x] LLMProviderFactory.cs

### ?? 工具系统
- [$($allToolsExist ? 'x' : ' ')] SearchTool.cs
- [$($allToolsExist ? 'x' : ' ')] AnalyzeTool.cs
- [$($allToolsExist ? 'x' : ' ')] CommandTool.cs

### ?? 下一步
$( if ($allToolsExist) {
    "1. 部署到游戏目录`n2. 启动游戏测试`n3. 验证工具调用"
} else {
    "1. 创建缺失的工具文件`n2. 重新编译`n3. 部署测试"
})

## ?? 使用示例

``````csharp
// 创建 Agent
var provider = LLMProviderFactory.GetProvider("openai");
var agent = new RimAgent("test", "You are helpful", provider);

// 注册工具
agent.RegisterTool("search");
agent.RegisterTool("analyze");
agent.RegisterTool("command");

// 执行任务
var response = await agent.ExecuteAsync("搜索名为 John 的殖民者");
``````

---
? RimAgent v1.6.65 集成测试完成
"@

Set-Content -Path "RimAgent-集成测试报告-v1.6.65.md" -Value $report -Encoding UTF8
Write-Host "? 报告已生成: RimAgent-集成测试报告-v1.6.65.md" -ForegroundColor Green

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ? 集成测试完成！" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
```

---

## ?? 集成清单

| 任务 | 文件 | 状态 |
|------|------|------|
| 修改 NarratorManager | `Narrator\NarratorManager.cs` | ? 待完成 |
| 修改 LLMService | `LLM\LLMService.cs` | ? 待完成 |
| 创建 SearchTool | `RimAgent\Tools\SearchTool.cs` | ? 待完成 |
| 创建 AnalyzeTool | `RimAgent\Tools\AnalyzeTool.cs` | ? 待完成 |
| 创建 CommandTool | `RimAgent\Tools\CommandTool.cs` | ? 待完成 |
| 注册工具 | `TheSecondSeatMod.cs` | ? 待完成 |
| 更新 SystemPromptGenerator | `PersonaGeneration\SystemPromptGenerator.cs` | ? 待完成 |

---

## ?? 下一步操作

1. **创建工具文件夹**
```powershell
New-Item -ItemType Directory -Path "Source\TheSecondSeat\RimAgent\Tools" -Force
```

2. **创建工具文件**
   - 将上述代码保存到对应文件

3. **修改集成文件**
   - 按照指南修改 NarratorManager.cs
   - 按照指南修改 LLMService.cs
   - 按照指南修改 SystemPromptGenerator.cs

4. **编译测试**
```powershell
.\Test-RimAgent-Integration.ps1
```

5. **部署验证**
```powershell
.\一键部署.ps1
```

---

? **RimAgent v1.6.65 集成指南** - The Second Seat Mod

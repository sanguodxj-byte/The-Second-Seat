# ?? RimAgent 日志缺失 - 快速修复方案

## 问题确认
诊断显示所有文件都正常部署，但游戏中没看到日志。

---

## ? 解决方案

### 方案1: 完全重启游戏（推荐）

#### 步骤：
1. **完全关闭游戏**
   - 不要只是退出到主菜单
   - 确保进程完全结束

2. **重新启动游戏**
   - StaticConstructorOnStartup 只在启动时执行
   - 启动后立即查看日志

3. **查找日志**
在游戏启动后的前几行日志中搜索：
```
[The Second Seat]
```

---

### 方案2: 强制重新部署

如果方案1无效，执行以下操作：

```powershell
# 1. 完全删除游戏中的 Assemblies 文件夹
Remove-Item "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies" -Recurse -Force

# 2. 重新编译和部署
.\编译并部署到游戏.ps1

# 3. 重启游戏
```

---

### 方案3: 游戏内验证（如果游戏已启动）

如果游戏已经在运行，按 **`~`** 键打开控制台，执行：

```csharp
// 检查类是否加载
var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "TheSecondSeat");
    
if (assembly != null) {
    Log.Message("? TheSecondSeat DLL 已加载");
    
    // 检查 TheSecondSeatInit
    var initType = assembly.GetType("TheSecondSeat.TheSecondSeatInit");
    Log.Message("TheSecondSeatInit 类: " + (initType != null ? "存在" : "不存在"));
    
    // 检查 RimAgentTools
    var toolsType = assembly.GetType("TheSecondSeat.RimAgent.RimAgentTools");
    if (toolsType != null) {
        var method = toolsType.GetMethod("GetRegisteredToolNames", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (method != null) {
            var tools = method.Invoke(null, null);
            Log.Message("已注册工具: " + tools);
        }
    }
} else {
    Log.Error("? TheSecondSeat DLL 未加载！");
}
```

---

### 方案4: 增强日志（开发调试）

如果需要更详细的日志，可以修改 `TheSecondSeatMod.cs`：

```csharp
static TheSecondSeatInit()
{
    // 添加第一行日志（确保这是第一件事）
    Verse.Log.Warning("========================================");
    Verse.Log.Warning("[The Second Seat] ? STARTING INITIALIZATION");
    Verse.Log.Warning("========================================");
    
    Verse.Log.Message("[The Second Seat] AI Narrator Assistant initialized");
    
    // ... 其余代码
}
```

然后重新编译和部署。

---

## ?? 日志查找技巧

### 在游戏日志文件中搜索
日志文件位置：
```
C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

使用 PowerShell 搜索：
```powershell
# 搜索 The Second Seat 相关日志
Select-String -Path "C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log" -Pattern "The Second Seat" -Context 2, 2

# 搜索 RimAgent 相关日志
Select-String -Path "C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log" -Pattern "RimAgent" -Context 2, 2
```

### 在游戏控制台中搜索
1. 按 **`~`** 键打开控制台
2. 在控制台顶部有搜索框
3. 输入 "Second Seat" 或 "RimAgent"

---

## ?? 预期日志内容

如果 RimAgent 成功初始化，应该看到：

```
[The Second Seat] AI Narrator Assistant initialized
[The Second Seat] ? LLM Providers initialized
[The Second Seat] ? RimAgent tools registered: search, analyze, command
[RimAgentTools] Tool 'search' registered
[RimAgentTools] Tool 'analyze' registered
[RimAgentTools] Tool 'command' registered
```

如果只看到第一行，说明后续初始化失败了。

---

## ?? 如果出现异常

如果看到类似以下错误：
```
[The Second Seat] Failed to initialize LLM Providers: ...
[The Second Seat] Failed to register RimAgent tools: ...
```

这说明代码执行了，但遇到异常。请：
1. 复制完整的错误信息
2. 检查是否缺少依赖的文件
3. 检查是否有编译错误被忽略了

---

## ?? 最可能的原因

根据诊断结果，最可能的原因是：

### ? 游戏没有重启
- StaticConstructorOnStartup 只在游戏启动时执行
- 如果 DLL 是在游戏运行时部署的，必须重启游戏

### ? 日志被其他消息淹没
- RimWorld 启动时会输出大量日志
- 需要主动搜索 "The Second Seat"

---

## ?? 推荐操作流程

1. **完全关闭游戏** ?
2. **重新启动游戏** ?
3. **启动后立即查看控制台** ??
4. **搜索 "The Second Seat"** ??
5. **如果还是没有，使用方案3验证** ??

---

## ?? 如果还是没有日志

请提供以下信息：
1. 游戏版本
2. 是否有其他 Mod
3. Player.log 文件的最后 100 行
4. 使用方案3的验证结果

---

? **99%的情况下，只需要重启游戏即可看到日志！**

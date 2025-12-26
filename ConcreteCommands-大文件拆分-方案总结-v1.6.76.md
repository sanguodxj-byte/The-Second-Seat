# ConcreteCommands.cs 大文件拆分 - 方案总结 v1.6.76

## ?? 拆分目标

将 `ConcreteCommands.cs`（1315 行）拆分为 19 个独立文件，提高代码可维护性。

---

## ?? 文件结构对比

### 拆分前
```
Source\TheSecondSeat\Commands\Implementations\
└── ConcreteCommands.cs  (1315 行，18 个类)
```

### 拆分后
```
Source\TheSecondSeat\Commands\Implementations\
├── ConcreteCommands_Index.cs          # 索引文件（已创建）?
├── Common\
│   └── BatchCommandHelpers.cs         # 通用辅助类（已创建）?
├── Resource\                           # 资源管理命令（5个）?
│   ├── BatchMineCommand.cs
│   ├── BatchLoggingCommand.cs
│   ├── DesignatePlantCutCommand.cs
│   ├── ForbidItemsCommand.cs
│   └── AllowItemsCommand.cs
├── Harvest\                            # 收获命令（1个）?
│   └── BatchHarvestCommand.cs
├── Equipment\                          # 装备命令（2个）?
│   ├── BatchEquipCommand.cs
│   └── EquipWeaponCommand.cs
├── Repair\                             # 修理命令（1个）?
│   └── PriorityRepairCommand.cs
├── Combat\                             # 战斗命令（2个）?
│   ├── EmergencyRetreatCommand.cs
│   └── BatchCaptureCommand.cs
├── Pawn\                               # 殖民者管理命令（4个）?
│   ├── DraftPawnCommand.cs
│   ├── MovePawnCommand.cs
│   ├── HealPawnCommand.cs
│   └── SetWorkPriorityCommand.cs
├── Policy\                             # 政策命令（1个）?
│   └── ChangePolicyCommand.cs
└── Event\                              # 事件命令（2个）?
    ├── TriggerEventCommand.cs
    └── ScheduleEventCommand.cs
```

---

## ? 已完成的工作

1. ? **创建文件夹结构**（9 个文件夹）
2. ? **创建索引文件** `ConcreteCommands_Index.cs`
3. ? **创建通用辅助类** `Common/BatchCommandHelpers.cs`

---

## ? 待完成的工作

### 方案 A：手动逐个拆分（推荐，质量最高）
**优点**：可以逐个审查、优化每个类  
**缺点**：工作量大，需要 1-2 小时

**步骤**：
1. 从原文件中复制某个命令类
2. 创建对应的独立文件
3. 保持命名空间不变：`TheSecondSeat.Commands.Implementations`
4. 添加必要的 `using` 语句
5. 编译测试

**示例**（BatchHarvestCommand）：
```csharp
// Source\TheSecondSeat\Commands\Implementations\Harvest\BatchHarvestCommand.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using TheSecondSeat.Commands.Implementations.Common; // 引入辅助类

namespace TheSecondSeat.Commands.Implementations
{
    public class BatchHarvestCommand : BaseAICommand
    {
        public override string ActionName => "BatchHarvest";
        // ... 原有代码
    }
}
```

---

### 方案 B：自动化批量拆分（快速，但需验证）
**优点**：5 分钟完成所有拆分  
**缺点**：需要编写复杂的解析脚本

**PowerShell 脚本思路**：
```powershell
# 1. 读取原文件
$content = Get-Content ConcreteCommands.cs -Raw

# 2. 使用正则表达式提取每个类
$classes = $content | Select-String -Pattern "public class (\w+) : BaseAICommand\s*{([\s\S]*?)^    }" -AllMatches

# 3. 为每个类创建独立文件
foreach ($class in $classes) {
    $className = $class.Matches.Groups[1].Value
    $classContent = $class.Matches.Groups[0].Value
    # 创建文件...
}
```

---

### 方案 C：渐进式拆分（稳妥）
**优点**：边拆分边测试，风险最低  
**缺点**：时间较长

**步骤**：
1. 先拆分 5 个最常用的命令（BatchHarvest, BatchEquip, BatchMine, BatchLogging, PriorityRepair）
2. 编译测试
3. 游戏内测试
4. 确认无问题后继续拆分剩余命令

---

## ?? 推荐方案

**方案 C（渐进式拆分）** - 平衡质量与效率

### Phase 1：拆分核心命令（5 个）
- ? `BatchCommandHelpers.cs`（已完成）
- ? `BatchHarvestCommand.cs`
- ? `BatchEquipCommand.cs`
- ? `BatchMineCommand.cs`
- ? `BatchLoggingCommand.cs`
- ? `PriorityRepairCommand.cs`

### Phase 2：编译测试
```powershell
dotnet build Source\TheSecondSeat\TheSecondSeat.csproj -c Release
```

### Phase 3：拆分剩余命令（13 个）
如果 Phase 1 成功，继续拆分剩余命令。

---

## ?? 向后兼容保障

### 命名空间保持不变
```csharp
// ? 拆分前
namespace TheSecondSeat.Commands.Implementations
{
    public class BatchHarvestCommand : BaseAICommand { }
}

// ? 拆分后（文件：Harvest/BatchHarvestCommand.cs）
namespace TheSecondSeat.Commands.Implementations
{
    public class BatchHarvestCommand : BaseAICommand { }
}
```

### 公共 API 完全兼容
- 类名不变
- 方法签名不变
- 继承关系不变
- 现有调用代码无需修改

---

## ?? 下一步操作

### 选项 1：继续手动拆分（推荐）
我可以帮你逐个创建剩余的命令类文件。

### 选项 2：使用自动化脚本
我可以编写 PowerShell 脚本自动完成所有拆分。

### 选项 3：暂停拆分
保持当前状态（索引文件 + 辅助类），后续根据需要拆分。

---

**你希望采用哪种方案？**  
- **A**：继续手动拆分（我逐个创建文件）
- **B**：使用自动化脚本（5 分钟完成）
- **C**：渐进式拆分（先拆分 5 个核心命令，测试后再继续）
- **D**：暂停拆分（保持当前状态）

---

**版本**：v1.6.76  
**日期**：2025-12-26  
**状态**：?? 等待确认方案

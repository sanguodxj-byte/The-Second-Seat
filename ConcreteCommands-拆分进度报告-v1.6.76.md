# ConcreteCommands.cs 拆分进度报告 v1.6.76

## ?? 当前进度：7/19 (37%)

### ? 已完成（7 个文件）

1. ? **Common/BatchCommandHelpers.cs** - 通用辅助类
2. ? **Resource/BatchMineCommand.cs** - 批量采矿
3. ? **Resource/BatchLoggingCommand.cs** - 批量伐木
4. ? **Resource/DesignatePlantCutCommand.cs** - 砍伐植物
5. ? **Resource/ForbidItemsCommand.cs** - 禁止物品
6. ? **Resource/AllowItemsCommand.cs** - 允许物品
7. ? **Harvest/BatchHarvestCommand.cs** - 批量收获

---

### ? 待创建（12 个文件）

#### Equipment 命令（2 个）
8. ? **Equipment/BatchEquipCommand.cs** - 批量装备（所有殖民者自动装备最佳武器）
9. ? **Equipment/EquipWeaponCommand.cs** - 装备武器（单个殖民者装备指定武器）

#### Repair 命令（1 个）
10. ? **Repair/PriorityRepairCommand.cs** - 优先修理损坏建筑

#### Combat 命令（2 个）
11. ? **Combat/EmergencyRetreatCommand.cs** - 紧急撤退（征召所有殖民者）
12. ? **Combat/BatchCaptureCommand.cs** - 批量俘虏（俘虏所有倒下的敌人）

#### Pawn 命令（4 个）
13. ? **Pawn/DraftPawnCommand.cs** - 征召/解除征召殖民者
14. ? **Pawn/MovePawnCommand.cs** - 移动殖民者到指定位置
15. ? **Pawn/HealPawnCommand.cs** - 治疗受伤殖民者
16. ? **Pawn/SetWorkPriorityCommand.cs** - 设置工作优先级

#### Policy 命令（1 个）
17. ? **Policy/ChangePolicyCommand.cs** - 修改殖民地政策

#### Event 命令（2 个）
18. ? **Event/TriggerEventCommand.cs** - 触发事件（对弈者模式）
19. ? **Event/ScheduleEventCommand.cs** - 安排未来事件（对弈者模式）

---

## ?? 下一步方案

### 方案 A：继续手动逐个创建（推荐，但慢）
我继续逐个创建剩余的 12 个文件。

**预计时间**：约 20-30 分钟

---

### 方案 B：自动化批量创建（快速）
使用 PowerShell 脚本从原文件中自动提取并创建所有剩余文件。

**预计时间**：约 5 分钟

**脚本逻辑**：
```powershell
# 读取原文件
$content = Get-Content ConcreteCommands.cs -Raw

# 提取每个类的代码块（使用正则表达式）
$classPattern = "public class (\w+Command) : BaseAICommand\s*{([\s\S]*?)^    }"

# 为每个类创建独立文件
# 添加必要的 using 语句
# 保持命名空间不变
```

---

### 方案 C：渐进式创建（平衡）
先创建剩余最关键的 5 个命令：
- BatchEquipCommand
- PriorityRepairCommand
- EmergencyRetreatCommand
- BatchCaptureCommand
- TriggerEventCommand

然后编译测试，确认无问题后再创建剩余 7 个。

---

## ?? 建议

**我推荐方案 C（渐进式创建）**，理由：
1. ? 先创建最常用的命令，快速看到效果
2. ? 边创建边测试，降低风险
3. ? 如果编译出错，可以快速定位问题

---

**你希望采用哪个方案？**
- **A**：继续手动逐个创建（我继续）
- **B**：使用自动化脚本（5 分钟完成）
- **C**：渐进式创建（先创建 5 个关键命令）

直接告诉我 A/B/C 即可，我立即执行。

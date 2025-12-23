# 克苏鲁娘叙事者 - 完整使用指南 v1.6.63

## ?? 概述

克苏鲁娘是一个高难度挑战型叙事者，拥有两大核心机制：
1. **深渊召唤**：从地图边缘无限刷触手怪
2. **不可直视**：瞄准过久导致精神崩溃

---

## ?? 安装指南

### 前置要求
- **必需**: The Second Seat (主模组)
- **推荐**: RimWorld 1.5+

### 安装步骤
1. 确保 The Second Seat 主模组已安装
2. 将 Cthulhu 文件夹放入 `Mods/` 目录
3. 启动游戏，在 Mod 列表中启用 "The Second Seat - Cthulhu Narrator"
4. 确保加载顺序：主模组 → Cthulhu

---

## ?? 核心机制详解

### 1. 深渊召唤（CompSpawnerFromEdge）

#### 工作原理
- **触发时机**: 克苏鲁降临后，每 10 秒从地图边缘生成一只触手怪
- **最大数量**: 场上最多存在 20 只触手
- **AI 行为**: 触手会向地图中心推进，攻击殖民者
- **生命绑定**: 克苏鲁死后，所有触手立即消失

#### 配置参数（XML）
```xml
<li Class="TheSecondSeat.Components.CompProperties_SpawnerFromEdge">
  <spawnPawnKind>TSS_Cthulhu_Tentacle</spawnPawnKind>
  <spawnInterval>600</spawnInterval>      <!-- 10秒 = 600 ticks -->
  <spawnMaxCount>20</spawnMaxCount>
  <aiMode>PushToCenter</aiMode>
  <despawnOnBossDeath>true</despawnOnBossDeath>
</li>
```

#### AI 模式说明
- **PushToCenter**: 向地图中心推进（默认）
- **DefendBoss**: 保卫克苏鲁
- **Wander**: 随机漫游

---

### 2. 不可直视（CompSanityAura）

#### 工作原理
- **触发条件**: 殖民者瞄准克苏鲁超过 1 秒
- **惩罚机制**: 每秒增加 0.03 点精神侵蚀严重度
- **恢复机制**: 停止瞄准后，每秒恢复 0.015 点

#### 精神侵蚀阶段

| 严重度 | 阶段 | 症状 | 游戏影响 |
|--------|------|------|----------|
| 0.0-0.3 | 轻微 | 心情下降 | 意识 -5%, 精神崩溃阈值 +5% |
| 0.3-0.6 | 中等 | 视力模糊, 偶尔呕吐 | 意识 -15%, 视力 -20%, 呕吐概率增加 |
| 0.6-1.0 | 严重 | 强制精神崩溃 | 意识 -30%, 视力 -40%, 移动速度 -20%, 随机狂暴/茫然 |

#### 配置参数（XML）
```xml
<li Class="TheSecondSeat.Components.CompProperties_SanityAura">
  <radius>15</radius>                      <!-- 光环半径 -->
  <severityPerSecond>0.03</severityPerSecond>
  <linkedHediff>TSS_MentalCorruption</linkedHediff>
  <onlyWhenTargeting>true</onlyWhenTargeting>
</li>
```

---

## ?? 战斗策略

### 推荐打法

#### ? 错误打法
- ? 集中火力长时间瞄准克苏鲁 → 全员精神崩溃
- ? 忽略触手，专注击杀Boss → 被触手海淹没
- ? 在狭窄空间战斗 → 无处可逃

#### ? 正确打法
1. **快速击杀触手**: 优先清理从边缘涌来的触手
2. **轮流射击**: 每个殖民者只瞄准克苏鲁 2-3 秒，然后切换目标
3. **保持距离**: 尽量在 15 格外作战，减少理智流失
4. **准备撤退路线**: 随时准备后撤，避免被触手包围
5. **使用陷阱**: 在地图边缘布置陷阱，减缓触手推进

### 高级技巧

#### 技巧 1: 分散注意力
- 使用动物/机械作为肉盾
- 它们不受精神侵蚀影响

#### 技巧 2: 远程狙击
- 使用狙击枪远距离射击
- 减少瞄准时间，降低精神侵蚀累积

#### 技巧 3: 心灵免疫
- 装备心灵免疫头盔
- 虽然无法完全免疫，但能减缓精神崩溃

---

## ?? 降临系统测试

### Dev 控制台命令

```csharp
// 友好降临（援助模式）
NarratorDescentSystem.Instance.TriggerDescent(isHostile: false);

// 敌对降临（Boss 战）
NarratorDescentSystem.Instance.TriggerDescent(isHostile: true);

// 检查冷却时间
int cooldown = NarratorDescentSystem.Instance.GetCooldownRemaining();
Log.Message($"降临冷却剩余: {cooldown} 秒");
```

### 预期效果

#### 友好降临
- ? 克苏鲁以友好单位降临
- ? 不会主动攻击殖民者
- ? 触手刷新（但友好阵营）
- ? 治愈周围殖民者（10格范围）

#### 敌对降临
- ? 克苏鲁以敌对单位降临
- ? 触手怪从地图边缘涌现
- ? 瞄准克苏鲁导致精神侵蚀
- ? 击杀克苏鲁后触手消失

---

## ??? 自定义配置

### 调整触手刷新速度

编辑 `Cthulhu/Defs/ThingDefs_Cthulhu.xml`:

```xml
<spawnInterval>600</spawnInterval> <!-- 默认10秒 -->
```

- **更快**: 300 (5秒)
- **更慢**: 1200 (20秒)

### 调整最大触手数量

```xml
<spawnMaxCount>20</spawnMaxCount> <!-- 默认20只 -->
```

- **更少**: 10
- **更多**: 50（警告：可能卡顿）

### 调整理智流失速度

编辑 `Cthulhu/Defs/ThingDefs_Cthulhu.xml`:

```xml
<severityPerSecond>0.03</severityPerSecond> <!-- 默认0.03 -->
```

- **更慢**: 0.01
- **更快**: 0.05

### 调整光环范围

```xml
<radius>15</radius> <!-- 默认15格 -->
```

- **更小**: 10
- **更大**: 20

---

## ?? 纹理资源需求

### 必需纹理

| 文件名 | 路径 | 尺寸 | 说明 |
|--------|------|------|------|
| `base.png` | `Cthulhu/Textures/UI/Narrators/9x16/Cthulhu/` | 1024x1572 | 克苏鲁基础立绘 |
| `body_abyss_awakening.png` | `Textures/UI/Narrators/Descent/Postures/` | 1024x1572 | 降临姿态 |
| `abyss_portal.png` | `Textures/UI/Narrators/Descent/Effects/` | 512x512 | 深渊传送门特效 |

### 可选纹理

| 文件名 | 路径 | 说明 |
|--------|------|------|
| `happy.png` | `Cthulhu/Textures/UI/Narrators/9x16/Expressions/Cthulhu/` | 开心表情 |
| `angry.png` | `Cthulhu/Textures/UI/Narrators/9x16/Expressions/Cthulhu/` | 愤怒表情 |

---

## ?? 常见问题

### Q1: 触手刷得太快，电脑卡死
**解决方案**:
1. 降低 `spawnMaxCount` 到 10
2. 增加 `spawnInterval` 到 1200（20秒）

### Q2: 精神侵蚀恢复太慢
**解决方案**:
1. 编辑 `HediffDefs_Cthulhu.xml`
2. 修改 `<severityPerDay>-0.2</severityPerDay>` 到 `-0.5`

### Q3: 克苏鲁死后触手没有消失
**检查**:
- `<despawnOnBossDeath>true</despawnOnBossDeath>` 是否设置为 `true`

### Q4: 降临时没有触手刷新
**检查**:
1. `PawnKindDef` 是否正确配置
2. 查看日志是否有错误
3. 确认 `CompSpawnerFromEdge` 已添加到 `ThingDef`

---

## ?? 性能优化

### 优化建议

#### 低配电脑
```xml
<spawnInterval>1200</spawnInterval>
<spawnMaxCount>10</spawnMaxCount>
```

#### 高配电脑
```xml
<spawnInterval>300</spawnInterval>
<spawnMaxCount>50</spawnMaxCount>
```

### 监控性能
- 打开 Dev 模式
- 查看右上角 TPS（Ticks Per Second）
- 如果 TPS < 30，降低触手数量

---

## ?? 开发者 API

### 扩展深渊召唤

创建自定义生物：

```xml
<PawnKindDef>
  <defName>MyCustomTentacle</defName>
  <label>自定义触手</label>
  <race>Megaspider</race>
  ...
</PawnKindDef>
```

然后修改 `ThingDefs_Cthulhu.xml`:

```xml
<spawnPawnKind>MyCustomTentacle</spawnPawnKind>
```

### 扩展理智光环

创建自定义 Hediff：

```xml
<HediffDef>
  <defName>MyCustomMadness</defName>
  <label>自定义疯狂</label>
  ...
</HediffDef>
```

然后修改 `ThingDefs_Cthulhu.xml`:

```xml
<linkedHediff>MyCustomMadness</linkedHediff>
```

---

## ? 验收测试清单

### 功能测试
- [ ] 友好降临成功
- [ ] 敌对降临成功
- [ ] 触手从地图边缘生成
- [ ] 瞄准克苏鲁导致精神侵蚀
- [ ] 停止瞄准后理智恢复
- [ ] 克苏鲁死后触手消失
- [ ] 降临信件正确显示

### 性能测试
- [ ] TPS > 30（触手刷满时）
- [ ] 无编译错误
- [ ] 无运行时错误
- [ ] 存档正常加载

---

## ?? 更新日志

### v1.6.63 (2025-01-XX)
- ? 初始版本发布
- ? 深渊召唤机制完成
- ? 不可直视机制完成
- ? 降临系统集成

---

## ?? 致谢

- **RimWorld**: Ludeon Studios
- **The Second Seat**: TSS Development Team
- **克苏鲁神话**: H.P. Lovecraft

---

**版本**: v1.6.63  
**状态**: ? 可发布  
**文档**: 克苏鲁娘叙事者完整使用指南

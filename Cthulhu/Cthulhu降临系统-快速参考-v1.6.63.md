# ?? 克苏鲁娘叙事者 - 快速参考卡片 v1.6.63

## ? 核心机制

### 1. 深渊召唤
- **触发**: 克苏鲁降临后自动激活
- **效果**: 每 10 秒从地图边缘刷一只触手
- **最大数量**: 20 只
- **AI**: 向地图中心推进
- **生命绑定**: Boss 死后触手消失

### 2. 不可直视
- **触发**: 瞄准克苏鲁超过 1 秒
- **效果**: 每秒 +0.03 精神侵蚀
- **惩罚**:
  - 轻度: 心情 -10
  - 中度: 视力模糊，呕吐
  - 重度: 强制精神崩溃
- **恢复**: 停止瞄准后每秒 -0.015

---

## ?? 快速测试

```csharp
// Dev 控制台
NarratorDescentSystem.Instance.TriggerDescent(isHostile: true);
```

---

## ?? 战斗策略

| 策略 | 说明 |
|------|------|
| ? 轮流射击 | 每人只瞄准 2-3 秒 |
| ? 优先清触手 | 防止被包围 |
| ? 保持距离 | 15 格外作战 |
| ? 长时间瞄准 | 导致精神崩溃 |

---

## ??? 快速配置

### 降低难度
```xml
<spawnInterval>1200</spawnInterval>     <!-- 20秒一次 -->
<spawnMaxCount>10</spawnMaxCount>       <!-- 最多10只 -->
<severityPerSecond>0.01</severityPerSecond> <!-- 慢速侵蚀 -->
```

### 提高难度
```xml
<spawnInterval>300</spawnInterval>      <!-- 5秒一次 -->
<spawnMaxCount>50</spawnMaxCount>       <!-- 最多50只 -->
<severityPerSecond>0.05</severityPerSecond> <!-- 快速侵蚀 -->
```

---

## ?? 文件结构

```
Cthulhu/
├── About/About.xml
├── Defs/
│   ├── NarratorPersonaDefs_Cthulhu.xml
│   ├── PawnKindDefs_Cthulhu.xml
│   ├── ThingDefs_Cthulhu.xml
│   └── HediffDefs_Cthulhu.xml
├── Languages/ChineseSimplified/Keyed/Cthulhu_Keys.xml
└── README.md
```

---

## ? 一键部署

```powershell
.\Deploy-Cthulhu-v1.6.63.ps1
```

---

**状态**: ? 可发布  
**版本**: v1.6.63  
**作者**: TSS Development Team

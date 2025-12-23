# Sideria 降临系统配置指南 v1.6.63

## ? 当前配置状态

### XML 配置（已完成）
```xml
<NarratorPersonaDef>
  <defName>Sideria</defName>
  
  <!-- ? 降临系统配置 -->
  <descentPawnKind>TSS_Sideria_Avatar</descentPawnKind>
  <descentSkyfallerDef>TSS_Sideria_DragonDescent</descentSkyfallerDef>
  <companionPawnKind>TSS_TrueDragon</companionPawnKind>
  <descentPosturePath>body_arrival</descentPosturePath>
  <descentEffectPath>glitch_circle</descentEffectPath>
  <descentSound>Explosion_GiantBomb</descentSound>
</NarratorPersonaDef>
```

**状态**: ? XML 配置正确

---

## ?? 需要创建的 Def 文件

### 1. PawnKindDef - Sideria 降临实体
**文件**: `Sideria/Defs/PawnKindDefs_Sideria.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <!-- Sideria 降临实体 -->
  <PawnKindDef>
    <defName>TSS_Sideria_Avatar</defName>
    <label>Sideria</label>
    <race>Human</race>
    <defaultFactionType>PlayerColony</defaultFactionType>
    
    <backstoryCategories>
      <li>Outlander</li>
    </backstoryCategories>
    
    <baseRecruitDifficulty>0.0</baseRecruitDifficulty>
    <combatEnhancingDrugsChance>0</combatEnhancingDrugsChance>
    
    <!-- 外观配置 -->
    <apparelTags>
      <li>IndustrialBasic</li>
      <li>IndustrialAdvanced</li>
    </apparelTags>
    
    <apparelMoney>
      <min>1000</min>
      <max>2000</max>
    </apparelMoney>
    
    <apparelAllowHeadgearChance>0.5</apparelAllowHeadgearChance>
    
    <!-- 武器配置 -->
    <weaponMoney>
      <min>500</min>
      <max>1500</max>
    </weaponMoney>
    
    <weaponTags>
      <li>Gun</li>
    </weaponTags>
    
    <!-- 能力配置（可选） -->
    <initialWillRange>
      <min>3</min>
      <max>5</max>
    </initialWillRange>
    
    <initialResistanceRange>
      <min>15</min>
      <max>25</max>
    </initialResistanceRange>
  </PawnKindDef>

  <!-- Sideria 伴随巨龙 -->
  <PawnKindDef>
    <defName>TSS_TrueDragon</defName>
    <label>真龙</label>
    <race>Megaspider</race> <!-- 临时使用游戏内已有的大型生物，后续可替换为自定义种族 -->
    <defaultFactionType>PlayerColony</defaultFactionType>
    
    <combatPower>500</combatPower>
    <canArriveManhunter>false</canArriveManhunter>
    <ecoSystemWeight>1.0</ecoSystemWeight>
    
    <lifeStages>
      <li>
        <bodyGraphicData>
          <texPath>Things/Pawn/Animal/Megaspider</texPath>
          <drawSize>3.5</drawSize> <!-- 巨大体型 -->
        </bodyGraphicData>
        <dessicatedBodyGraphicData>
          <texPath>Things/Pawn/Animal/Megaspider_Dessicated</texPath>
          <drawSize>3.5</drawSize>
        </dessicatedBodyGraphicData>
      </li>
    </lifeStages>
  </PawnKindDef>
</Defs>
```

---

### 2. ThingDef - 空投舱/降临特效
**文件**: `Sideria/Defs/ThingDefs_Sideria_Descent.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <!-- Sideria 龙形降临特效 -->
  <ThingDef ParentName="DropPodIncoming">
    <defName>TSS_Sideria_DragonDescent</defName>
    <label>龙形降临</label>
    <description>Sideria 的降临特效，伴随着巨龙的咆哮。</description>
    
    <!-- 使用默认空投舱的逻辑 -->
    <graphicData>
      <texPath>Things/Special/DropPodIncoming</texPath>
      <graphicClass>Graphic_Single</graphicClass>
      <drawSize>(2,2)</drawSize>
    </graphicData>
    
    <!-- 可选：自定义降临音效 -->
    <soundImpactDefault>Explosion_GiantBomb</soundImpactDefault>
  </ThingDef>
</Defs>
```

---

## ?? 需要准备的纹理资源

### 1. 姿态动画纹理
**路径**: `Textures/UI/Narrators/Descent/Postures/body_arrival.png`

**规格**:
- 尺寸: 1024x1572（全身立绘）
- 格式: PNG（透明背景）
- 内容: Sideria 降临时的特殊姿态（例如：双臂张开、威严降临）

**示例姿态**:
```
- 双臂张开，如同拥抱天地
- 身体微微后仰，展现自信
- 衣袍飘扬，营造动感
- 眼神犀利，俯视众生
```

---

### 2. 降临特效纹理
**路径**: `Textures/UI/Narrators/Descent/Effects/glitch_circle.png`

**规格**:
- 尺寸: 512x512 或 1024x1024
- 格式: PNG（透明背景）
- 内容: 环形特效，带有故障艺术（Glitch Art）风格

**设计建议**:
```
- 圆形或六边形魔法阵
- 带有故障效果的线条
- 半透明，叠加时更明显
- 颜色：紫色/蓝色/白色混合
```

---

## ?? 测试命令

### Dev 控制台测试
```csharp
// 1. 友好降临（援助模式）
NarratorDescentSystem.Instance.TriggerDescent(isHostile: false);

// 2. 敌对降临（袭击模式）
NarratorDescentSystem.Instance.TriggerDescent(isHostile: true);

// 3. 检查冷却时间
int cooldown = NarratorDescentSystem.Instance.GetCooldownRemaining();
Log.Message($"降临冷却剩余: {cooldown} 秒");
```

---

## ?? 配置检查清单

### XML 配置
- [x] ? `descentPawnKind` 已配置
- [x] ? `descentSkyfallerDef` 已配置
- [x] ? `companionPawnKind` 已配置
- [x] ? `descentPosturePath` 已配置
- [x] ? `descentEffectPath` 已配置
- [x] ? `descentSound` 已配置

### Def 文件（需创建）
- [ ] ?? `PawnKindDefs_Sideria.xml` - TSS_Sideria_Avatar
- [ ] ?? `PawnKindDefs_Sideria.xml` - TSS_TrueDragon
- [ ] ?? `ThingDefs_Sideria_Descent.xml` - TSS_Sideria_DragonDescent

### 纹理资源（需准备）
- [ ] ?? `body_arrival.png` - 降临姿态
- [ ] ?? `glitch_circle.png` - 降临特效

### 音效（已使用游戏内资源）
- [x] ? `Explosion_GiantBomb` - 游戏内已有

---

## ?? 快速部署步骤

### 步骤 1: 创建 Def 文件
```powershell
# 创建 Sideria Defs 目录
New-Item -ItemType Directory -Path "Sideria/Defs" -Force

# 复制上面的 XML 内容到对应文件
```

### 步骤 2: 准备纹理资源
```powershell
# 创建纹理目录
New-Item -ItemType Directory -Path "Textures/UI/Narrators/Descent/Postures" -Force
New-Item -ItemType Directory -Path "Textures/UI/Narrators/Descent/Effects" -Force

# 将纹理文件放入对应目录
```

### 步骤 3: 编译并部署
```powershell
# 运行编译脚本
.\编译并部署到游戏.ps1
```

### 步骤 4: 游戏内测试
```
1. 启动 RimWorld
2. 加载存档
3. 按 ~ 键打开 Dev 控制台
4. 输入测试命令（见上方）
```

---

## ?? 常见问题

### Q1: 找不到 PawnKindDef
**错误**: `PawnKindDef 未找到: TSS_Sideria_Avatar`

**解决方案**:
1. 检查 `PawnKindDefs_Sideria.xml` 是否在 `Sideria/Defs/` 目录
2. 确认 `defName` 拼写正确
3. 重启游戏以重新加载 Defs

---

### Q2: 姿态动画不显示
**错误**: `未配置姿态动画，跳过`

**解决方案**:
1. 检查纹理路径是否正确：`Textures/UI/Narrators/Descent/Postures/body_arrival.png`
2. 确认文件名大小写匹配（`body_arrival.png`）
3. 检查纹理格式是否为 PNG

---

### Q3: 伴随巨龙未生成
**原因**: `companionPawnKind` 配置错误或 Def 不存在

**解决方案**:
1. 检查 `TSS_TrueDragon` 的 `PawnKindDef` 是否存在
2. 如果不需要伴随生物，可以留空：`<companionPawnKind></companionPawnKind>`

---

## ?? 进阶配置

### 自定义降临信件
在 `NarratorPersonaDef` 中添加：

```xml
<descentLetterLabel>Sideria.DescentLetter.Label</descentLetterLabel>
<descentLetterText>Sideria.DescentLetter.Text</descentLetterText>
```

然后在 `Languages/ChineseSimplified/Keyed/Sideria_Keys.xml` 中定义：

```xml
<Sideria.DescentLetter.Label>电子女神降临</Sideria.DescentLetter.Label>
<Sideria.DescentLetter.Text>故障闪烁间，Sideria 穿越虚拟与现实的边界，降临到了这片土地。她的到来伴随着电流的嗡鸣和巨龙的咆哮。</Sideria.DescentLetter.Text>
```

---

## ? 验收标准

### 功能验收
- [ ] 友好降临成功生成 Sideria 小人
- [ ] 敌对降临成功生成敌对 Sideria
- [ ] 伴随巨龙成功生成
- [ ] 姿态动画正确播放
- [ ] 特效正确显示
- [ ] 音效正确播放
- [ ] 降临信件正确显示

### 性能验收
- [ ] 降临过程流畅（无卡顿）
- [ ] 无编译错误
- [ ] 无运行时错误

---

**状态**: ?? 配置完成 50%  
**下一步**: 创建 Def 文件和纹理资源  
**预计完成时间**: 1-2 小时（取决于美术资源准备）

---

**版本**: v1.6.63  
**文档**: Sideria 降临系统配置指南  
**作者**: TSS 开发团队

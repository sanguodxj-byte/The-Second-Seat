# The Second Seat - Narrator Event System Documentation
# 叙事者事件系统文档与参考

此文档详细说明了 "The Second Seat" 模组中的叙事者事件系统。
该系统允许叙事者（AI）根据好感度、游戏状态和自定义逻辑触发各种动态事件。
AI 可以阅读此文档以理解如何创建新的事件定义 (NarratorEventDef)。

This document details the Narrator Event System in "The Second Seat" mod.
It allows the Narrator (AI) to trigger dynamic events based on affinity, game state, 
and custom logic. AI can read this to understand how to create new NarratorEventDefs.

---

## 1. 核心概念 (Core Concepts)

- **NarratorEventDef**: 定义一个事件的完整结构。
- **Triggers**: 决定事件是否可以触发的条件列表（所有条件必须满足）。
- **Actions**: 事件触发时执行的操作列表（按顺序执行）。

---

## 2. 触发器 (Triggers)

所有触发器位于 `TheSecondSeat.Framework.Triggers` 命名空间下。

### AffinityRangeTrigger
检查叙事者好感度是否在指定范围内。

```xml
<li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
  <minAffinity>80</minAffinity>
  <maxAffinity>100</maxAffinity>
</li>
```

### ColonistCountTrigger
检查殖民者数量。

```xml
<li Class="TheSecondSeat.Framework.Triggers.ColonistCountTrigger">
  <minCount>3</minCount>
</li>
```

### WealthRangeTrigger
检查殖民地财富。

```xml
<li Class="TheSecondSeat.Framework.Triggers.WealthRangeTrigger">
  <minWealth>50000</minWealth>
  <checkTotal>true</checkTotal>
</li>
```

### SeasonTrigger
检查当前季节。

```xml
<li Class="TheSecondSeat.Framework.Triggers.SeasonTrigger">
  <allowedSeasons>
    <li>Spring</li>
    <li>Summer</li>
  </allowedSeasons>
</li>
```

### TimeRangeTrigger
检查当前时间（小时）。

```xml
<li Class="TheSecondSeat.Framework.Triggers.TimeRangeTrigger">
  <minHour>6</minHour>
  <maxHour>12</maxHour>
</li>
```

### 其他触发器
- **RandomChanceTrigger**: 额外的随机检查。
- **MoodStateTrigger**: 检查殖民者心情状态。

---

## 3. 动作 (Actions)

所有动作位于 `TheSecondSeat.Framework.Actions` 命名空间下。

### ShowDialogueAction
显示叙事者对话窗口。

```xml
<li Class="TheSecondSeat.Framework.Actions.ShowDialogueAction">
  <dialogueText>你好，指挥官！</dialogueText>
  <messageType>PositiveEvent</messageType>
</li>
```

### NarratorSpeakAction
类似 ShowDialogueAction，但支持 TTS 和更高级的文本键。

```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <text>我有些东西要给你。</text>
  <showDialogue>true</showDialogue>
</li>
```

### SpawnResourceAction
在玩家附近生成资源。

```xml
<li Class="TheSecondSeat.Framework.Actions.SpawnResourceAction">
  <delayTicks>120</delayTicks>
  <resourceType>Steel</resourceType>
  <amount>100</amount>
  <dropNearPlayer>true</dropNearPlayer>
</li>
```

### ModifyAffinityAction
修改叙事者好感度。

```xml
<li Class="TheSecondSeat.Framework.Actions.ModifyAffinityAction">
  <delta>5</delta>
  <reason>礼物赠送</reason>
</li>
```

### TriggerEventAction
链式触发另一个 NarratorEventDef。

```xml
<li Class="TheSecondSeat.Framework.Actions.TriggerEventAction">
  <targetEventDefName>AnotherEvent</targetEventDefName>
</li>
```

### StartIncidentAction
强制触发原版事件（如袭击、商队）。

```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>RaidEnemy</incidentDef>
  <points>1000</points>
  <forced>true</forced>
</li>
```

### StrikeLightningAction
降下雷击（惩罚用）。

```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeCount>5</strikeCount>
  <damageAmount>50</damageAmount>
  <causesFire>true</causesFire>
</li>
```

### GiveHediffAction
给殖民者添加健康状态（如疾病或增强）。

```xml
<li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
  <hediffDef>Flu</hediffDef>
  <targetCount>2</targetCount>
</li>
```

---

## 4. 完整示例 (Full Example)

```xml
<TheSecondSeat.Framework.NarratorEventDef>
  <defName>Example_HighAffinity_Gift</defName>
  <eventLabel>Affinity Gift</eventLabel>
  <category>Reward</category>
  <priority>50</priority>
  <chance>0.5</chance>
  <cooldownTicks>60000</cooldownTicks>
  
  <triggers>
    <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
      <minAffinity>80</minAffinity>
    </li>
  </triggers>
  
  <actions>
    <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
      <text>Take this gift, you earned it.</text>
    </li>
    <li Class="TheSecondSeat.Framework.Actions.SpawnResourceAction">
      <resourceType>Gold</resourceType>
      <amount>50</amount>
    </li>
  </actions>
</TheSecondSeat.Framework.NarratorEventDef>
```

---

# Sideria 降临系统文档

本文档详细介绍了 Sideria 叙事者降临系统 (Descent System) 的 XML 定义和配置，包括降临实体、召唤物（血棘龙与煌耀龙）、技能以及事件触发机制。

## 1. 核心概念

Sideria 的降临不仅仅是生成一个 Pawn，而是生成一个拥有特殊属性和能力的实体。

*   **肉体类型 (FleshType)**: 使用自定义的 `TSS_SideriaFlesh`，归类为 `CorpsesMechanoid`，以获得类似机械体的伤害效果和免疫部分状态，但保持生物特性。
*   **渲染树 (RenderTree)**: 使用自定义渲染树 `Sideria_SingleImage_RenderTree`，配合 `PawnRenderNodeWorker_SideriaBody` 实现单张纹理的三视图渲染（类似机械体）。
*   **种族定义 (ThingDef)**: `Sideria_DescentRace` 定义了实体的基础属性，如极高的生命值、抗性、无痛觉、不需睡眠进食等。

## 2. 实体定义 (Defs)

### 2.1 降临实体 (Avatar)

*   **ThingDef**: `Sideria_DescentRace`
    *   `fleshType`: `TSS_SideriaFlesh`
    *   `intelligence`: `Animal` (但这只是为了规避部分人类逻辑，实际上由自定义组件控制)
    *   `comps`: 包含 `TheSecondSeat.Components.CompProperties_DraftableAnimal`，使其可被征召。
*   **PawnKindDef**: `TSS_Sideria_Avatar`
    *   `race`: `Sideria_DescentRace`
    *   `lifeStages`: 定义了使用的纹理路径 `Sideria/Narrators/Descent/Pawn/Sideria_Full`。

### 2.2 召唤物：血棘龙 (BloodThorn Dragon)

Sideria 的攻击型召唤物，擅长近战和流血伤害。

*   **ThingDef**: `Sideria_BloodThornDragon_Race`
    *   `thinkTreeMain`: `Sideria_Dragon_ThinkTree` (自动索敌攻击)
    *   `baseBodySize`: 2.0
    *   `tools`: 包含造成流血的撕咬和尾击。
*   **PawnKindDef**: `Sideria_BloodThornDragon`
    *   `texPath`: `Sideria/Narrators/Descent/Dragon/BloodThorn` (单张纹理)
    *   `combatPower`: 200

### 2.3 召唤物：煌耀龙 (Radiant Dragon)

Sideria 的支援/控制型召唤物，擅长范围伤害或特殊效果（根据后续技能扩展）。

*   **ThingDef**: `Sideria_RadiantDragon_Race`
    *   `thinkTreeMain`: `Sideria_Dragon_ThinkTree`
    *   `baseBodySize`: 2.0
*   **PawnKindDef**: `Sideria_RadiantDragon`
    *   `texPath`: `Sideria/Narrators/Descent/Dragon/Radiant` (单张纹理)
    *   `combatPower`: 200

## 3. 技能与能力 (Abilities)

Sideria 的能力通过 `Sideria_DivineBody` Hediff 赋予。

### 3.1 核心技能

*   **Blood Nova (`Sideria_Skill_BloodNova`)**:
    *   效果：以自身为中心释放猩红能量爆发。
    *   伤害：`Sideria_Bleed_Damage` (猩红侵蚀)，附加 `Sideria_ScarletBloom_BuildUp` Hediff。
    *   视觉：红色透明爆炸效果。
*   **Titan Grasp (`Sideria_Skill_TitanGrasp`)**:
    *   效果：单体控制技能，造成眩晕。
    *   范围：24.9

### 3.2 召唤技能

*   **Summon: BloodThorn (`Sideria_Skill_SummonBloodThorn`)**:
    *   效果：在指定位置召唤一只血棘龙。
    *   冷却：3600 ticks
*   **Summon: Radiant (`Sideria_Skill_SummonRadiant`)**:
    *   效果：在指定位置召唤一只煌耀龙。
    *   冷却：3600 ticks

### 3.3 龙族专属技能

*   **Destructive Rend (`Sideria_Skill_DestructiveRend`)**:
    *   使用者：血棘龙
    *   效果：对受伤部位造成大量伤害。
*   **Punishing Strike (`Sideria_Skill_PunishingStrike`)**:
    *   使用者：煌耀龙
    *   效果：召唤金色轨道光束轰炸目标区域。
    *   引导时间：1秒

## 4. 状态与效果 (Hediffs & Damage)

### 4.1 神性躯体 (`Sideria_DivineBody`)

*   赋予 Sideria 所有主动技能。
*   提供巨大的属性加成（近战伤害、命中、闪避、射击精度）。
*   免疫绝大多数疾病和环境状态（中暑、中毒、感染等）。

### 4.2 猩红绽放 (`Sideria_ScarletBloom_BuildUp`)

*   一种可叠加的致命状态。
*   **机制**：
    *   `HediffComp_Stacker`: 叠加至 3 层时直接致死 (`onMaxStacks: Kill`)。
    *   自然消退：每天 -0.5 层。
*   **阶段**：
    1.  一之型
    2.  二之型
    3.  三之型·终焉 (致死)

### 4.3 猩红侵蚀伤害 (`Sideria_Bleed_Damage`)

*   造成 `Sideria_ScarletBloom_BuildUp` Hediff 的伤害类型。
*   无直接物理伤害 (`defaultDamage: 0`)，纯粹施加状态。
*   穿透所有护甲 (`harmAllLayersUntilOutside: true`)。

## 5. 事件定义 (Incidents)

定义了 Sideria 降临的两种形式，均由 C# 代码 (`IncidentWorker_NarratorDescent`) 触发。

*   **TSS_NarratorDescent_Friendly**: 友好/中立降临，协助玩家。
*   **TSS_NarratorDescent_Hostile**: 敌对降临，作为强力威胁出现。

## 6. AI 与行为

*   **Sideria_Dragon_ThinkTree**:
    *   定义了龙的简单 AI：倒地 -> 听从命令 (LordDuty) -> 自动攻击敌人 -> 随机徘徊。
    *   配合 `CompDraftableAnimal`，允许玩家在征召状态下控制它们，而在非征召状态下它们会自动索敌。

---

**注意**: 修改纹理路径或 DefName 时，请务必同步更新 C# 代码中的引用（如果存在硬编码）以及相关的 XML 引用。

---

# Sideria 降临系统 XML 定义参考

以下是 `Sideria_Descent_Defs.xml` 文件的完整内容副本，用于快速参考。

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Defs>
  <!--
    =====================================================
    ⭐ Sideria 降临系统完整定义
    ⭐ 包含：降临实体、战斗系统、思维树
    =====================================================
  -->

  <!-- ==================== 自定义肉体类型 (规避机械师系统限制) ==================== -->
  <FleshTypeDef>
    <defName>TSS_SideriaFlesh</defName>
    <corpseCategory>CorpsesMechanoid</corpseCategory>
    <damageEffecter>Damage_HitMechanoid</damageEffecter>
    <genericWounds>
      <li><texture>Things/Pawn/Wounds/WoundMechA</texture></li>
      <li><texture>Things/Pawn/Wounds/WoundMechB</texture></li>
      <li><texture>Things/Pawn/Wounds/WoundMechC</texture></li>
    </genericWounds>
  </FleshTypeDef>

  <!-- ==================== 渲染树定义 (单纹理三视图) ==================== -->
  <PawnRenderTreeDef>
    <defName>Sideria_SingleImage_RenderTree</defName>
    <root Class="PawnRenderNodeProperties_Parent">
      <debugLabel>Root</debugLabel>
      <tagDef>Root</tagDef>
      <children>
        <li>
          <debugLabel>Body</debugLabel>
          <tagDef>Body</tagDef>
          <nodeClass>PawnRenderNode_AnimalPart</nodeClass>
          <workerClass>TheSecondSeat.PawnRenderNodeWorker_SideriaBody</workerClass>
          <texPath>Sideria/Narrators/Descent/Pawn/Sideria_Full</texPath>
          <useRottenColor>false</useRottenColor>
        </li>
      </children>
    </root>
  </PawnRenderTreeDef>

  <!-- ==================== 降临实体种族定义 (Bio-Mechanoid) ==================== -->
  <ThingDef ParentName="BasePawn">
    <defName>Sideria_DescentRace</defName>
    <label>Sideria 'Dragon Guard' Descent Form</label>
    <description>The physical manifestation of Sideria, the Dragon Guard. A divine being walking among mortals, wielding the power of the dragon bloodline.</description>
    
    <statBases>
      <MarketValue>10000</MarketValue>
      <MoveSpeed>6.5</MoveSpeed>
      <ComfyTemperatureMin>-200</ComfyTemperatureMin>
      <ComfyTemperatureMax>200</ComfyTemperatureMax>
      <ArmorRating_Sharp>1.5</ArmorRating_Sharp>
      <ArmorRating_Blunt>1.5</ArmorRating_Blunt>
      <PsychicSensitivity>2.0</PsychicSensitivity>
      <Flammability>0</Flammability>
      <ToxicResistance>1</ToxicResistance>
      <GlobalLearningFactor>0</GlobalLearningFactor>
    </statBases>
    
    <race>
      <renderTree>Sideria_SingleImage_RenderTree</renderTree>
      <intelligence>Animal</intelligence>
      <thinkTreeMain>Animal</thinkTreeMain>
      <needsRest>false</needsRest>
      <foodType>None</foodType>
      <!-- 禁用所有动物功能 -->
      <trainability>None</trainability>
      <petness>0</petness>
      <playerCanChangeMaster>false</playerCanChangeMaster>
      <fleshType>TSS_SideriaFlesh</fleshType>
      <predator>false</predator>
      <manhunterOnDamageChance>0</manhunterOnDamageChance>
      <manhunterOnTameFailChance>0</manhunterOnTameFailChance>
      
      <body>Human</body>
      <baseBodySize>1.2</baseBodySize>
      <baseHealthScale>5.0</baseHealthScale>
      <lifeExpectancy>9999</lifeExpectancy>
      <hasGenders>false</hasGenders>
      <bloodDef>Filth_Blood</bloodDef>
      <!-- nameGenerator removed - Sideria's name is set directly in code -->
      
      <lifeStageAges>
        <li>
          <def>AnimalAdult</def>
          <minAge>0</minAge>
          <soundWounded>Pawn_Mech_Centipede_Wounded</soundWounded>
          <soundDeath>Pawn_Mech_Centipede_Death</soundDeath>
        </li>
      </lifeStageAges>
    </race>

    <!-- 禁止宰杀 -->
    <receivesSignals>false</receivesSignals>

    <inspectorTabs>
      <li>ITab_Pawn_Health</li>
      <li>ITab_Pawn_Needs</li>
      <li>ITab_Pawn_Character</li>
      <li>ITab_Pawn_Log</li>
    </inspectorTabs>

    <tools>
      <li>
        <label>divine strike</label>
        <capacities><li>Cut</li></capacities>
        <power>35</power>
        <cooldownTime>1.5</cooldownTime>
        <linkedBodyPartsGroup>RightHand</linkedBodyPartsGroup>
        <extraMeleeDamages>
          <li><def>Sideria_Bleed_Damage</def><amount>10</amount></li>
        </extraMeleeDamages>
      </li>
    </tools>

    <!-- 直接添加征召组件 -->
    <comps>
      <li Class="TheSecondSeat.Components.CompProperties_DraftableAnimal" />
    </comps>
  </ThingDef>

  <!-- ==================== 降临实体 PawnKindDef ==================== -->
  <PawnKindDef>
    <defName>TSS_Sideria_Avatar</defName>
    <label>Sideria 'Dragon Guard'</label>
    <race>Sideria_DescentRace</race>
    <combatPower>9999</combatPower>
    <defaultFactionType>Ancients</defaultFactionType> <aiAvoidCover>true</aiAvoidCover>
    <destroyGearOnDrop>true</destroyGearOnDrop>
    <apparelMoney>0</apparelMoney>
    <weaponMoney>0</weaponMoney>
    <initialWillRange>10~20</initialWillRange>
    <initialResistanceRange>10~20</initialResistanceRange>
    
    <lifeStages>
      <li>
        <bodyGraphicData>
          <texPath>Sideria/Narrators/Descent/Pawn/Sideria_Full</texPath>
          <graphicClass>Graphic_Multi</graphicClass>
          <drawSize>2.5</drawSize>
          <shaderType>CutoutComplex</shaderType>
        </bodyGraphicData>
      </li>
    </lifeStages>
  </PawnKindDef>

  <!-- ==================== 技能定义 ==================== -->
  <AbilityDef>
    <defName>Sideria_Skill_BloodNova</defName>
    <label>Blood Nova</label>
    <description>Unleashes a burst of corrupted energy.</description>
    <iconPath>UI/Narrators/Descent/Effects/Aura</iconPath>
    <cooldownTicksRange>600</cooldownTicksRange>
    <targetRequired>False</targetRequired>
    <verbProperties>
      <verbClass>Verb_CastAbility</verbClass>
      <range>15.9</range>
      <targetParams><canTargetSelf>True</canTargetSelf></targetParams>
    </verbProperties>
    <comps>
      <li Class="CompProperties_AbilityLaunchProjectile">
        <projectileDef>Sideria_BloodNova_Proj</projectileDef>
      </li>
    </comps>
  </AbilityDef>

  <ThingDef ParentName="BaseBullet">
    <defName>Sideria_BloodNova_Proj</defName>
    <label>blood nova</label>
    <graphicData>
      <texPath>Things/Projectile/Bullet_Small</texPath>
      <graphicClass>Graphic_Single</graphicClass>
      <shaderType>Transparent</shaderType>
      <color>(255, 0, 0, 255)</color>
    </graphicData>
    <projectile>
      <damageDef>Sideria_Bleed_Damage</damageDef>
      <damageAmountBase>25</damageAmountBase>
      <speed>100</speed>
      <explosionRadius>5.9</explosionRadius>
      <postExplosionSpawnThingDef>Filth_Blood</postExplosionSpawnThingDef>
    </projectile>
  </ThingDef>

  <AbilityDef>
    <defName>Sideria_Skill_TitanGrasp</defName>
    <label>Titan Grasp</label>
    <description>Crushes a single target.</description>
    <iconPath>UI/Narrators/Descent/Effects/Aura</iconPath>
    <cooldownTicksRange>1200</cooldownTicksRange>
    <verbProperties>
      <verbClass>Verb_CastAbility</verbClass>
      <range>24.9</range>
      <targetParams><canTargetPawns>True</canTargetPawns></targetParams>
    </verbProperties>
    <comps>
      <li Class="CompProperties_AbilityEffect">
        <compClass>CompAbilityEffect_Stun</compClass>
        <goodwillImpact>-30</goodwillImpact>
      </li>
    </comps>
  </AbilityDef>

  <!-- ==================== Hediff Giver Set (保留) ==================== -->
  <HediffGiverSetDef>
    <defName>Sideria_Innate_Givers</defName>
    <hediffGivers>
      <li Class="HediffGiver_Birthday">
        <hediff>Sideria_DivineBody</hediff>
      </li>
      <li Class="HediffGiver_Birthday">
        <hediff>MechlinkImplant</hediff>
      </li>
    </hediffGivers>
  </HediffGiverSetDef>

  <!-- ==================== 神性躯体 ==================== -->
  <HediffDef>
    <defName>Sideria_DivineBody</defName>
    <label>神性躯体</label>
    <description>Sideria 的神性构造赋予了她超越常理的抗性和力量，并解锁了龙血技能。</description>
    <hediffClass>HediffWithComps</hediffClass>
    <defaultLabelColor>(0.8, 0.8, 1.0)</defaultLabelColor>
    <isBad>false</isBad>
    <comps>
      <!-- 通过 Hediff 赋予技能 (使用 abilityDefs 列表一次性赋予多个) -->
      <li Class="HediffCompProperties_GiveAbility">
        <abilityDefs>
          <li>Sideria_Skill_BloodNova</li>
          <li>Sideria_Skill_TitanGrasp</li>
          <li>Sideria_Skill_SummonBloodThorn</li>
          <li>Sideria_Skill_SummonRadiant</li>
        </abilityDefs>
      </li>
    </comps>
    <stages>
      <li>
        <statOffsets>
          <MeleeDamageFactor>2.0</MeleeDamageFactor>
          <MeleeHitChance>10.0</MeleeHitChance>
          <MeleeDodgeChance>5.0</MeleeDodgeChance>
          <ShootingAccuracyPawn>10.0</ShootingAccuracyPawn>
        </statOffsets>
        <makeImmuneTo>
          <li>Hypothermia</li>
          <li>Heatstroke</li>
          <li>ToxicBuildup</li>
          <li>Scaria</li>
          <li>Flu</li>
          <li>Plague</li>
          <li>Malaria</li>
          <li>SleepingSickness</li>
          <li>WoundInfection</li>
          <li>FoodPoisoning</li>
          <li>CryptosleepSickness</li>
        </makeImmuneTo>
      </li>
    </stages>
  </HediffDef>

  <!-- ==================== 猩红绽放 Hediff (保留) ==================== -->
  <HediffDef>
    <defName>Sideria_ScarletBloom_BuildUp</defName>
    <label>猩红绽放</label>
    <description>猩红色的能量正在侵蚀目标的生命本质。叠加至3层时，目标将迎来终结。</description>
    <hediffClass>HediffWithComps</hediffClass>
    <defaultLabelColor>(0.8, 0.0, 0.0)</defaultLabelColor>
    <lethalSeverity>1.0</lethalSeverity>
    <comps>
      <li Class="HediffCompProperties_SeverityPerDay">
        <severityPerDay>-0.5</severityPerDay>
      </li>
      <li Class="TheSecondSeat.Components.HediffCompProperties_Stacker">
        <maxStacks>3</maxStacks>
        <severityPerStack>0.33</severityPerStack>
        <displayStackCount>true</displayStackCount>
        <onMaxStacks>Kill</onMaxStacks>
      </li>
    </comps>
    <stages>
      <li>
        <minSeverity>0.0</minSeverity>
        <label>一之型</label>
      </li>
      <li>
        <minSeverity>0.33</minSeverity>
        <label>二之型</label>
      </li>
      <li>
        <minSeverity>0.66</minSeverity>
        <label>三之型·终焉</label>
        <lifeThreatening>true</lifeThreatening>
      </li>
    </stages>
  </HediffDef>

  <!-- ==================== 猩红绽放 Damage (重命名为 Sideria_Bleed_Damage) ==================== -->
  <DamageDef>
    <defName>Sideria_Bleed_Damage</defName>
    <label>猩红侵蚀</label>
    <workerClass>DamageWorker_AddGlobal</workerClass>
    <externalViolence>false</externalViolence>
    <deathMessage>{0} 已被猩红绽放吞噬。</deathMessage>
    <hediff>Sideria_ScarletBloom_BuildUp</hediff>
    <hediffSolid>Sideria_ScarletBloom_BuildUp</hediffSolid>
    <defaultDamage>0</defaultDamage>
    <harmAllLayersUntilOutside>true</harmAllLayersUntilOutside>
    <impactSoundType>Toxic</impactSoundType>
    <armorCategory>Heat</armorCategory>
  </DamageDef>

  <!-- ==================== 龙的思维树 (自动攻击) ==================== -->
  <ThinkTreeDef>
    <defName>Sideria_Dragon_ThinkTree</defName>
    <thinkRoot Class="ThinkNode_Priority">
      <subNodes>
        <li Class="ThinkNode_Subtree">
          <treeDef>Downed</treeDef>
        </li>
        <li Class="ThinkNode_Subtree">
          <treeDef>LordDuty</treeDef>
        </li>
        <li Class="JobGiver_AIFightEnemies">
          <targetAcquireRadius>500</targetAcquireRadius>
          <targetKeepRadius>500</targetKeepRadius>
        </li>
        <li Class="JobGiver_WanderAnywhere">
          <maxDanger>Deadly</maxDanger>
        </li>
      </subNodes>
    </thinkRoot>
  </ThinkTreeDef>

  <!-- ==================== 血棘龙 (BloodThorn Dragon) ==================== -->
  <ThingDef ParentName="BasePawn">
    <defName>Sideria_BloodThornDragon_Race</defName>
    <label>BloodThorn Dragon</label>
    <description>A construct of blood and thorns, summoned by Sideria.</description>
    <statBases>
      <MarketValue>0</MarketValue>
      <MoveSpeed>5.0</MoveSpeed>
      <ArmorRating_Sharp>0.8</ArmorRating_Sharp>
      <ArmorRating_Blunt>0.5</ArmorRating_Blunt>
      <ComfyTemperatureMin>-100</ComfyTemperatureMin>
      <ComfyTemperatureMax>100</ComfyTemperatureMax>
    </statBases>
    <race>
      <thinkTreeMain>Sideria_Dragon_ThinkTree</thinkTreeMain>
      <intelligence>Animal</intelligence>
      <fleshType>TSS_SideriaFlesh</fleshType>
      <needsRest>false</needsRest>
      <foodType>None</foodType>
      <trainability>None</trainability>
      <lifeExpectancy>100</lifeExpectancy>
      <hasGenders>false</hasGenders>
      <body>QuadrupedAnimalWithPawsAndTail</body>
      <baseBodySize>2.0</baseBodySize>
      <baseHealthScale>3.0</baseHealthScale>
      <lifeStageAges>
        <li>
          <def>AnimalAdult</def>
          <minAge>0</minAge>
          <soundWounded>Pawn_Mech_Scyther_Wounded</soundWounded>
          <soundDeath>Pawn_Mech_Scyther_Death</soundDeath>
        </li>
      </lifeStageAges>
    </race>
    <tools>
      <li>
        <label>thorn bite</label>
        <capacities><li>Bite</li></capacities>
        <power>20</power>
        <cooldownTime>1.6</cooldownTime>
        <linkedBodyPartsGroup>Teeth</linkedBodyPartsGroup>
        <chanceFactor>0.7</chanceFactor>
      </li>
      <li>
        <label>tail slash</label>
        <capacities><li>Cut</li></capacities>
        <power>15</power>
        <cooldownTime>1.5</cooldownTime>
        <linkedBodyPartsGroup>HeadAttackTool</linkedBodyPartsGroup>
      </li>
    </tools>
    <comps>
      <li Class="TheSecondSeat.Components.CompProperties_DraftableAnimal" />
    </comps>
  </ThingDef>

  <PawnKindDef>
    <defName>Sideria_BloodThornDragon</defName>
    <label>BloodThorn Dragon</label>
    <race>Sideria_BloodThornDragon_Race</race>
    <combatPower>200</combatPower>
    <defaultFactionType>Ancients</defaultFactionType>
    <lifeStages>
      <li>
        <bodyGraphicData>
          <texPath>Sideria/Narrators/Descent/Dragon/BloodThorn</texPath>
          <graphicClass>Graphic_Single</graphicClass>
          <drawSize>3.0</drawSize>
        </bodyGraphicData>
      </li>
    </lifeStages>
  </PawnKindDef>

  <!-- ==================== 煌耀龙 (Radiant Dragon) ==================== -->
  <ThingDef ParentName="BasePawn">
    <defName>Sideria_RadiantDragon_Race</defName>
    <label>Radiant Dragon</label>
    <description>A construct of pure light, summoned by Sideria.</description>
    <statBases>
      <MarketValue>0</MarketValue>
      <MoveSpeed>6.0</MoveSpeed>
      <ArmorRating_Sharp>0.6</ArmorRating_Sharp>
      <ArmorRating_Blunt>0.4</ArmorRating_Blunt>
      <ComfyTemperatureMin>-100</ComfyTemperatureMin>
      <ComfyTemperatureMax>100</ComfyTemperatureMax>
    </statBases>

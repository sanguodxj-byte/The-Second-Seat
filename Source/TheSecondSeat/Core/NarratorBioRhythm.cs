using System;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// ⭐ v1.9.6: 活动状态枚举
    /// </summary>
    public enum NarratorActivity
    {
        Observing,      // 观察殖民地
        Analyzing,      // 分析数据/推演
        Resting,        // 休息/发呆
        MealTime,       // 进食/品茶
        Alert,          // 警觉 (有威胁时)
        Greeting        // 刚被唤醒/问候玩家
    }

    public enum LifeState
    {
        Sleeping,       // 睡眠 (深夜)
        MorningRoutine, // 晨间准备 (早上)
        Working,        // 工作中 (叙事/推演) - 这是玩家在线时的主要状态
        Leisure,        // 闲暇/摸鱼 (傍晚)
        MealTime        // 进食
    }

    /// <summary>
    /// ⭐ v1.9.6: 叙事者生物节律系统（增强版）
    /// ⭐ v2.4.0: 威胁检测阈值可配置
    /// 新增：精力系统、活动状态、行为指导
    /// </summary>
    public class NarratorBioRhythm : GameComponent
    {
        private float currentMood = 50f; // 0-100
        private float targetMood = 50f;
        private int ticksToNextMoodUpdate = 0;
        
        // ⭐ v1.9.6: 精力系统
        private float currentEnergy = 80f; // 0-100
        private NarratorActivity currentActivity = NarratorActivity.Observing;
        private int ticksToNextActivityChange = 0;
        
        // ⭐ v2.2.0: 服装系统 (由 LLM 或节律控制)
        private string currentApparelTag = "";

        // 情绪惯性 (越小变化越快)
        private const float MoodInertia = 0.05f; 
        
        // ⭐ v1.9.6: 精力惯性
        private const float EnergyDecayRate = 0.01f; // 每次更新衰减
        private const float EnergyRecoveryRate = 0.02f; // 白天恢复速度
        
        // ⭐ v2.4.0: 威胁检测配置（可通过设置调整）
        private static float threatAlertThreshold = 1000f;  // 威胁点数阈值
        private static float threatAlertChance = 0.3f;      // 进入警觉状态的概率

        /// <summary>
        /// 当前情绪值 (0-100)
        /// </summary>
        public float CurrentMood => currentMood;
        
        /// <summary>
        /// ⭐ v1.9.6: 当前精力值 (0-100)
        /// </summary>
        public float CurrentEnergy => currentEnergy;
        
        /// <summary>
        /// ⭐ v1.9.6: 当前活动状态
        /// </summary>
        public NarratorActivity CurrentActivity => currentActivity;

        /// <summary>
        /// ⭐ v2.2.0: 当前服装标签 (为空表示默认/法袍)
        /// </summary>
        public string CurrentApparelTag
        {
            get => currentApparelTag;
            set => currentApparelTag = value;
        }

        public NarratorBioRhythm(Game game) {}

        public override void GameComponentTick()
        {
            var settings = TheSecondSeat.Settings.TheSecondSeatMod.Settings;
            if (settings != null && !settings.enableBioRhythm) return;

            // 每 250 ticks (约4秒) 更新一次数值计算，极低开销
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                UpdateMood();
            }
            
            // ⭐ v2.3.0: 每 60 ticks (约1秒) 检测一次打瞌睡状态
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                NarratorIdleSystem.Tick();
            }
        }

        private void UpdateMood()
        {
            float oldMood = currentMood;

            // 1. 情绪自然漂移 (向目标值插值)
            currentMood = Mathf.Lerp(currentMood, targetMood, MoodInertia);

            // 2. 周期性改变目标情绪 (模拟"没来由"的心情变化)
            ticksToNextMoodUpdate -= 250;
            if (ticksToNextMoodUpdate <= 0)
            {
                // 设置下一个随机情绪目标 (30-70是常态，极端值概率低)
                targetMood = Mathf.Clamp(Rand.Gaussian(50f, 15f), 0f, 100f);
                
                // 下次变动在 2-4 游戏小时后 (5000-10000 ticks)
                ticksToNextMoodUpdate = Rand.Range(5000, 10000); 
            }

            // 3. 表情/立绘联动 (Visual Feedback)
            // 如果情绪发生极端的改变（例如跌破 20 或 突破 80），可以主动触发一次表情更新
            CheckAndTriggerExpressionChange(oldMood, currentMood);
            
            // ⭐ v1.9.6: 更新精力和活动
            UpdateEnergy();
            UpdateActivity();
        }
        
        /// <summary>
        /// ⭐ v1.9.6: 更新精力值
        /// </summary>
        private void UpdateEnergy()
        {
            int hour = DateTime.Now.Hour;
            
            // 深夜 (22:00 - 7:00): 精力快速下降
            if (hour >= 22 || hour < 7)
            {
                currentEnergy -= EnergyDecayRate * 3f;
            }
            // 白天 (7:00 - 22:00): 精力缓慢恢复或维持
            else
            {
                currentEnergy += EnergyRecoveryRate;
            }
            
            // 精力受心情影响
            if (currentMood > 70f)
            {
                currentEnergy += 0.005f; // 好心情增加精力
            }
            else if (currentMood < 30f)
            {
                currentEnergy -= 0.005f; // 坏心情消耗精力
            }
            
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, 100f);
        }
        
        /// <summary>
        /// ⭐ v1.9.6: 更新当前活动
        /// </summary>
        private void UpdateActivity()
        {
            ticksToNextActivityChange -= 250;
            if (ticksToNextActivityChange > 0) return;
            
            // 下次活动变化在 1-3 游戏小时后
            ticksToNextActivityChange = Rand.Range(2500, 7500);
            
            int hour = DateTime.Now.Hour;
            
            // 根据时间和精力决定活动
            if (hour >= 0 && hour < 7)
            {
                // 深夜：主要休息
                currentActivity = currentEnergy < 30f ? NarratorActivity.Resting : NarratorActivity.Observing;
            }
            else if (hour >= 7 && hour < 9)
            {
                // 早晨：晨间准备
                currentActivity = Rand.Chance(0.5f) ? NarratorActivity.MealTime : NarratorActivity.Observing;
            }
            else if (hour >= 12 && hour < 14)
            {
                // 午餐时间
                currentActivity = Rand.Chance(0.6f) ? NarratorActivity.MealTime : NarratorActivity.Analyzing;
            }
            else if (hour >= 18 && hour < 20)
            {
                // 晚餐时间
                currentActivity = Rand.Chance(0.6f) ? NarratorActivity.MealTime : NarratorActivity.Resting;
            }
            else
            {
                // 工作时间：根据精力分配
                if (currentEnergy > 60f)
                {
                    currentActivity = Rand.Chance(0.7f) ? NarratorActivity.Analyzing : NarratorActivity.Observing;
                }
                else if (currentEnergy > 30f)
                {
                    currentActivity = NarratorActivity.Observing;
                }
                else
                {
                    currentActivity = NarratorActivity.Resting;
                }
            }
            
            // ⭐ v2.4.0: 检查威胁状态（使用可配置阈值）
            try
            {
                if (Find.CurrentMap != null)
                {
                    float threatPoints = RimWorld.StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap);
                    // 如果威胁点数超过阈值，进入警觉状态
                    if (threatPoints > threatAlertThreshold && Rand.Chance(threatAlertChance))
                    {
                        currentActivity = NarratorActivity.Alert;
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[NarratorBioRhythm] 威胁检测触发: {threatPoints:F0} > {threatAlertThreshold:F0}");
                        }
                    }
                }
            }
            catch { /* 忽略错误 */ }
        }
        
        /// <summary>
        /// ⭐ v2.4.0: 设置威胁检测阈值
        /// </summary>
        /// <param name="threshold">威胁点数阈值（默认1000）</param>
        /// <param name="chance">进入警觉状态的概率（默认0.3）</param>
        public static void SetThreatAlertConfig(float threshold, float chance)
        {
            threatAlertThreshold = Mathf.Max(0f, threshold);
            threatAlertChance = Mathf.Clamp01(chance);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[NarratorBioRhythm] 威胁检测配置更新: 阈值={threatAlertThreshold}, 概率={threatAlertChance}");
            }
        }
        
        /// <summary>
        /// ⭐ v2.4.0: 获取当前威胁检测阈值
        /// </summary>
        public static float ThreatAlertThreshold => threatAlertThreshold;
        
        /// <summary>
        /// ⭐ v2.4.0: 获取当前威胁警觉概率
        /// </summary>
        public static float ThreatAlertChance => threatAlertChance;

        private void CheckAndTriggerExpressionChange(float oldMood, float newMood)
        {
            string currentDefName = NarratorController.CurrentPersonaDefName;
            if (string.IsNullOrEmpty(currentDefName)) return;

            // 跌破 20
            if (oldMood >= 20f && newMood < 20f)
            {
                // 强制切换为悲伤/疲惫表情
                ExpressionSystem.SetExpression(currentDefName, ExpressionType.Sad, 300, "Mood low threshold");
            }
            // 突破 80
            else if (oldMood <= 80f && newMood > 80f)
            {
                // 强制切换为开心表情
                ExpressionSystem.SetExpression(currentDefName, ExpressionType.Happy, 300, "Mood high threshold");
            }
        }

        /// <summary>
        /// ⭐ v1.9.6: 获取当前生物节律上下文（增强版 - 包含行为指导）
        /// </summary>
        public string GetCurrentBioContext()
        {
            var settings = TheSecondSeat.Settings.TheSecondSeatMod.Settings;
            if (settings != null && !settings.enableBioRhythm) return "";

            var now = DateTime.Now;
            string scheduleState = GetScheduleStateDescription(now);
            string moodDesc = GetMoodDescription();
            string energyDesc = GetEnergyDescription();
            string activityDesc = GetActivityDescription();
            
            // ⭐ v1.9.6: 生成行为指导
            string behaviorInstruction = GenerateBehaviorInstruction();
            
            return $"{ "TSS_Bio_Context_Title".Translate() }\n" +
                   $"- { "TSS_Bio_Time".Translate() }: {now:HH:mm} ({scheduleState})\n" +
                   $"- { "TSS_Bio_Energy".Translate() }: {currentEnergy:F0}/100 ({energyDesc})\n" +
                   $"- { "TSS_Bio_Activity".Translate() }: {currentActivity} ({activityDesc})\n" +
                   $"- { "TSS_Bio_Mood".Translate() }: {currentMood:F0}/100 ({moodDesc})\n" +
                   $"{ "TSS_Bio_Instruction_Label".Translate() } {behaviorInstruction}\n";
        }
        
        /// <summary>
        /// ⭐ v1.9.6: 生成行为指导（直接指导 LLM 如何扮演当前状态）
        /// </summary>
        private string GenerateBehaviorInstruction()
        {
            var sb = new System.Text.StringBuilder();
            
            // 仅在极端状态下添加强制指导，否则留空让 LLM 自行发挥
            
            // 极端精力指导
            if (currentEnergy < 10f)
            {
                sb.Append("TSS_Bio_Inst_Exhausted".Translate() + " ");
            }
            
            // 极端心情指导
            if (currentMood < 10f)
            {
                sb.Append("TSS_Bio_Inst_BadMood".Translate() + " ");
            }
            
            // 特殊活动指导 (仅保留关键活动)
            switch (currentActivity)
            {
                case NarratorActivity.MealTime:
                    sb.Append("TSS_Bio_Inst_MealTime".Translate() + " ");
                    break;
                case NarratorActivity.Alert:
                    sb.Append("TSS_Bio_Inst_Alert".Translate() + " ");
                    break;
            }
            
            // 时间指导 (深夜)
            int hour = DateTime.Now.Hour;
            if (hour >= 2 && hour < 5) // 缩小深夜范围，避免误伤熬夜党
            {
                sb.Append("TSS_Bio_Inst_LateNight".Translate() + " ");
            }
            
            // 如果没有特殊指令，提示 LLM 参考数据
            if (sb.Length == 0)
            {
                // 使用通用指令 (如果没有对应的 Key，就直接用英文或硬编码中文，这里假设有一个默认 Key)
                // 或者留空，完全依赖 Prompt 中的指示
                // sb.Append("TSS_Bio_Inst_Default".Translate());
            }
            
            return sb.ToString().Trim();
        }

        private string GetScheduleStateDescription(DateTime time)
        {
            int hour = time.Hour;
            if (hour >= 0 && hour < 7) return "TSS_Bio_Schedule_DeepNight".Translate();
            if (hour >= 7 && hour < 9) return "TSS_Bio_Schedule_Morning".Translate();
            if (hour >= 9 && hour < 12) return "TSS_Bio_Schedule_MorningWork".Translate();
            if (hour >= 12 && hour < 14) return "TSS_Bio_Schedule_Midday".Translate();
            if (hour >= 14 && hour < 18) return "TSS_Bio_Schedule_Afternoon".Translate();
            if (hour >= 18 && hour < 20) return "TSS_Bio_Schedule_Evening".Translate();
            if (hour >= 20 && hour < 23) return "TSS_Bio_Schedule_NightRest".Translate();
            return "TSS_Bio_Schedule_DeepNight".Translate();
        }

        private string GetMoodDescription()
        {
            if (currentMood > 80) return "TSS_Bio_Mood_Elated".Translate();
            if (currentMood > 60) return "TSS_Bio_Mood_Happy".Translate();
            if (currentMood > 40) return "TSS_Bio_Mood_Calm".Translate();
            if (currentMood > 20) return "TSS_Bio_Mood_Gloomy".Translate();
            return "TSS_Bio_Mood_Irritable".Translate();
        }
        
        /// <summary>
        /// ⭐ v1.9.6: 精力描述
        /// </summary>
        private string GetEnergyDescription()
        {
            if (currentEnergy > 80) return "TSS_Bio_Energy_Energetic".Translate();
            if (currentEnergy > 60) return "TSS_Bio_Energy_Active".Translate();
            if (currentEnergy > 40) return "TSS_Bio_Energy_Normal".Translate();
            if (currentEnergy > 20) return "TSS_Bio_Energy_Tired".Translate();
            return "TSS_Bio_Energy_Exhausted".Translate();
        }
        
        /// <summary>
        /// ⭐ v1.9.6: 活动描述
        /// </summary>
        private string GetActivityDescription()
        {
            return currentActivity switch
            {
                NarratorActivity.Observing => "TSS_Bio_Activity_Observing".Translate(),
                NarratorActivity.Analyzing => "TSS_Bio_Activity_Analyzing".Translate(),
                NarratorActivity.Resting => "TSS_Bio_Activity_Resting".Translate(),
                NarratorActivity.MealTime => "TSS_Bio_Activity_MealTime".Translate(),
                NarratorActivity.Alert => "TSS_Bio_Activity_Alert".Translate(),
                NarratorActivity.Greeting => "TSS_Bio_Activity_Greeting".Translate(),
                _ => "TSS_Bio_Activity_Idle".Translate()
            };
        }

        // 必须保存数据，否则读档后心情会重置
        public override void ExposeData()
        {
            Scribe_Values.Look(ref currentMood, "currentMood", 50f);
            Scribe_Values.Look(ref targetMood, "targetMood", 50f);
            Scribe_Values.Look(ref ticksToNextMoodUpdate, "ticksToNextMoodUpdate", 0);
            
            // ⭐ v1.9.6: 保存新增字段
            Scribe_Values.Look(ref currentEnergy, "currentEnergy", 80f);
            Scribe_Values.Look(ref currentActivity, "currentActivity", NarratorActivity.Observing);
            Scribe_Values.Look(ref ticksToNextActivityChange, "ticksToNextActivityChange", 0);
            
            // ⭐ v2.2.0: 保存服装标签
            Scribe_Values.Look(ref currentApparelTag, "currentApparelTag", "");
        }
        
        /// <summary>
        /// ⭐ v1.9.6: 设置活动状态（供外部调用，例如袭击开始时）
        /// </summary>
        public void SetActivity(NarratorActivity activity)
        {
            currentActivity = activity;
            ticksToNextActivityChange = Rand.Range(2500, 5000); // 重置计时器
        }
        
        /// <summary>
        /// ⭐ v1.9.6: 修改精力值（供外部调用）
        /// </summary>
        public void ModifyEnergy(float delta)
        {
            currentEnergy = Mathf.Clamp(currentEnergy + delta, 0f, 100f);
        }

        /// <summary>
        /// ⭐ v2.2.0: 直接设置精力值
        /// </summary>
        public void SetEnergy(float value)
        {
            currentEnergy = Mathf.Clamp(value, 0f, 100f);
        }
    }
}

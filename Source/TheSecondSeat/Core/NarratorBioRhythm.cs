using System;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Core
{
    public enum LifeState
    {
        Sleeping,       // 睡眠 (深夜)
        MorningRoutine, // 晨间准备 (早上)
        Working,        // 工作中 (叙事/推演) - 这是玩家在线时的主要状态
        Leisure,        // 闲暇/摸鱼 (傍晚)
        MealTime        // 进食
    }

    public class NarratorBioRhythm : GameComponent
    {
        private float currentMood = 50f; // 0-100
        private float targetMood = 50f;
        private int ticksToNextMoodUpdate = 0;
        
        // 情绪惯性 (越小变化越快)
        private const float MoodInertia = 0.05f; 

        /// <summary>
        /// 当前情绪值 (0-100)
        /// </summary>
        public float CurrentMood => currentMood;

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
        }

        private void UpdateMood()
        {
            float oldMood = currentMood;

            // 1. 情绪自然漂移 (向目标值插值)
            currentMood = Mathf.Lerp(currentMood, targetMood, MoodInertia);

            // 2. 周期性改变目标情绪 (模拟“没来由”的心情变化)
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
        }

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

        // 获取当前的时间状态描述
        public string GetCurrentBioContext()
        {
            var settings = TheSecondSeat.Settings.TheSecondSeatMod.Settings;
            if (settings != null && !settings.enableBioRhythm) return "";

            var now = DateTime.Now;
            string scheduleState = GetScheduleStateDescription(now);
            string moodDesc = GetMoodDescription();
            
            return $"[叙事者状态]\n" +
                   $"- 现实时间: {now:HH:mm}\n" +
                   $"- 生理状态: {scheduleState}\n" +
                   $"- 当前心情值: {currentMood:F1}/100 ({moodDesc})\n";
        }

        private string GetScheduleStateDescription(DateTime time)
        {
            int hour = time.Hour;
            if (hour >= 0 && hour < 7) return "深夜(被玩家唤醒，困倦)";
            if (hour >= 7 && hour < 9) return "清晨(刚起床)";
            if (hour >= 9 && hour < 18) return "工作时间(精神集中)";
            if (hour >= 18 && hour < 20) return "晚餐时间(可能在进食)";
            if (hour >= 20 && hour < 23) return "晚间休息(放松)";
            return "深夜";
        }

        private string GetMoodDescription()
        {
            if (currentMood > 80) return "极度兴奋/开心";
            if (currentMood > 60) return "愉悦";
            if (currentMood > 40) return "平静";
            if (currentMood > 20) return "低落/郁闷";
            return "极度抑郁/烦躁";
        }

        // 必须保存数据，否则读档后心情会重置
        public override void ExposeData()
        {
            Scribe_Values.Look(ref currentMood, "currentMood", 50f);
            Scribe_Values.Look(ref targetMood, "targetMood", 50f);
            Scribe_Values.Look(ref ticksToNextMoodUpdate, "ticksToNextMoodUpdate", 0);
        }
    }
}

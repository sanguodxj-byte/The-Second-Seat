using System;
using System.Collections.Generic;
using TheSecondSeat.LLM;
using TheSecondSeat.PersonaGeneration;
using Verse;

namespace TheSecondSeat.Core.Components
{
    /// <summary>
    /// Handles narrator expression scheduling and application
    /// </summary>
    public class NarratorExpressionController
    {
        private class ScheduledExpression
        {
            public string narratorDefName;
            public ExpressionType expression;
            public int triggerTick; // 触发的游戏 tick
            public int durationTicks;
            public int intensity;
        }

        private List<ScheduledExpression> scheduledExpressions = new List<ScheduledExpression>();

        public void Tick()
        {
            ProcessScheduledExpressions();
            
            // ⭐ v1.11.3: 更新口型动画系统状态
            // 确保 TTS 播放状态被正确跟踪
            MouthAnimationSystem.Update(UnityEngine.Time.deltaTime);
        }

        /// <summary>
        /// ? v1.6.82: 应用表情 - 支持单表情和多表情序列
        /// </summary>
        public void ApplyExpressionFromResponse(LLMResponse response, string narratorDefName, string dialogueText)
        {
            // 优先级: emotionSequence > emotions > expression > 自动推断
            
            // 1. 检查详细情绪序列 (emotionSequence)
            if (response.emotionSequence != null && response.emotionSequence.Count > 0)
            {
                ApplyEmotionSequence(response.emotionSequence, narratorDefName);
                return;
            }
            
            // 2. 检查紧凑情绪序列 (emotions: "happy|sad|angry")
            if (!string.IsNullOrEmpty(response.emotions))
            {
                ApplyCompactEmotionSequence(response.emotions, narratorDefName, dialogueText);
                return;
            }
            
            // 3. 单表情模式 (expression)
            if (!string.IsNullOrEmpty(response.expression))
            {
                // ? 修复：如果 expression 包含管道符，视为紧凑序列处理
                if (response.expression.Contains("|"))
                {
                    Log.Warning($"[NarratorController] 检测到 expression 字段包含多个表情 ({response.expression})，自动转为序列处理");
                    ApplyCompactEmotionSequence(response.expression, narratorDefName, dialogueText);
                    return;
                }

                // 使用 EmotionParser 解析
                var (expressionType, intensity) = EmotionParser.Parse(response.expression);

                if (expressionType != ExpressionType.Neutral || intensity > 0)
                {
                    ExpressionSystem.SetExpression(
                        narratorDefName,
                        expressionType,
                        180,  // 3 秒
                        "对话触发",
                        intensity // 传递解析出的强度
                    );
                    Log.Message($"[NarratorController] AI 表情切换: {expressionType} (强度: {intensity})");
                }
                else
                {
                    Log.Warning($"[NarratorController] 解析表情失败或为中性: {response.expression}");
                    // 回退到自动推断
                    ExpressionSystem.UpdateExpressionByDialogueTone(narratorDefName, dialogueText);
                }
                return;
            }
            
            // 4. 没有提供表情，自动推断
            ExpressionSystem.UpdateExpressionByDialogueTone(narratorDefName, dialogueText);
        }

        /// <summary>
        /// ? v1.6.82: 应用详细情绪序列
        /// </summary>
        private void ApplyEmotionSequence(List<EmotionSegment> segments, string narratorDefName)
        {
            Log.Message($"[NarratorController] 应用情绪序列: {segments.Count} 个片段");
            
            // 计算总时长
            float totalDuration = 0f;
            foreach (var segment in segments)
            {
                // 如果没有指定时长，按文本长度估算 (中文约 3字/秒)
                float segmentDuration = segment.estimatedDuration > 0
                    ? segment.estimatedDuration
                    : segment.text.Length / 3f;
                totalDuration += segmentDuration;
            }
            
            // 创建延迟表情切换任务
            float accumulatedDelay = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                float delay = accumulatedDelay;
                
                // 解析情绪标签
                var (expressionType, intensity) = EmotionParser.Parse(segment.emotion);
                
                if (expressionType != ExpressionType.Neutral || intensity > 0)
                {
                    // 使用延迟任务设置表情
                    int delayTicks = (int)(delay * 60); // 秒转换为 ticks
                    float segmentDuration = segment.estimatedDuration > 0
                        ? segment.estimatedDuration
                        : segment.text.Length / 3f;
                    int durationTicks = (int)(segmentDuration * 60);
                    
                    // 记录延迟执行
                    ScheduleExpressionChange(narratorDefName, expressionType, delayTicks, durationTicks, intensity);
                    
                    accumulatedDelay += segmentDuration;
                }
            }
        }

        /// <summary>
        /// ? v1.6.82: 应用紧凑情绪序列 (emotions: "happy|sad|angry")
        /// </summary>
        private void ApplyCompactEmotionSequence(string emotionsStr, string narratorDefName, string dialogueText)
        {
            string[] emotions = emotionsStr.Split('|');
            Log.Message($"[NarratorController] 应用紧凑情绪序列: {emotionsStr} ({emotions.Length} 个)");
            
            // 按句子分割对话（按标点）
            string[] sentences = System.Text.RegularExpressions.Regex.Split(dialogueText, @"(?<=[。！？\.!\?])");
            sentences = System.Array.FindAll(sentences, s => !string.IsNullOrWhiteSpace(s));
            
            // 计算每个情绪的持续时间
            float totalChars = dialogueText.Length;
            float charsPerSecond = 3f; // 中文约 3字/秒
            float totalDuration = totalChars / charsPerSecond;
            float durationPerEmotion = totalDuration / emotions.Length;
            
            // 设置第一个表情（立即）
            if (emotions.Length > 0)
            {
                var (firstExpressionType, firstIntensity) = EmotionParser.Parse(emotions[0].Trim());
                
                if (firstExpressionType != ExpressionType.Neutral || firstIntensity > 0)
                {
                    int firstDuration = (int)(durationPerEmotion * 60);
                    ExpressionSystem.SetExpression(
                        narratorDefName,
                        firstExpressionType,
                        firstDuration,
                        "情绪序列-1",
                        firstIntensity
                    );
                    Log.Message($"[NarratorController] 情绪 1/{emotions.Length}: {firstExpressionType} (强度: {firstIntensity}) (立即)");
                }
            }
            
            // 调度后续表情切换
            for (int i = 1; i < emotions.Length; i++)
            {
                var (expressionType, intensity) = EmotionParser.Parse(emotions[i].Trim());
                
                if (expressionType != ExpressionType.Neutral || intensity > 0)
                {
                    // 延迟时间 = 前面所有表情的持续时间总和
                    int delayTicks = (int)(i * durationPerEmotion * 60);
                    int durationTicks = (int)(durationPerEmotion * 60);
                    
                    ScheduleExpressionChange(narratorDefName, expressionType, delayTicks, durationTicks, intensity);
                    Log.Message($"[NarratorController] 情绪 {i+1}/{emotions.Length}: {expressionType} (强度: {intensity}) (延迟 {delayTicks} ticks)");
                }
            }
        }

        /// <summary>
        /// ? 调度一个延迟的表情切换
        /// </summary>
        private void ScheduleExpressionChange(string narratorDefName, ExpressionType expression, int delayTicks, int durationTicks, int intensity = 0)
        {
            int currentTick = GenTicks.TicksGame;
            scheduledExpressions.Add(new ScheduledExpression
            {
                narratorDefName = narratorDefName,
                expression = expression,
                triggerTick = currentTick + delayTicks,
                durationTicks = durationTicks,
                intensity = intensity
            });
        }

        /// <summary>
        /// ? 在 GameComponentTick 中调用，处理到期的表情切换
        /// </summary>
        private void ProcessScheduledExpressions()
        {
            if (scheduledExpressions.Count == 0) return;
            
            int currentTick = GenTicks.TicksGame;
            
            // 使用倒序循环避免额外的 List 分配
            for (int i = scheduledExpressions.Count - 1; i >= 0; i--)
            {
                var item = scheduledExpressions[i];
                if (item.triggerTick <= currentTick)
                {
                    // 应用表情
                    ExpressionSystem.SetExpression(
                        item.narratorDefName,
                        item.expression,
                        item.durationTicks,
                        "定时情绪序列",
                        item.intensity // 传递 intensity
                    );
                    
                    // 从列表中移除
                    scheduledExpressions.RemoveAt(i);
                }
            }
        }
    }
}

using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using TheSecondSeat.RimAgent;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// v2.8.8: Agent 高级配置窗口（精简版）
    /// 包含：多模态分析 + RimAgent 设置 + 并发管理
    /// LLM 和 TTS 设置已移至主设置界面的独立选项卡
    /// </summary>
    public class Dialog_UnifiedAgentSettings : Window
    {
        private Settings.TheSecondSeatSettings settings;
        private Vector2 scrollPosition = Vector2.zero;
        
        // 紧凑行高
        private const float CompactRowHeight = 24f;
        private const float LabelWidth = 120f;
        
        public override Vector2 InitialSize => new Vector2(650f, 400f);

        public Dialog_UnifiedAgentSettings()
        {
            var mod = LoadedModManager.GetMod<Settings.TheSecondSeatMod>();
            settings = mod?.GetSettings<Settings.TheSecondSeatSettings>();
            
            this.doCloseX = true;
            this.doCloseButton = false;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, "Agent 高级配置");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域
            Rect contentRect = new Rect(0f, 35f, inRect.width, inRect.height - 80f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 400f);
            
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float y = 0f;
            float width = viewRect.width;
            
            // ===== 1. 多模态分析配置 =====
            y = DrawSectionCompact(viewRect, y, "多模态分析", (float startY) =>
            {
                float cy = startY;
                
                // 启用开关
                Rect checkRect = new Rect(0, cy, width, CompactRowHeight);
                Widgets.CheckboxLabeled(checkRect, "启用多模态分析", ref settings.enableMultimodalAnalysis);
                cy += CompactRowHeight;
                
                if (settings.enableMultimodalAnalysis)
                {
                    // 提供商
                    Widgets.Label(new Rect(0, cy, LabelWidth, CompactRowHeight), "提供商:");
                    float btnWidth = (width - LabelWidth - 10) / 3f;
                    if (Widgets.ButtonText(new Rect(LabelWidth, cy, btnWidth - 2, CompactRowHeight - 2), "OpenAI", true, true, settings.multimodalProvider == "openai"))
                        settings.multimodalProvider = "openai";
                    if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth, cy, btnWidth - 2, CompactRowHeight - 2), "DeepSeek", true, true, settings.multimodalProvider == "deepseek"))
                        settings.multimodalProvider = "deepseek";
                    if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth * 2, cy, btnWidth - 2, CompactRowHeight - 2), "Gemini", true, true, settings.multimodalProvider == "gemini"))
                        settings.multimodalProvider = "gemini";
                    cy += CompactRowHeight + 4;
                    
                    cy = DrawCompactTextField(cy, width, "API 密钥:", ref settings.multimodalApiKey, true);
                    cy = DrawCompactTextField(cy, width, "视觉模型:", ref settings.visionModel);
                    
                    // 保存按钮
                    if (Widgets.ButtonText(new Rect(width - 100, cy, 95, 22), "保存多模态"))
                    {
                        SaveMultimodalSettings();
                    }
                    cy += 26;
                }
                
                return cy;
            });
            
            // ===== 2. RimAgent 设置 =====
            y = DrawSectionCompact(viewRect, y, "RimAgent 设置", (float startY) =>
            {
                float cy = startY;
                
                // Agent 名称
                cy = DrawCompactTextField(cy, width, "Agent名称:", ref settings.agentName);
                
                // 重试和历史 - 同一行
                Widgets.Label(new Rect(0, cy, 80, CompactRowHeight), $"重试: {settings.maxRetries}");
                settings.maxRetries = (int)Widgets.HorizontalSlider(new Rect(80, cy + 4, 100, 16f), settings.maxRetries, 1, 10);
                Widgets.Label(new Rect(200, cy, 80, CompactRowHeight), $"延迟: {settings.retryDelay:F1}s");
                settings.retryDelay = Widgets.HorizontalSlider(new Rect(280, cy + 4, 100, 16f), settings.retryDelay, 0.5f, 10f);
                Widgets.Label(new Rect(400, cy, 80, CompactRowHeight), $"历史: {settings.maxHistoryMessages}");
                settings.maxHistoryMessages = (int)Widgets.HorizontalSlider(new Rect(480, cy + 4, width - 490, 16f), settings.maxHistoryMessages, 5, 100);
                cy += CompactRowHeight + 4;
                
                return cy;
            });
            
            // ===== 3. 并发管理设置 =====
            y = DrawSectionCompact(viewRect, y, "并发管理", (float startY) =>
            {
                float cy = startY;
                
                // 同一行布局
                Widgets.Label(new Rect(0, cy, 80, CompactRowHeight), $"并发: {settings.maxConcurrent}");
                settings.maxConcurrent = (int)Widgets.HorizontalSlider(new Rect(80, cy + 4, 120, 16f), settings.maxConcurrent, 1, 20);
                Widgets.Label(new Rect(220, cy, 100, CompactRowHeight), $"超时: {settings.requestTimeout}s");
                settings.requestTimeout = (int)Widgets.HorizontalSlider(new Rect(320, cy + 4, 120, 16f), settings.requestTimeout, 10, 300);
                Widgets.CheckboxLabeled(new Rect(460, cy, width - 470, CompactRowHeight), "启用重试", ref settings.enableRetry);
                cy += CompactRowHeight + 4;
                
                return cy;
            });
            
            Widgets.EndScrollView();
            
            // ===== 底部按钮 =====
            float btnY = inRect.height - 40f;
            if (Widgets.ButtonText(new Rect(10, btnY, 120, 32), "应用设置"))
            {
                ApplySettings();
                Messages.Message("Agent 配置已应用", MessageTypeDefOf.PositiveEvent);
            }
            
            if (Widgets.ButtonText(new Rect(inRect.width - 80, btnY, 70, 32), "关闭"))
            {
                Close();
            }
        }
        
        /// <summary>
        /// 绘制紧凑型文本输入行
        /// </summary>
        private float DrawCompactTextField(float y, float width, string label, ref string value, bool isPassword = false)
        {
            Widgets.Label(new Rect(0, y, LabelWidth, CompactRowHeight), label);
            
            Rect inputRect = new Rect(LabelWidth, y, width - LabelWidth - 5, CompactRowHeight - 2);
            
            if (isPassword && !string.IsNullOrEmpty(value))
            {
                // 密码字段 - 显示遮罩但编辑时显示真实值
                string displayValue = new string('*', Math.Min(value.Length, 30));
                if (Mouse.IsOver(inputRect))
                {
                    value = Widgets.TextField(inputRect, value);
                }
                else
                {
                    Widgets.Label(inputRect, displayValue);
                    if (Widgets.ButtonInvisible(inputRect))
                    {
                        // 点击时启动编辑
                    }
                }
            }
            else
            {
                value = Widgets.TextField(inputRect, value ?? "");
            }
            
            return y + CompactRowHeight + 2;
        }
        
        /// <summary>
        /// 绘制紧凑型分组区域
        /// </summary>
        private float DrawSectionCompact(Rect viewRect, float startY, string title, Func<float, float> drawContent)
        {
            float y = startY;
            float width = viewRect.width;
            
            // 标题背景
            Rect titleRect = new Rect(0, y, width, 22f);
            Widgets.DrawBoxSolid(titleRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(8, y, width - 10, 22f), title);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 24f;
            
            // 内容
            y = drawContent(y);
            
            y += 6f;
            
            return y;
        }
        
        /// <summary>
        /// 保存多模态设置
        /// </summary>
        private void SaveMultimodalSettings()
        {
            try
            {
                if (settings.enableMultimodalAnalysis)
                {
                    PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                        settings.multimodalProvider,
                        settings.multimodalApiKey,
                        settings.visionModel,
                        settings.textAnalysisModel
                    );
                }
                settings.Write();
                Messages.Message("多模态设置已保存", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Messages.Message($"保存失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 应用并保存所有设置
        /// </summary>
        private void ApplySettings()
        {
            try
            {
                // 配置多模态分析
                if (settings.enableMultimodalAnalysis)
                {
                    PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                        settings.multimodalProvider,
                        settings.multimodalApiKey,
                        settings.visionModel,
                        settings.textAnalysisModel
                    );
                }
                
                // 更新并发管理器设置
                ConcurrentRequestManager.Instance.UpdateSettings(
                    settings.maxConcurrent,
                    settings.requestTimeout,
                    settings.enableRetry
                );
                
                // 保存到磁盘
                settings.Write();
                
                Log.Message("[UnifiedAgentSettings] Agent 配置已应用并保存");
            }
            catch (Exception ex)
            {
                Log.Error($"[UnifiedAgentSettings] 应用设置失败: {ex.Message}");
                Messages.Message($"应用设置失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}

using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Narrator;
using TheSecondSeat.Settings;
using TheSecondSeat.Utils;
using TheSecondSeat.Core;
using System;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ✅ v1.9.0 Refactored: 全身立绘面板（协调器类）
    /// 这个类现在作为协调者，将绘制、交互、动画等逻辑委托给专门的处理器类。
    /// </summary>
    [StaticConstructorOnStartup]
    public class FullBodyPortraitPanel
    {
        // ==================== 子系统处理器 ====================
        public readonly PortraitDrawer Drawer;
        public readonly PortraitInteractionHandler InteractionHandler;
        public readonly PortraitAnimationHandler AnimationHandler;
        public readonly FloatingTextSystem FloatingTextSystem;
        
        // ==================== 核心状态和数据 ====================
        public Rect DrawRect { get; private set; }
        public NarratorPersonaDef CurrentPersona { get; private set; }
        public Storyteller.StorytellerAgent StorytellerAgent => Current.Game?.GetComponent<Storyteller.StorytellerAgent>();

        private ExpressionType lastExpression = ExpressionType.Neutral;
        private int portraitUpdateTick = 0;
        private const int PORTRAIT_UPDATE_INTERVAL = 30;

        // ==================== 初始化 ====================
        
        public FullBodyPortraitPanel()
        {
            // 初始化子系统
            Drawer = new PortraitDrawer(this);
            InteractionHandler = new PortraitInteractionHandler(this);
            AnimationHandler = new PortraitAnimationHandler(this);
            FloatingTextSystem = new FloatingTextSystem();
            
            // 初始化尺寸和位置
            float width = TSSFrameworkConfig.Portrait.OriginalWidth * TSSFrameworkConfig.Portrait.DefaultScaleFactor;
            float height = TSSFrameworkConfig.Portrait.OriginalHeight * TSSFrameworkConfig.Portrait.DefaultScaleFactor;
            float x = TSSFrameworkConfig.Portrait.PanelOffsetX;
            float y = (Verse.UI.screenHeight - height) / 2f + TSSFrameworkConfig.Portrait.PanelOffsetY;
            DrawRect = new Rect(x, y, width, height);
        }

        // ==================== 主绘制与更新循环 ====================

        /// <summary>
        /// 主绘制入口（由 PortraitOverlaySystem 调用）
        /// </summary>
        public void Draw()
        {
            // 1. 更新核心数据
            UpdatePortrait();
            
            // 2. 更新动画状态机
            AnimationHandler.Update();
            MouthAnimationSystem.Update(Time.deltaTime);
            
            // 3. 处理用户输入与交互 (此方法内部会绘制交互UI)
            InteractionHandler.HandleInteractions();

            // 4. 绘制立绘本身
            Drawer.Draw();
            
            // 5. 绘制浮动文字
            FloatingTextSystem.UpdateAndDraw();

            // 6. 绘制缩放手柄 (如果鼠标悬停)
            if (Mouse.IsOver(DrawRect))
            {
                Rect handleRect = new Rect(DrawRect.xMax - 20f, DrawRect.yMax - 20f, 20f, 20f);
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.DrawTextureFitted(handleRect, TexUI.WinExpandWidget, 1f);
                GUI.color = Color.white;
            }
            
            // 7. 绘制工具提示
            if (Mouse.IsOver(DrawRect) && !AnimationHandler.IsPlayingAnimation)
            {
                string tooltip = BuildTooltip();
                TooltipHandler.TipRegion(DrawRect, tooltip);
            }
        }
        
        // ==================== 公共接口 (供外部和子系统调用) ====================

        public bool TriggerPostureAnimation(string postureName, string effectName, float duration, Action callback = null)
        {
            return AnimationHandler.TriggerAnimation(postureName, effectName, duration, callback);
        }

        public void TriggerExpression(ExpressionType expression, float duration = 2f, int intensity = 0)
        {
            if (CurrentPersona == null) return;
            ExpressionSystem.SetExpression(CurrentPersona.defName, expression, ExpressionTrigger.Manual, (int)(duration * 60), intensity);
            if (lastExpression != expression)
            {
                LayeredPortraitCompositor.ClearCache(CurrentPersona.defName, lastExpression);
            }
        }
        
        public void RestoreDefaultExpression()
        {
            if (CurrentPersona == null) return;
            var agent = StorytellerAgent;
            if (agent != null)
            {
                ExpressionType defaultExpression = agent.currentMood switch
                {
                    Storyteller.MoodState.Melancholic or Storyteller.MoodState.Angry => ExpressionType.Sad,
                    _ => agent.GetAffinity() switch
                    {
                        > 80f => ExpressionType.Shy,
                        > 40f => ExpressionType.Happy,
                        > -20f => ExpressionType.Neutral,
                        > -60f => ExpressionType.Sad,
                        _ => ExpressionType.Angry
                    }
                };
                TriggerExpression(defaultExpression, duration: 99999f);
            }
            else
            {
                TriggerExpression(ExpressionType.Neutral, duration: 99999f);
            }
        }

        public void AddFloatingText(string text, Color color)
        {
            Vector2 startPos = new Vector2(DrawRect.center.x, DrawRect.y + 50f);
            FloatingTextSystem.Add(text, startPos, color);
        }
        
        public void ModifyAffinity(float delta, string reason)
        {
            StorytellerAgent?.ModifyAffinity(delta, reason);
        }

        public void ShowInteractionText(string phrase)
        {
            if (!string.IsNullOrEmpty(phrase) && CurrentPersona != null)
            {
                string narratorName = !string.IsNullOrEmpty(CurrentPersona.label) ? CurrentPersona.label : CurrentPersona.narratorName;
                DialogueOverlayPanel.SetStreamingMessage($"【{narratorName}】{phrase}");
                DialogueOverlayPanel.StartStreaming(0f); // 交互文本瞬时出现
            }
        }
        
        public void UpdateDrawRect(float x, float y, float width, float height)
        {
            DrawRect = new Rect(x, y, width, height);
        }
        
        public string GetPersonaResourceName()
        {
            return CurrentPersona?.GetResourceName() ?? "";
        }
        
        public bool HasDescentResources()
        {
            if (CurrentPersona == null) return false;
            return TSS_AssetLoader.HasDescentResources(GetPersonaResourceName());
        }

        // ==================== 私有方法 (核心逻辑) ====================

        private void UpdatePortrait()
        {
            if (Find.TickManager.TicksGame - portraitUpdateTick < PORTRAIT_UPDATE_INTERVAL) return;
            portraitUpdateTick = Find.TickManager.TicksGame;

            try
            {
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                CurrentPersona = persona;

                if (persona != null)
                {
                    var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
                    if (expressionState.CurrentExpression != lastExpression)
                    {
                        lastExpression = expressionState.CurrentExpression;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[FullBodyPortraitPanel] 更新立绘失败: {ex.Message}");
                CurrentPersona = null;
            }
        }

        private string BuildTooltip()
        {
            if (CurrentPersona == null) return "";
            
            bool shiftHeld = Event.current.shift;
            string tooltip = $"{CurrentPersona.narratorName}\n表情: {lastExpression}";
            
            tooltip += shiftHeld ? "\n\n? 互动模式已激活" : "\n\n?? 按住 Shift 键激活互动模式";

            return tooltip;
        }
    }
}

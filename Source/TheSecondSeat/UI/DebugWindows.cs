using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.PersonaGeneration.Scriban;
using TheSecondSeat.Narrator;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Core;
// 使用别名避免 AffinityTier 冲突
using AffinityTier = TheSecondSeat.Narrator.AffinityTier;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ⭐ v1.9.7: 统一调试面板入口
    /// 提供一站式访问所有调试工具
    /// </summary>
    public class Dialog_DebugHub : Window
    {
        private NarratorManager manager;
        private NarratorPersonaDef persona;

        public override Vector2 InitialSize => new Vector2(400f, 500f);

        public Dialog_DebugHub(NarratorManager manager, NarratorPersonaDef persona)
        {
            this.manager = manager;
            this.persona = persona;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.closeOnCancel = true;
            this.absorbInputAroundWindow = true;
            this.draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), "TSS_Debug_Hub_Title".Translate());
            Text.Font = GameFont.Small;
            curY += 50f;

            // 当前叙事者信息
            Widgets.DrawBoxSolid(new Rect(0f, curY, inRect.width, 60f), new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.Label(new Rect(10f, curY + 5f, inRect.width - 20f, 25f), 
                "TSS_Debug_CurrentNarrator".Translate(persona?.narratorName ?? "None"));
            Widgets.Label(new Rect(10f, curY + 30f, inRect.width - 20f, 25f), 
                $"DefName: {persona?.defName ?? "N/A"}");
            curY += 70f;

            // 调试工具按钮
            float buttonHeight = 40f;
            float buttonSpacing = 10f;

            // 好感度调试
            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, buttonHeight), "TSS_Debug_Btn_Favorability".Translate()))
            {
                Find.WindowStack.Add(new Dialog_FavorabilityDebug(manager));
            }
            curY += buttonHeight + buttonSpacing;

            // 表情调试
            if (persona != null && Widgets.ButtonText(new Rect(0f, curY, inRect.width, buttonHeight), "TSS_Debug_Btn_Expression".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ExpressionDebug(persona));
            }
            curY += buttonHeight + buttonSpacing;

            // 生物节律调试
            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, buttonHeight), "TSS_Debug_Btn_BioRhythm".Translate()))
            {
                Find.WindowStack.Add(new Dialog_BioRhythmDebug());
            }
            curY += buttonHeight + buttonSpacing;

            // 模板渲染测试
            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, buttonHeight), "TSS_Debug_Btn_TemplateTest".Translate()))
            {
                Find.WindowStack.Add(new Dialog_TemplateRenderTest(persona));
            }
            curY += buttonHeight + buttonSpacing;

            // 模式切换
            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, buttonHeight), "TSS_Debug_Btn_ModeSwitch".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ModeSwitchDebug(manager));
            }
            curY += buttonHeight + buttonSpacing;

            // 快速测试对话
            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, buttonHeight), "TSS_Debug_Btn_QuickTest".Translate()))
            {
                // 触发一次测试对话
                Messages.Message("TSS_Debug_TestDialogTriggered".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            curY += buttonHeight + buttonSpacing;

            // LLM 调试器 (Token Monitor)
            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, buttonHeight), "LLM Debugger & Token Monitor"))
            {
                Find.WindowStack.Add(new Dialog_LLMDebugger());
            }
            curY += buttonHeight + buttonSpacing;

            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "TSS_Debug_Close".Translate()))
            {
                Close();
            }
        }
    }

    /// <summary>
    /// ⭐ v1.9.7: 生物节律调试窗口
    /// 快速调整精力、心情、活动状态
    /// </summary>
    public class Dialog_BioRhythmDebug : Window
    {
        private NarratorBioRhythm bioRhythm;
        private float targetEnergy = 80f;
        private float targetMood = 50f;

        public override Vector2 InitialSize => new Vector2(550f, 600f);

        public Dialog_BioRhythmDebug()
        {
            this.bioRhythm = Current.Game?.GetComponent<NarratorBioRhythm>();
            if (bioRhythm != null)
            {
                targetEnergy = bioRhythm.CurrentEnergy;
                targetMood = bioRhythm.CurrentMood;
            }
            this.doCloseButton = false;
            this.doCloseX = true;
            this.closeOnCancel = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (bioRhythm == null)
            {
                Widgets.Label(inRect, "TSS_Debug_BioRhythm_NotAvailable".Translate());
                return;
            }

            float curY = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), "TSS_Debug_BioRhythm_Title".Translate());
            Text.Font = GameFont.Small;
            curY += 50f;

            // ============ 精力区域 ============
            DrawSectionHeader(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_Energy_Section".Translate());
            curY += 30f;

            // 精力条
            DrawStatusBar(new Rect(0f, curY, inRect.width, 25f), bioRhythm.CurrentEnergy, 100f, GetEnergyColor(bioRhythm.CurrentEnergy));
            curY += 30f;

            // 精力描述
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), 
                $"{ "TSS_Debug_CurrentValue".Translate() }: {bioRhythm.CurrentEnergy:F0}/100 ({GetEnergyDescription(bioRhythm.CurrentEnergy)})");
            curY += 30f;

            // 精力调整按钮
            float buttonWidth = (inRect.width - 40f) / 5f;
            float buttonX = 0f;

            string[] energyLabels = { "-50", "-10", "+10", "+50", "MAX" };
            float[] energyDeltas = { -50f, -10f, 10f, 50f, 100f - bioRhythm.CurrentEnergy };

            for (int i = 0; i < energyLabels.Length; i++)
            {
                if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 30f), energyLabels[i]))
                {
                    bioRhythm.ModifyEnergy(energyDeltas[i]);
                }
                buttonX += buttonWidth + 10f;
            }
            curY += 45f;

            // ============ 活动状态区域 ============
            DrawSectionHeader(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_Activity_Section".Translate());
            curY += 30f;

            // 当前活动
            GUI.color = GetActivityColor(bioRhythm.CurrentActivity);
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), 
                $"{ "TSS_Debug_CurrentActivity".Translate() }: {bioRhythm.CurrentActivity} ({GetActivityDescription(bioRhythm.CurrentActivity)})");
            GUI.color = Color.white;
            curY += 30f;

            // 活动切换按钮 (两行)
            var activities = Enum.GetValues(typeof(NarratorActivity)).Cast<NarratorActivity>().ToArray();
            buttonWidth = (inRect.width - 20f) / 3f;
            buttonX = 0f;

            for (int i = 0; i < activities.Length; i++)
            {
                if (i > 0 && i % 3 == 0)
                {
                    curY += 35f;
                    buttonX = 0f;
                }

                var activity = activities[i];
                bool isCurrent = activity == bioRhythm.CurrentActivity;
                
                GUI.color = isCurrent ? Color.green : GetActivityColor(activity);
                if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 30f), activity.ToString()))
                {
                    bioRhythm.SetActivity(activity);
                }
                GUI.color = Color.white;
                buttonX += buttonWidth + 10f;
            }
            curY += 45f;

            // ============ 心情区域 ============
            DrawSectionHeader(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_Mood_Section".Translate());
            curY += 30f;

            // 心情条
            DrawStatusBar(new Rect(0f, curY, inRect.width, 25f), bioRhythm.CurrentMood, 100f, GetMoodColor(bioRhythm.CurrentMood));
            curY += 30f;

            // 心情描述
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), 
                $"{ "TSS_Debug_CurrentValue".Translate() }: {bioRhythm.CurrentMood:F0}/100 ({GetMoodDescription(bioRhythm.CurrentMood)})");
            curY += 40f;

            // ============ 行为指导预览 ============
            DrawSectionHeader(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_BehaviorInstruction".Translate());
            curY += 30f;

            string bioContext = bioRhythm.GetCurrentBioContext();
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.9f, 0.8f);
            Widgets.Label(new Rect(0f, curY, inRect.width, 80f), bioContext);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "TSS_Debug_Close".Translate()))
            {
                Close();
            }
        }

        private void DrawSectionHeader(Rect rect, string text)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.3f, 0.4f, 0.5f));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 5f, rect.y, rect.width - 10f, rect.height), text);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawStatusBar(Rect rect, float value, float max, Color fillColor)
        {
            float normalized = Mathf.Clamp01(value / max);
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * normalized, rect.height), fillColor);
            Widgets.DrawBox(rect, 1);
        }

        private Color GetEnergyColor(float energy)
        {
            if (energy > 70f) return new Color(0.3f, 0.8f, 0.3f);
            if (energy > 40f) return new Color(0.8f, 0.8f, 0.3f);
            if (energy > 20f) return new Color(0.8f, 0.5f, 0.2f);
            return new Color(0.8f, 0.2f, 0.2f);
        }

        private string GetEnergyDescription(float energy)
        {
            if (energy > 80) return "TSS_Bio_Energy_Energetic".Translate();
            if (energy > 60) return "TSS_Bio_Energy_Active".Translate();
            if (energy > 40) return "TSS_Bio_Energy_Normal".Translate();
            if (energy > 20) return "TSS_Bio_Energy_Tired".Translate();
            return "TSS_Bio_Energy_Exhausted".Translate();
        }

        private Color GetActivityColor(NarratorActivity activity)
        {
            return activity switch
            {
                NarratorActivity.Observing => new Color(0.5f, 0.7f, 0.9f),
                NarratorActivity.Analyzing => new Color(0.7f, 0.5f, 0.9f),
                NarratorActivity.Resting => new Color(0.6f, 0.6f, 0.6f),
                NarratorActivity.MealTime => new Color(0.9f, 0.7f, 0.4f),
                NarratorActivity.Alert => new Color(0.9f, 0.3f, 0.3f),
                NarratorActivity.Greeting => new Color(0.4f, 0.9f, 0.6f),
                _ => Color.white
            };
        }

        private string GetActivityDescription(NarratorActivity activity)
        {
            return activity switch
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

        private Color GetMoodColor(float mood)
        {
            if (mood > 70f) return new Color(0.3f, 0.9f, 0.5f);
            if (mood > 40f) return new Color(0.7f, 0.7f, 0.7f);
            if (mood > 20f) return new Color(0.7f, 0.5f, 0.7f);
            return new Color(0.6f, 0.3f, 0.3f);
        }

        private string GetMoodDescription(float mood)
        {
            if (mood > 80) return "TSS_Bio_Mood_Elated".Translate();
            if (mood > 60) return "TSS_Bio_Mood_Happy".Translate();
            if (mood > 40) return "TSS_Bio_Mood_Calm".Translate();
            if (mood > 20) return "TSS_Bio_Mood_Gloomy".Translate();
            return "TSS_Bio_Mood_Irritable".Translate();
        }
    }

    /// <summary>
    /// ⭐ v1.9.7: 模板渲染测试窗口
    /// 测试 Scriban 模板渲染结果
    /// </summary>
    public class Dialog_TemplateRenderTest : Window
    {
        private NarratorPersonaDef persona;
        private Vector2 scrollPosition;
        private string selectedTemplate = "SystemPrompt_Master_Scriban";
        private string renderResult = "";
        private bool isRendering = false;

        private static readonly string[] availableTemplates = new[]
        {
            "SystemPrompt_Master_Scriban",
            "SystemPrompt_EventDirector_Scriban",
            "Event_Identity",
            "Event_Context",
            "OutputFormat_Structure",
            "OutputFormat_Fields"
        };

        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public Dialog_TemplateRenderTest(NarratorPersonaDef persona)
        {
            this.persona = persona;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.closeOnCancel = true;
            this.absorbInputAroundWindow = true;
            this.draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), "TSS_Debug_Template_Title".Translate());
            Text.Font = GameFont.Small;
            curY += 50f;

            // 模板选择
            Widgets.Label(new Rect(0f, curY, 100f, 25f), "TSS_Debug_SelectTemplate".Translate());
            if (Widgets.ButtonText(new Rect(110f, curY, 300f, 25f), selectedTemplate))
            {
                var options = new List<FloatMenuOption>();
                foreach (var template in availableTemplates)
                {
                    var t = template; // 闭包捕获
                    options.Add(new FloatMenuOption(t, () => selectedTemplate = t));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            curY += 35f;

            // 渲染按钮
            if (Widgets.ButtonText(new Rect(0f, curY, 150f, 35f), "TSS_Debug_RenderTemplate".Translate()))
            {
                RenderTemplate();
            }

            // 复制结果按钮
            if (!string.IsNullOrEmpty(renderResult) && Widgets.ButtonText(new Rect(160f, curY, 150f, 35f), "TSS_Debug_CopyResult".Translate()))
            {
                GUIUtility.systemCopyBuffer = renderResult;
                Messages.Message("TSS_Debug_CopiedToClipboard".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            curY += 45f;

            // 结果显示区域
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_RenderResult".Translate());
            curY += 30f;

            var resultRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 50f);
            Widgets.DrawBoxSolid(resultRect, new Color(0.08f, 0.08f, 0.08f, 0.95f));

            if (isRendering)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(resultRect, "TSS_Debug_Rendering".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else if (!string.IsNullOrEmpty(renderResult))
            {
                var viewRect = new Rect(0f, 0f, resultRect.width - 20f, Text.CalcHeight(renderResult, resultRect.width - 30f) + 20f);
                Widgets.BeginScrollView(resultRect.ContractedBy(5f), ref scrollPosition, viewRect);
                
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.9f, 0.95f, 0.9f);
                Widgets.Label(new Rect(5f, 5f, viewRect.width - 10f, viewRect.height), renderResult);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                
                Widgets.EndScrollView();
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(resultRect, "TSS_Debug_ClickToRender".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "TSS_Debug_Close".Translate()))
            {
                Close();
            }
        }

        private void RenderTemplate()
        {
            isRendering = true;
            try
            {
                // 构建测试上下文
                var manager = NarratorManager.Instance;
                if (manager == null)
                {
                    renderResult = "[Error: NarratorManager not available]";
                    return;
                }
                var context = PromptContextBuilder.Build(manager);
                
                // 渲染模板
                renderResult = PromptRenderer.Render(selectedTemplate, context);
                
                // 添加统计信息
                int charCount = renderResult.Length;
                int lineCount = renderResult.Split('\n').Length;
                renderResult = $"[统计: {charCount} 字符, {lineCount} 行]\n\n{renderResult}";
            }
            catch (Exception ex)
            {
                renderResult = $"[渲染错误]\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
            }
            finally
            {
                isRendering = false;
            }
        }
    }

    /// <summary>
    /// ⭐ v1.9.7: 模式切换调试窗口
    /// 快速切换 Assistant/Opponent/Engineer 模式
    /// </summary>
    public class Dialog_ModeSwitchDebug : Window
    {
        private NarratorManager manager;

        public override Vector2 InitialSize => new Vector2(450f, 350f);

        public Dialog_ModeSwitchDebug(NarratorManager manager)
        {
            this.manager = manager;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.closeOnCancel = true;
            this.absorbInputAroundWindow = true;
            this.draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), "TSS_Debug_ModeSwitch_Title".Translate());
            Text.Font = GameFont.Small;
            curY += 50f;

            // 当前模式
            var currentMode = manager?.CurrentNarratorMode ?? NarratorMode.Assistant;
            GUI.color = GetModeColor(currentMode);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 30f), 
                "TSS_Debug_CurrentMode".Translate(currentMode.ToString()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 50f;

            // 模式切换按钮
            float buttonHeight = 50f;

            // Assistant 模式
            DrawModeButton(new Rect(0f, curY, inRect.width, buttonHeight), 
                NarratorMode.Assistant, 
                "TSS_Debug_Mode_Assistant".Translate(), 
                "TSS_Debug_Mode_Assistant_Desc".Translate(),
                currentMode == NarratorMode.Assistant);
            curY += buttonHeight + 10f;

            // Opponent 模式
            DrawModeButton(new Rect(0f, curY, inRect.width, buttonHeight), 
                NarratorMode.Opponent, 
                "TSS_Debug_Mode_Opponent".Translate(), 
                "TSS_Debug_Mode_Opponent_Desc".Translate(),
                currentMode == NarratorMode.Opponent);
            curY += buttonHeight + 10f;

            // Engineer 模式
            DrawModeButton(new Rect(0f, curY, inRect.width, buttonHeight), 
                NarratorMode.Engineer, 
                "TSS_Debug_Mode_Engineer".Translate(), 
                "TSS_Debug_Mode_Engineer_Desc".Translate(),
                currentMode == NarratorMode.Engineer);
            curY += buttonHeight + 10f;

            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "TSS_Debug_Close".Translate()))
            {
                Close();
            }
        }

        private void DrawModeButton(Rect rect, NarratorMode mode, string title, string desc, bool isCurrent)
        {
            if (isCurrent)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.4f, 0.3f, 0.5f));
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.3f, 0.3f, 0.3f));
            }

            Widgets.DrawBox(rect, 1);

            // 标题
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = GetModeColor(mode);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 25f), title);
            
            // 描述
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 28f, rect.width - 20f, 20f), desc);
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(rect) && !isCurrent)
            {
                manager?.SetNarratorMode(mode);
                Messages.Message("TSS_Debug_ModeSwitched".Translate(mode.ToString()), MessageTypeDefOf.NeutralEvent);
            }
        }

        private Color GetModeColor(NarratorMode mode)
        {
            return mode switch
            {
                NarratorMode.Assistant => new Color(0.4f, 0.8f, 0.5f),
                NarratorMode.Opponent => new Color(0.9f, 0.4f, 0.4f),
                NarratorMode.Engineer => new Color(0.5f, 0.6f, 0.9f),
                _ => Color.white
            };
        }
    }

    /// <summary>
    /// 好感度调试窗口
    /// 快速调整好感度，测试不同等级的表现
    /// </summary>
    public class Dialog_FavorabilityDebug : Window
    {
        private NarratorManager manager;
        private float targetFavorability;

        public override Vector2 InitialSize => new Vector2(520f, 650f);

        public Dialog_FavorabilityDebug(NarratorManager manager)
        {
            this.manager = manager;
            this.targetFavorability = manager.Favorability;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.closeOnCancel = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), "TSS_Debug_Favorability_Title".Translate());
            Text.Font = GameFont.Small;
            curY += 50f;

            // 当前好感度显示
            var currentTier = manager.CurrentTier;
            var tierColor = GetTierColor(currentTier);
            
            GUI.color = tierColor;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 30f), 
                "TSS_Debug_CurrentFavorability".Translate(manager.Favorability.ToString("F0"), GetTierName(currentTier)));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 40f;

            // 好感度条可视化
            DrawFavorabilityBar(new Rect(0f, curY, inRect.width, 30f), manager.Favorability);
            curY += 40f;

            // 快速调整按钮
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_QuickAdjust".Translate());
            curY += 30f;

            float buttonWidth = (inRect.width - 30f) / 4f;
            float buttonX = 0f;

            if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 35f), "-100"))
            {
                manager.ModifyFavorability(-100f, "调试：快速减少");
                targetFavorability = manager.Favorability;
            }
            buttonX += buttonWidth + 10f;

            if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 35f), "-10"))
            {
                manager.ModifyFavorability(-10f, "调试：减少");
                targetFavorability = manager.Favorability;
            }
            buttonX += buttonWidth + 10f;

            if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 35f), "+10"))
            {
                manager.ModifyFavorability(10f, "调试：增加");
                targetFavorability = manager.Favorability;
            }
            buttonX += buttonWidth + 10f;

            if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 35f), "+100"))
            {
                manager.ModifyFavorability(100f, "调试：快速增加");
                targetFavorability = manager.Favorability;
            }
            curY += 45f;

            // 滑动条精确设置
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_PreciseSet".Translate());
            curY += 30f;

            targetFavorability = Widgets.HorizontalSlider(
                new Rect(0f, curY, inRect.width, 25f), 
                targetFavorability, 
                -1000f, 
                1000f, 
                true, 
                $"{targetFavorability:F0}"
            );
            curY += 35f;

            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, 35f), "TSS_Debug_Apply".Translate(targetFavorability.ToString("F0"))))
            {
                float change = targetFavorability - manager.Favorability;
                manager.ModifyFavorability(change, $"Debug: Set to {targetFavorability:F0}");
            }
            curY += 45f;

            // 快捷等级设置
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "TSS_Debug_QuickTier".Translate());
            curY += 30f;

            buttonX = 0f;
            buttonWidth = (inRect.width - 30f) / 4f;

            var tiers = new[]
            {
                (AffinityTier.Hatred, -850f),
                (AffinityTier.Hostile, -550f),
                (AffinityTier.Cold, -250f),
                (AffinityTier.Indifferent, 0f),
                (AffinityTier.Warm, 200f),
                (AffinityTier.Devoted, 450f),
                (AffinityTier.Adoration, 725f),
                (AffinityTier.SoulBound, 925f)
            };

            for (int i = 0; i < tiers.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    curY += 40f;
                    buttonX = 0f;
                }

                var (tier, value) = tiers[i];
                string tierName = GetTierName(tier);
                GUI.color = GetTierColor(tier);
                
                if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 35f), tierName))
                {
                    float change = value - manager.Favorability;
                    manager.ModifyFavorability(change, $"Debug: Set to {tier}");
                    targetFavorability = value;
                }
                
                GUI.color = Color.white;
                buttonX += buttonWidth + 10f;
            }

            curY += 50f;

            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "TSS_Debug_Close".Translate()))
            {
                Close();
            }
        }

        private void DrawFavorabilityBar(Rect rect, float value)
        {
            var normalized = (value + 1000f) / 2000f;
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            Color fillColor = GetFavorabilityColor(value);
            var fillRect = new Rect(rect.x, rect.y, rect.width * normalized, rect.height);
            Widgets.DrawBoxSolid(fillRect, fillColor);
            Widgets.DrawBox(rect, 1);
            float centerX = rect.x + rect.width * 0.5f;
            Widgets.DrawLine(new Vector2(centerX, rect.y), new Vector2(centerX, rect.yMax), new Color(1f, 1f, 1f, 0.3f), 1f);
        }

        private Color GetTierColor(AffinityTier tier)
        {
            return tier switch
            {
                AffinityTier.Hatred => new Color(0.60f, 0.10f, 0.10f),
                AffinityTier.Hostile => new Color(0.80f, 0.25f, 0.25f),
                AffinityTier.Cold => new Color(0.55f, 0.55f, 0.75f),
                AffinityTier.Indifferent => new Color(0.65f, 0.65f, 0.65f),
                AffinityTier.Warm => new Color(0.80f, 0.75f, 0.40f),
                AffinityTier.Devoted => new Color(0.80f, 0.50f, 0.75f),
                AffinityTier.Adoration => new Color(0.90f, 0.40f, 0.70f),
                AffinityTier.SoulBound => new Color(1.00f, 0.80f, 0.20f),
                _ => Color.white
            };
        }

        private string GetTierName(AffinityTier tier) => $"TSS_Tier_{tier}".Translate();

        private Color GetFavorabilityColor(float value)
        {
            if (value < -700f) return new Color(0.60f, 0.10f, 0.10f);
            if (value < -400f) return new Color(0.80f, 0.25f, 0.25f);
            if (value < -100f) return new Color(0.70f, 0.50f, 0.70f);
            if (value < 100f) return new Color(0.65f, 0.65f, 0.65f);
            if (value < 300f) return new Color(0.80f, 0.75f, 0.40f);
            if (value < 600f) return new Color(0.80f, 0.50f, 0.75f);
            if (value < 850f) return new Color(0.90f, 0.40f, 0.70f);
            return new Color(1.00f, 0.80f, 0.20f);
        }
    }

    /// <summary>
    /// 表情调试窗口
    /// 快速切换表情，测试立绘表现
    /// </summary>
    public class Dialog_ExpressionDebug : Window
    {
        private NarratorPersonaDef persona;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public Dialog_ExpressionDebug(NarratorPersonaDef persona)
        {
            this.persona = persona;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.closeOnCancel = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), "TSS_Debug_Expression_Title".Translate(persona.narratorName));
            Text.Font = GameFont.Small;
            curY += 50f;

            var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
            ExpressionType currentExpression = expressionState.CurrentExpression;

            float leftWidth = 250f;
            float rightWidth = inRect.width - leftWidth - 20f;

            Rect leftRect = new Rect(0f, curY, leftWidth, inRect.height - curY - 60f);
            Widgets.Label(new Rect(leftRect.x, leftRect.y, leftRect.width, 25f), "TSS_Debug_CurrentExpression".Translate(GetExpressionDisplayName(currentExpression)));
            
            var portraitRect = new Rect(leftRect.x + 10f, leftRect.y + 30f, leftWidth - 20f, leftWidth - 20f);
            var texture = PortraitLoader.LoadPortrait(persona, currentExpression);
            
            if (texture != null)
                GUI.DrawTexture(portraitRect, texture, ScaleMode.ScaleToFit);
            else
                Widgets.DrawBoxSolid(portraitRect, persona.primaryColor);
            Widgets.DrawBox(portraitRect);
            
            float descY = portraitRect.yMax + 10f;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(leftRect.x, descY, leftRect.width, 40f), GetExpressionDescription(currentExpression));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect rightRect = new Rect(leftWidth + 20f, curY, rightWidth, inRect.height - curY - 60f);
            Widgets.Label(new Rect(rightRect.x, rightRect.y, rightRect.width, 25f), "TSS_Debug_ClickToSwitch".Translate());
            
            var listRect = new Rect(rightRect.x, rightRect.y + 30f, rightRect.width, rightRect.height - 30f);
            DrawExpressionList(listRect, currentExpression);

            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "TSS_Debug_Close".Translate()))
                Close();
        }

        private void DrawExpressionList(Rect rect, ExpressionType currentExpression)
        {
            var allExpressions = Enum.GetValues(typeof(ExpressionType)).Cast<ExpressionType>().ToList();
            var viewRect = new Rect(0, 0, rect.width - 20f, allExpressions.Count * 40f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float curY = 0f;
            foreach (var expression in allExpressions)
            {
                var itemRect = new Rect(0, curY, viewRect.width, 35f);
                bool isCurrent = expression == currentExpression;
                
                if (isCurrent)
                    Widgets.DrawBoxSolid(itemRect, new Color(0.2f, 0.6f, 0.4f, 0.3f));
                else if (Mouse.IsOver(itemRect))
                    Widgets.DrawBoxSolid(itemRect, new Color(1f, 1f, 1f, 0.1f));

                var iconRect = new Rect(itemRect.x + 5f, itemRect.y + 5f, 25f, 25f);
                Widgets.DrawBoxSolid(iconRect, GetExpressionColor(expression));
                Widgets.DrawBox(iconRect);

                var labelRect = new Rect(itemRect.x + 40f, itemRect.y, itemRect.width - 40f, itemRect.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = isCurrent ? Color.green : Color.white;
                Widgets.Label(labelRect, $"{GetExpressionDisplayName(expression)}   {GetExpressionDescription(expression)}");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(itemRect))
                {
                    ExpressionSystem.SetExpression(persona.defName, expression);
                    Messages.Message("TSS_Debug_SwitchedToExpression".Translate(GetExpressionDisplayName(expression)), MessageTypeDefOf.NeutralEvent);
                }
                curY += 40f;
            }
            Widgets.EndScrollView();
        }

        private string GetExpressionDisplayName(ExpressionType expression) => $"TSS_Expression_{expression}".Translate();
        private string GetExpressionDescription(ExpressionType expression) => $"TSS_Expression_{expression}_Desc".Translate();

        private Color GetExpressionColor(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => new Color(0.7f, 0.7f, 0.7f),
                ExpressionType.Happy => new Color(1.0f, 0.8f, 0.2f),
                ExpressionType.Sad => new Color(0.4f, 0.6f, 0.8f),
                ExpressionType.Angry => new Color(0.9f, 0.2f, 0.2f),
                ExpressionType.Surprised => new Color(1.0f, 0.6f, 0.0f),
                ExpressionType.Worried => new Color(0.7f, 0.5f, 0.3f),
                ExpressionType.Smug => new Color(0.5f, 0.8f, 0.3f),
                ExpressionType.Disappointed => new Color(0.5f, 0.5f, 0.6f),
                ExpressionType.Thoughtful => new Color(0.6f, 0.4f, 0.7f),
                ExpressionType.Annoyed => new Color(0.8f, 0.6f, 0.2f),
                _ => Color.gray
            };
        }
    }
}

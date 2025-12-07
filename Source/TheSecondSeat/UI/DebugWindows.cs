using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 好感度调试窗口
    /// ? 快速调整好感度，测试不同等级的表现
    /// </summary>
    public class Dialog_FavorabilityDebug : Window
    {
        private NarratorManager manager;
        private float targetFavorability;

        public override Vector2 InitialSize => new Vector2(520f, 650f); // ? 增加高度：400→650

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

            // 标题（移除emoji）
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), "[调试] 好感度工具");
            Text.Font = GameFont.Small;
            curY += 50f;

            // 当前好感度显示
            var currentTier = manager.CurrentTier;
            var tierColor = GetTierColor(currentTier);
            
            GUI.color = tierColor;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 30f), 
                $"当前好感度：{manager.Favorability:F0} ({GetTierName(currentTier)})");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 40f;

            // 好感度条可视化
            DrawFavorabilityBar(new Rect(0f, curY, inRect.width, 30f), manager.Favorability);
            curY += 40f;

            // 快速调整按钮
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "快速调整：");
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
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "精确设置：");
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

            if (Widgets.ButtonText(new Rect(0f, curY, inRect.width, 35f), $"应用：{targetFavorability:F0}"))
            {
                float change = targetFavorability - manager.Favorability;
                manager.ModifyFavorability(change, $"调试：设置为 {targetFavorability:F0}");
            }
            curY += 45f;

            // 快捷等级设置
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "快捷等级：");
            curY += 30f;

            buttonX = 0f;
            buttonWidth = (inRect.width - 30f) / 4f;

            var tiers = new[]
            {
                (AffinityTier.Hatred, -850f, "憎恨"),
                (AffinityTier.Hostile, -550f, "敌意"),
                (AffinityTier.Cold, -250f, "疏远"),
                (AffinityTier.Indifferent, 0f, "冷淡"),
                (AffinityTier.Warm, 200f, "温暖"),
                (AffinityTier.Devoted, 450f, "倾心"),
                (AffinityTier.Adoration, 725f, "爱慕"),
                (AffinityTier.SoulBound, 925f, "魂之友")
            };

            for (int i = 0; i < tiers.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    curY += 40f;
                    buttonX = 0f;
                }

                var (tier, value, name) = tiers[i];
                GUI.color = GetTierColor(tier);
                
                if (Widgets.ButtonText(new Rect(buttonX, curY, buttonWidth, 35f), name))
                {
                    float change = value - manager.Favorability;
                    manager.ModifyFavorability(change, $"调试：设置为 {name}");
                    targetFavorability = value;
                }
                
                GUI.color = Color.white;
                buttonX += buttonWidth + 10f;
            }

            curY += 50f;

            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "关闭"))
            {
                Close();
            }
        }

        private void DrawFavorabilityBar(Rect rect, float value)
        {
            // 归一化：-1000~1000 → 0~1
            var normalized = (value + 1000f) / 2000f;
            
            // 背景
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            
            // 填充条
            Color fillColor = GetFavorabilityColor(value);
            var fillRect = new Rect(rect.x, rect.y, rect.width * normalized, rect.height);
            Widgets.DrawBoxSolid(fillRect, fillColor);
            
            // 边框
            Widgets.DrawBox(rect, 1);
            
            // 中心标记线（0 的位置）
            float centerX = rect.x + rect.width * 0.5f;
            Widgets.DrawLine(
                new Vector2(centerX, rect.y),
                new Vector2(centerX, rect.yMax),
                new Color(1f, 1f, 1f, 0.3f),
                1f
            );
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

        private string GetTierName(AffinityTier tier)
        {
            return $"TSS_Tier_{tier}".Translate();
        }

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
    /// ? 快速切换表情，测试立绘表现
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

            // 标题（移除emoji）
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 40f), $"[调试] 表情 - {persona.narratorName}");
            Text.Font = GameFont.Small;
            curY += 50f;

            // ? 每次绘制时重新获取当前表情状态（确保实时更新）
            var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
            ExpressionType currentExpression = expressionState.CurrentExpression;

            // ? 调整布局：左侧立绘预览，右侧表情列表
            float leftWidth = 250f;
            float rightWidth = inRect.width - leftWidth - 20f;

            // 左侧区域：立绘预览
            Rect leftRect = new Rect(0f, curY, leftWidth, inRect.height - curY - 60f);
            
            // 当前表情标签
            Widgets.Label(new Rect(leftRect.x, leftRect.y, leftRect.width, 25f), $"当前表情：{GetExpressionDisplayName(currentExpression)}");
            
            // 立绘预览（调整大小和位置）
            var portraitRect = new Rect(leftRect.x + 10f, leftRect.y + 30f, leftWidth - 20f, leftWidth - 20f);
            
            // ? 添加加载日志
            Log.Message($"[ExpressionDebug] 正在加载立绘: {persona.defName}, 表情: {currentExpression}");
            var texture = PortraitLoader.LoadPortrait(persona, currentExpression);
            Log.Message($"[ExpressionDebug] 加载结果: {(texture != null ? "成功" : "失败")}");
            
            if (texture != null)
            {
                GUI.DrawTexture(portraitRect, texture, ScaleMode.ScaleToFit);
            }
            else
            {
                Widgets.DrawBoxSolid(portraitRect, persona.primaryColor);
            }
            Widgets.DrawBox(portraitRect);
            
            // 表情说明
            float descY = portraitRect.yMax + 10f;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(leftRect.x, descY, leftRect.width, 40f), 
                GetExpressionDescription(currentExpression));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 右侧区域：表情列表
            Rect rightRect = new Rect(leftWidth + 20f, curY, rightWidth, inRect.height - curY - 60f);
            
            Widgets.Label(new Rect(rightRect.x, rightRect.y, rightRect.width, 25f), "点击切换表情：");
            
            var listRect = new Rect(rightRect.x, rightRect.y + 30f, rightRect.width, rightRect.height - 30f);
            DrawExpressionList(listRect, currentExpression);

            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f), "关闭"))
            {
                Close();
            }
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
                {
                    Widgets.DrawBoxSolid(itemRect, new Color(0.2f, 0.6f, 0.4f, 0.3f));
                }
                else if (Mouse.IsOver(itemRect))
                {
                    Widgets.DrawBoxSolid(itemRect, new Color(1f, 1f, 1f, 0.1f));
                }

                var buttonRect = itemRect.ContractedBy(2f);
                
                // 表情图标（可选，使用颜色块）
                var iconRect = new Rect(buttonRect.x + 5f, buttonRect.y + 5f, 25f, 25f);
                Widgets.DrawBoxSolid(iconRect, GetExpressionColor(expression));
                Widgets.DrawBox(iconRect);

                // 表情名称
                var labelRect = new Rect(buttonRect.x + 40f, buttonRect.y, buttonRect.width - 40f, buttonRect.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                
                string displayName = GetExpressionDisplayName(expression);
                string description = GetExpressionDescription(expression);
                
                GUI.color = isCurrent ? Color.green : Color.white;
                Widgets.Label(labelRect, $"{displayName}   {description}");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(itemRect))
                {
                    // ? 添加详细日志
                    Log.Message($"[ExpressionDebug] 点击表情按钮: {expression}");
                    Log.Message($"[ExpressionDebug] 人格 defName: {persona.defName}");
                    Log.Message($"[ExpressionDebug] 人格 narratorName: {persona.narratorName}");
                    
                    ExpressionSystem.SetExpression(persona.defName, expression);
                    Messages.Message($"已切换至表情：{displayName}", MessageTypeDefOf.NeutralEvent);
                    
                    // ? 强制刷新窗口
                    Log.Message($"[ExpressionDebug] 表情切换完成，当前状态: {ExpressionSystem.GetExpressionState(persona.defName).CurrentExpression}");
                }

                curY += 40f;
            }

            Widgets.EndScrollView();
        }

        private string GetExpressionDisplayName(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => "[=] 中性",
                ExpressionType.Happy => "[^_^] 开心",
                ExpressionType.Sad => "[T_T] 悲伤",
                ExpressionType.Angry => "[>_<] 愤怒",
                ExpressionType.Surprised => "[o_o] 惊讶",
                ExpressionType.Worried => "[-_-] 担忧",
                ExpressionType.Smug => "[^v^] 得意",
                ExpressionType.Disappointed => "[u_u] 失望",
                ExpressionType.Thoughtful => "[?_?] 沉思",
                ExpressionType.Annoyed => "[~_~] 烦躁",
                _ => expression.ToString()
            };
        }

        private string GetExpressionDescription(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => "(默认)",
                ExpressionType.Happy => "(殖民地繁荣)",
                ExpressionType.Sad => "(殖民者死亡)",
                ExpressionType.Angry => "(被攻击)",
                ExpressionType.Surprised => "(意外事件)",
                ExpressionType.Worried => "(危险预警)",
                ExpressionType.Smug => "(大胜)",
                ExpressionType.Disappointed => "(任务失败)",
                ExpressionType.Thoughtful => "(战略决策)",
                ExpressionType.Annoyed => "(频繁请求)",
                _ => ""
            };
        }

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

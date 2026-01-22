using UnityEngine;
using Verse;
using RimWorld;
using System;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Settings
{
    public partial class TheSecondSeatMod
    {
        private void DrawBasicSettingsTab(Rect rect)
        {
            Vector2 scrollPos = tabManager.GetScrollPosition();
            float contentHeight = 900f;
            
            SettingsUIComponents.DrawScrollableCardContent(rect, ref scrollPos, contentHeight, (viewRect) =>
            {
                float y = viewRect.y + SettingsUIComponents.MediumGap;
                float cardWidth = viewRect.width - 10f;
                
                // === 难度模式选择 ===
                float difficultyHeight = 200f;
                Rect difficultyRect = new Rect(viewRect.x, y, cardWidth, difficultyHeight);
                SettingsUIComponents.DrawSettingsGroup(difficultyRect, "难度模式", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 难度模式按钮组
                    float buttonWidth = (contentRect.width - 20f) / 3f;
                    float buttonHeight = 80f;
                    
                    // Assistant 模式
                    Rect assistantRect = new Rect(contentRect.x, cy, buttonWidth, buttonHeight);
                    DrawDifficultyButton(assistantRect, AIDifficultyMode.Assistant, "助手模式", 
                        "AI 会尽力帮助玩家", assistantModeIcon);
                    
                    // Opponent 模式
                    Rect opponentRect = new Rect(contentRect.x + buttonWidth + 10f, cy, buttonWidth, buttonHeight);
                    DrawDifficultyButton(opponentRect, AIDifficultyMode.Opponent, "对手模式", 
                        "AI 会挑战玩家", opponentModeIcon);
                    
                    // Engineer 模式
                    Rect engineerRect = new Rect(contentRect.x + (buttonWidth + 10f) * 2, cy, buttonWidth, buttonHeight);
                    DrawDifficultyButton(engineerRect, AIDifficultyMode.Engineer, "工程师模式", 
                        "AI 专注于技术分析", engineerModeIcon);
                    
                    cy += buttonHeight + 10f;
                    
                    // 当前模式说明
                    string modeDesc = Settings.difficultyMode switch
                    {
                        AIDifficultyMode.Assistant => "助手模式：叙事者会积极帮助你管理殖民地，提供建议和资源支持。",
                        AIDifficultyMode.Opponent => "对手模式：叙事者会制造挑战，考验你的生存能力，但不会故意让你失败。",
                        AIDifficultyMode.Engineer => "工程师模式：叙事者会分析技术问题，帮助你优化和调试。",
                        _ => ""
                    };
                    
                    Rect descRect = new Rect(contentRect.x, cy, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(descRect, modeDesc, InfoBoxType.Info);
                });
                y += difficultyHeight + SettingsUIComponents.MediumGap;
                
                // === 通用设置 ===
                // 将基础功能和UI设置合并，并使用双列布局
                float generalHeight = 160f;
                Rect generalRect = new Rect(viewRect.x, y, cardWidth, generalHeight);
                SettingsUIComponents.DrawSettingsGroup(generalRect, "通用设置", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    float colWidth = (contentRect.width - 10f) / 2f;
                    
                    // 第一列
                    Rect col1Rect = new Rect(contentRect.x, cy, colWidth, 28f);
                    SettingsUIComponents.DrawToggleSetting(col1Rect, "好感度系统",
                        "启用角色好感度追踪功能", ref Settings.enableAffinitySystem);
                    col1Rect.y += 34f;
                    
                    SettingsUIComponents.DrawToggleSetting(col1Rect, "主动对话",
                        "允许 AI 主动发起对话", ref Settings.enableProactiveDialogue);
                    col1Rect.y += 34f;
                    
                    SettingsUIComponents.DrawToggleSetting(col1Rect, "立绘模式",
                        "在对话时显示全身立绘", ref Settings.usePortraitMode);

                    // 第二列
                    Rect col2Rect = new Rect(contentRect.x + colWidth + 10f, cy, colWidth, 28f);
                    SettingsUIComponents.DrawToggleSetting(col2Rect, "精简提示词",
                        "使用更短的系统提示词以减少 Token 消耗", ref Settings.useCompactPrompt);
                    col2Rect.y += 34f;
                    
                    SettingsUIComponents.DrawToggleSetting(col2Rect, "调试模式",
                        "启用开发者调试信息输出", ref Settings.debugMode);
                });
                y += generalHeight + SettingsUIComponents.MediumGap;
                // 全局提示词已移至高级选项卡
            });
            
            tabManager.SetScrollPosition(scrollPos);
        }
        
        private Vector2 globalPromptScrollPos = Vector2.zero;
        
        private void DrawDifficultyButton(Rect rect, AIDifficultyMode mode, string label, string tooltip, Texture2D icon)
        {
            bool isSelected = Settings.difficultyMode == mode;
            Color bgColor = isSelected ? SettingsUIComponents.AccentBlue : new Color(0.15f, 0.15f, 0.15f);
            
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect, 1);
            
            // 图标
            if (icon != null)
            {
                Rect iconRect = new Rect(rect.x + (rect.width - 32f) / 2f, rect.y + 8f, 32f, 32f);
                GUI.DrawTexture(iconRect, icon);
            }
            
            // 标签
            Rect labelRect = new Rect(rect.x, rect.y + 44f, rect.width, 24f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            GUI.color = isSelected ? Color.white : Color.gray;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 点击
            if (Widgets.ButtonInvisible(rect))
            {
                Settings.difficultyMode = mode;
            }
            
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}

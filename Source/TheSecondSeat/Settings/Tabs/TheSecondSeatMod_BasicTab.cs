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
                
                // === 基础功能设置 ===
                float basicHeight = 180f;
                Rect basicRect = new Rect(viewRect.x, y, cardWidth, basicHeight);
                SettingsUIComponents.DrawSettingsGroup(basicRect, "基础功能", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 调试模式
                    Rect debugRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(debugRect, "调试模式", 
                        "启用开发者调试信息输出", ref Settings.debugMode);
                    cy += 34f;
                    
                    // 精简提示词
                    Rect compactRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(compactRect, "精简提示词", 
                        "使用更短的系统提示词以减少 Token 消耗", ref Settings.useCompactPrompt);
                    cy += 34f;
                    
                    // 好感度系统
                    Rect affinityRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(affinityRect, "好感度系统", 
                        "启用角色好感度追踪功能", ref Settings.enableAffinitySystem);
                    cy += 34f;
                    
                    // 主动对话
                    Rect proactiveRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(proactiveRect, "主动对话", 
                        "允许 AI 主动发起对话", ref Settings.enableProactiveDialogue);
                });
                y += basicHeight + SettingsUIComponents.MediumGap;
                
                // === UI 设置 ===
                float uiHeight = 100f;
                Rect uiRect = new Rect(viewRect.x, y, cardWidth, uiHeight);
                SettingsUIComponents.DrawSettingsGroup(uiRect, "界面设置", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 立绘模式
                    Rect portraitRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(portraitRect, "立绘模式", 
                        "在对话时显示全身立绘", ref Settings.usePortraitMode);
                });
                y += uiHeight + SettingsUIComponents.MediumGap;
                
                // === 提示词管理 ===
                float promptHeight = 120f;
                Rect promptRect = new Rect(viewRect.x, y, cardWidth, promptHeight);
                SettingsUIComponents.DrawSettingsGroup(promptRect, "提示词管理", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    Rect initBtnRect = new Rect(contentRect.x, cy, 180f, 30f);
                    if (Widgets.ButtonText(initBtnRect, "初始化提示词"))
                    {
                        PromptLoader.InitializeUserPrompts();
                    }
                    TooltipHandler.TipRegion(initBtnRect, "将 Mod 内置提示词复制到 Config 目录，以便进行自定义修改。");
                    
                    Rect manageBtnRect = new Rect(contentRect.x + 190f, cy, 180f, 30f);
                    if (Widgets.ButtonText(manageBtnRect, "管理提示词"))
                    {
                        Find.WindowStack.Add(new UI.PromptManagementWindow());
                    }
                    TooltipHandler.TipRegion(manageBtnRect, "打开提示词管理窗口，编辑自定义提示词。");
                    
                    cy += 35f;
                    
                    Rect descRect = new Rect(contentRect.x, cy, contentRect.width, 40f);
                    Widgets.Label(descRect, "提示：修改提示词后，需要重启游戏或重新加载存档才能生效。自定义提示词位于 Config/TheSecondSeat/Prompts 目录下。");
                });
                y += promptHeight + SettingsUIComponents.MediumGap;

                // === 全局提示词 ===
                float globalPromptHeight = 180f;
                Rect globalRect = new Rect(viewRect.x, y, cardWidth, globalPromptHeight);
                SettingsUIComponents.DrawSettingsGroup(globalRect, "全局提示词", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    Rect labelRect = new Rect(contentRect.x, cy, contentRect.width, 24f);
                    Widgets.Label(labelRect, "附加到所有对话的自定义提示词：");
                    cy += 28f;
                    
                    Rect textRect = new Rect(contentRect.x, cy, contentRect.width, 100f);
                    //Widgets.DrawBox(textRect); // for debugging
                    Settings.globalPrompt = Widgets.TextArea(textRect, Settings.globalPrompt);
                });
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

using UnityEngine;
using Verse;
using RimWorld;
using System;

namespace TheSecondSeat.Settings
{
    /// <summary>
    /// 设置界面 UI 辅助类
    /// 提取自 ModSettings.cs，减少主文件代码量
    /// </summary>
    public static class SettingsUI
    {
        /// <summary>
        /// 绘制难度模式选项（带图标）
        /// </summary>
        public static void DrawDifficultyOption(
            Rect rect, 
            Texture2D? icon, 
            string title, 
            string subtitle, 
            string description, 
            bool isSelected, 
            Color accentColor)
        {
            // 背景
            if (isSelected)
            {
                Widgets.DrawBoxSolid(rect, new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 0.5f));
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.25f, 0.25f, 0.5f));
            }
            
            // 边框
            if (isSelected)
            {
                GUI.color = accentColor;
                Widgets.DrawBox(rect, 2);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBox(rect, 1);
            }
            
            var innerRect = rect.ContractedBy(5f);
            
            // 图标区域（左侧）
            float iconSize = 50f;
            var iconRect = new Rect(innerRect.x, innerRect.y, iconSize, iconSize);
            
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }
            else
            {
                // 占位符：绘制带颜色的方块
                Widgets.DrawBoxSolid(iconRect, accentColor * 0.5f);
                
                // 绘制模式首字母
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(iconRect, title.Substring(0, 1));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            
            // 文字区域（右侧）
            float textX = innerRect.x + iconSize + 10f;
            float textWidth = innerRect.width - iconSize - 10f;
            
            // 标题
            Text.Font = GameFont.Small;
            GUI.color = isSelected ? accentColor : Color.white;
            var titleRect = new Rect(textX, innerRect.y, textWidth, 20f);
            Widgets.Label(titleRect, title + (isSelected ? " [已选择]" : ""));
            
            // 副标题
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            var subtitleRect = new Rect(textX, innerRect.y + 18f, textWidth, 16f);
            Widgets.Label(subtitleRect, subtitle);
            
            // 描述（悬停时显示）
            if (Mouse.IsOver(rect))
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                var descRect = new Rect(textX, innerRect.y + 34f, textWidth, 20f);
                Widgets.Label(descRect, description);
            }
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// 绘制折叠区域
        /// </summary>
        public static void DrawCollapsibleSection(
            Listing_Standard listing, 
            string title, 
            ref bool collapsed, 
            Action drawContent)
        {
            var headerRect = listing.GetRect(30f);
            
            // 绘制标题背景
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            // 绘制箭头和标题
            var arrowRect = new Rect(headerRect.x + 5f, headerRect.y + 5f, 20f, 20f);
            var titleRect = new Rect(headerRect.x + 30f, headerRect.y, headerRect.width - 30f, headerRect.height);
            
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;
            
            // 绘制箭头
            string arrow = collapsed ? ">" : "v";
            Widgets.Label(arrowRect, arrow);
            
            // 点击切换折叠
            if (Widgets.ButtonInvisible(headerRect))
            {
                collapsed = !collapsed;
            }
            
            // 如果未折叠，绘制内容
            if (!collapsed)
            {
                listing.Gap(8f);
                drawContent();
                listing.Gap(12f);
            }
            
            listing.GapLine();
        }
        
        /// <summary>
        /// 加载难度图标
        /// </summary>
        public static void LoadDifficultyIcons(ref Texture2D? assistantIcon, ref Texture2D? opponentIcon)
        {
            if (assistantIcon == null)
            {
                assistantIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/assistant_large", false);
            }
            if (opponentIcon == null)
            {
                opponentIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/opponent_large", false);
            }
        }
    }
}

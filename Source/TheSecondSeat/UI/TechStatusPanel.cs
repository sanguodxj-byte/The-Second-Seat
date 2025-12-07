using UnityEngine;
using Verse;
using System;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 科技感状态面板 - 纯代码绘制版本（不需要纹理）
    /// </summary>
    public static class TechStatusPanel
    {
        // 颜色定义
        private static readonly Color CyanGlow = new Color(0f, 0.9f, 1f, 1f);
        private static readonly Color GreenGlow = new Color(0f, 1f, 0.53f, 1f);
        private static readonly Color RedGlow = new Color(1f, 0.2f, 0.4f, 1f);
        private static readonly Color DarkBg = new Color(0.08f, 0.12f, 0.16f, 0.95f);
        private static readonly Color PanelBg = new Color(0.16f, 0.20f, 0.24f, 0.9f);

        /// <summary>
        /// 绘制科技感状态面板
        /// </summary>
        public static void DrawStatusPanel(Rect rect, bool isOnline, float syncProgress, bool hasError, string errorMessage = "")
        {
            // 主背景
            DrawTechBackground(rect);
            
            // 标题
            var titleRect = new Rect(rect.x + 10, rect.y + 10, rect.width - 20, 30);
            DrawTechTitle(titleRect, "NARRATOR.OS");
            
            // 在线状态
            var onlineRect = new Rect(rect.x + 20, rect.y + 50, 120, 30);
            DrawOnlineStatus(onlineRect, isOnline);
            
            // 同步进度
            var syncRect = new Rect(rect.x + 20, rect.y + 90, 120, 120);
            DrawSyncCircle(syncRect, syncProgress);
            
            // 连接状态
            var linkRect = new Rect(rect.x + 20, rect.y + 220, 120, 120);
            DrawLinkStatus(linkRect, hasError, errorMessage);
            
            // 底部进度条
            var progressRect = new Rect(rect.x + 10, rect.y + rect.height - 30, rect.width - 20, 20);
            DrawProgressBar(progressRect, syncProgress);
        }

        /// <summary>
        /// 绘制科技背景
        /// </summary>
        private static void DrawTechBackground(Rect rect)
        {
            // 主背景
            GUI.color = DarkBg;
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Color.white;
            
            // 发光边框
            GUI.color = CyanGlow;
            Widgets.DrawBox(rect, 2);
            
            // 内边框
            var innerRect = rect.ContractedBy(8f);
            GUI.color = new Color(CyanGlow.r, CyanGlow.g, CyanGlow.b, 0.3f);
            Widgets.DrawBox(innerRect, 1);
            GUI.color = Color.white;
            
            // 角落装饰
            DrawCornerDecoration(new Vector2(rect.x, rect.y), 20f, CyanGlow);
            DrawCornerDecoration(new Vector2(rect.xMax, rect.y), 20f, CyanGlow);
            DrawCornerDecoration(new Vector2(rect.x, rect.yMax), 20f, CyanGlow);
            DrawCornerDecoration(new Vector2(rect.xMax, rect.yMax), 20f, CyanGlow);
        }

        /// <summary>
        /// 绘制标题
        /// </summary>
        private static void DrawTechTitle(Rect rect, string title)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = CyanGlow;
            Widgets.Label(rect, title);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            // 标题下划线
            var lineRect = new Rect(rect.x + 20, rect.yMax - 2, rect.width - 40, 2);
            GUI.color = new Color(CyanGlow.r, CyanGlow.g, CyanGlow.b, 0.5f);
            Widgets.DrawBoxSolid(lineRect, GUI.color);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制在线状态
        /// </summary>
        private static void DrawOnlineStatus(Rect rect, bool isOnline)
        {
            // 状态指示灯
            var indicatorRect = new Rect(rect.x, rect.y + 5, 16, 16);
            var color = isOnline ? GreenGlow : new Color(0.5f, 0.5f, 0.5f, 1f);
            
            // 发光效果
            GUI.color = new Color(color.r, color.g, color.b, 0.3f);
            Widgets.DrawBoxSolid(indicatorRect.ExpandedBy(4f), GUI.color);
            
            // 核心
            GUI.color = color;
            DrawCircle(indicatorRect.center, 8f, color);
            GUI.color = Color.white;
            
            // 文本
            var labelRect = new Rect(rect.x + 24, rect.y, rect.width - 24, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = color;
            Widgets.Label(labelRect, isOnline ? "TSS_StatusPanel_Online".Translate() : "TSS_StatusPanel_Offline".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 绘制同步环形进度
        /// </summary>
        private static void DrawSyncCircle(Rect rect, float progress)
        {
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) / 2f - 10f;
            
            // 背景圆环
            GUI.color = new Color(PanelBg.r, PanelBg.g, PanelBg.b, 0.5f);
            DrawCircleOutline(center, radius, 4f, Color.gray);
            
            // 进度圆环
            GUI.color = CyanGlow;
            DrawArc(center, radius, 0f, progress * 360f, 4f, CyanGlow);
            GUI.color = Color.white;
            
            // 中心文字
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = CyanGlow;
            var labelRect = new Rect(center.x - 50, center.y - 10, 100, 20);
            Widgets.Label(labelRect, $"{"TSS_StatusPanel_Sync".Translate()}: {progress:P0}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 绘制连接状态
        /// </summary>
        private static void DrawLinkStatus(Rect rect, bool hasError, string errorMessage)
        {
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) / 2f - 10f;
            
            var color = hasError ? RedGlow : GreenGlow;
            
            // 齿轮图标（简化版）
            DrawGearIcon(center, radius * 0.6f, color);
            
            // 标签
            var labelRect = new Rect(rect.x, rect.yMax + 5, rect.width, 20);
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = color;
            Widgets.Label(labelRect, "TSS_StatusPanel_Link".Translate());
            
            if (hasError && !string.IsNullOrEmpty(errorMessage))
            {
                var errorRect = new Rect(rect.x, rect.yMax + 25, rect.width, 30);
                Text.Font = GameFont.Tiny;
                GUI.color = RedGlow;
                Widgets.Label(errorRect, $"{"TSS_StatusPanel_Error".Translate()}\n{errorMessage}");
                Text.Font = GameFont.Small;
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 绘制底部进度条
        /// </summary>
        private static void DrawProgressBar(Rect rect, float progress)
        {
            // 背景
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            
            // 进度填充
            var fillRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
            GUI.color = new Color(GreenGlow.r, GreenGlow.g, GreenGlow.b, 0.6f);
            Widgets.DrawBoxSolid(fillRect, GUI.color);
            
            // 边框
            GUI.color = CyanGlow;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
        }

        // ===== 辅助绘制方法 =====

        /// <summary>
        /// 绘制圆形
        /// </summary>
        private static void DrawCircle(Vector2 center, float radius, Color color)
        {
            var segments = 32;
            var angleStep = 360f / segments;
            
            for (int i = 0; i < segments; i++)
            {
                var angle1 = i * angleStep * Mathf.Deg2Rad;
                var angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
                
                var p1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * radius;
                var p2 = center + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * radius;
                
                // 使用三角形填充（简化）
                var rect = new Rect(p1.x - 1, p1.y - 1, 2, 2);
                Widgets.DrawBoxSolid(rect, color);
            }
        }

        /// <summary>
        /// 绘制圆形轮廓
        /// </summary>
        private static void DrawCircleOutline(Vector2 center, float radius, float thickness, Color color)
        {
            var segments = 64;
            var angleStep = 360f / segments;
            
            GUI.color = color;
            for (int i = 0; i < segments; i++)
            {
                var angle = i * angleStep * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var rect = new Rect(p.x - thickness / 2, p.y - thickness / 2, thickness, thickness);
                Widgets.DrawBoxSolid(rect, color);
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制圆弧
        /// </summary>
        private static void DrawArc(Vector2 center, float radius, float startAngle, float endAngle, float thickness, Color color)
        {
            var segments = 64;
            var angleRange = endAngle - startAngle;
            var angleStep = angleRange / segments;
            
            GUI.color = color;
            for (int i = 0; i <= segments; i++)
            {
                var angle = (startAngle + i * angleStep - 90f) * Mathf.Deg2Rad; // -90度使0度从顶部开始
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var rect = new Rect(p.x - thickness / 2, p.y - thickness / 2, thickness, thickness);
                Widgets.DrawBoxSolid(rect, color);
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制齿轮图标（简化版）
        /// </summary>
        private static void DrawGearIcon(Vector2 center, float radius, Color color)
        {
            // 外圆
            GUI.color = new Color(color.r, color.g, color.b, 0.3f);
            DrawCircle(center, radius, color);
            
            // 内圆
            GUI.color = color;
            DrawCircle(center, radius * 0.6f, color);
            
            // 齿
            for (int i = 0; i < 8; i++)
            {
                var angle = i * 45f * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 1.2f;
                var rect = new Rect(p.x - 3, p.y - 3, 6, 6);
                Widgets.DrawBoxSolid(rect, color);
            }
            
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制角落装饰
        /// </summary>
        private static void DrawCornerDecoration(Vector2 corner, float size, Color color)
        {
            GUI.color = new Color(color.r, color.g, color.b, 0.5f);
            
            // 简单的角落标记
            var rect1 = new Rect(corner.x - 2, corner.y - 2, size / 4, 2);
            var rect2 = new Rect(corner.x - 2, corner.y - 2, 2, size / 4);
            
            Widgets.DrawBoxSolid(rect1, color);
            Widgets.DrawBoxSolid(rect2, color);
            
            GUI.color = Color.white;
        }
    }
}

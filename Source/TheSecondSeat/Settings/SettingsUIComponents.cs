using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Collections.Generic;

namespace TheSecondSeat.Settings
{
    /// <summary>
    /// 选项卡定义
    /// </summary>
    public class TabDefinition
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public Color AccentColor { get; set; }
        public Action<Rect> DrawContent { get; set; }
        
        public TabDefinition(string name, string icon, Color accentColor, Action<Rect> drawContent)
        {
            Name = name;
            Icon = icon;
            AccentColor = accentColor;
            DrawContent = drawContent;
        }
    }

    /// <summary>
    /// 选项卡管理器 - 管理选项卡状态和滚动位置
    /// </summary>
    public class TabManager
    {
        public int CurrentTab { get; set; } = 0;
        public Dictionary<int, Vector2> TabScrollPositions { get; } = new Dictionary<int, Vector2>();
        public List<TabDefinition> Tabs { get; } = new List<TabDefinition>();
        
        public Vector2 GetScrollPosition()
        {
            if (!TabScrollPositions.ContainsKey(CurrentTab))
            {
                TabScrollPositions[CurrentTab] = Vector2.zero;
            }
            return TabScrollPositions[CurrentTab];
        }
        
        public void SetScrollPosition(Vector2 position)
        {
            TabScrollPositions[CurrentTab] = position;
        }
        
        public void AddTab(string name, string icon, Color accentColor, Action<Rect> drawContent)
        {
            Tabs.Add(new TabDefinition(name, icon, accentColor, drawContent));
        }
        
        public void SwitchTo(int tabIndex)
        {
            if (tabIndex >= 0 && tabIndex < Tabs.Count)
            {
                CurrentTab = tabIndex;
            }
        }
    }

    /// <summary>
    /// 设置界面UI组件库
    /// 提供现代化的卡片、选项卡等UI组件
    /// </summary>
    public static class SettingsUIComponents
    {
        // 颜色定义
        public static readonly Color CardBackground = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        public static readonly Color CardBackgroundHover = new Color(0.18f, 0.18f, 0.18f, 0.95f);
        public static readonly Color CardBorder = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        public static readonly Color TabActive = new Color(0.25f, 0.4f, 0.6f, 1f);
        public static readonly Color TabInactive = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        public static readonly Color TabHover = new Color(0.2f, 0.3f, 0.4f, 0.9f);
        public static readonly Color AccentBlue = new Color(0.3f, 0.5f, 0.8f, 1f);
        public static readonly Color AccentGreen = new Color(0.3f, 0.7f, 0.4f, 1f);
        public static readonly Color AccentRed = new Color(0.7f, 0.3f, 0.3f, 1f);
        public static readonly Color AccentYellow = new Color(0.8f, 0.7f, 0.3f, 1f);
        public static readonly Color AccentPurple = new Color(0.6f, 0.4f, 0.8f, 1f);
        public static readonly Color AccentOrange = new Color(0.9f, 0.5f, 0.2f, 1f);
        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextSecondary = new Color(0.7f, 0.7f, 0.7f, 1f);
        public static readonly Color TextMuted = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        // 间距常量
        public const float RowHeight = 28f;
        public const float CardPadding = 12f;
        public const float SmallGap = 6f;
        public const float MediumGap = 12f;
        public const float LargeGap = 20f;

        /// <summary>
        /// 绘制选项卡栏
        /// </summary>
        public static int DrawTabBar(Rect rect, string[] tabNames, int currentTab, Action<int> onTabChanged = null)
        {
            float tabWidth = rect.width / tabNames.Length;
            float tabHeight = rect.height;
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                Rect tabRect = new Rect(rect.x + i * tabWidth, rect.y, tabWidth, tabHeight);
                bool isActive = i == currentTab;
                bool isHovered = Mouse.IsOver(tabRect);
                
                // 背景
                Color bgColor = isActive ? TabActive : (isHovered ? TabHover : TabInactive);
                Widgets.DrawBoxSolid(tabRect, bgColor);
                
                // 底部高亮条（活动选项卡）
                if (isActive)
                {
                    Rect highlightRect = new Rect(tabRect.x, tabRect.yMax - 3f, tabRect.width, 3f);
                    Widgets.DrawBoxSolid(highlightRect, AccentBlue);
                }
                
                // 文字
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = isActive ? TextPrimary : TextSecondary;
                Widgets.Label(tabRect, tabNames[i]);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                
                // 点击事件
                if (Widgets.ButtonInvisible(tabRect))
                {
                    currentTab = i;
                    onTabChanged?.Invoke(i);
                }
            }
            
            return currentTab;
        }

        /// <summary>
        /// 绘制卡片容器
        /// </summary>
        public static void DrawCard(Rect rect, string title, Action<Rect> drawContent, Color? accentColor = null)
        {
            Color accent = accentColor ?? AccentBlue;
            
            // 卡片背景
            Widgets.DrawBoxSolid(rect, CardBackground);
            
            // 左侧装饰条
            Rect accentBar = new Rect(rect.x, rect.y, 4f, rect.height);
            Widgets.DrawBoxSolid(accentBar, accent);
            
            // 边框
            GUI.color = CardBorder;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            
            Rect innerRect = rect.ContractedBy(12f);
            innerRect.x += 4f; // 考虑左侧装饰条
            innerRect.width -= 4f;
            
            // 标题
            if (!string.IsNullOrEmpty(title))
            {
                Text.Font = GameFont.Medium;
                GUI.color = TextPrimary;
                Rect titleRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 28f);
                Widgets.Label(titleRect, title);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                
                // 内容区域
                Rect contentRect = new Rect(innerRect.x, innerRect.y + 32f, innerRect.width, innerRect.height - 32f);
                drawContent(contentRect);
            }
            else
            {
                drawContent(innerRect);
            }
        }

        /// <summary>
        /// 绘制设置项（带标签和控件）
        /// </summary>
        public static float DrawSettingRow(Rect rect, string label, string tooltip, Action<Rect> drawControl)
        {
            float labelWidth = rect.width * 0.45f;
            float controlWidth = rect.width * 0.5f;
            float rowHeight = 28f;
            
            Rect rowRect = new Rect(rect.x, rect.y, rect.width, rowHeight);
            
            // 悬停效果
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
            }
            
            // 标签
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextPrimary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 工具提示
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
            
            // 控件区域
            Rect controlRect = new Rect(rowRect.xMax - controlWidth, rowRect.y, controlWidth, rowHeight);
            drawControl(controlRect);
            
            return rowHeight;
        }

        /// <summary>
        /// 绘制开关设置项
        /// </summary>
        public static float DrawToggleSetting(Rect rect, string label, string tooltip, ref bool value)
        {
            float rowHeight = 28f;
            
            Rect rowRect = new Rect(rect.x, rect.y, rect.width, rowHeight);
            
            // 悬停效果
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
            }
            
            float labelWidth = rect.width * 0.45f;
            
            // 标签
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextPrimary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 工具提示
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
            
            // 复选框
            float checkboxSize = 24f;
            Rect checkboxRect = new Rect(
                rowRect.xMax - checkboxSize - 10f,
                rowRect.y + (rowRect.height - checkboxSize) / 2f,
                checkboxSize,
                checkboxSize
            );
            Widgets.Checkbox(checkboxRect.position, ref value);
            
            return rowHeight;
        }

        /// <summary>
        /// 绘制滑块设置项
        /// </summary>
        public static float DrawSliderSetting(Rect rect, string label, string tooltip, ref float value, float min, float max, string format = "F1")
        {
            float rowHeight = 28f;
            
            Rect rowRect = new Rect(rect.x, rect.y, rect.width, rowHeight);
            
            // 悬停效果
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
            }
            
            float labelWidth = rect.width * 0.35f;
            float valueWidth = 60f;
            float sliderWidth = rect.width - labelWidth - valueWidth - 20f;
            
            // 标签
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextPrimary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
            
            // 滑块
            Rect sliderRect = new Rect(labelRect.xMax + 10f, rowRect.y + 4f, sliderWidth, rowHeight - 8f);
            value = Widgets.HorizontalSlider(sliderRect, value, min, max);
            
            // 值显示
            Rect valueRect = new Rect(sliderRect.xMax + 10f, rowRect.y, valueWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = TextSecondary;
            Widgets.Label(valueRect, value.ToString(format));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            return rowHeight;
        }

        /// <summary>
        /// 绘制文本输入设置项
        /// </summary>
        public static float DrawTextFieldSetting(Rect rect, string label, string tooltip, ref string value, bool isPassword = false)
        {
            float rowHeight = 28f;
            
            Rect rowRect = new Rect(rect.x, rect.y, rect.width, rowHeight);
            
            float labelWidth = rect.width * 0.35f;
            float textFieldWidth = rect.width - labelWidth - 10f;
            
            // 标签
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextPrimary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
            
            // 文本框
            Rect textFieldRect = new Rect(labelRect.xMax + 10f, rowRect.y + 2f, textFieldWidth, rowHeight - 4f);
            
            // 绘制文本框背景
            Widgets.DrawBoxSolid(textFieldRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            
            if (isPassword && !string.IsNullOrEmpty(value))
            {
                // 密码模式：显示星号
                string displayValue = new string('*', Math.Min(value.Length, 20));
                GUI.SetNextControlName("PasswordField" + rect.y);
                value = Widgets.TextField(textFieldRect, value);
            }
            else
            {
                value = Widgets.TextField(textFieldRect, value ?? "");
            }
            
            return rowHeight;
        }

        /// <summary>
        /// 绘制下拉选择设置项
        /// </summary>
        public static float DrawDropdownSetting(Rect rect, string label, string tooltip, string currentValue, string[] options, Action<string> onSelect)
        {
            float rowHeight = 28f;
            
            Rect rowRect = new Rect(rect.x, rect.y, rect.width, rowHeight);
            
            float labelWidth = rect.width * 0.35f;
            float buttonWidth = rect.width - labelWidth - 10f;
            
            // 标签
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextPrimary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
            
            // 下拉按钮
            Rect buttonRect = new Rect(labelRect.xMax + 10f, rowRect.y + 2f, buttonWidth, rowHeight - 4f);
            
            if (Widgets.ButtonText(buttonRect, currentValue))
            {
                var menuOptions = new List<FloatMenuOption>();
                foreach (var option in options)
                {
                    string optionCopy = option;
                    menuOptions.Add(new FloatMenuOption(option, () => onSelect(optionCopy)));
                }
                Find.WindowStack.Add(new FloatMenu(menuOptions));
            }
            
            return rowHeight;
        }

        /// <summary>
        /// 绘制分组标题
        /// </summary>
        public static float DrawSectionHeader(Rect rect, string title, Color? color = null)
        {
            float height = 32f;
            Color headerColor = color ?? AccentBlue;
            
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, height);
            
            // 背景渐变效果
            Widgets.DrawBoxSolid(headerRect, new Color(headerColor.r * 0.2f, headerColor.g * 0.2f, headerColor.b * 0.2f, 0.5f));
            
            // 左侧装饰
            Rect decorRect = new Rect(headerRect.x, headerRect.y, 4f, height);
            Widgets.DrawBoxSolid(decorRect, headerColor);
            
            // 文字
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = headerColor;
            Rect labelRect = new Rect(headerRect.x + 12f, headerRect.y, headerRect.width - 12f, height);
            Widgets.Label(labelRect, title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            return height;
        }

        /// <summary>
        /// 绘制提示信息
        /// </summary>
        public static float DrawInfoBox(Rect rect, string message, InfoBoxType type = InfoBoxType.Info)
        {
            float height = 40f;
            
            Color bgColor, borderColor, textColor;
            string prefix;
            
            switch (type)
            {
                case InfoBoxType.Success:
                    bgColor = new Color(0.1f, 0.3f, 0.1f, 0.5f);
                    borderColor = AccentGreen;
                    textColor = AccentGreen;
                    prefix = "[OK] ";
                    break;
                case InfoBoxType.Warning:
                    bgColor = new Color(0.3f, 0.25f, 0.1f, 0.5f);
                    borderColor = AccentYellow;
                    textColor = AccentYellow;
                    prefix = "[!] ";
                    break;
                case InfoBoxType.Error:
                    bgColor = new Color(0.3f, 0.1f, 0.1f, 0.5f);
                    borderColor = AccentRed;
                    textColor = AccentRed;
                    prefix = "[X] ";
                    break;
                default:
                    bgColor = new Color(0.1f, 0.15f, 0.25f, 0.5f);
                    borderColor = AccentBlue;
                    textColor = AccentBlue;
                    prefix = "[i] ";
                    break;
            }
            
            Rect boxRect = new Rect(rect.x, rect.y, rect.width, height);
            
            // 背景
            Widgets.DrawBoxSolid(boxRect, bgColor);
            
            // 左边框
            Rect borderRect = new Rect(boxRect.x, boxRect.y, 3f, height);
            Widgets.DrawBoxSolid(borderRect, borderColor);
            
            // 文字
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = textColor;
            Rect textRect = new Rect(boxRect.x + 10f, boxRect.y, boxRect.width - 15f, height);
            Widgets.Label(textRect, prefix + message);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            return height;
        }

        /// <summary>
        /// 绘制分隔线
        /// </summary>
        public static float DrawSeparator(Rect rect, float height = 1f)
        {
            float paddedHeight = height + 8f;
            Rect lineRect = new Rect(rect.x, rect.y + 4f, rect.width, height);
            Widgets.DrawBoxSolid(lineRect, CardBorder);
            return paddedHeight;
        }

        /// <summary>
        /// 绘制按钮（现代风格）
        /// </summary>
        public static bool DrawButton(Rect rect, string label, Color? color = null, bool enabled = true)
        {
            Color buttonColor = color ?? AccentBlue;
            bool isHovered = Mouse.IsOver(rect) && enabled;
            
            // 禁用状态
            if (!enabled)
            {
                buttonColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }
            
            // 背景
            Color bgColor = isHovered ? 
                new Color(buttonColor.r * 1.2f, buttonColor.g * 1.2f, buttonColor.b * 1.2f, buttonColor.a) :
                buttonColor;
            Widgets.DrawBoxSolid(rect, bgColor);
            
            // 文字
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = enabled ? TextPrimary : TextMuted;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            return enabled && Widgets.ButtonInvisible(rect);
        }

        /// <summary>
        /// 绘制单选按钮组
        /// </summary>
        public static float DrawRadioGroup(Rect rect, string label, string[] options, ref int selectedIndex)
        {
            float height = 28f + options.Length * 26f;
            float y = rect.y;
            
            // 标签
            Text.Font = GameFont.Small;
            GUI.color = TextPrimary;
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), label);
            GUI.color = Color.white;
            y += 28f;
            
            // 选项
            for (int i = 0; i < options.Length; i++)
            {
                Rect optionRect = new Rect(rect.x + 20f, y, rect.width - 20f, 24f);
                bool isSelected = i == selectedIndex;
                
                // 圆形指示器
                Rect circleRect = new Rect(optionRect.x, optionRect.y + 4f, 16f, 16f);
                GUI.color = isSelected ? AccentBlue : TextMuted;
                Widgets.DrawBox(circleRect, 1);
                if (isSelected)
                {
                    Rect innerCircle = circleRect.ContractedBy(4f);
                    Widgets.DrawBoxSolid(innerCircle, AccentBlue);
                }
                GUI.color = Color.white;
                
                // 文字
                Rect labelRect = new Rect(circleRect.xMax + 8f, optionRect.y, optionRect.width - 24f, 24f);
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = isSelected ? TextPrimary : TextSecondary;
                Widgets.Label(labelRect, options[i]);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                
                // 点击
                if (Widgets.ButtonInvisible(optionRect))
                {
                    selectedIndex = i;
                }
                
                y += 26f;
            }
            
            return height;
        }

        /// <summary>
        /// 绘制完整的选项卡界面
        /// </summary>
        public static void DrawTabbedInterface(Rect rect, TabManager tabManager, float tabBarHeight = 40f)
        {
            if (tabManager.Tabs.Count == 0) return;
            
            // 选项卡栏
            Rect tabBarRect = new Rect(rect.x, rect.y, rect.width, tabBarHeight);
            DrawEnhancedTabBar(tabBarRect, tabManager);
            
            // 内容区域
            Rect contentRect = new Rect(rect.x, rect.y + tabBarHeight + 4f, rect.width, rect.height - tabBarHeight - 4f);
            
            // 绘制当前选项卡内容
            if (tabManager.CurrentTab >= 0 && tabManager.CurrentTab < tabManager.Tabs.Count)
            {
                var currentTabDef = tabManager.Tabs[tabManager.CurrentTab];
                currentTabDef.DrawContent?.Invoke(contentRect);
            }
        }

        /// <summary>
        /// 绘制增强版选项卡栏
        /// </summary>
        private static void DrawEnhancedTabBar(Rect rect, TabManager tabManager)
        {
            float tabWidth = rect.width / tabManager.Tabs.Count;
            
            // 背景
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.08f, 0.95f));
            
            for (int i = 0; i < tabManager.Tabs.Count; i++)
            {
                var tabDef = tabManager.Tabs[i];
                Rect tabRect = new Rect(rect.x + i * tabWidth, rect.y, tabWidth, rect.height);
                bool isActive = i == tabManager.CurrentTab;
                bool isHovered = Mouse.IsOver(tabRect);
                
                // 背景
                Color bgColor = isActive ? new Color(tabDef.AccentColor.r * 0.3f, tabDef.AccentColor.g * 0.3f, tabDef.AccentColor.b * 0.3f, 0.8f) :
                                (isHovered ? new Color(0.2f, 0.2f, 0.2f, 0.8f) : Color.clear);
                if (bgColor != Color.clear)
                {
                    Widgets.DrawBoxSolid(tabRect, bgColor);
                }
                
                // 底部高亮条
                if (isActive)
                {
                    Rect highlightRect = new Rect(tabRect.x + 2f, tabRect.yMax - 3f, tabRect.width - 4f, 3f);
                    Widgets.DrawBoxSolid(highlightRect, tabDef.AccentColor);
                }
                
                // 图标 + 文字
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = isActive ? tabDef.AccentColor : (isHovered ? TextPrimary : TextSecondary);
                string displayText = !string.IsNullOrEmpty(tabDef.Icon) ? $"{tabDef.Icon} {tabDef.Name}" : tabDef.Name;
                Widgets.Label(tabRect, displayText);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                
                // 点击
                if (Widgets.ButtonInvisible(tabRect))
                {
                    tabManager.SwitchTo(i);
                }
            }
            
            // 底部分隔线
            Rect bottomLine = new Rect(rect.x, rect.yMax, rect.width, 1f);
            Widgets.DrawBoxSolid(bottomLine, CardBorder);
        }

        /// <summary>
        /// 绘制带滚动的卡片内容区域
        /// </summary>
        public static void DrawScrollableCardContent(Rect rect, ref Vector2 scrollPosition, float contentHeight, Action<Rect> drawContent)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            drawContent(viewRect);
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 绘制设置组（多个设置项的容器）
        /// </summary>
        public static float DrawSettingsGroup(Rect rect, string title, Color? accentColor, Action<Rect> drawSettings)
        {
            float headerHeight = 36f;
            Color accent = accentColor ?? AccentBlue;
            
            // 组背景
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.6f));
            
            // 顶部装饰条
            Rect topBar = new Rect(rect.x, rect.y, rect.width, 3f);
            Widgets.DrawBoxSolid(topBar, accent);
            
            // 标题区域
            Rect headerRect = new Rect(rect.x, rect.y + 3f, rect.width, headerHeight);
            Widgets.DrawBoxSolid(headerRect, new Color(accent.r * 0.15f, accent.g * 0.15f, accent.b * 0.15f, 0.8f));
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = accent;
            Rect titleRect = new Rect(headerRect.x + 12f, headerRect.y, headerRect.width - 12f, headerRect.height);
            Widgets.Label(titleRect, title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 内容区域
            Rect contentRect = new Rect(rect.x + CardPadding, rect.y + 3f + headerHeight + SmallGap,
                                        rect.width - CardPadding * 2f, rect.height - headerHeight - 3f - SmallGap * 2f);
            drawSettings(contentRect);
            
            // 边框
            GUI.color = new Color(accent.r * 0.5f, accent.g * 0.5f, accent.b * 0.5f, 0.5f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            
            return rect.height;
        }

        /// <summary>
        /// 绘制难度模式选择卡片
        /// </summary>
        public static bool DrawModeCard(Rect rect, string title, string subtitle, string description,
                                         bool isSelected, Color accentColor, Texture2D icon = null)
        {
            bool clicked = false;
            bool isHovered = Mouse.IsOver(rect);
            
            // 背景
            Color bgColor = isSelected ?
                new Color(accentColor.r * 0.2f, accentColor.g * 0.2f, accentColor.b * 0.2f, 0.9f) :
                (isHovered ? CardBackgroundHover : CardBackground);
            Widgets.DrawBoxSolid(rect, bgColor);
            
            // 边框
            GUI.color = isSelected ? accentColor : (isHovered ? new Color(0.5f, 0.5f, 0.5f) : CardBorder);
            Widgets.DrawBox(rect, isSelected ? 2 : 1);
            GUI.color = Color.white;
            
            Rect innerRect = rect.ContractedBy(10f);
            float y = innerRect.y;
            
            // 图标
            float iconSize = 48f;
            Rect iconRect = new Rect(innerRect.x + (innerRect.width - iconSize) / 2f, y, iconSize, iconSize);
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }
            else
            {
                Widgets.DrawBoxSolid(iconRect, accentColor * 0.4f);
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(iconRect, title.Substring(0, 1));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            y += iconSize + 10f;
            
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = isSelected ? accentColor : TextPrimary;
            Rect titleRect = new Rect(innerRect.x, y, innerRect.width, 22f);
            Widgets.Label(titleRect, title);
            y += 22f;
            
            // 副标题
            Text.Font = GameFont.Small;
            GUI.color = TextSecondary;
            Rect subtitleRect = new Rect(innerRect.x, y, innerRect.width, 18f);
            Widgets.Label(subtitleRect, subtitle);
            y += 20f;
            
            // 描述
            Text.Font = GameFont.Tiny;
            GUI.color = TextMuted;
            Rect descRect = new Rect(innerRect.x, y, innerRect.width, 36f);
            Widgets.Label(descRect, description);
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 选中标记
            if (isSelected)
            {
                Rect checkRect = new Rect(rect.xMax - 24f, rect.y + 6f, 18f, 18f);
                Widgets.DrawBoxSolid(checkRect, accentColor);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                Widgets.Label(checkRect, "OK");
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            
            clicked = Widgets.ButtonInvisible(rect);
            return clicked;
        }

        /// <summary>
        /// 绘制API配置区域
        /// </summary>
        public static float DrawApiConfigSection(Rect rect, string providerName,
                                                  ref string endpoint, ref string apiKey, ref string model,
                                                  string endpointTooltip = null, string apiKeyTooltip = null, string modelTooltip = null)
        {
            float y = rect.y;
            float rowHeight = 32f;
            
            // Endpoint
            Rect endpointRect = new Rect(rect.x, y, rect.width, rowHeight);
            DrawTextFieldSetting(endpointRect, "API 端点", endpointTooltip ?? "LLM API 服务器地址", ref endpoint);
            y += rowHeight + SmallGap;
            
            // API Key
            Rect apiKeyRect = new Rect(rect.x, y, rect.width, rowHeight);
            DrawTextFieldSetting(apiKeyRect, "API 密钥", apiKeyTooltip ?? "API 访问密钥（留空使用本地服务）", ref apiKey, true);
            y += rowHeight + SmallGap;
            
            // Model
            Rect modelRect = new Rect(rect.x, y, rect.width, rowHeight);
            DrawTextFieldSetting(modelRect, "模型名称", modelTooltip ?? "要使用的模型ID", ref model);
            y += rowHeight;
            
            return y - rect.y;
        }

        /// <summary>
        /// 绘制整数输入设置项
        /// </summary>
        public static float DrawIntFieldSetting(Rect rect, string label, string tooltip, ref int value, int min = 0, int max = int.MaxValue)
        {
            float rowHeight = 28f;
            
            Rect rowRect = new Rect(rect.x, rect.y, rect.width, rowHeight);
            
            float labelWidth = rect.width * 0.45f;
            float textFieldWidth = 80f;
            
            // 标签
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextPrimary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
            
            // 文本框
            Rect textFieldRect = new Rect(rowRect.xMax - textFieldWidth, rowRect.y + 2f, textFieldWidth, rowHeight - 4f);
            Widgets.DrawBoxSolid(textFieldRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            
            string valueStr = value.ToString();
            valueStr = Widgets.TextField(textFieldRect, valueStr);
            
            if (int.TryParse(valueStr, out int newValue))
            {
                value = Mathf.Clamp(newValue, min, max);
            }
            
            return rowHeight;
        }

        /// <summary>
        /// 绘制多行文本区域
        /// </summary>
        public static float DrawTextAreaSetting(Rect rect, string label, string tooltip, ref string value, float textAreaHeight = 100f)
        {
            float totalHeight = 24f + textAreaHeight + SmallGap;
            float y = rect.y;
            
            // 标签
            Rect labelRect = new Rect(rect.x, y, rect.width, 22f);
            Text.Font = GameFont.Small;
            GUI.color = TextPrimary;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            y += 24f;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
            
            // 文本区域
            Rect textAreaRect = new Rect(rect.x, y, rect.width, textAreaHeight);
            Widgets.DrawBoxSolid(textAreaRect, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            GUI.color = CardBorder;
            Widgets.DrawBox(textAreaRect, 1);
            GUI.color = Color.white;
            
            value = Widgets.TextArea(textAreaRect.ContractedBy(4f), value ?? "");
            
            return totalHeight;
        }

        /// <summary>
        /// 绘制操作按钮组
        /// </summary>
        public static float DrawButtonGroup(Rect rect, params (string label, Color color, Action onClick)[] buttons)
        {
            float buttonWidth = (rect.width - (buttons.Length - 1) * SmallGap) / buttons.Length;
            float buttonHeight = 32f;
            
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, color, onClick) = buttons[i];
                Rect buttonRect = new Rect(rect.x + i * (buttonWidth + SmallGap), rect.y, buttonWidth, buttonHeight);
                
                if (DrawButton(buttonRect, label, color))
                {
                    onClick?.Invoke();
                }
            }
            
            return buttonHeight;
        }

        /// <summary>
        /// 绘制带描述的复选框
        /// </summary>
        public static float DrawCheckboxWithDescription(Rect rect, string label, string description, ref bool value)
        {
            float totalHeight = 48f;
            
            Rect rowRect = new Rect(rect.x, rect.y, rect.width, totalHeight);
            
            // 悬停效果
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
            }
            
            // 复选框
            float checkboxSize = 24f;
            Rect checkboxRect = new Rect(rect.x, rect.y + 12f, checkboxSize, checkboxSize);
            Widgets.Checkbox(checkboxRect.position, ref value);
            
            // 标签
            Rect labelRect = new Rect(rect.x + 30f, rect.y + 4f, rect.width - 30f, 22f);
            Text.Font = GameFont.Small;
            GUI.color = value ? TextPrimary : TextSecondary;
            Widgets.Label(labelRect, label);
            
            // 描述
            Rect descRect = new Rect(rect.x + 30f, rect.y + 24f, rect.width - 30f, 20f);
            Text.Font = GameFont.Tiny;
            GUI.color = TextMuted;
            Widgets.Label(descRect, description);
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            return totalHeight;
        }

        /// <summary>
        /// 绘制状态指示器
        /// </summary>
        public static void DrawStatusIndicator(Rect rect, string text, bool isActive, Color activeColor = default)
        {
            if (activeColor == default) activeColor = AccentGreen;
            
            Color dotColor = isActive ? activeColor : TextMuted;
            Color textColor = isActive ? TextPrimary : TextMuted;
            
            // 状态点
            float dotSize = 10f;
            Rect dotRect = new Rect(rect.x, rect.y + (rect.height - dotSize) / 2f, dotSize, dotSize);
            Widgets.DrawBoxSolid(dotRect, dotColor);
            
            // 文字
            Rect textRect = new Rect(rect.x + dotSize + 6f, rect.y, rect.width - dotSize - 6f, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = textColor;
            Widgets.Label(textRect, text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }

    /// <summary>
    /// 信息框类型
    /// </summary>
    public enum InfoBoxType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
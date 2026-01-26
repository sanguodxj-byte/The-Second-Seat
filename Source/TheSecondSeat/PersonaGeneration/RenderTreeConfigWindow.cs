using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.TTS;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 表情映射管理器 (RenderTreeConfigWindow)
    /// 允许在游戏内查看、修改和预览立绘渲染配置
    /// </summary>
    public class RenderTreeConfigWindow : Window
    {
        private NarratorPersonaDef currentPersona;
        private RenderTreeDef currentDef;
        private LipSyncMappingDef currentLipSyncDef;
        private List<OutfitDef> currentOutfits; // ⭐ v2.7.0: 服装配置
        private Vector2 scrollPosition;
        private float lastScrollViewHeight = 1000f; // 动态计算滚动高度
        
        private enum ConfigTab { Expressions, Speaking, Body, Accessories, HeadPat, LipSync, Outfits }
        private ConfigTab currentTab = ConfigTab.Expressions;
        
        // 预览相关
        private Texture previewTexture; // ⭐ v2.6.1: 改为 Texture 类型，支持 RenderTexture
        private string previewExpressionName = "Neutral"; // ⭐ v2.6.0: 改为字符串，支持动态表情
        private int previewVariant = 0;
        private bool isPreviewDirty = true;
        private float lastEditTime = 0f; // 上次编辑时间，用于防抖
        private const float PreviewUpdateDelay = 0.5f; // 防抖延迟（秒）
        
        // ⭐ v2.6.0: 新表情输入
        private string newExpressionName = "";
        private string newExpressionEyesTexture = "";
        private string newExpressionMouthTexture = "";
        
        // ⭐ v2.6.1: 性能优化 - 缓存表情名称列表
        private List<string> cachedExpressionNames = null;
        private RenderTreeDef cachedDefForExpressions = null;
        private int cachedExpressionCount = -1;
        
        // ⭐ v2.4.0: 增加默认窗口高度，避免预览被遮挡
        public override Vector2 InitialSize => new Vector2(1000f, 800f);
        
        public RenderTreeConfigWindow()
        {
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.resizeable = true;
            this.draggable = true;
            
            // 默认选中第一个 Narrator
            currentPersona = DefDatabase<NarratorPersonaDef>.AllDefsListForReading.FirstOrDefault();
            ReloadDefs();
        }
        
        private void ReloadDefs()
        {
            if (currentPersona != null)
            {
                currentDef = RenderTreeDefManager.GetRenderTree(currentPersona.defName);
                
                string lipSyncPath = RenderTreeConfigIO.GetDefaultLipSyncSavePath(currentPersona.defName);
                currentLipSyncDef = RenderTreeConfigIO.LoadLipSyncMappingFromXml(lipSyncPath);
                if (currentLipSyncDef == null)
                {
                    currentLipSyncDef = new LipSyncMappingDef
                    {
                        defName = currentPersona.defName,
                        mappings = new List<LipSyncMappingDef.GroupMapping>()
                    };
                }

                // ⭐ v2.7.0: 加载服装配置
                string outfitPath = OutfitConfigIO.GetDefaultSavePath(currentPersona.defName);
                if (System.IO.File.Exists(outfitPath))
                {
                    currentOutfits = OutfitConfigIO.LoadOutfitsFromXml(outfitPath);
                }
                else
                {
                    // 如果没有自定义配置，加载默认的 Defs
                    currentOutfits = new List<OutfitDef>(OutfitDefManager.GetOutfitsForPersona(currentPersona.defName));
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, 300, 30), "TSS_RenderTree_WindowTitle".Translate());
            Text.Font = GameFont.Small;
            
            // 顶部工具栏
            float topY = 0f;
            
            // 选择 Persona
            if (Widgets.ButtonText(new Rect(320, topY, 200, 30), currentPersona?.narratorName ?? "TSS_RenderTree_SelectNarrator".Translate()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var def in DefDatabase<NarratorPersonaDef>.AllDefsListForReading)
                {
                    options.Add(new FloatMenuOption(def.narratorName, () =>
                    {
                        currentPersona = def;
                        ReloadDefs();
                        MarkDirty();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            // 保存按钮
            if (Widgets.ButtonText(new Rect(530, topY, 80, 30), "TSS_RenderTree_Save".Translate()))
            {
                if (currentTab == ConfigTab.Outfits)
                {
                    if (currentOutfits != null && currentPersona != null)
                    {
                        string path = OutfitConfigIO.GetDefaultSavePath(currentPersona.defName);
                        OutfitConfigIO.SaveOutfitsToXml(currentPersona.defName, currentOutfits, path);
                    }
                }
                else if (currentTab == ConfigTab.LipSync)
                {
                    if (currentLipSyncDef != null)
                    {
                        string path = RenderTreeConfigIO.GetDefaultLipSyncSavePath(currentLipSyncDef.defName);
                        RenderTreeConfigIO.SaveLipSyncMappingToXml(currentLipSyncDef, path);
                    }
                }
                else if (currentDef != null)
                {
                    string path = RenderTreeConfigIO.GetDefaultSavePath(currentDef.defName);
                    RenderTreeConfigIO.SaveToXml(currentDef, path);
                }
            }
            
            // 读取按钮
            if (Widgets.ButtonText(new Rect(615, topY, 80, 30), "TSS_RenderTree_Load".Translate()))
            {
                if (currentTab == ConfigTab.Outfits)
                {
                    if (currentPersona != null)
                    {
                        string path = OutfitConfigIO.GetDefaultSavePath(currentPersona.defName);
                        var loadedOutfits = OutfitConfigIO.LoadOutfitsFromXml(path);
                        if (loadedOutfits != null && loadedOutfits.Count > 0)
                        {
                            currentOutfits = loadedOutfits;
                            Messages.Message("TSS_RenderTree_LoadSuccess".Translate(path), MessageTypeDefOf.PositiveEvent, false);
                        }
                        else
                        {
                            Messages.Message("TSS_RenderTree_LoadFailed".Translate(path), MessageTypeDefOf.NeutralEvent, false);
                        }
                    }
                }
                else if (currentTab == ConfigTab.LipSync)
                {
                    if (currentPersona != null)
                    {
                        string path = RenderTreeConfigIO.GetDefaultLipSyncSavePath(currentPersona.defName);
                        var loadedDef = RenderTreeConfigIO.LoadLipSyncMappingFromXml(path);
                        if (loadedDef != null)
                        {
                            currentLipSyncDef = loadedDef;
                            Messages.Message("TSS_RenderTree_LipSync_LoadSuccess".Translate(), MessageTypeDefOf.PositiveEvent, false);
                        }
                        else
                        {
                            Messages.Message("TSS_RenderTree_LipSync_LoadFailed".Translate(), MessageTypeDefOf.NeutralEvent, false);
                        }
                    }
                }
                else if (currentDef != null)
                {
                    string path = RenderTreeConfigIO.GetDefaultSavePath(currentDef.defName);
                    var loadedDef = RenderTreeConfigIO.LoadFromXml(path);
                    if (loadedDef != null)
                    {
                        currentDef.expressions = loadedDef.expressions;
                        currentDef.speaking = loadedDef.speaking;
                        currentDef.bodyMappings = loadedDef.bodyMappings;
                        currentDef.accessories = loadedDef.accessories;
                        currentDef.headPat = loadedDef.headPat;
                        InvalidateExpressionCache();
                        MarkDirty(true);
                        Messages.Message("TSS_RenderTree_LoadSuccess".Translate(path), MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message("TSS_RenderTree_LoadFailed".Translate(path), MessageTypeDefOf.NeutralEvent, false);
                    }
                }
            }
            
            // 显示配置文件路径提示
            if (currentDef != null)
            {
                string configPath = "";
                if (currentTab == ConfigTab.Outfits)
                    configPath = OutfitConfigIO.GetDefaultSavePath(currentPersona?.defName ?? "");
                else if (currentTab == ConfigTab.LipSync)
                    configPath = RenderTreeConfigIO.GetDefaultLipSyncSavePath(currentPersona?.defName ?? "");
                else
                    configPath = RenderTreeConfigIO.GetDefaultSavePath(currentDef.defName);
                    
                TooltipHandler.TipRegion(new Rect(530, topY, 165, 30), "TSS_RenderTree_ConfigPath".Translate() + "\n" + configPath);
            }
            
            // 如果没有 Def，显示提示
            if (currentDef == null)
            {
                Widgets.Label(new Rect(0, 40, inRect.width, 30), "TSS_RenderTree_NoDefFound".Translate());
                return;
            }
            
            Widgets.Label(new Rect(0, 40, inRect.width, 20), $"DefName: {currentDef.defName}");
            
            // 分割视图：左侧预览，右侧配置
            float contentY = 70f;
            float previewWidth = 350f;
            float configX = previewWidth + 20f;
            float configWidth = inRect.width - configX;
            float contentHeight = inRect.height - contentY;
            
            // === 左侧：预览区域 ===
            Rect previewRect = new Rect(0, contentY, previewWidth, contentHeight);
            DrawPreviewArea(previewRect);
            
            // === 右侧：配置区域 ===
            Rect configRect = new Rect(configX, contentY, configWidth, contentHeight);
            
            // 绘制标签页
            float tabHeight = 30f;
            Rect tabRect = new Rect(configRect.x, configRect.y, configRect.width, tabHeight);
            DrawTabs(tabRect);
            
            // 绘制配置内容
            Rect configContentRect = new Rect(configRect.x, configRect.y + tabHeight + 10f, configRect.width, configRect.height - tabHeight - 10f);
            DrawConfigArea(configContentRect);
        }

        private void DrawTabs(Rect rect)
        {
            float tabWidth = rect.width / 7f; // ⭐ v2.7.0: 增加到7个Tab
            
            // ⭐ v2.6.6: 使用翻译键替代双语硬编码
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, tabWidth, rect.height), "TSS_RenderTree_Tab_Expressions".Translate()))
                currentTab = ConfigTab.Expressions;
                
            if (Widgets.ButtonText(new Rect(rect.x + tabWidth, rect.y, tabWidth, rect.height), "TSS_RenderTree_Tab_Speaking".Translate()))
                currentTab = ConfigTab.Speaking;
                
            if (Widgets.ButtonText(new Rect(rect.x + tabWidth * 2, rect.y, tabWidth, rect.height), "TSS_RenderTree_Tab_Body".Translate()))
                currentTab = ConfigTab.Body;
                
            if (Widgets.ButtonText(new Rect(rect.x + tabWidth * 3, rect.y, tabWidth, rect.height), "TSS_RenderTree_Tab_Accessories".Translate()))
                currentTab = ConfigTab.Accessories;

            if (Widgets.ButtonText(new Rect(rect.x + tabWidth * 4, rect.y, tabWidth, rect.height), "TSS_RenderTree_Tab_HeadPat".Translate()))
                currentTab = ConfigTab.HeadPat;

            if (Widgets.ButtonText(new Rect(rect.x + tabWidth * 5, rect.y, tabWidth, rect.height), "TSS_RenderTree_Tab_LipSync".Translate()))
                currentTab = ConfigTab.LipSync;
            
            // ⭐ v2.7.0: 添加服装Tab
            if (Widgets.ButtonText(new Rect(rect.x + tabWidth * 6, rect.y, tabWidth, rect.height), "TSS_RenderTree_Tab_Outfits".Translate()))
                currentTab = ConfigTab.Outfits;
                
            // 高亮当前标签
            float highlightX = rect.x + (int)currentTab * tabWidth;
            Widgets.DrawHighlight(new Rect(highlightX, rect.y + rect.height - 4f, tabWidth, 4f));
        }
        
        private void DrawPreviewArea(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            // 使用 Listing_Standard 布局
            Rect innerRect = rect.ContractedBy(8f);
            Listing_Standard list = new Listing_Standard();
            list.Begin(innerRect);
            
            list.Label("<b>预览控制</b>");
            list.Gap(4f);
            
            // ⭐ v2.6.0: 从 RenderTreeDef 动态获取表情列表
            var expressionNames = GetConfiguredExpressionNames();
            
            const int previewButtonsPerRow = 5;
            const float previewButtonHeight = 20f;
            const float previewButtonSpacing = 2f;
            float previewAvailableWidth = list.ColumnWidth;
            float previewButtonWidth = (previewAvailableWidth - previewButtonSpacing * (previewButtonsPerRow - 1)) / previewButtonsPerRow;
            
            // 当前表情显示
            list.Label($"<color=#AAAAAA>当前: <b>{previewExpressionName}</b> (变体 {previewVariant})</color>");
            list.Gap(2f);
            
            // 绘制表情类型网格（基于配置的表情列表）
            int previewRowCount = Mathf.CeilToInt((float)expressionNames.Count / previewButtonsPerRow);
            for (int row = 0; row < previewRowCount; row++)
            {
                Rect rowRect = list.GetRect(previewButtonHeight);
                for (int col = 0; col < previewButtonsPerRow; col++)
                {
                    int index = row * previewButtonsPerRow + col;
                    if (index >= expressionNames.Count) break;
                    
                    string exprName = expressionNames[index];
                    float x = rowRect.x + col * (previewButtonWidth + previewButtonSpacing);
                    Rect btnRect = new Rect(x, rowRect.y, previewButtonWidth, previewButtonHeight);
                    
                    // 高亮当前预览的表情
                    bool isSelected = previewExpressionName == exprName;
                    if (isSelected)
                    {
                        Widgets.DrawHighlight(btnRect);
                    }
                    
                    // 使用缩写显示
                    string shortName = exprName;
                    if (shortName.Length > 6) shortName = shortName.Substring(0, 5) + "..";
                    
                    if (Widgets.ButtonText(btnRect, shortName, true, true, true))
                    {
                        previewExpressionName = exprName;
                        previewVariant = 0;
                        MarkDirty();
                    }
                    
                    // 鼠标悬停提示完整名称
                    TooltipHandler.TipRegion(btnRect, exprName);
                }
                list.Gap(previewButtonSpacing);
            }
            
            list.Gap(4f);
            
            // ⭐ v2.6.0: 紧凑的变体选择（基于字符串表情名）
            int maxVariants = GetVariantCountByName(previewExpressionName);
            if (maxVariants > 0)
            {
                Rect variantLabelRect = list.GetRect(18f);
                Widgets.Label(variantLabelRect, "<color=#888888>变体:</color>");
                
                Rect variantRow = list.GetRect(20f);
                float variantBtnWidth = Mathf.Min(35f, (variantRow.width - 4f) / (maxVariants + 1));
                
                // 基础变体按钮
                Rect baseBtnRect = new Rect(variantRow.x, variantRow.y, variantBtnWidth, 20f);
                if (previewVariant == 0) Widgets.DrawHighlight(baseBtnRect);
                if (Widgets.ButtonText(baseBtnRect, "0", true, true, true))
                {
                    previewVariant = 0;
                    MarkDirty();
                }
                
                // 其他变体按钮
                for (int i = 1; i <= maxVariants; i++)
                {
                    Rect vBtnRect = new Rect(variantRow.x + i * (variantBtnWidth + 2f), variantRow.y, variantBtnWidth, 20f);
                    if (previewVariant == i) Widgets.DrawHighlight(vBtnRect);
                    if (Widgets.ButtonText(vBtnRect, i.ToString(), true, true, true))
                    {
                        previewVariant = i;
                        MarkDirty();
                    }
                }
                list.Gap(4f);
            }
            
            // 刷新按钮（紧凑）
            Rect refreshRow = list.GetRect(22f);
            if (Widgets.ButtonText(new Rect(refreshRow.x, refreshRow.y, refreshRow.width / 2 - 2, 22f), "刷新"))
            {
                MarkDirty(true);
            }
            if (Widgets.ButtonText(new Rect(refreshRow.x + refreshRow.width / 2 + 2, refreshRow.y, refreshRow.width / 2 - 2, 22f), "清除缓存"))
            {
                if (currentPersona != null && TryParseExpressionType(previewExpressionName, out var exprType))
                {
                    LayeredPortraitCompositor.ClearCache(currentPersona.defName, exprType);
                    MarkDirty(true);
                }
            }
            list.Gap(8f);
            
            // 预览图像
            float remainingHeight = innerRect.height - list.CurHeight - 5f;
            if (remainingHeight < 80f) remainingHeight = 80f;
            
            Rect imageRect = list.GetRect(remainingHeight);
            
            // 绘制深灰色背景
            Widgets.DrawBoxSolid(imageRect, new Color(0.12f, 0.12f, 0.12f));
            Widgets.DrawBox(imageRect);
            
            // 防抖更新逻辑
            if (isPreviewDirty && (Time.realtimeSinceStartup - lastEditTime > PreviewUpdateDelay))
            {
                UpdatePreview();
            }
            
            if (previewTexture != null)
            {
                Widgets.DrawTextureFitted(imageRect, previewTexture, 1.0f);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(imageRect, "加载中...");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            list.End();
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 获取已配置的表情名称列表
        /// ⭐ v2.6.1: 性能优化 - 使用缓存避免每帧重新创建列表
        /// </summary>
        private List<string> GetConfiguredExpressionNames()
        {
            // 检查缓存是否有效
            int currentCount = currentDef?.expressions?.Count ?? 0;
            if (cachedExpressionNames != null && 
                cachedDefForExpressions == currentDef && 
                cachedExpressionCount == currentCount)
            {
                return cachedExpressionNames;
            }
            
            // 重建缓存
            var names = new List<string>();
            if (currentDef?.expressions != null)
            {
                foreach (var mapping in currentDef.expressions)
                {
                    if (!string.IsNullOrEmpty(mapping.expression))
                    {
                        names.Add(mapping.expression);
                    }
                }
            }
            // 确保至少有 Neutral
            if (names.Count == 0)
            {
                names.Add("Neutral");
            }
            
            // 更新缓存
            cachedExpressionNames = names;
            cachedDefForExpressions = currentDef;
            cachedExpressionCount = currentCount;
            
            return names;
        }
        
        /// <summary>
        /// ⭐ v2.6.1: 使缓存失效
        /// </summary>
        private void InvalidateExpressionCache()
        {
            cachedExpressionNames = null;
            cachedExpressionCount = -1;
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 根据表情名称获取变体数量
        /// </summary>
        private int GetVariantCountByName(string expressionName)
        {
            if (currentDef?.expressions == null) return 0;
            var mapping = currentDef.expressions.FirstOrDefault(m => m.expression == expressionName);
            return mapping?.variants?.Count ?? 0;
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 尝试将字符串解析为 ExpressionType 枚举
        /// </summary>
        private bool TryParseExpressionType(string name, out ExpressionType result)
        {
            return Enum.TryParse(name, true, out result);
        }
        
        private void UpdatePreview()
        {
            if (currentPersona == null) return;
            
            var config = currentPersona.GetLayeredConfig();
            if (config == null) return;
            
            // ⭐ v2.6.0: 尝试将字符串表情名解析为枚举（用于兼容现有系统）
            if (!TryParseExpressionType(previewExpressionName, out var previewExpressionType))
            {
                // 如果无法解析，使用 Neutral 作为回退
                previewExpressionType = ExpressionType.Neutral;
            }
            
            // 临时设置 ExpressionSystem 的状态以影响合成
            var state = ExpressionSystem.GetExpressionState(currentPersona.defName);
            var oldVariant = state.CurrentVariant;
            var oldIntensity = state.Intensity;
            
            // 设置预览状态
            state.CurrentVariant = previewVariant;
            state.Intensity = previewVariant; // 同时设置 Intensity 以覆盖两者
            
            // 强制重新合成
            #pragma warning disable CS0618
            LayeredPortraitCompositor.ClearCache(currentPersona.defName, previewExpressionType);
            
            // ⭐ v2.6.1: 不再强制转换为 Texture2D，直接使用 Texture
            // CompositeLayers 可能返回 RenderTexture 或 Texture2D
            previewTexture = LayeredPortraitCompositor.CompositeLayers(config, previewExpressionType);
            #pragma warning restore CS0618
            
            // 恢复状态
            state.CurrentVariant = oldVariant;
            state.Intensity = oldIntensity;
            
            isPreviewDirty = false;
        }

        /// <summary>
        /// ⭐ v2.4.0: 关闭窗口时清理资源
        /// </summary>
        public override void PostClose()
        {
            base.PostClose();
            // 不销毁 previewTexture，理由同 UpdatePreview
            previewTexture = null;
        }
        
        private void DrawConfigArea(Rect rect)
        {
            // 使用上一帧计算的高度 (增加缓冲以防止闪烁)
            Rect viewRect = new Rect(0, 0, rect.width - 16, Mathf.Max(lastScrollViewHeight, rect.height));
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            
            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);
            
            try
            {
                switch (currentTab)
                {
                    case ConfigTab.Expressions:
                        DrawExpressionsConfig(list);
                        break;
                    case ConfigTab.Speaking:
                        DrawSpeakingConfig(list);
                        break;
                    case ConfigTab.Body:
                        DrawBodyConfig(list);
                        break;
                    case ConfigTab.Accessories:
                        DrawAccessoriesConfig(list);
                        break;
                    case ConfigTab.HeadPat:
                        DrawHeadPatConfig(list);
                        break;
                    case ConfigTab.LipSync:
                        DrawLipSyncConfig(list);
                        break;
                    case ConfigTab.Outfits:
                        DrawOutfitsConfig(list);
                        break;
                }
            }
            finally
            {
                // 确保在 End 之前获取高度
                float newHeight = list.CurHeight + 100f; // 增加额外缓冲
                
                list.End();
                Widgets.EndScrollView();
                
                // 更新高度以便下一帧使用
                if (Event.current.type == EventType.Repaint)
                {
                    // 平滑高度变化，防止剧烈跳动
                    if (Mathf.Abs(newHeight - lastScrollViewHeight) > 1f)
                    {
                        lastScrollViewHeight = newHeight;
                    }
                }
            }
        }

        private void DrawExpressionsConfig(Listing_Standard list)
        {
            list.Label($"<b>{"TSS_RenderTree_ExpressionMappings".Translate()}</b>");
            list.Gap(4);
            
            // ⭐ v2.6.6: 移除多余的表情预览按钮区域（左侧预览区已有）
            // 直接显示添加新表情和配置表格
            
            // ⭐ v2.6.0: 添加新表情输入框（含纹理配置）
            list.Label($"<color=#888888>{"TSS_RenderTree_AddNewExpression".Translate()}:</color>");
            
            // 第一行：表情名称
            Rect addRow1 = list.GetRect(24);
            Widgets.Label(new Rect(addRow1.x, addRow1.y, 70, 24), "表情名称:");
            newExpressionName = Widgets.TextField(new Rect(addRow1.x + 75, addRow1.y, 150, 24), newExpressionName);
            list.Gap(2);
            
            // 第二行：眼睛纹理
            Rect addRow2 = list.GetRect(24);
            GUI.color = new Color(0.5f, 0.9f, 0.9f); // 青色
            Widgets.Label(new Rect(addRow2.x, addRow2.y, 70, 24), "眼睛:");
            GUI.color = Color.white;
            newExpressionEyesTexture = Widgets.TextField(new Rect(addRow2.x + 75, addRow2.y, 200, 24), newExpressionEyesTexture);
            // 自动填充提示
            if (string.IsNullOrEmpty(newExpressionEyesTexture) && !string.IsNullOrEmpty(newExpressionName))
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(new Rect(addRow2.x + 280, addRow2.y, 150, 24), $"(默认: {newExpressionName.ToLower()}_eyes)");
                GUI.color = Color.white;
            }
            list.Gap(2);
            
            // 第三行：嘴巴纹理
            Rect addRow3 = list.GetRect(24);
            GUI.color = new Color(0.9f, 0.6f, 0.6f); // 红色
            Widgets.Label(new Rect(addRow3.x, addRow3.y, 70, 24), "嘴巴:");
            GUI.color = Color.white;
            newExpressionMouthTexture = Widgets.TextField(new Rect(addRow3.x + 75, addRow3.y, 200, 24), newExpressionMouthTexture);
            // 自动填充提示
            if (string.IsNullOrEmpty(newExpressionMouthTexture) && !string.IsNullOrEmpty(newExpressionName))
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(new Rect(addRow3.x + 280, addRow3.y, 150, 24), $"(默认: {newExpressionName.ToLower()}_mouth)");
                GUI.color = Color.white;
            }
            list.Gap(4);
            
            // 添加按钮
            Rect addBtnRow = list.GetRect(26);
            if (Widgets.ButtonText(new Rect(addBtnRow.x + 75, addBtnRow.y, 120, 26), "添加表情"))
            {
                if (!string.IsNullOrWhiteSpace(newExpressionName))
                {
                    string exprName = newExpressionName.Trim();
                    string eyesTex = string.IsNullOrWhiteSpace(newExpressionEyesTexture) 
                        ? $"{exprName.ToLower()}_eyes" 
                        : newExpressionEyesTexture.Trim();
                    string mouthTex = string.IsNullOrWhiteSpace(newExpressionMouthTexture) 
                        ? $"{exprName.ToLower()}_mouth" 
                        : newExpressionMouthTexture.Trim();
                    
                    EnsureExpressionMappingByNameWithTextures(exprName, eyesTex, mouthTex);
                    
                    // 清空输入框
                    newExpressionName = "";
                    newExpressionEyesTexture = "";
                    newExpressionMouthTexture = "";
                }
            }
            list.Gap(8);
            
            // ⭐ v2.6.0: 紧凑的表情配置表格
            if (currentDef.expressions == null)
            {
                currentDef.expressions = new List<ExpressionMapping>();
            }
            
            // ⭐ v2.6.2: 优化表头布局，使眼睛和嘴巴纹理配置更明显
            Rect headerRect = list.GetRect(22);
            float col0 = 0;      // 表情类型
            float col1 = 85;     // 变体数
            float col2 = 120;    // 眼睛纹理
            float col3 = 280;    // 嘴巴纹理
            float col4 = 440;    // 操作
            
            // 绘制表头背景
            Widgets.DrawBoxSolid(headerRect, new Color(0.15f, 0.15f, 0.15f));
            
            GUI.color = new Color(0.9f, 0.9f, 0.5f); // 黄色高亮表头
            Widgets.Label(new Rect(headerRect.x + col0, headerRect.y + 2, 80, 20), "表情类型");
            Widgets.Label(new Rect(headerRect.x + col1, headerRect.y + 2, 30, 20), "变体");
            GUI.color = new Color(0.5f, 0.9f, 0.9f); // 青色高亮眼睛
            Widgets.Label(new Rect(headerRect.x + col2, headerRect.y + 2, 155, 20), "眼睛纹理 (eyes)");
            GUI.color = new Color(0.9f, 0.6f, 0.6f); // 红色高亮嘴巴
            Widgets.Label(new Rect(headerRect.x + col3, headerRect.y + 2, 155, 20), "嘴巴纹理 (mouth)");
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(headerRect.x + col4, headerRect.y + 2, 60, 20), "操作");
            GUI.color = Color.white;
            
            list.Gap(2);
            
            // ⭐ v2.6.4: 先处理上一帧的待处理操作
            ProcessPendingOperations();
            
            // ⭐ v2.6.1: 性能优化 - 使用 for 循环避免每帧创建新列表
            // 使用索引标记需要删除的项，在循环结束后统一删除
            int indexToRemove = -1;
            for (int i = 0; i < currentDef.expressions.Count; i++)
            {
                if (DrawCompactExpressionRow(list, currentDef.expressions[i], col0, col1, col2, col3, col4, i))
                {
                    indexToRemove = i;
                }
            }
            // 延迟删除，避免在遍历时修改集合
            if (indexToRemove >= 0)
            {
                currentDef.expressions.RemoveAt(indexToRemove);
                InvalidateExpressionCache();
                MarkDirty();
            }
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 确保指定表情名称有映射配置（基于字符串）
        /// </summary>
        private void EnsureExpressionMappingByName(string exprName)
        {
            EnsureExpressionMappingByNameWithTextures(exprName, $"{exprName.ToLower()}_eyes", $"{exprName.ToLower()}_mouth");
        }
        
        /// <summary>
        /// ⭐ v2.6.3: 确保指定表情名称有映射配置（含自定义纹理）
        /// </summary>
        private void EnsureExpressionMappingByNameWithTextures(string exprName, string eyesTexture, string mouthTexture)
        {
            if (currentDef.expressions == null)
            {
                currentDef.expressions = new List<ExpressionMapping>();
            }
            
            if (!currentDef.expressions.Any(m => m.expression == exprName))
            {
                var newMapping = new ExpressionMapping
                {
                    expression = exprName,
                    variants = new List<RenderTreeExpressionVariant>
                    {
                        new RenderTreeExpressionVariant { variant = 0, eyesTexture = eyesTexture, textureName = mouthTexture }
                    }
                };
                currentDef.expressions.Add(newMapping);
                InvalidateExpressionCache();
                MarkDirty();
            }
        }
        
        // ⭐ v2.6.4: 用于存储待删除的变体索引，避免在遍历时修改集合
        private int pendingVariantRemoveExpressionIndex = -1;
        private int pendingVariantRemoveIndex = -1;
        
        /// <summary>
        /// ⭐ v2.6.0: 绘制紧凑的表情配置行（基于字符串）
        /// ⭐ v2.6.1: 返回 true 表示需要删除此表情
        /// ⭐ v2.6.4: 修复集合修改异常，使用延迟删除
        /// </summary>
        private bool DrawCompactExpressionRow(Listing_Standard list, ExpressionMapping mapping, float col0, float col1, float col2, float col3, float col4, int expressionIndex)
        {
            bool shouldDelete = false;
            
            // 计算此表情需要的行数
            int variantCount = mapping.variants?.Count ?? 0;
            if (variantCount == 0) variantCount = 1;
            
            const float rowHeight = 22f;
            const float rowSpacing = 1f;
            
            // 绘制表情类型标签（跨所有变体行）
            Rect firstRowRect = list.GetRect(rowHeight);
            
            // 高亮当前预览的表情
            if (previewExpressionName == mapping.expression)
            {
                Widgets.DrawHighlight(new Rect(firstRowRect.x, firstRowRect.y, list.ColumnWidth, rowHeight * variantCount + rowSpacing * (variantCount - 1)));
            }
            
            // 表情类型名称（可点击预览）
            Rect exprRect = new Rect(firstRowRect.x + col0, firstRowRect.y, 85, rowHeight);
            if (Widgets.ButtonText(exprRect, mapping.expression ?? "?", false, true, true))
            {
                previewExpressionName = mapping.expression ?? "Neutral";
                previewVariant = 0;
                MarkDirty();
            }
            
            // 变体数量显示
            Widgets.Label(new Rect(firstRowRect.x + col1, firstRowRect.y, 35, rowHeight), $"x{variantCount}");
            
            // 第一个变体的配置
            if (mapping.variants != null && mapping.variants.Count > 0)
            {
                var firstVariant = mapping.variants[0];
                DrawVariantFieldsV2(firstRowRect, firstVariant, col2, col3);
            }
            
            // 添加变体按钮 - 标记为待添加，不直接修改
            if (Widgets.ButtonText(new Rect(firstRowRect.x + col4, firstRowRect.y, 25, rowHeight), "+"))
            {
                // 使用延迟添加，在下一帧处理
                pendingAddVariantExpressionIndex = expressionIndex;
            }
            
            // 删除表情按钮 - 标记为需要删除，不直接删除
            if (Widgets.ButtonText(new Rect(firstRowRect.x + col4 + 28, firstRowRect.y, 25, rowHeight), "X"))
            {
                shouldDelete = true;
            }
            
            // 绘制额外的变体行（只读显示，不在遍历中修改）
            if (mapping.variants != null && mapping.variants.Count > 1)
            {
                for (int i = 1; i < mapping.variants.Count; i++)
                {
                    list.Gap(rowSpacing);
                    Rect variantRow = list.GetRect(rowHeight);
                    var variant = mapping.variants[i];
                    
                    // 变体编号
                    Widgets.Label(new Rect(variantRow.x + col1, variantRow.y, 35, rowHeight), $"#{variant.variant}");
                    
                    // 变体字段
                    DrawVariantFieldsV2(variantRow, variant, col2, col3);
                    
                    // 删除变体按钮 - 标记为待删除
                    if (Widgets.ButtonText(new Rect(variantRow.x + col4, variantRow.y, 25, rowHeight), "-"))
                    {
                        pendingVariantRemoveExpressionIndex = expressionIndex;
                        pendingVariantRemoveIndex = i;
                    }
                    
                    // 预览变体按钮
                    if (Widgets.ButtonText(new Rect(variantRow.x + col4 + 28, variantRow.y, 25, rowHeight), ">"))
                    {
                        previewExpressionName = mapping.expression ?? "Neutral";
                        previewVariant = variant.variant;
                        MarkDirty();
                    }
                }
            }
            
            list.Gap(3);
            return shouldDelete;
        }
        
        // ⭐ v2.6.4: 用于存储待添加变体的表情索引
        private int pendingAddVariantExpressionIndex = -1;
        
        /// <summary>
        /// ⭐ v2.6.4: 处理待处理的添加/删除操作，避免在遍历时修改集合
        /// ⭐ v2.6.5: 修复闪烁问题 - 只在有实际操作时才处理
        /// </summary>
        private void ProcessPendingOperations()
        {
            // 如果没有待处理的操作，直接返回
            if (pendingAddVariantExpressionIndex < 0 && 
                pendingVariantRemoveExpressionIndex < 0)
            {
                return;
            }
            
            // 处理待添加的变体
            if (pendingAddVariantExpressionIndex >= 0 && 
                currentDef?.expressions != null && 
                pendingAddVariantExpressionIndex < currentDef.expressions.Count)
            {
                var mapping = currentDef.expressions[pendingAddVariantExpressionIndex];
                int nextVariant = (mapping.variants?.Count ?? 0);
                if (mapping.variants == null) mapping.variants = new List<RenderTreeExpressionVariant>();
                mapping.variants.Add(new RenderTreeExpressionVariant
                {
                    variant = nextVariant,
                    eyesTexture = $"{(mapping.expression ?? "expr").ToLower()}{nextVariant}_eyes",
                    textureName = $"{(mapping.expression ?? "expr").ToLower()}{nextVariant}_mouth"
                });
                InvalidateExpressionCache();
                MarkDirty();
                pendingAddVariantExpressionIndex = -1;
            }
            
            // 处理待删除的变体
            if (pendingVariantRemoveExpressionIndex >= 0 && 
                pendingVariantRemoveIndex >= 0 &&
                currentDef?.expressions != null && 
                pendingVariantRemoveExpressionIndex < currentDef.expressions.Count)
            {
                var mapping = currentDef.expressions[pendingVariantRemoveExpressionIndex];
                if (mapping.variants != null && pendingVariantRemoveIndex < mapping.variants.Count)
                {
                    mapping.variants.RemoveAt(pendingVariantRemoveIndex);
                    MarkDirty();
                }
                pendingVariantRemoveExpressionIndex = -1;
                pendingVariantRemoveIndex = -1;
            }
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 绘制变体的眼睛和嘴巴纹理字段（使用 RenderTreeExpressionVariant）
        /// </summary>
        private void DrawVariantFieldsV2(Rect rowRect, RenderTreeExpressionVariant variant, float col2, float col3)
        {
            // 眼睛纹理
            string newEyes = Widgets.TextField(new Rect(rowRect.x + col2, rowRect.y, 155, 20), variant.eyesTexture ?? "");
            if (newEyes != (variant.eyesTexture ?? ""))
            {
                variant.eyesTexture = newEyes;
                MarkDirty();
            }
            
            // 嘴巴纹理
            string newMouth = Widgets.TextField(new Rect(rowRect.x + col3, rowRect.y, 155, 20), variant.textureName ?? "");
            if (newMouth != (variant.textureName ?? ""))
            {
                variant.textureName = newMouth;
                MarkDirty();
            }
        }
        
        private void DrawSpeakingConfig(Listing_Standard list)
        {
            list.Label("<b>口型映射 (Viseme Mappings)</b>");
            list.Gap();
            
            if (currentDef.speaking?.visemeMap != null)
            {
                foreach (var viseme in currentDef.speaking.visemeMap)
                {
                    Rect r = list.GetRect(24);
                    Widgets.Label(new Rect(r.x, r.y, 100, 24), viseme.viseme);
                    viseme.textureName = Widgets.TextField(new Rect(r.x + 110, r.y, 300, 24), viseme.textureName);
                    list.Gap(4);
                }
            }
        }

        private void DrawBodyConfig(Listing_Standard list)
        {
            list.Label("<b>服装与姿态映射 (Clothing & Posture) - 替换 base_body</b>");
            list.Gap();

            if (list.ButtonText("添加新映射"))
            {
                currentDef.bodyMappings.Add(new BodyMapping());
            }
            list.Gap();

            if (currentDef.bodyMappings != null)
            {
                // 表头
                Rect header = list.GetRect(24);
                float w1 = 100; // Posture
                float w2 = 150; // Apparel
                float w3 = 200; // Texture
                float x = header.x;
                
                Widgets.Label(new Rect(x, header.y, w1, 24), "姿态"); x += w1 + 10;
                Widgets.Label(new Rect(x, header.y, w2, 24), "服装标签"); x += w2 + 10;
                Widgets.Label(new Rect(x, header.y, w3, 24), "纹理名称"); x += w3 + 10;
                
                for (int i = 0; i < currentDef.bodyMappings.Count; i++)
                {
                    var mapping = currentDef.bodyMappings[i];
                    Rect r = list.GetRect(24);
                    x = r.x;
                    
                    mapping.posture = Widgets.TextField(new Rect(x, r.y, w1, 24), mapping.posture); x += w1 + 10;
                    mapping.apparelTag = Widgets.TextField(new Rect(x, r.y, w2, 24), mapping.apparelTag); x += w2 + 10;
                    mapping.textureName = Widgets.TextField(new Rect(x, r.y, w3, 24), mapping.textureName); x += w3 + 10;
                    
                    if (Widgets.ButtonText(new Rect(x, r.y, 30, 24), "X"))
                    {
                        currentDef.bodyMappings.RemoveAt(i);
                        i--;
                    }
                    
                    list.Gap(4);
                }
            }
        }

        private void DrawAccessoriesConfig(Listing_Standard list)
        {
            list.Label("<b>配件与特效 (Accessories & Effects) - 覆盖最上层</b>");
            list.Gap();

            if (list.ButtonText("添加新配件"))
            {
                currentDef.accessories.Add(new AccessoryMapping());
            }
            list.Gap();

            if (currentDef.accessories != null)
            {
                // 表头
                Rect header = list.GetRect(24);
                float w1 = 100; // Name
                float w2 = 200; // Texture
                float w3 = 100; // Condition
                float w4 = 60;  // Z
                float x = header.x;
                
                Widgets.Label(new Rect(x, header.y, w1, 24), "名称"); x += w1 + 10;
                Widgets.Label(new Rect(x, header.y, w2, 24), "纹理名称"); x += w2 + 10;
                Widgets.Label(new Rect(x, header.y, w3, 24), "条件"); x += w3 + 10;
                Widgets.Label(new Rect(x, header.y, w4, 24), "Z偏移"); x += w4 + 10;

                for (int i = 0; i < currentDef.accessories.Count; i++)
                {
                    var acc = currentDef.accessories[i];
                    Rect r = list.GetRect(24);
                    x = r.x;
                    
                    acc.name = Widgets.TextField(new Rect(x, r.y, w1, 24), acc.name); x += w1 + 10;
                    acc.textureName = Widgets.TextField(new Rect(x, r.y, w2, 24), acc.textureName); x += w2 + 10;
                    acc.condition = Widgets.TextField(new Rect(x, r.y, w3, 24), acc.condition); x += w3 + 10;
                    
                    string zStr = Widgets.TextField(new Rect(x, r.y, w4, 24), acc.zOffset.ToString());
                    if (float.TryParse(zStr, out float z)) acc.zOffset = z;
                    x += w4 + 10;
                    
                    if (Widgets.ButtonText(new Rect(x, r.y, 30, 24), "X"))
                    {
                        currentDef.accessories.RemoveAt(i);
                        i--;
                    }
                    
                    list.Gap(4);
                }
            }
        }

        private void DrawHeadPatConfig(Listing_Standard list)
        {
            list.Label("<b>摸头动画配置 (Head Pat Animation)</b>");
            list.Gap();

            if (currentDef.headPat == null) currentDef.headPat = new HeadPatConfig();

            list.CheckboxLabeled("启用递进动画 (Enable Progressive)", ref currentDef.headPat.enabled);
            list.Gap();

            if (list.ButtonText("添加阶段 (Add Phase)"))
            {
                float nextThreshold = currentDef.headPat.phases.Count * 2f;
                currentDef.headPat.phases.Add(new HeadPatPhase { durationThreshold = nextThreshold });
                currentDef.headPat.phases.Sort((a, b) => a.durationThreshold.CompareTo(b.durationThreshold));
            }
            list.Gap();

            if (currentDef.headPat.phases != null)
            {
                // 表头
                Rect header = list.GetRect(24);
                float w1 = 70;  // Threshold
                float w2 = 150; // Texture
                float w3 = 100; // Expression
                float w4 = 50;  // Variant
                float w5 = 100; // Sound
                float x = header.x;
                
                Widgets.Label(new Rect(x, header.y, w1, 24), "阈值(秒)"); x += w1 + 10;
                Widgets.Label(new Rect(x, header.y, w2, 24), "手部纹理"); x += w2 + 10;
                Widgets.Label(new Rect(x, header.y, w3, 24), "表情"); x += w3 + 10;
                Widgets.Label(new Rect(x, header.y, w4, 24), "变体"); x += w4 + 10;
                Widgets.Label(new Rect(x, header.y, w5, 24), "音效"); x += w5 + 10;

                for (int i = 0; i < currentDef.headPat.phases.Count; i++)
                {
                    var phase = currentDef.headPat.phases[i];
                    Rect r = list.GetRect(24);
                    x = r.x;
                    
                    string threshStr = Widgets.TextField(new Rect(x, r.y, w1, 24), phase.durationThreshold.ToString("F1"));
                    if (float.TryParse(threshStr, out float t)) phase.durationThreshold = t;
                    x += w1 + 10;
                    
                    phase.textureName = Widgets.TextField(new Rect(x, r.y, w2, 24), phase.textureName); x += w2 + 10;
                    phase.expression = Widgets.TextField(new Rect(x, r.y, w3, 24), phase.expression); x += w3 + 10;
                    
                    // 变体编号输入框 (0=无变体, 1-5=使用指定变体)
                    string variantStr = Widgets.TextField(new Rect(x, r.y, w4, 24), phase.variant.ToString());
                    if (int.TryParse(variantStr, out int v)) phase.variant = v;
                    x += w4 + 10;
                    
                    phase.sound = Widgets.TextField(new Rect(x, r.y, w5, 24), phase.sound); x += w5 + 10;
                    
                    if (Widgets.ButtonText(new Rect(x, r.y, 30, 24), "X"))
                    {
                        currentDef.headPat.phases.RemoveAt(i);
                        i--;
                    }
                    
                    list.Gap(4);
                }
            }
        }

        private void DrawLipSyncConfig(Listing_Standard list)
        {
            list.Label($"<b>{"TSS_RenderTree_LipSync_ConfigTitle".Translate()}</b>");
            list.Gap();

            if (currentLipSyncDef == null)
            {
                list.Label("TSS_RenderTree_LipSync_NoDefLoaded".Translate());
                return;
            }

            // Global Settings
            list.Label("TSS_RenderTree_LipSync_GlobalSettings".Translate());
            
            // Attack/Release
            if (list.ButtonText("TSS_RenderTree_LipSync_AttackViseme".Translate(currentLipSyncDef.attackViseme)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (VisemeCode code in Enum.GetValues(typeof(VisemeCode)))
                {
                    options.Add(new FloatMenuOption(code.ToString(), () => currentLipSyncDef.attackViseme = code));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            if (list.ButtonText("TSS_RenderTree_LipSync_ReleaseViseme".Translate(currentLipSyncDef.releaseViseme)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (VisemeCode code in Enum.GetValues(typeof(VisemeCode)))
                {
                    options.Add(new FloatMenuOption(code.ToString(), () => currentLipSyncDef.releaseViseme = code));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            string sustainStr = currentLipSyncDef.sustainFrames.ToString();
            list.TextFieldNumericLabeled("TSS_RenderTree_LipSync_SustainFrames".Translate(), ref currentLipSyncDef.sustainFrames, ref sustainStr, 0, 60);

            // Default Viseme
            if (list.ButtonText("TSS_RenderTree_LipSync_DefaultViseme".Translate(currentLipSyncDef.defaultViseme)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (VisemeCode code in Enum.GetValues(typeof(VisemeCode)))
                {
                    options.Add(new FloatMenuOption(code.ToString(), () =>
                    {
                        currentLipSyncDef.defaultViseme = code;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            list.GapLine();

            // Mappings List
            list.Label("TSS_RenderTree_LipSync_MappingsList".Translate());
            
            if (list.ButtonText("TSS_RenderTree_LipSync_AddNewMapping".Translate()))
            {
                currentLipSyncDef.mappings.Add(new LipSyncMappingDef.GroupMapping());
            }
            
            list.Gap();

            for (int i = 0; i < currentLipSyncDef.mappings.Count; i++)
            {
                var mapping = currentLipSyncDef.mappings[i];
                Rect rect = list.GetRect(30f);
                
                // Group Dropdown
                if (Widgets.ButtonText(new Rect(rect.x, rect.y, 150, 24), mapping.group.ToString()))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (PhonemeGroup pg in Enum.GetValues(typeof(PhonemeGroup)))
                    {
                        options.Add(new FloatMenuOption(pg.ToString(), () =>
                        {
                            mapping.group = pg;
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }

                // Arrow
                Widgets.Label(new Rect(rect.x + 160, rect.y, 30, 24), "->");

                // Viseme Dropdown
                if (Widgets.ButtonText(new Rect(rect.x + 190, rect.y, 150, 24), mapping.viseme.ToString()))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (VisemeCode code in Enum.GetValues(typeof(VisemeCode)))
                    {
                        options.Add(new FloatMenuOption(code.ToString(), () =>
                        {
                            mapping.viseme = code;
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }

                // Remove Button
                if (Widgets.ButtonText(new Rect(rect.x + 350, rect.y, 30, 24), "X"))
                {
                    currentLipSyncDef.mappings.RemoveAt(i);
                    i--;
                }

                list.Gap(4f);
            }
        }
        private void DrawOutfitsConfig(Listing_Standard list)
        {
            list.Label($"<b>{"TSS_RenderTree_Tab_Outfits".Translate()}</b>");
            list.Gap();
            
            if (currentPersona == null)
            {
                list.Label("请先选择叙事者");
                return;
            }

            if (list.ButtonText("添加新服装"))
            {
                if (currentOutfits == null) currentOutfits = new List<OutfitDef>();
                var newOutfit = new OutfitDef
                {
                    defName = $"{currentPersona.defName}_Outfit_{DateTime.Now.Ticks}",
                    label = "New Outfit",
                    outfitTag = "Casual",
                    personaDefName = currentPersona.defName,
                    priority = 0
                };
                currentOutfits.Add(newOutfit);
            }
            list.Gap();

            if (currentOutfits == null || currentOutfits.Count == 0)
            {
                list.Label("无可用服装定义 (No OutfitDefs found for this persona)");
                return;
            }

            // Headers
            Rect header = list.GetRect(24);
            float wTag = 100;
            float wLabel = 150;
            float wPriority = 60;
            float wBody = 200;
            float wDesc = 200;
            float x = header.x;
            
            Widgets.Label(new Rect(x, header.y, wTag, 24), "Tag"); x += wTag + 5;
            Widgets.Label(new Rect(x, header.y, wLabel, 24), "Label"); x += wLabel + 5;
            Widgets.Label(new Rect(x, header.y, wPriority, 24), "Priority"); x += wPriority + 5;
            Widgets.Label(new Rect(x, header.y, wBody, 24), "Body Texture"); x += wBody + 5;
            Widgets.Label(new Rect(x, header.y, wDesc, 24), "Description"); x += wDesc + 5;

            for (int i = 0; i < currentOutfits.Count; i++)
            {
                var outfit = currentOutfits[i];
                Rect r = list.GetRect(24);
                x = r.x;

                outfit.outfitTag = Widgets.TextField(new Rect(x, r.y, wTag, 24), outfit.outfitTag); x += wTag + 5;
                outfit.label = Widgets.TextField(new Rect(x, r.y, wLabel, 24), outfit.label); x += wLabel + 5;
                
                string priStr = outfit.priority.ToString();
                Widgets.TextFieldNumeric(new Rect(x, r.y, wPriority, 24), ref outfit.priority, ref priStr); x += wPriority + 5;
                
                outfit.bodyTexture = Widgets.TextField(new Rect(x, r.y, wBody, 24), outfit.bodyTexture); x += wBody + 5;
                outfit.outfitDescription = Widgets.TextField(new Rect(x, r.y, wDesc, 24), outfit.outfitDescription); x += wDesc + 5;

                if (Widgets.ButtonText(new Rect(x, r.y, 30, 24), "X"))
                {
                    currentOutfits.RemoveAt(i);
                    i--;
                }
                list.Gap(4);
            }
        }

        private void MarkDirty(bool immediate = false)
        {
            isPreviewDirty = true;
            lastEditTime = immediate ? 0f : Time.realtimeSinceStartup;
        }
    }
}

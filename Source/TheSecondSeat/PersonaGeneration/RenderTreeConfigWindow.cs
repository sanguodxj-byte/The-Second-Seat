using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

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
        private Vector2 scrollPosition;
        
        // 预览相关
        private Texture2D previewTexture;
        private ExpressionType previewExpression = ExpressionType.Neutral;
        private int previewVariant = 0;
        private bool isPreviewDirty = true;
        
        public override Vector2 InitialSize => new Vector2(1000f, 750f);
        
        public RenderTreeConfigWindow()
        {
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.resizeable = true;
            this.draggable = true;
            
            // 默认选中第一个 Narrator
            currentPersona = DefDatabase<NarratorPersonaDef>.AllDefsListForReading.FirstOrDefault();
            if (currentPersona != null)
            {
                currentDef = RenderTreeDefManager.GetRenderTree(currentPersona.defName);
            }
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, 300, 30), "表情映射管理器");
            Text.Font = GameFont.Small;
            
            // 顶部工具栏
            float topY = 0f;
            float topHeight = 40f;
            
            // 选择 Persona
            if (Widgets.ButtonText(new Rect(320, topY, 200, 30), currentPersona?.narratorName ?? "选择叙事者"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var def in DefDatabase<NarratorPersonaDef>.AllDefsListForReading)
                {
                    options.Add(new FloatMenuOption(def.narratorName, () =>
                    {
                        currentPersona = def;
                        currentDef = RenderTreeDefManager.GetRenderTree(def.defName);
                        isPreviewDirty = true;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            // 保存按钮
            if (Widgets.ButtonText(new Rect(530, topY, 120, 30), "保存 XML"))
            {
                if (currentDef != null)
                {
                    string path = RenderTreeConfigIO.GetDefaultSavePath(currentDef.defName);
                    RenderTreeConfigIO.SaveToXml(currentDef, path);
                }
            }
            
            // 如果没有 Def，显示提示
            if (currentDef == null)
            {
                Widgets.Label(new Rect(0, 40, inRect.width, 30), "未找到有效的 RenderTreeDef");
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
            DrawConfigArea(configRect);
        }
        
        private void DrawPreviewArea(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            // 预览控制
            float y = rect.y + 10f;
            float contentWidth = rect.width - 20f;
            float x = rect.x + 10f;
            
            Widgets.Label(new Rect(x, y, contentWidth, 24f), "预览控制");
            y += 30f;
            
            // 选择表情
            if (Widgets.ButtonText(new Rect(x, y, contentWidth, 30f), $"表情: {previewExpression}"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (ExpressionType expr in Enum.GetValues(typeof(ExpressionType)))
                {
                    options.Add(new FloatMenuOption(expr.ToString(), () =>
                    {
                        previewExpression = expr;
                        // 重置变体
                        previewVariant = 0;
                        isPreviewDirty = true;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 35f;
            
            // 选择变体
            int maxVariants = currentDef.GetVariantCount(previewExpression);
            if (maxVariants > 0)
            {
                string variantLabel = previewVariant == 0 ? "基础 (Variant 0)" : $"变体 {previewVariant}";
                if (Widgets.ButtonText(new Rect(x, y, contentWidth, 30f), variantLabel))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("基础 (Variant 0)", () => { previewVariant = 0; isPreviewDirty = true; }));
                    
                    for (int i = 1; i <= maxVariants; i++)
                    {
                        int v = i;
                        options.Add(new FloatMenuOption($"变体 {v}", () => { previewVariant = v; isPreviewDirty = true; }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                y += 35f;
            }
            
            // 刷新按钮
            if (Widgets.ButtonText(new Rect(x, y, contentWidth, 30f), "刷新预览"))
            {
                isPreviewDirty = true;
            }
            y += 40f;
            
            // 预览图像
            float previewSize = contentWidth;
            Rect imageRect = new Rect(x, y, previewSize, previewSize * 1.5f); // 9:16 比例
            Widgets.DrawBoxSolid(imageRect, Color.black);
            
            if (isPreviewDirty)
            {
                UpdatePreview();
            }
            
            if (previewTexture != null)
            {
                GUI.DrawTexture(imageRect, previewTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(imageRect, "预览加载中...");
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }
        
        private void UpdatePreview()
        {
            if (currentPersona == null) return;
            
            var config = currentPersona.GetLayeredConfig();
            if (config == null) return;
            
            // 临时设置 ExpressionSystem 的状态以影响合成
            var state = ExpressionSystem.GetExpressionState(currentPersona.defName);
            var oldVariant = state.CurrentVariant;
            var oldIntensity = state.Intensity;
            
            // 设置预览状态
            state.CurrentVariant = previewVariant;
            state.Intensity = previewVariant; // 同时设置 Intensity 以覆盖两者
            
            // 强制重新合成
            // 注意：这里调用的是同步方法，为了避免异步带来的复杂性，在预览时暂时阻塞一下是可以接受的
            #pragma warning disable CS0618
            LayeredPortraitCompositor.ClearCache(currentPersona.defName, previewExpression);
            previewTexture = (Texture2D)LayeredPortraitCompositor.CompositeLayers(config, previewExpression);
            #pragma warning restore CS0618
            
            // 恢复状态
            state.CurrentVariant = oldVariant;
            state.Intensity = oldIntensity;
            
            isPreviewDirty = false;
        }
        
        private void DrawConfigArea(Rect rect)
        {
            Rect viewRect = new Rect(0, 0, rect.width - 16, 3000); // 增加高度以容纳更多内容
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            
            float y = 0;
            float width = viewRect.width;
            
            // === 表情映射 ===
            Widgets.Label(new Rect(0, y, width, 25), "<b>表情映射 (Expression Mappings)</b>");
            y += 30;
            
            if (currentDef.expressions != null && currentDef.expressions.Count > 0)
            {
                foreach (var mapping in currentDef.expressions)
                {
                    DrawExpressionMapping(mapping, ref y, width);
                    y += 10;
                    Widgets.DrawLineHorizontal(0, y, width);
                    y += 10;
                }
            }
            else
            {
                Widgets.Label(new Rect(0, y, width, 25), "<color=red>无表情配置数据 (Expressions list is empty)</color>");
                y += 30;
                if (Widgets.ButtonText(new Rect(0, y, 200, 30), "加载默认配置"))
                {
                    currentDef.expressions = RenderTreeDefManager.GetDefault().expressions;
                }
                y += 40;
            }
            
            y += 20;
            
            // === 口型映射 ===
            Widgets.Label(new Rect(0, y, width, 25), "<b>口型映射 (Viseme Mappings)</b>");
            y += 30;
            
            if (currentDef.speaking?.visemeMap != null)
            {
                foreach (var viseme in currentDef.speaking.visemeMap)
                {
                    Widgets.Label(new Rect(10, y, 100, 24), viseme.viseme);
                    viseme.textureName = Widgets.TextField(new Rect(120, y, 200, 24), viseme.textureName);
                    y += 26;
                }
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawExpressionMapping(ExpressionMapping mapping, ref float y, float width)
        {
            Widgets.Label(new Rect(0, y, 150, 24), $"表情: {mapping.expression}");
            
            // 基础配置
            Rect suffixRect = new Rect(160, y, 150, 24);
            Widgets.Label(new Rect(160, y - 20, 100, 20), "基础后缀:");
            string newSuffix = Widgets.TextField(suffixRect, mapping.suffix);
            if (newSuffix != mapping.suffix)
            {
                mapping.suffix = newSuffix;
                isPreviewDirty = true;
            }
            
            y += 30;
            
            // 变体配置
            if (mapping.variants != null)
            {
                Widgets.Label(new Rect(20, y, 100, 24), "变体配置:");
                y += 24;
                
                // 表头
                float col1 = 40;  // Label
                float col2 = 120; // Suffix
                float col3 = 280; // Eyes
                float col4 = 440; // Mouth
                
                Widgets.Label(new Rect(col2, y, 140, 20), "后缀 (Suffix)");
                Widgets.Label(new Rect(col3, y, 140, 20), "眼睛纹理 (Eyes)");
                Widgets.Label(new Rect(col4, y, 140, 20), "嘴巴纹理 (Mouth)");
                y += 20;
                
                foreach (var variant in mapping.variants)
                {
                    Rect r = new Rect(col1, y, width - col1, 24);
                    Widgets.Label(new Rect(r.x, r.y, 70, 24), $"变体 {variant.variant}:");
                    
                    // Suffix
                    string vSuffix = Widgets.TextField(new Rect(col2, r.y, 140, 24), variant.suffix);
                    if (vSuffix != variant.suffix)
                    {
                        variant.suffix = vSuffix;
                        isPreviewDirty = true;
                    }
                    
                    // Eyes Texture
                    string vEyes = Widgets.TextField(new Rect(col3, r.y, 140, 24), variant.eyesTexture ?? "");
                    if (vEyes != variant.eyesTexture)
                    {
                        variant.eyesTexture = vEyes;
                        isPreviewDirty = true;
                    }
                    
                    // Mouth Texture
                    string vMouth = Widgets.TextField(new Rect(col4, r.y, 140, 24), variant.textureName ?? "");
                    if (vMouth != variant.textureName)
                    {
                        variant.textureName = vMouth;
                        isPreviewDirty = true;
                    }
                    
                    y += 26;
                }
            }
        }
    }
}
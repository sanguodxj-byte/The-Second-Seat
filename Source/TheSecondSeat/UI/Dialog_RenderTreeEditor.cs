using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;
using RimWorld;

namespace TheSecondSeat.UI
{
    public class Dialog_RenderTreeEditor : Window
    {
        private Vector2 scrollPositionLeft = Vector2.zero;
        private Vector2 scrollPositionRight = Vector2.zero;
        private PersonaGeneration.RenderTreeDef selectedDef = null;
        
        // Cache available defs
        private List<PersonaGeneration.RenderTreeDef> allRenderTreeDefs = new List<PersonaGeneration.RenderTreeDef>();

        // Tab definitions
        private enum EditorTab { Expressions, Speaking }
        private EditorTab currentTab = EditorTab.Expressions;

        public override Vector2 InitialSize => new Vector2(1000f, 760f);

        public Dialog_RenderTreeEditor()
        {
            doCloseX = true;
            forcePause = true;
            resizeable = true;
            draggable = true;
            
            RefreshDefList();
        }

        private void RefreshDefList()
        {
            allRenderTreeDefs = new List<PersonaGeneration.RenderTreeDef>();
            
            if (DefDatabase<PersonaGeneration.RenderTreeDef>.DefCount > 0)
            {
                allRenderTreeDefs.AddRange(DefDatabase<PersonaGeneration.RenderTreeDef>.AllDefsListForReading);
            }
            
            var defaultDef = RenderTreeDefManager.GetDefault();
            if (defaultDef != null && !allRenderTreeDefs.Contains(defaultDef))
            {
                allRenderTreeDefs.Add(defaultDef);
            }
            
            foreach (var persona in DefDatabase<NarratorPersonaDef>.AllDefsListForReading)
            {
                if (persona.renderTreeDef != null && !allRenderTreeDefs.Contains(persona.renderTreeDef))
                {
                    allRenderTreeDefs.Add(persona.renderTreeDef);
                }
            }

            allRenderTreeDefs = allRenderTreeDefs.OrderBy(d => d.defName).ToList();

            if (selectedDef == null && allRenderTreeDefs.Count > 0)
            {
                selectedDef = allRenderTreeDefs[0];
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "渲染树编辑器 (Render Tree Editor)");
            Text.Font = GameFont.Small;

            float topMargin = 50f;
            float bottomMargin = 50f;
            float leftWidth = 220f;
            float gap = 10f;
            float rightWidth = inRect.width - leftWidth - gap;
            float height = inRect.height - topMargin - bottomMargin;

            Rect leftRect = new Rect(inRect.x, inRect.y + topMargin, leftWidth, height);
            Widgets.DrawMenuSection(leftRect);
            
            Rect viewRectLeft = new Rect(0, 0, leftWidth - 16f, allRenderTreeDefs.Count * 30f);
            Widgets.BeginScrollView(leftRect, ref scrollPositionLeft, viewRectLeft);
            
            float y = 0f;
            foreach (var def in allRenderTreeDefs)
            {
                Rect rowRect = new Rect(0, y, viewRectLeft.width, 30f);
                if (selectedDef == def)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                
                if (Widgets.ButtonText(rowRect, def.defName, true, false, true))
                {
                    selectedDef = def;
                }
                y += 30f;
            }
            Widgets.EndScrollView();

            Rect rightRect = new Rect(inRect.x + leftWidth + gap, inRect.y + topMargin, rightWidth, height);
            Widgets.DrawMenuSection(rightRect);

            if (selectedDef != null)
            {
                Rect innerRect = rightRect.ContractedBy(10f);
                
                List<TabRecord> tabs = new List<TabRecord>
                {
                    new TabRecord("表情映射 (Expressions)", () => currentTab = EditorTab.Expressions, currentTab == EditorTab.Expressions),
                    new TabRecord("说话配置 (Speaking)", () => currentTab = EditorTab.Speaking, currentTab == EditorTab.Speaking)
                };
                
                Rect tabRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 30f);
                TabDrawer.DrawTabs(tabRect, tabs);

                Rect contentRect = new Rect(innerRect.x, innerRect.y + 40f, innerRect.width, innerRect.height - 40f);
                
                if (currentTab == EditorTab.Expressions)
                {
                    DrawExpressionsTab(contentRect);
                }
                else
                {
                    DrawSpeakingTab(contentRect);
                }
            }

            float btnWidth = 120f;
            float btnHeight = 40f;
            float btnY = inRect.height - btnHeight;

            if (Widgets.ButtonText(new Rect(inRect.width - btnWidth, btnY, btnWidth, btnHeight), "关闭"))
            {
                Close();
            }
            
            if (Widgets.ButtonText(new Rect(inRect.width - btnWidth * 2 - 10f, btnY, btnWidth, btnHeight), "保存配置"))
            {
                if (selectedDef != null)
                {
                    string path = RenderTreeConfigIO.GetDefaultSavePath(selectedDef.defName);
                    RenderTreeConfigIO.SaveToXml(selectedDef, path);
                }
            }
        }

        private void DrawExpressionsTab(Rect rect)
        {
            if (selectedDef.expressions == null) selectedDef.expressions = new List<ExpressionMapping>();

            Rect viewRect = new Rect(0, 0, rect.width - 16f, CalculateExpressionsHeight());
            Widgets.BeginScrollView(rect, ref scrollPositionRight, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            if (listing.ButtonText("添加新表情映射 (+ Add Expression)"))
            {
                selectedDef.expressions.Add(new ExpressionMapping { expression = "NewExpression" });
            }
            
            if (listing.ButtonText("从默认配置初始化表情 (Initialize from Defaults)"))
            {
                InitializeExpressionsFromDefault();
            }

            if (listing.ButtonText("扫描纹理文件夹 (Scan Textures)"))
            {
                ScanTexturesForExpressions();
            }
            
            listing.Gap();

            for (int i = 0; i < selectedDef.expressions.Count; i++)
            {
                var mapping = selectedDef.expressions[i];
                DrawExpressionMapping(listing, mapping, i);
                listing.GapLine();
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private float CalculateExpressionsHeight()
        {
            if (selectedDef?.expressions == null) return 100f;

            // Base height for top buttons
            float h = 120f;

            // Add height for each expression mapping
            foreach (var mapping in selectedDef.expressions)
            {
                // Estimated height for the mapping item
                h += 160f;
                
                // Add additional height for each variant
                if (mapping.variants != null)
                {
                    h += mapping.variants.Count * 30f;
                }
            }
            return h;
        }

        private void DrawExpressionMapping(Listing_Standard listing, ExpressionMapping mapping, int index)
        {
            Rect nameRect = listing.GetRect(24f);
            Widgets.Label(nameRect.LeftHalf(), "表情类型 (Expression):");
            mapping.expression = Widgets.TextField(nameRect.RightHalf(), mapping.expression);

            Rect suffixRect = listing.GetRect(24f);
            Widgets.Label(suffixRect.LeftHalf(), "基础后缀 (Base Suffix):");
            mapping.suffix = Widgets.TextField(suffixRect.RightHalf(), mapping.suffix);

            listing.Gap(4f);
            listing.Label("变体 (Variants):");
            
            if (mapping.variants == null) mapping.variants = new List<RenderTreeExpressionVariant>();

            for (int v = 0; v < mapping.variants.Count; v++)
            {
                var variant = mapping.variants[v];
                Rect varRect = listing.GetRect(24f);
                
                // Adjusted widths for a more compact layout
                float idW = 35f;
                float removeW = 24f;
                float gap = 4f;
                float fieldW = (varRect.width - idW - removeW - (gap * 3)) / 3f;

                Rect idRect = new Rect(varRect.x, varRect.y, idW, varRect.height);
                Rect suffRect = new Rect(idRect.xMax + gap, varRect.y, fieldW, varRect.height);
                Rect eyeRect = new Rect(suffRect.xMax + gap, varRect.y, fieldW, varRect.height);
                Rect mouthRect = new Rect(eyeRect.xMax + gap, varRect.y, fieldW, varRect.height);
                Rect delRect = new Rect(mouthRect.xMax + gap, varRect.y, removeW, varRect.height);

                string idBuffer = variant.variant.ToString();
                Widgets.TextFieldNumeric(idRect, ref variant.variant, ref idBuffer);

                DrawTextFieldWithResourceSelector(suffRect, variant.suffix, newVal => variant.suffix = newVal);
                TooltipHandler.TipRegion(suffRect, "变体后缀 (Variant Suffix)");

                DrawTextFieldWithResourceSelector(eyeRect, variant.eyesTexture, newVal => variant.eyesTexture = newVal);
                TooltipHandler.TipRegion(eyeRect, "眼睛纹理 (Eyes Texture)\nRight-click for options.");

                DrawTextFieldWithResourceSelector(mouthRect, variant.textureName, newVal => variant.textureName = newVal);
                TooltipHandler.TipRegion(mouthRect, "嘴巴纹理 (Mouth Texture)\nRight-click for options.");

                if (Widgets.ButtonImage(delRect, TexButton.Delete))
                {
                    mapping.variants.RemoveAt(v);
                    v--;
                }
            }

            if (listing.ButtonText("添加变体 (+ Variant)"))
            {
                int nextId = (mapping.variants.Count > 0) ? mapping.variants.Max(x => x.variant) + 1 : 1;
                mapping.variants.Add(new RenderTreeExpressionVariant { variant = nextId, suffix = $"{mapping.suffix}{nextId}" });
            }

            listing.Gap(4f);
            if (listing.ButtonText("删除此表情映射 (Remove Expression)"))
            {
                selectedDef.expressions.RemoveAt(index);
            }
        }

        private void InitializeExpressionsFromDefault()
        {
            var defaultDef = RenderTreeDefManager.GetDefault();
            if (defaultDef?.expressions == null) return;
            
            HashSet<string> availableTextures = ScanAllAvailableTextures();
            
            selectedDef.expressions.Clear();
            foreach (var expr in defaultDef.expressions)
            {
                var newExpr = new ExpressionMapping
                {
                    expression = expr.expression,
                    suffix = expr.suffix,
                    variants = new List<RenderTreeExpressionVariant>()
                };
                
                if (expr.variants != null)
                {
                    foreach (var v in expr.variants)
                    {
                        var newVariant = new RenderTreeExpressionVariant
                        {
                            variant = v.variant,
                            suffix = v.suffix
                        };

                        string baseSuffix = v.suffix?.TrimStart('_') ?? "";
                        string exprName = expr.expression.ToLower();

                        // Auto-fill textures
                        newVariant.eyesTexture = FindBestTextureMatch(availableTextures, baseSuffix, "eyes")
                                              ?? FindBestTextureMatch(availableTextures, exprName, "eyes")
                                              ?? v.eyesTexture;

                        newVariant.textureName = FindBestTextureMatch(availableTextures, baseSuffix, "mouth")
                                               ?? FindBestTextureMatch(availableTextures, exprName, "mouth")
                                               ?? v.textureName;

                        newExpr.variants.Add(newVariant);
                    }
                }
                
                selectedDef.expressions.Add(newExpr);
            }
        }

        private HashSet<string> ScanAllAvailableTextures()
        {
            HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string personaName = selectedDef.defName.Replace("_RenderTree", "");
            
            try
            {
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    string[] checkPaths = { "Textures", "Common/Textures", "1.5/Textures" };
                    foreach(var texRoot in checkPaths)
                    {
                        string rootPath = System.IO.Path.Combine(mod.RootDir, texRoot);
                        if (!System.IO.Directory.Exists(rootPath)) continue;

                        string targetPath = System.IO.Path.Combine(rootPath, personaName, "Narrators", "Layered");
                        ScanTexturesInDir(targetPath, found);
                        
                        targetPath = System.IO.Path.Combine(rootPath, "UI", "Narrators", "Layered");
                        ScanTexturesInDir(targetPath, found);
                        
                        targetPath = System.IO.Path.Combine(rootPath, personaName, "Narrators", "Expressions");
                        ScanTexturesInDir(targetPath, found);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Error scanning textures: {ex.Message}");
            }
            return found;
        }

        private void ScanTexturesInDir(string path, HashSet<string> found)
        {
            if (System.IO.Directory.Exists(path))
            {
                var files = System.IO.Directory.GetFiles(path, "*.png", System.IO.SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    found.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                }
            }
        }

        private void ScanTexturesForExpressions()
        {
             string personaName = selectedDef.defName.Replace("_RenderTree", "");
             Messages.Message($"已尝试扫描纹理 (针对 {personaName}) - 请手动检查路径", MessageTypeDefOf.NeutralEvent, false);
        }

        private void InitializeSpeakingFromDefault()
        {
            var defaultDef = RenderTreeDefManager.GetDefault();
            if (defaultDef?.speaking == null) return;
            
            selectedDef.speaking.visemeMap = new List<VisemeTextureMapping>();
            if (defaultDef.speaking.visemeMap != null)
            {
                foreach (var m in defaultDef.speaking.visemeMap)
                {
                    selectedDef.speaking.visemeMap.Add(new VisemeTextureMapping
                    {
                        viseme = m.viseme,
                        textureName = m.textureName
                    });
                }
            }
            
            selectedDef.speaking.opennessThresholds = new List<OpennessThreshold>();
            if (defaultDef.speaking.opennessThresholds != null)
            {
                foreach (var t in defaultDef.speaking.opennessThresholds)
                {
                    selectedDef.speaking.opennessThresholds.Add(new OpennessThreshold
                    {
                        threshold = t.threshold,
                        viseme = t.viseme
                    });
                }
            }
            
            selectedDef.speaking.azureVisemeMap = new List<AzureVisemeMapping>();
            if (defaultDef.speaking.azureVisemeMap != null)
            {
                foreach (var m in defaultDef.speaking.azureVisemeMap)
                {
                    selectedDef.speaking.azureVisemeMap.Add(new AzureVisemeMapping
                    {
                        azureId = m.azureId,
                        textureName = m.textureName
                    });
                }
            }
        }

        private void DrawSpeakingTab(Rect rect)
        {
            if (selectedDef.speaking == null) selectedDef.speaking = new SpeakingConfig();

            Rect viewRect = new Rect(0, 0, rect.width - 16f, 1200f);
            Widgets.BeginScrollView(rect, ref scrollPositionRight, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            if (listing.ButtonText("从默认配置初始化所有说话设置 (Initialize All Speaking Config)"))
            {
                InitializeSpeakingFromDefault();
            }
            listing.GapLine();

            listing.Label("<b>口型映射 (Viseme Map)</b>");
            listing.Gap(4f);
            
            if (selectedDef.speaking.visemeMap == null) selectedDef.speaking.visemeMap = new List<VisemeTextureMapping>();

            for (int i = 0; i < selectedDef.speaking.visemeMap.Count; i++)
            {
                var map = selectedDef.speaking.visemeMap[i];
                Rect r = listing.GetRect(24f);
                
                Rect labelRect = new Rect(r.x, r.y, 100f, r.height);
                Rect valRect = new Rect(labelRect.xMax, r.y, r.width - 130f, r.height);
                Rect delRect = new Rect(valRect.xMax + 5f, r.y, 25f, r.height);

                map.viseme = Widgets.TextField(labelRect, map.viseme);
                map.textureName = Widgets.TextField(valRect, map.textureName);
                
                if (Widgets.ButtonImage(delRect, TexButton.Delete))
                {
                    selectedDef.speaking.visemeMap.RemoveAt(i);
                    i--;
                }
            }
            
            if (listing.ButtonText("添加口型映射 (+ Viseme Map)"))
            {
                selectedDef.speaking.visemeMap.Add(new VisemeTextureMapping { viseme = "NewViseme", textureName = "TextureName" });
            }

            listing.GapLine();

            listing.Label("<b>开合度阈值 (Openness Thresholds)</b>");
            listing.Gap(4f);

            if (selectedDef.speaking.opennessThresholds == null) selectedDef.speaking.opennessThresholds = new List<OpennessThreshold>();

            for (int i = 0; i < selectedDef.speaking.opennessThresholds.Count; i++)
            {
                var th = selectedDef.speaking.opennessThresholds[i];
                Rect r = listing.GetRect(24f);

                Rect valRect = new Rect(r.x, r.y, 80f, r.height);
                Rect labelRect = new Rect(valRect.xMax + 10f, r.y, 100f, r.height);
                Rect delRect = new Rect(labelRect.xMax + 5f, r.y, 25f, r.height);

                string threshStr = Widgets.TextField(valRect, th.threshold.ToString("F2"));
                if (float.TryParse(threshStr, out float newTh)) th.threshold = newTh;
                
                th.viseme = Widgets.TextField(labelRect, th.viseme);

                if (Widgets.ButtonImage(delRect, TexButton.Delete))
                {
                    selectedDef.speaking.opennessThresholds.RemoveAt(i);
                    i--;
                }
            }

            if (listing.ButtonText("添加阈值 (+ Threshold)"))
            {
                selectedDef.speaking.opennessThresholds.Add(new OpennessThreshold { threshold = 0.5f, viseme = "Medium" });
            }

            listing.GapLine();

            listing.Label("<b>Azure Viseme ID 映射</b>");
            listing.Gap(4f);

            if (selectedDef.speaking.azureVisemeMap == null) selectedDef.speaking.azureVisemeMap = new List<AzureVisemeMapping>();

            for (int i = 0; i < selectedDef.speaking.azureVisemeMap.Count; i++)
            {
                var map = selectedDef.speaking.azureVisemeMap[i];
                Rect r = listing.GetRect(24f);

                Rect idRect = new Rect(r.x, r.y, 50f, r.height);
                Rect valRect = new Rect(idRect.xMax + 10f, r.y, r.width - 90f, r.height);
                Rect delRect = new Rect(valRect.xMax + 5f, r.y, 25f, r.height);

                string idStr = Widgets.TextField(idRect, map.azureId.ToString());
                if (int.TryParse(idStr, out int newId)) map.azureId = newId;

                map.textureName = Widgets.TextField(valRect, map.textureName);

                if (Widgets.ButtonImage(delRect, TexButton.Delete))
                {
                    selectedDef.speaking.azureVisemeMap.RemoveAt(i);
                    i--;
                }
            }

            if (listing.ButtonText("添加 Azure 映射 (+ Azure Map)"))
            {
                selectedDef.speaking.azureVisemeMap.Add(new AzureVisemeMapping { azureId = 0, textureName = "Closed_mouth" });
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private string FindBestTextureMatch(HashSet<string> availableTextures, string searchTerm, string type)
        {
            if (string.IsNullOrEmpty(searchTerm) || availableTextures == null || availableTextures.Count == 0)
                return null;

            searchTerm = searchTerm.ToLower();
            type = type?.ToLower() ?? "";

            // Try exact match first
            foreach (var tex in availableTextures)
            {
                if (tex.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                    return tex;
            }

            // Try match with type suffix
            foreach (var tex in availableTextures)
            {
                string lower = tex.ToLower();
                if (lower.Contains(searchTerm) && lower.Contains(type))
                    return tex;
            }

            // Try partial match
            foreach (var tex in availableTextures)
            {
                if (tex.ToLower().Contains(searchTerm))
                    return tex;
            }

            return null;
        }

        private void DrawTextFieldWithResourceSelector(Rect rect, string currentValue, Action<string> onValueChanged)
        {
            string newValue = Widgets.TextField(rect, currentValue ?? "");
            if (newValue != currentValue)
            {
                onValueChanged?.Invoke(newValue);
            }

            // Right-click for texture selection popup
            if (Mouse.IsOver(rect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                HashSet<string> textures = ScanAllAvailableTextures();
                if (textures.Count > 0)
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (var tex in textures.OrderBy(t => t))
                    {
                        string texName = tex;
                        options.Add(new FloatMenuOption(texName, () => onValueChanged?.Invoke(texName)));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                Event.current.Use();
            }
        }
    }
}

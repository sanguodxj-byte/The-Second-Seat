using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.UI
{
    public class PersonaSelectionWindow : Window
    {
        private Vector2 scrollPosition;
        private NarratorPersonaDef? selectedPersona;
        private NarratorManager narratorManager;

        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public PersonaSelectionWindow(NarratorManager manager)
        {
            this.narratorManager = manager;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            resizeable = true;
            draggable = true;
            
            selectedPersona = manager.GetCurrentPersona();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 40f), "选择叙事者人格");
            Text.Font = GameFont.Small;

            var listRect = new Rect(0f, 50f, inRect.width, inRect.height - 110f);
            DrawPersonaList(listRect);

            // 按钮区域
            var buttonY = inRect.height - 50f;
            
            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            bool multimodalEnabled = settings?.enableMultimodalAnalysis == true;
            
            // 从立绘生成新人格
            if (Widgets.ButtonText(new Rect(10f, buttonY, 200f, 35f), "从立绘生成新人格"))
            {
                if (!multimodalEnabled)
                {
                    Messages.Message("请先在模组设置中启用多模态分析", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    OpenPortraitPickerForNewPersona();
                }
            }
            
            if (Widgets.ButtonText(new Rect(inRect.width - 220f, buttonY, 100f, 35f), "应用"))
            {
                if (selectedPersona != null)
                {
                    narratorManager.LoadPersona(selectedPersona);
                    Messages.Message($"已切换为：{selectedPersona.narratorName}", MessageTypeDefOf.PositiveEvent);
                }
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width - 110f, buttonY, 100f, 35f), "取消"))
            {
                Close();
            }
        }

        private void DrawPersonaList(Rect rect)
        {
            var personas = NarratorManager.GetAllPersonas();
            var viewRect = new Rect(0f, 0f, rect.width - 20f, personas.Count * 150f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float curY = 0f;
            foreach (var persona in personas)
            {
                DrawPersonaCard(new Rect(0f, curY, viewRect.width, 140f), persona);
                curY += 150f;
            }

            Widgets.EndScrollView();
        }

        private void DrawPersonaCard(Rect rect, NarratorPersonaDef persona)
        {
            try
            {
                bool isSelected = selectedPersona == persona;
                bool isCurrent = narratorManager.GetCurrentPersona() == persona;

                if (isSelected)
                    Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.4f, 0.6f, 0.3f));
                else if (Mouse.IsOver(rect))
                    Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.1f));

                Widgets.DrawBox(rect);

                // 点击选择
                if (Mouse.IsOver(rect) && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        selectedPersona = persona;
                        Event.current.Use();
                    }
                    // 右键编辑菜单
                    else if (Event.current.button == 1)
                    {
                        ShowPersonaContextMenu(persona);
                        Event.current.Use();
                    }
                }

                var contentRect = rect.ContractedBy(10f);
                var portraitRect = new Rect(contentRect.x, contentRect.y, 100f, 100f);
                
                // ? 包装立绘加载，防止单个人格错误导致整个列表崩溃
                try
                {
                    DrawPortraitPreview(portraitRect, persona);
                }
                catch (Exception ex)
                {
                    // 绘制错误占位符
                    Widgets.DrawBoxSolid(portraitRect, Color.red * 0.5f);
                    Log.Warning($"[PersonaSelectionWindow] 立绘加载失败: {persona.defName} - {ex.Message}");
                }

                var infoRect = new Rect(contentRect.x + 110f, contentRect.y, contentRect.width - 110f, contentRect.height);
                float curY = infoRect.y;

                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(infoRect.x, curY, infoRect.width, 30f), persona.narratorName ?? "未知");
                
                if (isCurrent)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = Color.green;
                    Widgets.Label(new Rect(infoRect.x + infoRect.width - 80f, curY, 80f, 20f), "(当前)");
                    GUI.color = Color.white;
                }

                Text.Font = GameFont.Small;
                curY += 32f;

                // ? 包装分析，防止错误
                try
                {
                    var analysis = PersonaAnalyzer.AnalyzePersonaDef(persona);
                    GUI.color = GetPersonalityColor(analysis.SuggestedPersonality);
                    // ? 使用中文显示特质
                    string personalityText = analysis.SuggestedPersonality?.GetChineseName() ?? "未知";
                    Widgets.Label(new Rect(infoRect.x, curY, infoRect.width, 20f), $"个性：{personalityText}");
                    GUI.color = Color.white;
                }
                catch
                {
                    Widgets.Label(new Rect(infoRect.x, curY, infoRect.width, 20f), "个性：未知");
                }
                curY += 22f;

                Text.Font = GameFont.Tiny;
                
                // ? 包装风格摘要
                try
                {
                    if (persona.dialogueStyle != null)
                    {
                        Widgets.Label(new Rect(infoRect.x, curY, infoRect.width, 20f), GetStyleSummary(persona.dialogueStyle));
                    }
                }
                catch { }
                curY += 22f;

                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                string bio = persona.biography ?? "";
                var shortBio = bio.Length > 150 ? bio.Substring(0, 147) + "..." : bio;
                Widgets.Label(new Rect(infoRect.x, curY, infoRect.width, infoRect.yMax - curY), shortBio.Replace("\n", " "));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            catch (Exception ex)
            {
                // 整个卡片渲染失败时的降级处理
                Widgets.DrawBoxSolid(rect, Color.red * 0.3f);
                Widgets.DrawBox(rect);
                Text.Font = GameFont.Small;
                Widgets.Label(rect.ContractedBy(10f), $"渲染错误: {persona?.defName ?? "null"}\n{ex.Message}");
                Log.Error($"[PersonaSelectionWindow] DrawPersonaCard 失败: {ex}");
            }
        }

        private void DrawPortraitPreview(Rect rect, NarratorPersonaDef persona)
        {
            try
            {
                // 获取当前表情状态
                var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
                ExpressionType currentExpression = expressionState.CurrentExpression;
                
                // 加载带表情的立绘
                var texture = PortraitLoader.LoadPortrait(persona, currentExpression);
                
                if (texture != null)
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                else
                    Widgets.DrawBoxSolid(rect, persona.primaryColor);
                
                Widgets.DrawBox(rect);
                
                // 显示当前表情（调试用）
                var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                if (settings?.debugMode == true)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = Color.cyan;
                    Widgets.Label(new Rect(rect.x, rect.yMax - 15f, rect.width, 15f), currentExpression.ToString());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
            }
            catch (Exception ex)
            {
                // 立绘加载失败，显示颜色占位符
                Widgets.DrawBoxSolid(rect, persona.primaryColor);
                Widgets.DrawBox(rect);
                Log.Warning($"[PersonaSelectionWindow] DrawPortraitPreview 失败: {persona.defName} - {ex.Message}");
            }
            
            // 右键菜单移到外层处理，避免异常影响
        }
        
        private void ShowPortraitContextMenu(NarratorPersonaDef persona)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("使用 Mod 自带立绘", () => {
                    persona.useCustomPortrait = false;
                    PortraitLoader.ClearCache();
                }),
                new FloatMenuOption("选择自定义立绘...", () => OpenCustomPortraitPicker(persona)),
                // ? 修改：打开 Mod 立绘目录（开发者使用）
                new FloatMenuOption("打开 Mod 立绘目录（开发者）", () => PortraitLoader.OpenModPortraitsDirectory()),
                // ? 保留：打开用户立绘目录（玩家个性化）
                new FloatMenuOption("打开用户立绘目录（个性化）", () => PortraitLoader.OpenUserPortraitsDirectory())
            };
            
            if (persona.useCustomPortrait)
            {
                options.Add(new FloatMenuOption("清除自定义立绘", () => {
                    persona.useCustomPortrait = false;
                    persona.customPortraitPath = "";
                    PortraitLoader.ClearCache();
                }));
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        private void OpenCustomPortraitPicker(NarratorPersonaDef persona)
        {
            // ? 使用新的统一立绘列表
            var allPortraits = PortraitLoader.GetAllAvailablePortraits();
            
            if (allPortraits.Count == 0)
            {
                Messages.Message("未找到可用立绘", MessageTypeDefOf.RejectInput);
                PortraitLoader.OpenModPortraitsDirectory();
                return;
            }
            
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // 按来源分组显示
            var vanillaPortraits = allPortraits.Where(p => p.Source == PortraitSource.Vanilla).ToList();
            var modPortraits = allPortraits.Where(p => p.Source == PortraitSource.OtherMod).ToList();
            var thisModPortraits = allPortraits.Where(p => p.Source == PortraitSource.ThisMod).ToList();
            var userPortraits = allPortraits.Where(p => p.Source == PortraitSource.User).ToList();
            
            // 原版立绘
            if (vanillaPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 原版叙事者 ---", null));
                foreach (var portrait in vanillaPortraits)
                {
                    // ? 如果有Texture，直接传递
                    Texture2D? texture = portrait.Texture;
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, texture)));
                }
            }
            
            // 其他Mod立绘
            if (modPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 其他Mod叙事者 ---", null));
                foreach (var portrait in modPortraits)
                {
                    Texture2D? texture = portrait.Texture;
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, texture)));
                }
            }
            
            // 本Mod立绘
            if (thisModPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 本Mod立绘 ---", null));
                foreach (var portrait in thisModPortraits)
                {
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, null)));
                }
            }
            
            // 用户自定义立绘
            if (userPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 用户自定义 ---", null));
                foreach (var portrait in userPortraits)
                {
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, null)));
                }
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenPortraitPickerForNewPersona()
        {
            // ? 使用统一立绘列表
            var allPortraits = PortraitLoader.GetAllAvailablePortraits();
            
            if (allPortraits.Count == 0)
            {
                Messages.Message("未找到可用立绘", MessageTypeDefOf.RejectInput);
                PortraitLoader.OpenModPortraitsDirectory();
                return;
            }
            
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // 按来源分组显示
            var vanillaPortraits = allPortraits.Where(p => p.Source == PortraitSource.Vanilla).ToList();
            var modPortraits = allPortraits.Where(p => p.Source == PortraitSource.OtherMod).ToList();
            var thisModPortraits = allPortraits.Where(p => p.Source == PortraitSource.ThisMod).ToList();
            var userPortraits = allPortraits.Where(p => p.Source == PortraitSource.User).ToList();
            
            // 原版立绘
            if (vanillaPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 原版叙事者 ---", null));
                foreach (var portrait in vanillaPortraits)
                {
                    // ? 如果有Texture，直接传递
                    Texture2D? texture = portrait.Texture;
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, texture)));
                }
            }
            
            // 其他Mod立绘
            if (modPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 其他Mod叙事者 ---", null));
                foreach (var portrait in modPortraits)
                {
                    Texture2D? texture = portrait.Texture;
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, texture)));
                }
            }
            
            // 本Mod立绘
            if (thisModPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 本Mod立绘 ---", null));
                foreach (var portrait in thisModPortraits)
                {
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, null)));
                }
            }
            
            // 用户自定义立绘
            if (userPortraits.Count > 0)
            {
                options.Add(new FloatMenuOption("--- 用户自定义 ---", null));
                foreach (var portrait in userPortraits)
                {
                    options.Add(new FloatMenuOption(portrait.Name, () => CreatePersonaFromPortrait(portrait.Path, null)));
                }
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private async void CreatePersonaFromPortrait(string portraitPath, Texture2D? existingTexture = null)
        {
            try
            {
                Messages.Message("正在分析立绘...", MessageTypeDefOf.NeutralEvent);
                
                // ? 优先使用已有的Texture（ContentFinder加载的）
                Texture2D? texture = existingTexture;
                
                if (texture == null)
                {
                    // 如果没有现成的Texture，尝试加载
                    if (portraitPath.StartsWith("UI/"))
                    {
                        // ContentFinder路径
                        texture = ContentFinder<Texture2D>.Get(portraitPath, false);
                    }
                    else
                    {
                        // 文件路径
                        texture = PortraitLoader.LoadFromExternalFile(portraitPath);
                    }
                }
                
                if (texture == null)
                {
                    Messages.Message("无法加载立绘", MessageTypeDefOf.RejectInput);
                    return;
                }
                
                // 使用 MultimodalAnalysisService 分析立绘
                var visionResult = await MultimodalAnalysisService.Instance.AnalyzeTextureAsync(texture);
                if (visionResult == null)
                {
                    Messages.Message("API 分析失败", MessageTypeDefOf.NegativeEvent);
                    return;
                }
                
                // 从文件名或路径提取名称
                string fileName = Path.GetFileNameWithoutExtension(portraitPath);
                if (portraitPath.Contains("/"))
                {
                    fileName = portraitPath.Split('/').Last();
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }
                
                // ? 使用 API 返回的简介，并基于它进行深度分析
                string biography = !string.IsNullOrEmpty(visionResult.characterDescription) 
                    ? visionResult.characterDescription 
                    : "这是一位神秘的叙事者。";
                
                // ? 使用 PersonaAnalyzer 本地分析简介，获取对话风格和事件偏好
                var biographyAnalysis = PersonaAnalyzer.AnalyzeBiography(biography);
                
                // ? 创建人格（使用 API 图片分析 + 本地文本分析）
                var newPersona = new NarratorPersonaDef
                {
                    defName = $"CustomPersona_{Guid.NewGuid().ToString().Substring(0, 8)}",
                    label = fileName,
                    narratorName = fileName,
                    
                    // ? 使用 API 分析的描述作为简介
                    biography = biography,
                    
                    // ? 【修复】不再重复存储 visualDescription（已包含在 biography 中）
                    // visualDescription 保留空，避免 XML 内容重复
                    visualDescription = "",
                    visualElements = visionResult.visualElements ?? new List<string>(),  // 视觉元素列表仍保留
                    visualMood = visionResult.mood ?? "",  // 视觉氛围仍保留
                    
                    // ? 使用 API 提取的颜色
                    primaryColor = visionResult.GetPrimaryColor(),
                    accentColor = visionResult.GetAccentColor(),
                    
                    // ? 使用 API 推断的性格（优先）或本地分析结果
                    overridePersonality = visionResult.suggestedPersonality ?? biographyAnalysis.SuggestedPersonality?.ToString(),
                    
                    // ? 使用本地分析的对话风格（从简介推断）
                    dialogueStyle = biographyAnalysis.DialogueStyle != null 
                        ? new DialogueStyleDef
                        {
                            formalityLevel = biographyAnalysis.DialogueStyle.formalityLevel,
                            emotionalExpression = biographyAnalysis.DialogueStyle.emotionalExpression,
                            humorLevel = biographyAnalysis.DialogueStyle.humorLevel,
                            sarcasmLevel = biographyAnalysis.DialogueStyle.sarcasmLevel,
                            verbosity = biographyAnalysis.DialogueStyle.verbosity,
                            useEmoticons = biographyAnalysis.DialogueStyle.useEmoticons,
                            useEllipsis = biographyAnalysis.DialogueStyle.useEllipsis,
                            useExclamation = biographyAnalysis.DialogueStyle.useExclamation
                        }
                        : new DialogueStyleDef  // 降级：使用默认值
                        {
                            formalityLevel = 0.5f,
                            emotionalExpression = 0.5f,
                            humorLevel = 0.3f,
                            sarcasmLevel = 0.2f,
                            verbosity = 0.5f
                        },
                    
                    // ? 使用 API 提取的风格关键词 + 本地分析的关键词
                    toneTags = visionResult.styleKeywords ?? new List<string>(),
                    
                    // ? 使用本地分析的事件偏好（从简介推断）
                    eventPreferences = biographyAnalysis.EventPreferences != null
                        ? new EventPreferencesDef
                        {
                            positiveEventBias = biographyAnalysis.EventPreferences.positiveEventBias,
                            negativeEventBias = biographyAnalysis.EventPreferences.negativeEventBias,
                            chaosLevel = biographyAnalysis.EventPreferences.chaosLevel,
                            interventionFrequency = biographyAnalysis.EventPreferences.interventionFrequency
                        }
                        : null,  // 允许为空
                        
                    useCustomPortrait = false, // ? 使用导出的立绘
                    customPortraitPath = "",
                    portraitPath = "" // ? 将在导出时设置
                };
                
                // 合并风格关键词（去重）
                var combinedTags = new List<string>(visionResult.styleKeywords ?? new List<string>());
                foreach (var tag in biographyAnalysis.ToneTags)
                {
                    if (!combinedTags.Contains(tag))
                    {
                        combinedTags.Add(tag);
                    }
                }
                newPersona.toneTags = combinedTags;
                
                // ? 【核心功能】导出人格（复制立绘+生成XML）
                bool exportSuccess = PersonaDefExporter.ExportPersona(newPersona, portraitPath, texture);
                
                if (!exportSuccess)
                {
                    // 如果导出失败，仍然添加到运行时（但不持久化）
                    Log.Warning($"[PersonaSelectionWindow] 人格导出失败，仅添加到运行时: {newPersona.defName}");
                    DefDatabase<NarratorPersonaDef>.Add(newPersona);
                    Messages.Message($"?? 人格已创建但未保存到文件\n重启游戏后将丢失", MessageTypeDefOf.CautionInput);
                }
                
                PortraitLoader.ClearCache();
                selectedPersona = newPersona;
                
                // ? 显示详细的创建结果
                string resultMessage = $"? 成功创建人格：{fileName}\n" +
                                     $"性格：{newPersona.overridePersonality ?? "未知"}\n" +
                                     $"风格：正式度={newPersona.dialogueStyle.formalityLevel:F2}, 感性={newPersona.dialogueStyle.emotionalExpression:F2}\n" +
                                     $"关键词：{string.Join(", ", newPersona.toneTags)}";
                Messages.Message(resultMessage, MessageTypeDefOf.PositiveEvent);
                
                Log.Message($"[PersonaSelectionWindow] 创建新人格：{newPersona.defName}\n" +
                           $"  biography: {newPersona.biography.Substring(0, Math.Min(50, newPersona.biography.Length))}...\n" +
                           $"  personality: {newPersona.overridePersonality}\n" +
                           $"  dialogueStyle: formality={newPersona.dialogueStyle.formalityLevel:F2}, emotion={newPersona.dialogueStyle.emotionalExpression:F2}\n" +
                           $"  toneTags: {string.Join(", ", newPersona.toneTags)}");
            }
            catch (Exception ex)
            {
                Messages.Message($"创建人格失败：{ex.Message}", MessageTypeDefOf.NegativeEvent);
                Log.Error($"[PersonaSelectionWindow] 创建人格异常：{ex}");
            }
        }

        private Color GetPersonalityColor(PersonalityTrait? trait) => trait switch
        {
            PersonalityTrait.Benevolent => new Color(0.3f, 0.8f, 0.3f),
            PersonalityTrait.Sadistic => new Color(0.8f, 0.2f, 0.2f),
            PersonalityTrait.Chaotic => new Color(0.9f, 0.5f, 0.1f),
            PersonalityTrait.Strategic => new Color(0.3f, 0.5f, 0.8f),
            PersonalityTrait.Protective => new Color(0.5f, 0.7f, 0.9f),
            PersonalityTrait.Manipulative => new Color(0.6f, 0.3f, 0.7f),
            _ => Color.gray
        };

        private string GetStyleSummary(DialogueStyleDef style)
        {
            var parts = new List<string>();
            if (style.formalityLevel > 0.6f) parts.Add("正式");
            else if (style.formalityLevel < 0.4f) parts.Add("随意");
            if (style.emotionalExpression > 0.6f) parts.Add("感性");
            else if (style.emotionalExpression < 0.4f) parts.Add("理性");
            if (style.humorLevel > 0.5f) parts.Add("幽默");
            if (style.sarcasmLevel > 0.5f) parts.Add("讽刺");
            if (style.verbosity > 0.6f) parts.Add("详细");
            else if (style.verbosity < 0.4f) parts.Add("简洁");
            return parts.Count > 0 ? $"风格：{string.Join(", ", parts)}" : "风格：平衡";
        }

        /// <summary>
        /// 显示人格右键菜单（编辑选项）
        /// </summary>
        private void ShowPersonaContextMenu(NarratorPersonaDef persona)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // ✅ 添加：编辑人格（打开 Dialog_PersonaEditor）
            options.Add(new FloatMenuOption("编辑人格卡片", () => {
                Find.WindowStack.Add(new Dialog_PersonaEditor(persona));
            }));
            
            options.Add(new FloatMenuOption("--- 基本操作 ---", null));

            // ? 调试选项（仅在调试模式下显示）
            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            if (settings?.debugMode == true)
            {
                options.Add(new FloatMenuOption("[调试] 好感度", () => {
                    Find.WindowStack.Add(new Dialog_FavorabilityDebug(narratorManager));
                }));
                
                options.Add(new FloatMenuOption("[调试] 表情", () => {
                    Find.WindowStack.Add(new Dialog_ExpressionDebug(persona));
                }));
                
                options.Add(new FloatMenuOption("--- 普通选项 ---", null));
            }

            // 编辑人格名称
            options.Add(new FloatMenuOption("编辑名称", () => OpenNameEditor(persona)));

            // 编辑简介
            options.Add(new FloatMenuOption("编辑简介", () => OpenBiographyEditor(persona)));

            // 更换立绘 - ? 使用统一立绘列表
            options.Add(new FloatMenuOption("更换立绘", () => {
                var allPortraits = PortraitLoader.GetAllAvailablePortraits();
                
                if (allPortraits.Count == 0)
                {
                    PortraitLoader.OpenModPortraitsDirectory();
                    return;
                }
                
                List<FloatMenuOption> portraitOptions = new List<FloatMenuOption>();
                
                // 使用 Mod 自带立绘
                portraitOptions.Add(new FloatMenuOption("使用 Mod 自带立绘", () => {
                    persona.useCustomPortrait = false;
                    PortraitLoader.ClearCache();
                }));
                
                // 按来源分组
                var vanillaPortraits = allPortraits.Where(p => p.Source == PortraitSource.Vanilla).ToList();
                var modPortraits = allPortraits.Where(p => p.Source == PortraitSource.OtherMod).ToList();
                var thisModPortraits = allPortraits.Where(p => p.Source == PortraitSource.ThisMod).ToList();
                var userPortraits = allPortraits.Where(p => p.Source == PortraitSource.User).ToList();
                
                // 原版立绘
                if (vanillaPortraits.Count > 0)
                {
                    portraitOptions.Add(new FloatMenuOption("--- 原版叙事者 ---", null));
                    foreach (var portrait in vanillaPortraits)
                    {
                        portraitOptions.Add(new FloatMenuOption(portrait.Name, () => {
                            persona.useCustomPortrait = true;
                            persona.customPortraitPath = portrait.Path;
                            PortraitLoader.ClearCache();
                        }));
                    }
                }
                
                // 其他Mod立绘
                if (modPortraits.Count > 0)
                {
                    portraitOptions.Add(new FloatMenuOption("--- 其他Mod叙事者 ---", null));
                    foreach (var portrait in modPortraits)
                    {
                        portraitOptions.Add(new FloatMenuOption(portrait.Name, () => {
                            persona.useCustomPortrait = true;
                            persona.customPortraitPath = portrait.Path;
                            PortraitLoader.ClearCache();
                        }));
                    }
                }
                
                // 本Mod立绘
                if (thisModPortraits.Count > 0)
                {
                    portraitOptions.Add(new FloatMenuOption("--- 本Mod立绘 ---", null));
                    foreach (var portrait in thisModPortraits)
                    {
                        portraitOptions.Add(new FloatMenuOption(portrait.Name, () => {
                            persona.useCustomPortrait = true;
                            persona.customPortraitPath = portrait.Path;
                            PortraitLoader.ClearCache();
                        }));
                    }
                }
                
                // 用户立绘
                if (userPortraits.Count > 0)
                {
                    portraitOptions.Add(new FloatMenuOption("--- 用户自定义 ---", null));
                    foreach (var portrait in userPortraits)
                    {
                        portraitOptions.Add(new FloatMenuOption(portrait.Name, () => {
                            persona.useCustomPortrait = true;
                            persona.customPortraitPath = portrait.Path;
                            PortraitLoader.ClearCache();
                        }));
                    }
                }
                
                Find.WindowStack.Add(new FloatMenu(portraitOptions));
            }));

            // 复制人格
            options.Add(new FloatMenuOption("复制", () => DuplicatePersona(persona)));

            // ? 删除人格（对所有非内置人格显示）
            // 内置人格列表（受保护，不能删除）
            string[] protectedPersonas = { "Cassandra_Classic", "Randy_Random", "Phoebe_Chillax" };
            bool isProtected = protectedPersonas.Contains(persona.defName);
            
            if (!isProtected)
            {
                options.Add(new FloatMenuOption("删除", () => DeletePersona(persona)));
            }

            // 打开 Mod 立绘目录（推荐给开发者）
            options.Add(new FloatMenuOption("打开 Mod 立绘目录", () => PortraitLoader.OpenModPortraitsDirectory()));
            
            // 添加：打开用户立绘目录（给玩家使用）
            options.Add(new FloatMenuOption("打开用户立绘目录", () => PortraitLoader.OpenUserPortraitsDirectory()));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// 打开名称编辑器
        /// </summary>
        private void OpenNameEditor(NarratorPersonaDef persona)
        {
            Find.WindowStack.Add(new Dialog_RenamePersona(persona, false));
        }

        /// <summary>
        /// 打开简介编辑器
        /// </summary>
        private void OpenBiographyEditor(NarratorPersonaDef persona)
        {
            Find.WindowStack.Add(new Dialog_RenamePersona(persona, true));
        }

        /// <summary>
        /// 复制人格
        /// </summary>
        private void DuplicatePersona(NarratorPersonaDef original)
        {
            var newPersona = new NarratorPersonaDef
            {
                defName = $"CustomPersona_{Guid.NewGuid().ToString().Substring(0, 8)}",
                label = original.label + " (副本)",
                narratorName = original.narratorName + " (副本)",
                biography = original.biography,
                primaryColor = original.primaryColor,
                accentColor = original.accentColor,
                overridePersonality = original.overridePersonality,
                dialogueStyle = new DialogueStyleDef
                {
                    formalityLevel = original.dialogueStyle.formalityLevel,
                    emotionalExpression = original.dialogueStyle.emotionalExpression,
                    humorLevel = original.dialogueStyle.humorLevel,
                    sarcasmLevel = original.dialogueStyle.sarcasmLevel,
                    verbosity = original.dialogueStyle.verbosity
                },
                toneTags = new List<string>(original.toneTags),
                useCustomPortrait = original.useCustomPortrait,
                customPortraitPath = original.customPortraitPath,
                portraitPath = original.portraitPath
            };
            
            DefDatabase<NarratorPersonaDef>.Add(newPersona);
            selectedPersona = newPersona;
            Messages.Message($"已复制人格：{newPersona.narratorName}", MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// 删除自定义人格
        /// </summary>
        private void DeletePersona(NarratorPersonaDef persona)
        {
            string confirmMessage = "确定要删除人格\"" + persona.narratorName + "\"吗？\n\n" +
                                    "警告：如果此人格有对应的XML文件，需要手动删除文件并重启游戏。";
            
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                confirmMessage,
                () => {
                    try
                    {
                        // ? 修复：使用反射正确删除 Def
                        var defDatabaseType = typeof(DefDatabase<NarratorPersonaDef>);
                        var defListField = defDatabaseType.GetField("defsList", 
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        
                        if (defListField != null)
                        {
                            var defsList = defListField.GetValue(null) as List<NarratorPersonaDef>;
                            if (defsList != null && defsList.Contains(persona))
                            {
                                defsList.Remove(persona);
                                Log.Message($"[PersonaSelectionWindow] 已从 DefDatabase 删除人格: {persona.defName}");
                            }
                        }
                        
                        // ? 同时尝试从字典中删除（如果存在）
                        var defsByNameField = defDatabaseType.GetField("defsByName",
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        
                        if (defsByNameField != null)
                        {
                            var defsByName = defsByNameField.GetValue(null) as Dictionary<string, NarratorPersonaDef>;
                            if (defsByName != null && defsByName.ContainsKey(persona.defName))
                            {
                                defsByName.Remove(persona.defName);
                                Log.Message($"[PersonaSelectionWindow] 已从 defsByName 删除人格: {persona.defName}");
                            }
                        }
                        
                        // ? 清理选中状态
                        if (selectedPersona == persona)
                        {
                            selectedPersona = null;
                        }
                        
                        // ? 清理立绘缓存
                        PortraitLoader.ClearCache();
                        
                        // ? 如果当前使用的是这个人格，切换回默认人格
                        if (narratorManager.GetCurrentPersona() == persona)
                        {
                            var cassandra = DefDatabase<NarratorPersonaDef>.GetNamedSilentFail("Cassandra_Classic");
                            if (cassandra != null)
                            {
                                narratorManager.LoadPersona(cassandra);
                                Messages.Message("当前人格已被删除，已切换回 Cassandra", MessageTypeDefOf.CautionInput);
                            }
                        }
                        
                        string successMessage = "已删除人格\"" + persona.narratorName + "\"\n\n" +
                                              "提示：如需彻底删除，请在 Defs/NarratorPersonaDefs 文件夹中删除对应的 XML 文件并重启游戏。";
                        Messages.Message(successMessage, MessageTypeDefOf.PositiveEvent);
                        
                        Log.Message($"[PersonaSelectionWindow] 删除人格成功: {persona.defName}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[PersonaSelectionWindow] 删除人格失败: {ex.Message}\n{ex.StackTrace}");
                        Messages.Message($"删除人格失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                    }
                },
                true
            ));
        }
    }

    /// <summary>
    /// 人格编辑对话框（名称或简介）
    /// </summary>
    public class Dialog_RenamePersona : Window
    {
        private NarratorPersonaDef persona;
        private bool editingBio;
        private string text;

        public override Vector2 InitialSize => editingBio ? new Vector2(500f, 300f) : new Vector2(400f, 200f);

        public Dialog_RenamePersona(NarratorPersonaDef persona, bool editingBio)
        {
            this.persona = persona;
            this.editingBio = editingBio;
            this.text = editingBio ? persona.biography : persona.narratorName;
            this.doCloseButton = false; // ? 移除关闭按钮，使用ESC键关闭
            this.doCloseX = true;
            this.closeOnCancel = true; // ? ESC键关闭
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 40f), editingBio ? "编辑简介" : "编辑名称");
            Text.Font = GameFont.Small;

            var textRect = new Rect(0f, 50f, inRect.width, editingBio ? 120f : 30f);
            text = editingBio 
                ? Widgets.TextArea(textRect, text) 
                : Widgets.TextField(textRect, text);

            var buttonY = inRect.height - 40f;

            if (Widgets.ButtonText(new Rect(inRect.width - 220f, buttonY, 100f, 35f), "确定"))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (editingBio)
                    {
                        persona.biography = text.Trim();
                        Messages.Message("已修改简介", MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        persona.narratorName = text.Trim();
                        persona.label = text.Trim();
                        Messages.Message($"已修改名称为：{text.Trim()}", MessageTypeDefOf.PositiveEvent);
                    }
                }
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width - 110f, buttonY, 100f, 35f), "取消"))
            {
                Close();
            }
        }
    }
}

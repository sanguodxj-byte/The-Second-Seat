using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ?? v1.6.62: 多模态立绘分析人格生成弹窗
    /// 
    /// 功能：
    /// - 左侧显示立绘预览
    /// - 右侧输入人格名称、选择特质、用户补充
    /// - 右下角"开始分析"按钮
    /// - 分析完成后生成带个性标签的人格
    /// </summary>
    public class Dialog_MultimodalPersonaGeneration : Window
    {
        private Texture2D portraitTexture;
        private string portraitPath;
        private Vector2 scrollPosition = Vector2.zero;
        
        // 用户输入
        private string personaName = "";
        private List<string> selectedTraits = new List<string>();
        private string userSupplement = "";
        
        // 状态
        private bool isAnalyzing = false;
        private string statusMessage = "";
        
        // 可选特质列表（最多3个）
        private static readonly string[] AvailableTraits = new[]
        {
            "善良", "坚强", "温柔", "冷酷", "勇敢", "谨慎",
            "乐观", "悲观", "理性", "感性", "自信", "谦逊",
            "外向", "内向", "严肃", "活泼", "冷静", "热情"
        };
        
        // 布局常量
        private const float WINDOW_WIDTH = 900f;
        private const float WINDOW_HEIGHT = 700f;
        private const float PORTRAIT_WIDTH = 360f;
        private const float MARGIN = 20f;
        private const float INPUT_HEIGHT = 30f;
        private const float BUTTON_HEIGHT = 35f;
        
        public Dialog_MultimodalPersonaGeneration(Texture2D texture, string path)
        {
            this.portraitTexture = texture;
            this.portraitPath = path;
            
            this.doCloseX = true;
            this.doCloseButton = false;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Widgets.Label(titleRect, "多模态立绘分析 - 生成人格");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域（分左右两栏）
            float contentY = 50f;
            float contentHeight = inRect.height - 110f;
            
            // === 左侧：立绘预览 ===
            Rect leftRect = new Rect(MARGIN, contentY, PORTRAIT_WIDTH, contentHeight);
            DrawPortraitPreview(leftRect);
            
            // === 右侧：输入区域 ===
            float rightX = MARGIN + PORTRAIT_WIDTH + 20f;
            float rightWidth = inRect.width - rightX - MARGIN;
            Rect rightRect = new Rect(rightX, contentY, rightWidth, contentHeight);
            DrawInputArea(rightRect);
            
            // === 底部按钮 ===
            float buttonY = inRect.height - 50f;
            DrawBottomButtons(new Rect(MARGIN, buttonY, inRect.width - MARGIN * 2, BUTTON_HEIGHT));
        }

        /// <summary>
        /// 绘制左侧立绘预览
        /// </summary>
        private void DrawPortraitPreview(Rect rect)
        {
            Widgets.DrawBox(rect);
            
            if (portraitTexture != null)
            {
                // 计算缩放比例（保持宽高比）
                float textureAspect = (float)portraitTexture.width / portraitTexture.height;
                float rectAspect = rect.width / rect.height;
                
                Rect imageRect;
                if (textureAspect > rectAspect)
                {
                    // 纹理更宽，以宽度为准
                    float scaledHeight = rect.width / textureAspect;
                    imageRect = new Rect(
                        rect.x,
                        rect.y + (rect.height - scaledHeight) / 2f,
                        rect.width,
                        scaledHeight
                    );
                }
                else
                {
                    // 纹理更高，以高度为准
                    float scaledWidth = rect.height * textureAspect;
                    imageRect = new Rect(
                        rect.x + (rect.width - scaledWidth) / 2f,
                        rect.y,
                        scaledWidth,
                        rect.height
                    );
                }
                
                GUI.DrawTexture(imageRect, portraitTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                // 占位符
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "立绘预览\n(未加载)");
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        /// <summary>
        /// 绘制右侧输入区域
        /// </summary>
        private void DrawInputArea(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, 800f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            
            // 1. 人格名称
            Widgets.Label(new Rect(0f, curY, viewRect.width, INPUT_HEIGHT), "人格名称 *:");
            curY += INPUT_HEIGHT + 5f;
            
            Rect nameRect = new Rect(0f, curY, viewRect.width, INPUT_HEIGHT);
            personaName = Widgets.TextField(nameRect, personaName);
            curY += INPUT_HEIGHT + 15f;
            
            // 2. 特质选择（最多3个）
            Widgets.Label(new Rect(0f, curY, viewRect.width, INPUT_HEIGHT), 
                $"选择特质（最多3个，已选 {selectedTraits.Count}/3）:");
            curY += INPUT_HEIGHT + 5f;
            
            curY = DrawTraitSelection(viewRect, curY);
            curY += 15f;
            
            // 3. 用户补充
            Widgets.Label(new Rect(0f, curY, viewRect.width, INPUT_HEIGHT), 
                "用户补充 *（将与图像分析结合）:");
            curY += INPUT_HEIGHT + 5f;
            
            Text.Font = GameFont.Tiny;
            Rect hintRect = new Rect(0f, curY, viewRect.width, 40f);
            GUI.color = Color.gray;
            Widgets.Label(hintRect, 
                "提示：请描述角色的性格、背景、说话风格等。\n" +
                "这段描述将与AI的视觉分析结合，生成完整的人格数据。");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 45f;
            
            Rect supplementRect = new Rect(0f, curY, viewRect.width, 250f);
            userSupplement = Widgets.TextArea(supplementRect, userSupplement);
            curY += 260f;
            
            // 4. 状态消息
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUI.color = isAnalyzing ? Color.yellow : Color.green;
                Rect statusRect = new Rect(0f, curY, viewRect.width, 60f);
                Widgets.Label(statusRect, statusMessage);
                GUI.color = Color.white;
            }
            
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 绘制特质选择按钮（网格布局）
        /// </summary>
        private float DrawTraitSelection(Rect viewRect, float startY)
        {
            float curY = startY;
            float buttonWidth = (viewRect.width - 20f) / 3f; // 每行3个
            float buttonHeight = 30f;
            
            int col = 0;
            int row = 0;
            
            foreach (var trait in AvailableTraits)
            {
                bool isSelected = selectedTraits.Contains(trait);
                
                float x = col * (buttonWidth + 5f);
                float y = curY + row * (buttonHeight + 5f);
                Rect buttonRect = new Rect(x, y, buttonWidth, buttonHeight);
                
                // 选中状态高亮
                if (isSelected)
                {
                    GUI.color = new Color(0.5f, 1f, 0.5f);
                }
                
                if (Widgets.ButtonText(buttonRect, trait))
                {
                    if (isSelected)
                    {
                        // 取消选择
                        selectedTraits.Remove(trait);
                    }
                    else
                    {
                        // 添加选择（最多3个）
                        if (selectedTraits.Count < 3)
                        {
                            selectedTraits.Add(trait);
                        }
                        else
                        {
                            Messages.Message("最多只能选择3个特质", MessageTypeDefOf.RejectInput);
                        }
                    }
                }
                
                GUI.color = Color.white;
                
                col++;
                if (col >= 3)
                {
                    col = 0;
                    row++;
                }
            }
            
            // 计算总高度
            int totalRows = (AvailableTraits.Length + 2) / 3;
            return curY + totalRows * (buttonHeight + 5f);
        }

        /// <summary>
        /// 绘制底部按钮
        /// </summary>
        private void DrawBottomButtons(Rect rect)
        {
            float buttonWidth = 150f;
            float spacing = 10f;
            
            // 取消按钮（左侧）
            Rect cancelRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(cancelRect, "取消"))
            {
                this.Close();
            }
            
            // 开始分析按钮（右侧）
            Rect analyzeRect = new Rect(
                rect.x + rect.width - buttonWidth,
                rect.y,
                buttonWidth,
                rect.height
            );
            
            GUI.enabled = !isAnalyzing;
            if (Widgets.ButtonText(analyzeRect, isAnalyzing ? "分析中..." : "开始分析"))
            {
                StartAnalysis();
            }
            GUI.enabled = true;
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(personaName))
            {
                statusMessage = "错误: 请输入人格名称";
                Messages.Message("请输入人格名称", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(userSupplement))
            {
                statusMessage = "错误: 请输入用户补充描述";
                Messages.Message("请输入用户补充描述", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            if (userSupplement.Length < 20)
            {
                statusMessage = "错误: 用户补充太短，请至少输入20个字符";
                Messages.Message("用户补充太短，请输入更详细的描述", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            if (portraitTexture == null)
            {
                statusMessage = "错误: 立绘纹理未加载";
                Messages.Message("立绘纹理未加载，无法进行分析", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// ?? 开始多模态分析
        /// </summary>
        private void StartAnalysis()
        {
            if (!ValidateInput())
            {
                return;
            }
            
            if (isAnalyzing)
            {
                return;
            }
            
            isAnalyzing = true;
            statusMessage = "正在进行多模态分析，请稍候...\n这可能需要10-30秒";
            
            try
            {
                // 1. 构建完整的用户输入（特质 + 补充）
                string fullUserInput = BuildFullUserInput();
                
                // 2. 调用多模态分析服务
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                bool useMultimodal = modSettings?.enableMultimodalAnalysis ?? false;
                
                if (!useMultimodal)
                {
                    statusMessage = "错误: 多模态分析未启用，请在设置中启用";
                    Messages.Message("请在Mod设置中启用多模态分析功能", MessageTypeDefOf.RejectInput);
                    isAnalyzing = false;
                    return;
                }
                
                // 3. 执行分析
                var analysisService = MultimodalAnalysisService.Instance;
                
                // 确保服务已配置
                if (modSettings != null)
                {
                    analysisService.Configure(
                        modSettings.multimodalProvider,
                        modSettings.multimodalApiKey,
                        modSettings.visionModel,
                        modSettings.textAnalysisModel
                    );
                }
                
                // 调用分析（传递特质和用户补充）
                var analysisResult = analysisService.AnalyzePersonaImageWithTraits(
                    portraitTexture,
                    personaName,
                    selectedTraits,
                    fullUserInput
                );
                
                if (analysisResult != null)
                {
                    // 4. 创建人格定义
                    CreatePersonaFromAnalysis(analysisResult);
                    
                    statusMessage = "? 分析完成！人格已生成";
                    
                    // 延迟关闭窗口
                    System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => {
                        this.Close();
                    });
                }
                else
                {
                    statusMessage = "? 分析失败，请检查API配置或网络连接";
                    Messages.Message("多模态分析失败，请检查日志", MessageTypeDefOf.RejectInput);
                }
            }
            catch (Exception ex)
            {
                statusMessage = $"错误: {ex.Message}";
                Messages.Message($"分析失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                Log.Error($"[Dialog_MultimodalPersonaGeneration] 分析失败: {ex}");
            }
            finally
            {
                isAnalyzing = false;
            }
        }

        /// <summary>
        /// 构建完整的用户输入（特质 + 补充）
        /// </summary>
        private string BuildFullUserInput()
        {
            var parts = new List<string>();
            
            // 1. 添加特质
            if (selectedTraits.Count > 0)
            {
                parts.Add($"【核心特质】: {string.Join("、", selectedTraits)}");
            }
            
            // 2. 添加用户补充
            parts.Add($"【详细描述】: {userSupplement}");
            
            return string.Join("\n\n", parts);
        }

        /// <summary>
        /// 从分析结果创建人格定义
        /// </summary>
        private void CreatePersonaFromAnalysis(PersonaAnalysisResult analysisResult)
        {
            try
            {
                // 1. 创建基础人格定义
                NarratorPersonaDef newPersona = new NarratorPersonaDef();
                newPersona.defName = $"UserGenerated_{personaName.Replace(" ", "_")}_{DateTime.Now.Ticks}";
                newPersona.narratorName = personaName;
                newPersona.label = personaName;
                newPersona.enabled = true;
                
                // 2. 设置分析结果
                newPersona.SetAnalysis(analysisResult);
                
                // 3. ?? 设置个性标签（来自分析结果）
                if (analysisResult.PersonalityTags != null && analysisResult.PersonalityTags.Count > 0)
                {
                    newPersona.personalityTags = analysisResult.PersonalityTags;
                }
                
                // 4. ?? 设置用户选择的特质
                newPersona.selectedTraits = selectedTraits.ToList();
                
                // 5. 设置biography（用户输入 + AI分析）
                newPersona.biography = BuildFullBiography(analysisResult);
                
                // 6. 设置颜色（从分析结果）
                if (analysisResult.VisualTags != null && analysisResult.VisualTags.Count > 0)
                {
                    // 从视觉标签中提取颜色
                    newPersona.primaryColor = ExtractPrimaryColor(analysisResult);
                    newPersona.accentColor = ExtractAccentColor(analysisResult);
                }
                else
                {
                    newPersona.primaryColor = Color.white;
                    newPersona.accentColor = Color.gray;
                }
                
                // 7. 设置对话风格
                if (analysisResult.SuggestedDialogueStyle != null)
                {
                    newPersona.dialogueStyle = analysisResult.SuggestedDialogueStyle;
                }
                else
                {
                    newPersona.dialogueStyle = new DialogueStyleDef
                    {
                        formalityLevel = 0.5f,
                        emotionalExpression = 0.7f,
                        verbosity = 0.6f,
                        humorLevel = 0.5f,
                        sarcasmLevel = 0.3f,
                        useEmoticons = true,
                        useEllipsis = true,
                        useExclamation = true
                    };
                }
                
                // 8. 设置事件偏好
                newPersona.eventPreferences = new EventPreferencesDef
                {
                    positiveEventBias = 0.0f,
                    negativeEventBias = 0.0f,
                    chaosLevel = 0.5f,
                    interventionFrequency = 0.5f
                };
                
                // 9. 注册到 DefDatabase
                if (!DefDatabase<NarratorPersonaDef>.AllDefs.Contains(newPersona))
                {
                    DefDatabase<NarratorPersonaDef>.Add(newPersona);
                }
                
                // 10. 保存到文件
                bool exportSuccess = PersonaDefExporter.ExportPersona(
                    newPersona,
                    portraitPath,
                    portraitTexture
                );
                
                // 11. 显示成功消息
                string successMsg = $"人格 '{personaName}' 创建成功！\n\n";
                
                if (analysisResult.PersonalityTags != null && analysisResult.PersonalityTags.Count > 0)
                {
                    successMsg += $"?? 个性标签: {string.Join("、", analysisResult.PersonalityTags)}\n\n";
                }
                
                if (exportSuccess)
                {
                    successMsg += "? 人格已保存到 Defs 文件夹，重启游戏后永久生效。";
                }
                else
                {
                    successMsg += "?? 人格已加载到当前会话，但未能保存到文件。";
                }
                
                Messages.Message(successMsg, MessageTypeDefOf.PositiveEvent);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[Dialog_MultimodalPersonaGeneration] 人格创建成功: {personaName}");
                    Log.Message($"  - 个性标签: {string.Join(", ", newPersona.personalityTags ?? new List<string>())}");
                    Log.Message($"  - 用户特质: {string.Join(", ", newPersona.selectedTraits ?? new List<string>())}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_MultimodalPersonaGeneration] 创建人格失败: {ex}");
                Messages.Message($"创建人格失败: {ex.Message}", MessageTypeDefOf.RejectInput);
            }
        }

        /// <summary>
        /// 构建完整的biography（用户输入 + AI分析）
        /// </summary>
        private string BuildFullBiography(PersonaAnalysisResult analysisResult)
        {
            var parts = new List<string>();
            
            // 1. 用户输入部分
            parts.Add("【用户描述】");
            parts.Add(BuildFullUserInput());
            parts.Add("");
            
            // 2. AI视觉分析部分
            if (!string.IsNullOrEmpty(analysisResult.VisualDescription))
            {
                parts.Add("【AI视觉分析】");
                parts.Add(analysisResult.VisualDescription);
                parts.Add("");
            }
            
            // 3. 完整生成的biography
            if (!string.IsNullOrEmpty(analysisResult.GeneratedBiography))
            {
                parts.Add("【综合分析】");
                parts.Add(analysisResult.GeneratedBiography);
            }
            
            return string.Join("\n", parts);
        }

        /// <summary>
        /// 从分析结果提取主色调
        /// </summary>
        private Color ExtractPrimaryColor(PersonaAnalysisResult analysisResult)
        {
            // TODO: 从 analysisResult.VisualTags 或其他字段提取颜色
            return Color.white;
        }

        /// <summary>
        /// 从分析结果提取重音色
        /// </summary>
        private Color ExtractAccentColor(PersonaAnalysisResult analysisResult)
        {
            // TODO: 从 analysisResult.VisualTags 或其他字段提取颜色
            return Color.gray;
        }
    }
}

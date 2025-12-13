using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.UI
{
    public class Dialog_PersonaGenerationSettings : Window
    {
        private string personaName = "";
        private string personaBio = "";
        private Vector2 scrollPosition = Vector2.zero;
        private bool isGenerating = false;
        private string statusMessage = "";
        
        private const float WINDOW_WIDTH = 800f;
        private const float WINDOW_HEIGHT = 600f;
        private const float MARGIN = 20f;
        private const float BUTTON_HEIGHT = 35f;
        private const float INPUT_HEIGHT = 30f;
        
        public Dialog_PersonaGenerationSettings()
        {
            this.doCloseX = true;
            this.doCloseButton = false;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Widgets.Label(titleRect, "创建自定义人格");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect contentRect = new Rect(MARGIN, 50f, inRect.width - MARGIN * 2, inRect.height - 110f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 500f);
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            
            string instructionText = "请描述你想要的AI人格特征。你可以描述性格、说话方式、外貌特征等。\n\n" +
                                   "例如：\"一个温柔体贴的女性AI，说话轻声细语，喜欢照顾别人...\"";
            float instructionHeight = Text.CalcHeight(instructionText, viewRect.width);
            Rect instructionRect = new Rect(0f, curY, viewRect.width, instructionHeight);
            Widgets.Label(instructionRect, instructionText);
            curY += instructionHeight + 20f;
            
            Widgets.Label(new Rect(0f, curY, 150f, INPUT_HEIGHT), "人格名称:");
            Rect nameRect = new Rect(150f, curY, viewRect.width - 150f, INPUT_HEIGHT);
            personaName = Widgets.TextField(nameRect, personaName);
            curY += INPUT_HEIGHT + 10f;
            
            Widgets.Label(new Rect(0f, curY, 150f, INPUT_HEIGHT), "人格描述:");
            curY += INPUT_HEIGHT + 5f;
            
            Rect bioRect = new Rect(0f, curY, viewRect.width, 200f);
            personaBio = Widgets.TextArea(bioRect, personaBio);
            curY += 210f;
            
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUI.color = isGenerating ? Color.yellow : Color.green;
                Rect statusRect = new Rect(0f, curY, viewRect.width, 30f);
                Widgets.Label(statusRect, statusMessage);
                GUI.color = Color.white;
            }
            
            Widgets.EndScrollView();
            
            Rect buttonRect = new Rect(MARGIN, inRect.height - 50f, 150f, BUTTON_HEIGHT);
            
            if (Widgets.ButtonText(buttonRect, "生成人格"))
            {
                if (ValidateInput())
                {
                    GeneratePersona();
                }
            }
            
            buttonRect.x += 160f;
            if (Widgets.ButtonText(buttonRect, "取消"))
            {
                this.Close();
            }
            
            buttonRect.x += 160f;
            if (Widgets.ButtonText(buttonRect, "查看示例"))
            {
                ShowExamples();
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(personaName))
            {
                statusMessage = "错误: 请输入人格名称";
                Messages.Message("请输入人格名称", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(personaBio))
            {
                statusMessage = "错误: 请输入人格描述";
                Messages.Message("请输入人格描述", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            if (personaBio.Length < 20)
            {
                statusMessage = "错误: 人格描述太短，请至少输入20个字符";
                Messages.Message("人格描述太短，请输入更详细的描述", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            return true;
        }

        private void GeneratePersona()
        {
            if (isGenerating) return;
            
            isGenerating = true;
            statusMessage = "正在生成人格，请稍候...";
            
            try
            {
                // 1. ✅ 创建基础人格定义
                NarratorPersonaDef newPersona = new NarratorPersonaDef();
                newPersona.defName = $"UserGenerated_{personaName.Replace(" ", "_")}_{DateTime.Now.Ticks}";
                newPersona.narratorName = personaName;
                newPersona.biography = personaBio;  // 临时保存，后续会被多模态分析增强
                newPersona.label = personaName;
                newPersona.primaryColor = Color.white;
                newPersona.accentColor = Color.gray;
                newPersona.enabled = true;
                
                // 2. ✅ 默认对话风格（会被多模态分析覆盖）
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
                
                newPersona.eventPreferences = new EventPreferencesDef
                {
                    positiveEventBias = 0.0f,
                    negativeEventBias = 0.0f,
                    chaosLevel = 0.5f,
                    interventionFrequency = 0.5f
                };
                
                // 3. ✅ 多模态分析（如果启用）
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                bool useMultimodalAnalysis = modSettings?.enableMultimodalAnalysis ?? false;
                
                if (useMultimodalAnalysis)
                {
                    // ✅ 尝试从用户上传的立绘文件进行多模态分析
                    // 注意：这里假设用户已经将立绘放置在正确的文件夹中
                    // 路径：Textures/UI/Narrators/9x16/{人格名}/base.png
                    
                    string portraitPath = $"UI/Narrators/9x16/{personaName}/base";
                    Texture2D portraitTexture = ContentFinder<Texture2D>.Get(portraitPath, false);
                    
                    if (portraitTexture != null)
                    {
                        try
                        {
                            // ✅ 调用多模态分析服务
                            var analysisService = new MultimodalAnalysisService();
                            
                            // ✅ 将用户输入的简介作为额外提示传递给分析服务
                            var analysisResult = analysisService.AnalyzePersonaImage(
                                portraitTexture, 
                                personaName,
                                userBio: personaBio  // 传递用户输入的简介
                            );
                            
                            if (analysisResult != null)
                            {
                                // ✅ 使用分析结果增强人格定义
                                newPersona.SetAnalysis(analysisResult);
                                
                                // ✅ 更新 biography：结合 AI 分析和用户输入
                                if (!string.IsNullOrEmpty(analysisResult.GeneratedBiography))
                                {
                                    newPersona.biography = $"{personaBio}\n\n[AI 分析补充]\n{analysisResult.GeneratedBiography}";
                                }
                                
                                // ✅ 更新视觉描述
                                newPersona.visualDescription = analysisResult.VisualDescription;
                                newPersona.visualElements = analysisResult.VisualTags;
                                
                                // ✅ 更新对话风格（基于分析结果）
                                if (analysisResult.SuggestedDialogueStyle != null)
                                {
                                    newPersona.dialogueStyle = analysisResult.SuggestedDialogueStyle;
                                }
                                
                                statusMessage = "✅ 多模态分析完成！人格数据已生成";
                                
                                if (Prefs.DevMode)
                                {
                                    Log.Message($"[Dialog_PersonaGenerationSettings] 多模态分析成功：{analysisResult.VisualTags.Count} 个视觉标签");
                                }
                            }
                            else
                            {
                                Log.Warning("[Dialog_PersonaGenerationSettings] 多模态分析返回 null，使用默认配置");
                            }
                        }
                        catch (Exception analysisEx)
                        {
                            Log.Warning($"[Dialog_PersonaGenerationSettings] 多模态分析失败（使用默认配置）: {analysisEx.Message}");
                            statusMessage = "⚠️ 多模态分析失败，使用默认配置";
                        }
                    }
                    else
                    {
                        Log.Message($"[Dialog_PersonaGenerationSettings] 未找到立绘文件：{portraitPath}，跳过多模态分析");
                        statusMessage = "⚠️ 未找到立绘文件，跳过多模态分析";
                    }
                }
                else
                {
                    Log.Message("[Dialog_PersonaGenerationSettings] 多模态分析未启用，跳过");
                }
                
                // 4. ✅ 注册到 DefDatabase（本次游戏会话立即可用）
                if (!DefDatabase<NarratorPersonaDef>.AllDefs.Contains(newPersona))
                {
                    DefDatabase<NarratorPersonaDef>.Add(newPersona);
                }
                
                // 5. ✅ 永久保存到文件（使用 PersonaDefExporter）
                bool exportSuccess = false;
                try
                {
                    // 获取立绘纹理（如果存在）
                    string portraitPath = $"UI/Narrators/9x16/{personaName}/base";
                    Texture2D portraitForExport = ContentFinder<Texture2D>.Get(portraitPath, false);
                    
                    // ✅ 修复参数顺序
                    // ExportPersona(NarratorPersonaDef persona, string sourcePortraitPath, Texture2D texture)
                    exportSuccess = PersonaDefExporter.ExportPersona(
                        newPersona,              // persona
                        portraitPath,           // sourcePortraitPath (ContentFinder 路径)
                        portraitForExport       // texture
                    );
                }
                catch (Exception exportEx)
                {
                    Log.Warning($"[Dialog_PersonaGenerationSettings] 保存人格文件失败（但已加载到游戏中）: {exportEx.Message}");
                }
                
                // 6. ✅ 显示成功消息
                statusMessage = $"成功！人格 '{personaName}' 已创建";
                
                string successMsg = $"人格 '{personaName}' 创建成功！\n\n";
                if (exportSuccess)
                {
                    successMsg += "✅ 人格已保存到 Defs 文件夹，重启游戏后永久生效。";
                }
                else
                {
                    successMsg += "⚠️ 人格已加载到当前会话，但未能保存到文件。\n重启游戏后将丢失，请手动备份。";
                }
                
                if (useMultimodalAnalysis)
                {
                    successMsg += "\n\n💡 提示：AI 已根据你的描述和立绘生成了完整的人格数据。";
                }
                
                Messages.Message(successMsg, MessageTypeDefOf.PositiveEvent);
                
                // 7. 延迟关闭窗口
                System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => {
                    this.Close();
                });
            }
            catch (Exception ex)
            {
                statusMessage = $"错误: {ex.Message}";
                Messages.Message($"生成人格失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                Log.Error($"[The Second Seat] 生成人格失败: {ex}");
            }
            finally
            {
                isGenerating = false;
            }
        }

        private void ShowExamples()
        {
            string examples = "示例1 - 温柔型:\n" +
                            "名称: 小雪\n" +
                            "描述: 一个温柔体贴的女性AI，说话轻声细语，喜欢照顾别人。她有一头银白色的长发，猩红色的眼眸总是充满关怀。\n\n" +
                            "示例2 - 活泼型:\n" +
                            "名称: 小橙\n" +
                            "描述: 一个活泼开朗的AI，说话俏皮可爱，充满能量。她喜欢用emoji和颜文字，总是带着灿烂的笑容。\n\n" +
                            "示例3 - 严肃型:\n" +
                            "名称: 博士\n" +
                            "描述: 一个严谨理性的AI，说话简洁明了，注重效率。他总是以事实和数据为依据，给出最优化的建议。";
            
            Find.WindowStack.Add(new Dialog_MessageBox(examples, "确定", null, null, null, "人格描述示例"));
        }
    }
}

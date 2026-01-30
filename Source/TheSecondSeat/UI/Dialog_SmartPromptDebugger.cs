using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Linq;
using System.Collections.Generic;
using TheSecondSeat.SmartPrompt;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// SmartPrompt 调试器
    /// 测试意图识别，查看模块激活状态
    /// </summary>
    public class Dialog_SmartPromptDebugger : Window
    {
        private string testInput = "";
        private Vector2 leftScrollPosition = Vector2.zero;
        private Vector2 rightScrollPosition = Vector2.zero;
        private List<string> matchedIntents = new List<string>();
        private List<PromptModuleDef> matchedModules = new List<PromptModuleDef>();
        private string generatedPromptPreview = "";
        
        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Dialog_SmartPromptDebugger()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = false; // 允许查看其他内容
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "SmartPrompt 调试器");
            Text.Font = GameFont.Small;

            // 输入区域
            float y = 40f;
            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "测试输入 (模拟玩家发言):");
            y += 24f;
            
            testInput = Widgets.TextArea(new Rect(0f, y, inRect.width - 120f, 60f), testInput);
            
            if (Widgets.ButtonText(new Rect(inRect.width - 110f, y, 110f, 60f), "分析 & 生成"))
            {
                RunAnalysis();
            }
            y += 70f;

            // 分割线
            Widgets.DrawLineHorizontal(0f, y, inRect.width);
            y += 10f;

            // 结果区域 (左右分栏)
            float colWidth = (inRect.width - 20f) / 2f;
            float height = inRect.height - y - 10f;
            
            // 左栏：意图与模块
            Rect leftRect = new Rect(0f, y, colWidth, height);
            DrawLeftPanel(leftRect);
            
            // 右栏：生成的 Prompt 预览
            Rect rightRect = new Rect(colWidth + 20f, y, colWidth, height);
            DrawRightPanel(rightRect);
        }

        private void RunAnalysis()
        {
            if (string.IsNullOrEmpty(testInput)) return;

            // 1. 分析意图
            matchedIntents = SmartPromptIntegration.AnalyzeIntents(testInput);

            // 2. 路由模块
            var routeResult = IntentRouter.Instance.Route(testInput);
            matchedModules = routeResult.Success ? routeResult.Modules : new List<PromptModuleDef>();

            // 3. 生成 Prompt 预览
            // 注意：这里没有上下文，所以 Scriban 渲染可能不完整
            var buildResult = SmartPromptBuilder.Instance.Build(testInput);
            generatedPromptPreview = buildResult.Success ? buildResult.Prompt : $"Error: {buildResult.Error}";
        }

        private void DrawLeftPanel(Rect rect)
        {
            Widgets.BeginGroup(rect);
            float y = 0f;
            
            // 意图
            Widgets.Label(new Rect(0f, y, rect.width, 24f), $"<b>识别到的意图 ({matchedIntents.Count}):</b>");
            y += 24f;
            
            if (matchedIntents.Count > 0)
            {
                string intentsStr = string.Join(", ", matchedIntents);
                Widgets.Label(new Rect(10f, y, rect.width - 10f, 40f), intentsStr);
                y += 40f;
            }
            else
            {
                Widgets.Label(new Rect(10f, y, rect.width - 10f, 24f), "(无)");
                y += 24f;
            }
            y += 10f;

            // 模块
            Widgets.Label(new Rect(0f, y, rect.width, 24f), $"<b>激活的模块 ({matchedModules.Count}):</b>");
            y += 24f;

            Rect listRect = new Rect(0f, y, rect.width, rect.height - y);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, matchedModules.Count * 24f);
            
            Widgets.BeginScrollView(listRect, ref leftScrollPosition, viewRect);
            float ly = 0f;
            foreach (var module in matchedModules)
            {
                GUI.color = module.alwaysActive ? Color.gray : Color.white;
                Widgets.Label(new Rect(5f, ly, viewRect.width, 24f), $"{module.defName} [{module.moduleType}]");
                ly += 24f;
            }
            GUI.color = Color.white;
            Widgets.EndScrollView();

            Widgets.EndGroup();
        }

        private void DrawRightPanel(Rect rect)
        {
            Widgets.BeginGroup(rect);
            
            Widgets.Label(new Rect(0f, 0f, rect.width, 24f), "<b>生成的 Prompt 片段预览:</b>");
            
            Rect outRect = new Rect(0f, 30f, rect.width, rect.height - 30f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Text.CalcHeight(generatedPromptPreview, outRect.width - 16f) + 100f);
            if (viewRect.height < outRect.height) viewRect.height = outRect.height;

            Widgets.BeginScrollView(outRect, ref rightScrollPosition, viewRect);
            Widgets.Label(viewRect, generatedPromptPreview);
            Widgets.EndScrollView();
            
            Widgets.EndGroup();
        }
    }
}
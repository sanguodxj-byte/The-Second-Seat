using System;
using TheSecondSeat.Core.Components;
using TheSecondSeat.Monitoring;
using TheSecondSeat.Narrator;
using Verse;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// ⭐ v3.0.0: 叙事者主控制器（GameComponent）
    /// 
    /// 职责：管理游戏生命周期和叙事者组件协调
    /// 核心 AI 逻辑已委托给 NarratorAgent
    /// </summary>
    public class NarratorController : GameComponent
    {
        private NarratorManager? narratorManager;
        
        // Components
        private NarratorAssetLoader assetLoader;
        private NarratorExpressionController expressionController;
        private NarratorTTSHandler ttsHandler;
        
        // ⭐ v3.0.0: 核心 Agent（原 NarratorUpdateService）
        private NarratorAgent agent;
        
        // 首次加载标记（只在游戏加载时触发一次问候）
        private bool hasGreetedOnLoad = false;
        private int ticksSinceLoad = 0;
        private const int GreetingDelayTicks = 300; // 加载后5秒再发送问候
        
        // Expose properties for compatibility
        public string LastDialogue => agent?.LastDialogue ?? "";
        public bool IsProcessing => agent?.IsProcessing ?? false;
        public string LastError => agent?.LastError ?? "";
        
        /// <summary>
        /// 获取当前叙事者人格的 defName（静态属性，供外部访问）
        /// </summary>
        public static string? CurrentPersonaDefName
        {
            get
            {
                try
                {
                    var controller = Current.Game?.GetComponent<NarratorController>();
                    if (controller?.narratorManager != null)
                    {
                        var persona = controller.narratorManager.GetCurrentPersona();
                        return persona?.defName;
                    }
                }
                catch (Exception)
                {
                    // 静默处理异常
                }
                return null;
            }
        }

        public NarratorController(Game game) : base()
        {
            assetLoader = new NarratorAssetLoader();
            expressionController = new NarratorExpressionController();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            
            // 清除旧的聊天记录（防止跨存档污染）
            TheSecondSeat.UI.NarratorWindow.ClearChatHistory();

            if (narratorManager == null)
            {
                narratorManager = Current.Game.GetComponent<NarratorManager>();
            }
            
            ttsHandler = new NarratorTTSHandler(narratorManager);
            agent = new NarratorAgent(narratorManager, expressionController, ttsHandler);

            // Initialize LogListenerService for event-driven error monitoring
            LogListenerService.Instance.Initialize(NotifyRuntimeError);

            // Preload assets
            LongEventHandler.QueueLongEvent(() => 
            {
                if (narratorManager != null)
                {
                    assetLoader.PreloadAssetsOnMainThread();
                }
            }, "TSS_PreloadingAssets", false, null);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Ensure components are initialized
            if (narratorManager == null)
            {
                narratorManager = Current.Game.GetComponent<NarratorManager>();
                if (agent == null)
                {
                    ttsHandler = new NarratorTTSHandler(narratorManager);
                    agent = new NarratorAgent(narratorManager, expressionController, ttsHandler);
                }
            }

            // Expression Scheduling
            expressionController.Tick();
            
            // Initial Greeting Logic
            if (!hasGreetedOnLoad)
            {
                ticksSinceLoad++;
                if (ticksSinceLoad >= GreetingDelayTicks && !IsProcessing)
                {
                    hasGreetedOnLoad = true;
                    TriggerLoadGreeting();
                }
            }
        }

        /// <summary>
        /// 首次加载问候（只触发一次）
        /// </summary>
        private void TriggerLoadGreeting()
        {
            Log.Message("[NarratorController] 发送加载问候...");
            agent.TriggerUpdate("", hasGreetedOnLoad: false);
        }

        /// <summary>
        /// Manually trigger a narrator update
        /// </summary>
        public void TriggerNarratorUpdate(string userMessage = "")
        {
            agent.TriggerUpdate(userMessage, hasGreetedOnLoad: true);
        }

        /// <summary>
        /// Callback for LogListenerService when a runtime error occurs
        /// </summary>
        public void NotifyRuntimeError(string condition, string stackTrace)
        {
            if (IsProcessing) return;

            Log.Message($"[NarratorController] Event-driven error detected: {condition}");

            string alertMessage = $"[SYSTEM ALERT] A runtime error has been detected: \"{condition}\". " +
                                  "Please use the 'analyze_last_error' tool to investigate the cause. " +
                                  "If it looks like a configuration typo (e.g. in XML), try to fix it using 'patch_file'. " +
                                  "If you cannot fix it, briefly explain the issue to the player.";

            agent.TriggerUpdate(alertMessage, hasGreetedOnLoad: true);
        }
    }
}

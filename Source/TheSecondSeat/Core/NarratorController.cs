using System;
using TheSecondSeat.Core.Components;
using TheSecondSeat.Monitoring;
using TheSecondSeat.Narrator;
using Verse;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// Main controller that orchestrates the AI narrator loop
    /// Refactored to delegate responsibilities to specialized components.
    /// </summary>
    public class NarratorController : GameComponent
    {
        private NarratorManager? narratorManager;
        
        // Components
        private NarratorAssetLoader assetLoader;
        private NarratorExpressionController expressionController;
        // private NarratorRuntimeMonitor runtimeMonitor; // Removed in favor of LogListenerService
        private NarratorTTSHandler ttsHandler;
        private NarratorUpdateService updateService;
        
        // ? 首次加载标记（只在游戏加载时触发一次问候）
        private bool hasGreetedOnLoad = false;
        private int ticksSinceLoad = 0;
        private const int GreetingDelayTicks = 300; // 加载后5秒再发送问候
        
        // Expose properties for compatibility
        public string LastDialogue => updateService.LastDialogue;
        public bool IsProcessing => updateService.IsProcessing;
        public string LastError => updateService.LastError;
        
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
                catch { }
                return null;
            }
        }

        public NarratorController(Game game) : base()
        {
            // Initialize components
            assetLoader = new NarratorAssetLoader();
            expressionController = new NarratorExpressionController();
            
            // Runtime monitor needs a callback to trigger updates
            // runtimeMonitor = new NarratorRuntimeMonitor(TriggerNarratorUpdate);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            
            // Lazy initialization of components that depend on Game state or other components
            if (narratorManager == null)
            {
                narratorManager = Current.Game.GetComponent<NarratorManager>();
            }
            
            ttsHandler = new NarratorTTSHandler(narratorManager);
            updateService = new NarratorUpdateService(narratorManager, expressionController, ttsHandler);

            // Initialize LogListenerService for event-driven error monitoring
            LogListenerService.Instance.Initialize(NotifyRuntimeError);

            // Preload assets using LongEventHandler to avoid blocking the main thread during tick
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

            // Ensure components are initialized if FinalizeInit wasn't called or failed
            if (narratorManager == null)
            {
                narratorManager = Current.Game.GetComponent<NarratorManager>();
                if (updateService == null)
                {
                    ttsHandler = new NarratorTTSHandler(narratorManager);
                    updateService = new NarratorUpdateService(narratorManager, expressionController, ttsHandler);
                }
            }
            
            // 1. Asset Preloading - Moved to FinalizeInit/LongEventHandler
            // if (!assetLoader.HasPreloadedAssets && narratorManager != null)
            // {
            //     assetLoader.PreloadAssetsOnMainThread();
            // }

            // 2. Expression Scheduling
            expressionController.Tick();
            
            // 3. Runtime Error Monitoring - Replaced by LogListenerService
            // runtimeMonitor.Tick(IsProcessing);
            
            // 4. Initial Greeting Logic
            if (!hasGreetedOnLoad)
            {
                ticksSinceLoad++;
                if (ticksSinceLoad >= GreetingDelayTicks && !IsProcessing)
                {
                    hasGreetedOnLoad = true;
                    // ? 发送加载问候（只触发一次）
                    TriggerLoadGreeting();
                }
            }
        }

        /// <summary>
        /// ? 首次加载问候（只触发一次）
        /// </summary>
        private void TriggerLoadGreeting()
        {
            Log.Message("[NarratorController] 发送加载问候...");
            updateService.TriggerNarratorUpdate("", hasGreetedOnLoad: false);
        }

        /// <summary>
        /// Manually trigger a narrator update
        /// </summary>
        public void TriggerNarratorUpdate(string userMessage = "")
        {
            updateService.TriggerNarratorUpdate(userMessage, hasGreetedOnLoad: true); // Assume greeted if manually triggered, unless it's the greeting itself
        }

        /// <summary>
        /// Callback for LogListenerService when a runtime error occurs
        /// </summary>
        public void NotifyRuntimeError(string condition, string stackTrace)
        {
            // 如果 AI 正在处理中，跳过，避免打断
            if (IsProcessing) return;

            // 简单的防抖动或重复检查可以加在这里，但 LogListenerService 可能已经做了一些过滤
            // 这里我们直接构建警报消息

            Log.Message($"[NarratorController] Event-driven error detected: {condition}");

            string alertMessage = $"[SYSTEM ALERT] A runtime error has been detected: \"{condition}\". " +
                                  "Please use the 'analyze_last_error' tool to investigate the cause. " +
                                  "If it looks like a configuration typo (e.g. in XML), try to fix it using 'patch_file'. " +
                                  "If you cannot fix it, briefly explain the issue to the player.";

            // Trigger update on the main thread if needed, though LogListener callback comes from Unity main thread usually
            // but just to be safe and consistent
            updateService.TriggerNarratorUpdate(alertMessage, hasGreetedOnLoad: true);
        }
    }
}

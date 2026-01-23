using System;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Utils;
using Verse;

namespace TheSecondSeat.Core.Components
{
    /// <summary>
    /// Handles preloading of narrator assets on the main thread
    /// </summary>
    public class NarratorAssetLoader
    {
        private bool hasPreloadedAssets = false;

        public bool HasPreloadedAssets => hasPreloadedAssets;

        /// <summary>
        /// ⭐ v1.6.82: 在主线程预加载所有纹理资源
        /// 避免首次显示时的卡顿
        /// </summary>
        public void PreloadAssetsOnMainThread()
        {
            if (hasPreloadedAssets) return;
            hasPreloadedAssets = true;

            try
            {
                // 初始化主线程 ID
                TSS_AssetLoader.InitializeMainThread();
                
                // 获取所有已加载的叙事者人格
                var allPersonas = DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
                int preloadedCount = 0;
                
                foreach (var persona in allPersonas)
                {
                    if (persona == null) continue;
                    
                    // 预加载立绘
                    if (!string.IsNullOrEmpty(persona.portraitPath))
                    {
                        TSS_AssetLoader.LoadTexture(persona.portraitPath);
                    }
                    
                    // 预加载分层立绘配置
                    if (persona.useLayeredPortrait)
                    {
                        var config = persona.GetLayeredConfig();
                        if (config != null)
                        {
                            // 预加载所有表情的 base_body
                            LayeredPortraitCompositor.PreloadAllExpressions(config);
                        }
                    }
                    
                    // 预加载降临姿态
                    if (persona.hasDescentMode)
                    {
                        string personaName = persona.narratorName?.Split(' ')[0] ?? persona.defName;
                        
                        if (persona.descentPostures != null)
                        {
                            if (!string.IsNullOrEmpty(persona.descentPostures.standing))
                            {
                                TSS_AssetLoader.LoadDescentPosture(personaName, persona.descentPostures.standing);
                            }
                            if (!string.IsNullOrEmpty(persona.descentPostures.floating))
                            {
                                TSS_AssetLoader.LoadDescentPosture(personaName, persona.descentPostures.floating);
                            }
                            if (!string.IsNullOrEmpty(persona.descentPostures.combat))
                            {
                                TSS_AssetLoader.LoadDescentPosture(personaName, persona.descentPostures.combat);
                            }
                        }
                    }
                    
                    preloadedCount++;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorController] ⭐ 主线程预加载完成: {preloadedCount} 个叙事者人格");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 预加载资源失败: {ex.Message}");
            }
        }
    }
}

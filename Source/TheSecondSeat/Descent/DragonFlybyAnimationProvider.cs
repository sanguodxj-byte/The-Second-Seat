using System;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ⭐ v1.6.82: 实体飞掠动画提供者（主Mod内置）
    ///
    /// 使用方式（纯XML配置）：
    /// <![CDATA[
    /// <NarratorPersonaDef>
    ///     <defName>YourPersona_Name</defName>
    ///     <descentAnimationType>DragonFlyby</descentAnimationType>
    ///     <dragonShadowTexturePath>UI/Narrators/Descent/Effects/YourPersona/entity_shadow</dragonShadowTexturePath>
    ///     <descentSound>YourPersona_DescentSound</descentSound>
    /// </NarratorPersonaDef>
    /// ]]>
    ///
    /// 子Mod只需要在 Textures/ 文件夹中提供龙影纹理即可
    /// </summary>
    [StaticConstructorOnStartup]
    public class DragonFlybyAnimationProvider : IDescentAnimationProvider
    {
        // ==================== 接口属性 ====================
        
        public string AnimationType => "DragonFlyby";
        public float AnimationDuration => 4.0f; // 4秒飞掠动画
        public bool IsPlaying => isPlaying;
        
        // ==================== 状态字段 ====================
        
        private bool isPlaying = false;
        private float elapsedTime = 0f;
        private Action onCompleteCallback;
        
        private Map currentMap;
        private IntVec3 targetLocation;
        private NarratorPersonaDef currentPersona;
        private bool isHostile;
        
        // ==================== 特效渲染器 ====================
        
        private DescentEffectRenderer effectRenderer = new DescentEffectRenderer();
        
        // ==================== 静态初始化 ====================
        
        static DragonFlybyAnimationProvider()
        {
            // ⭐ 自动注册到注册表
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                DescentAnimationRegistry.Register(new DragonFlybyAnimationProvider());
                Log.Message("[DragonFlybyAnimationProvider] 已自动注册到降临动画注册表");
            });
        }
        
        // ==================== 接口方法 ====================
        
        public void StartAnimation(Map map, IntVec3 targetLoc, NarratorPersonaDef persona, bool hostile, Action onComplete = null)
        {
            Log.Message($"[DragonFlybyAnimationProvider] 收到动画请求: 目标={targetLoc}, 敌对={hostile}, Persona={persona?.defName}");

            if (isPlaying)
            {
                // 动画正在播放中，忽略新请求 (静默)
                return;
            }
            
            // 保存状态
            currentMap = map;
            targetLocation = targetLoc;
            currentPersona = persona;
            isHostile = hostile;
            onCompleteCallback = onComplete;
            
            isPlaying = true;
            elapsedTime = 0f;
            
            try
            {
                // 1. 加载实体阴影纹理（从人格配置读取路径）
                // ⭐ 修复线程问题：确保在主线程加载纹理
                LoadEntityShadowTexture(persona);

                // 2. 播放降临音效
                PlayDescentSound(persona, map);

                // 3. 启动实体阴影渲染器
                StartEntityShadowAnimation(map, targetLoc);
                
                // 4. 播放预兆特效（魔法阵）
                effectRenderer.PlayMagicCircle(targetLoc, hostile);
                
                Log.Message($"[DragonFlybyAnimationProvider] 动画启动序列完成");
            }
            catch (Exception ex)
            {
                Log.Error($"[DragonFlybyAnimationProvider] 启动动画失败: {ex}");
                StopAnimation();
            }
        }
        
        public void StopAnimation()
        {
            isPlaying = false;
            elapsedTime = 0f;
            
            // 停止实体阴影渲染器
            if (currentMap != null)
            {
                var shadowRenderer = DragonShadowRenderer.GetRenderer(currentMap);
                shadowRenderer?.StopAnimation();
            }
            
            onCompleteCallback = null;
            currentMap = null;
            currentPersona = null;
        }
        
        public void Update(float deltaTime)
        {
            if (!isPlaying) return;
            
            elapsedTime += deltaTime;
            
            // 更新飞掠进度
            float progress = elapsedTime / AnimationDuration;
            
            // 在飞掠过程中播放阴影效果
            if (currentMap != null)
            {
                effectRenderer.PlayEntityShadow(currentMap, targetLocation, progress);
            }
            
            // 检查动画是否完成
            if (elapsedTime >= AnimationDuration)
            {
                CompleteAnimation();
            }
        }
        
        // ==================== 私有方法 ====================
        
        /// <summary>
        /// 加载实体阴影纹理
        /// </summary>
        private void LoadEntityShadowTexture(NarratorPersonaDef persona)
        {
            if (persona == null) return;
            
            string texturePath = persona.GetDragonShadowFullPath();
            
            if (!string.IsNullOrEmpty(texturePath))
            {
                Log.Message($"[DragonFlybyAnimationProvider] 准备加载实体阴影纹理，路径: '{texturePath}'");
                
                // ⭐ 修复线程安全问题：纹理加载必须在主线程执行
                // 降临序列可能在后台线程触发
                LongEventHandler.ExecuteWhenFinished(() => 
                {
                    bool loaded = DragonShadowRenderer.LoadCustomTexture(texturePath);
                    if (loaded)
                    {
                        Log.Message($"[DragonFlybyAnimationProvider] 加载实体阴影纹理: {texturePath}");
                    }
                    else
                    {
                        // 纹理未找到，静默失败（使用备用效果）
                    }
                });
            }
            else
            {
                Log.Message("[DragonFlybyAnimationProvider] 未配置实体阴影纹理路径，使用备用粒子效果");
            }
        }
        
        /// <summary>
        /// 播放降临音效
        /// </summary>
        private void PlayDescentSound(NarratorPersonaDef persona, Map map)
        {
            if (persona == null || map == null) return;
            
            string soundDefName = persona.descentSound;
            
            if (!string.IsNullOrEmpty(soundDefName))
            {
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName);
                if (soundDef != null)
                {
                    try
                    {
                        SoundStarter.PlayOneShotOnCamera(soundDef, map);
                        Log.Message($"[DragonFlybyAnimationProvider] 播放音效: {soundDefName}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[DragonFlybyAnimationProvider] 播放音效失败: {ex.Message}");
                    }
                }
                else
                {
                    // 音效未找到，静默
                }
            }
        }
        
        /// <summary>
        /// 启动实体阴影渲染动画
        /// </summary>
        private void StartEntityShadowAnimation(Map map, IntVec3 target)
        {
            if (map == null)
            {
                // 地图为空，静默
                return;
            }
            
            var shadowRenderer = DragonShadowRenderer.GetRenderer(map);
            
            // ⭐ 如果组件不存在，动态创建并添加到地图
            if (shadowRenderer == null)
            {
                Log.Message("[DragonFlybyAnimationProvider] DragonShadowRenderer 组件不存在，正在动态创建...");
                shadowRenderer = new DragonShadowRenderer(map);
                map.components.Add(shadowRenderer);
                Log.Message("[DragonFlybyAnimationProvider] ✅ DragonShadowRenderer 组件已动态添加到地图");
            }
            
            // 启动动画
            shadowRenderer.StartAnimation(target, AnimationDuration * 0.8f); // 阴影动画稍短
            Log.Message($"[DragonFlybyAnimationProvider] 已启动阴影动画，HasTexture={DragonShadowRenderer.HasCustomTexture}");
        }
        
        /// <summary>
        /// 动画完成处理
        /// </summary>
        private void CompleteAnimation()
        {
            isPlaying = false;
            
            // 播放降临完成特效
            if (currentMap != null && targetLocation.IsValid && targetLocation.InBounds(currentMap))
            {
                effectRenderer.PlayDescentCompleteEffect(targetLocation, isHostile);
            }
            
            // 触发回调
            onCompleteCallback?.Invoke();
            onCompleteCallback = null;
            
            // 清理状态
            currentMap = null;
            currentPersona = null;
            
            Log.Message("[DragonFlybyAnimationProvider] 实体飞掠动画完成");
        }
    }
}

using System;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ⭐ v1.6.82: 闪电降临动画提供者（主Mod内置）
    /// 
    /// 使用多次闪电打击效果，配合雷鸣音效
    /// 
    /// 使用方式（纯XML配置）：
    /// <![CDATA[
    /// <NarratorPersonaDef>
    ///     <defName>MyNarrator_Persona</defName>
    ///     <descentAnimationType>Lightning</descentAnimationType>
    ///     <descentSound>Thunder_OnMap</descentSound>
    /// </NarratorPersonaDef>
    /// ]]>
    /// </summary>
    [StaticConstructorOnStartup]
    public class LightningAnimationProvider : IDescentAnimationProvider
    {
        // ==================== 接口属性 ====================
        
        public string AnimationType => "Lightning";
        public float AnimationDuration => 3.5f; // 3.5秒闪电动画
        public bool IsPlaying => isPlaying;
        
        // ==================== 状态字段 ====================
        
        private bool isPlaying = false;
        private float elapsedTime = 0f;
        private Action onCompleteCallback;
        
        private Map currentMap;
        private IntVec3 targetLocation;
        private NarratorPersonaDef currentPersona;
        private bool isHostile;
        
        // ==================== 闪电配置 ====================
        
        private const int LIGHTNING_COUNT = 5;           // 闪电次数
        private const float LIGHTNING_INTERVAL = 0.5f;   // 闪电间隔
        private const float MAIN_STRIKE_TIME = 2.5f;     // 主闪电时间
        
        private int lightningStrikeCount = 0;
        private float nextStrikeTime = 0f;
        
        // ==================== 特效渲染器 ====================
        
        private DescentEffectRenderer effectRenderer = new DescentEffectRenderer();
        
        // ==================== 静态初始化 ====================
        
        static LightningAnimationProvider()
        {
            // ⭐ 自动注册到注册表
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                DescentAnimationRegistry.Register(new LightningAnimationProvider());
                Log.Message("[LightningAnimationProvider] 已自动注册到降临动画注册表");
            });
        }
        
        // ==================== 接口方法 ====================
        
        public void StartAnimation(Map map, IntVec3 targetLoc, NarratorPersonaDef persona, bool hostile, Action onComplete = null)
        {
            if (isPlaying)
            {
                Log.Warning("[LightningAnimationProvider] 动画正在播放中，忽略新请求");
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
            lightningStrikeCount = 0;
            nextStrikeTime = 0.3f; // 第一次闪电延迟0.3秒
            
            try
            {
                // 1. 播放远处雷鸣（预兆）
                PlayDistantThunder(map);
                
                // 2. 天空变暗效果（使用粒子模拟）
                SpawnDarkClouds(map, targetLoc);
                
                Log.Message($"[LightningAnimationProvider] 开始闪电降临动画: 目标={targetLoc}, 敌对={hostile}");
            }
            catch (Exception ex)
            {
                Log.Error($"[LightningAnimationProvider] 启动动画失败: {ex}");
                StopAnimation();
            }
        }
        
        public void StopAnimation()
        {
            isPlaying = false;
            elapsedTime = 0f;
            lightningStrikeCount = 0;
            onCompleteCallback = null;
            currentMap = null;
            currentPersona = null;
        }
        
        public void Update(float deltaTime)
        {
            if (!isPlaying) return;
            
            elapsedTime += deltaTime;
            
            // 周围闪电效果（前5次）
            if (lightningStrikeCount < LIGHTNING_COUNT && elapsedTime >= nextStrikeTime)
            {
                SpawnSurroundingLightning();
                lightningStrikeCount++;
                nextStrikeTime = elapsedTime + LIGHTNING_INTERVAL;
            }
            
            // 主闪电（最后一击，直接命中目标点）
            if (elapsedTime >= MAIN_STRIKE_TIME && elapsedTime < MAIN_STRIKE_TIME + deltaTime * 2)
            {
                SpawnMainLightningStrike();
            }
            
            // 检查动画是否完成
            if (elapsedTime >= AnimationDuration)
            {
                CompleteAnimation();
            }
        }
        
        // ==================== 私有方法 ====================
        
        /// <summary>
        /// 播放远处雷鸣
        /// </summary>
        private void PlayDistantThunder(Map map)
        {
            try
            {
                // 使用原版雷鸣音效
                SoundDef thunderDef = DefDatabase<SoundDef>.GetNamedSilentFail("Thunder_OffMap");
                if (thunderDef != null)
                {
                    SoundStarter.PlayOneShotOnCamera(thunderDef, map);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 播放雷鸣失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 生成乌云效果
        /// </summary>
        private void SpawnDarkClouds(Map map, IntVec3 center)
        {
            try
            {
                // 使用烟雾粒子模拟乌云
                for (int i = 0; i < 10; i++)
                {
                    Vector3 offset = Rand.InsideUnitCircleVec3 * 8f;
                    Vector3 cloudPos = center.ToVector3Shifted() + offset;
                    cloudPos.y += 5f; // 抬高位置
                    
                    IntVec3 cloudCell = cloudPos.ToIntVec3();
                    if (cloudCell.InBounds(map))
                    {
                        FleckMaker.ThrowSmoke(cloudPos, map, 4f);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 生成乌云失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 生成周围闪电
        /// </summary>
        private void SpawnSurroundingLightning()
        {
            try
            {
                if (currentMap == null) return;
                
                // 在目标点周围随机位置劈下闪电
                float radius = 5f + lightningStrikeCount * 0.5f;
                float angle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
                
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                IntVec3 strikePos = targetLocation + offset.ToIntVec3();
                
                if (strikePos.InBounds(currentMap))
                {
                    // 生成闪电效果
                    SpawnLightningEffectAt(strikePos, false);
                    
                    // 播放雷鸣
                    PlayThunderSound(currentMap);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 生成周围闪电失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 生成主闪电（命中目标点）
        /// </summary>
        private void SpawnMainLightningStrike()
        {
            try
            {
                if (currentMap == null || !targetLocation.InBounds(currentMap)) return;
                
                // 主闪电命中目标点
                SpawnLightningEffectAt(targetLocation, true);
                
                // 播放近距离雷鸣
                PlayLoudThunder(currentMap);
                
                // 播放配置的音效
                PlayDescentSound();
                
                // 产生冲击波
                effectRenderer.PlayImpactEffect(targetLocation, isHostile);
                
                Log.Message("[LightningAnimationProvider] 主闪电命中目标点");
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 生成主闪电失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 在指定位置生成闪电效果
        /// </summary>
        private void SpawnLightningEffectAt(IntVec3 position, bool isMainStrike)
        {
            try
            {
                if (!position.InBounds(currentMap)) return;
                
                Vector3 drawPos = position.ToVector3Shifted();
                
                // 闪电光芒
                float glowSize = isMainStrike ? 6f : 3f;
                FleckMaker.ThrowLightningGlow(drawPos, currentMap, glowSize);
                
                // 电弧效果
                FleckMaker.ThrowMicroSparks(drawPos, currentMap);
                
                // 地面冲击
                if (isMainStrike)
                {
                    FleckMaker.ThrowDustPuff(position, currentMap, 3f);
                    
                    // 火焰效果（如果是敌对模式）
                    if (isHostile)
                    {
                        FleckMaker.ThrowFireGlow(drawPos, currentMap, 2f);
                    }
                }
                else
                {
                    FleckMaker.ThrowDustPuff(position, currentMap, 1.5f);
                }
                
                // 额外的电弧光芒
                FleckMaker.ThrowLightningGlow(drawPos, currentMap, glowSize * 0.5f);
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 生成闪电效果失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 播放雷鸣音效
        /// </summary>
        private void PlayThunderSound(Map map)
        {
            try
            {
                SoundDef thunderDef = DefDatabase<SoundDef>.GetNamedSilentFail("Thunder_OnMap");
                if (thunderDef != null)
                {
                    SoundStarter.PlayOneShotOnCamera(thunderDef, map);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 播放雷鸣失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 播放近距离雷鸣
        /// </summary>
        private void PlayLoudThunder(Map map)
        {
            try
            {
                // 尝试使用更响亮的雷鸣
                SoundDef thunderDef = DefDatabase<SoundDef>.GetNamedSilentFail("Thunder_OnMap");
                if (thunderDef != null)
                {
                    SoundStarter.PlayOneShotOnCamera(thunderDef, map);
                }
                
                // 再播放一次，增加音量感
                if (thunderDef != null)
                {
                    SoundStarter.PlayOneShotOnCamera(thunderDef, map);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 播放雷鸣失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 播放配置的降临音效
        /// </summary>
        private void PlayDescentSound()
        {
            try
            {
                if (currentPersona == null || string.IsNullOrEmpty(currentPersona.descentSound)) return;
                
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(currentPersona.descentSound);
                if (soundDef != null && currentMap != null)
                {
                    SoundStarter.PlayOneShotOnCamera(soundDef, currentMap);
                    Log.Message($"[LightningAnimationProvider] 播放音效: {currentPersona.descentSound}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LightningAnimationProvider] 播放音效失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 动画完成处理
        /// </summary>
        private void CompleteAnimation()
        {
            isPlaying = false;
            
            // 播放降临完成特效
            if (currentMap != null && targetLocation.InBounds(currentMap))
            {
                effectRenderer.PlayDescentCompleteEffect(targetLocation, isHostile);
                
                // 最后的光芒
                FleckMaker.ThrowLightningGlow(targetLocation.ToVector3Shifted(), currentMap, 4f);
            }
            
            // 触发回调
            onCompleteCallback?.Invoke();
            onCompleteCallback = null;
            
            // 清理状态
            currentMap = null;
            currentPersona = null;
            lightningStrikeCount = 0;
            
            Log.Message("[LightningAnimationProvider] 闪电降临动画完成");
        }
    }
}
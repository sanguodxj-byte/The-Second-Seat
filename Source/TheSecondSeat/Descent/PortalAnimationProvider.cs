using System;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ⭐ v1.6.82: 传送门动画提供者（主Mod内置）
    /// 
    /// 使用原版折跃（Skip）特效实现传送门效果
    /// 
    /// 使用方式（纯XML配置）：
    /// <![CDATA[
    /// <NarratorPersonaDef>
    ///     <defName>MyNarrator_Persona</defName>
    ///     <descentAnimationType>Portal</descentAnimationType>
    ///     <descentSound>Psycast_Skip_Entry</descentSound>
    /// </NarratorPersonaDef>
    /// ]]>
    /// </summary>
    [StaticConstructorOnStartup]
    public class PortalAnimationProvider : IDescentAnimationProvider
    {
        // ==================== 接口属性 ====================
        
        public string AnimationType => "Portal";
        public float AnimationDuration => 3.0f; // 3秒传送门动画
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
        
        static PortalAnimationProvider()
        {
            // ⭐ 自动注册到注册表
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                DescentAnimationRegistry.Register(new PortalAnimationProvider());
                Log.Message("[PortalAnimationProvider] 已自动注册到降临动画注册表");
            });
        }
        
        // ==================== 接口方法 ====================
        
        public void StartAnimation(Map map, IntVec3 targetLoc, NarratorPersonaDef persona, bool hostile, Action onComplete = null)
        {
            if (isPlaying)
            {
                Log.Warning("[PortalAnimationProvider] 动画正在播放中，忽略新请求");
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
                // 1. 播放传送门出现音效
                PlayPortalSound(persona, map, "entry");
                
                // 2. 生成折跃入口特效（使用原版 Skip 效果）
                SpawnSkipEntryEffect(map, targetLoc);
                
                // 3. 播放魔法阵特效
                effectRenderer.PlayMagicCircle(targetLoc, hostile);
                
                Log.Message($"[PortalAnimationProvider] 开始传送门动画: 目标={targetLoc}, 敌对={hostile}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PortalAnimationProvider] 启动动画失败: {ex}");
                StopAnimation();
            }
        }
        
        public void StopAnimation()
        {
            isPlaying = false;
            elapsedTime = 0f;
            onCompleteCallback = null;
            currentMap = null;
            currentPersona = null;
        }
        
        public void Update(float deltaTime)
        {
            if (!isPlaying) return;
            
            elapsedTime += deltaTime;
            float progress = elapsedTime / AnimationDuration;
            
            // 传送门效果持续播放
            if (currentMap != null && targetLocation.InBounds(currentMap))
            {
                // 每0.3秒产生一次粒子效果
                if (elapsedTime % 0.3f < deltaTime)
                {
                    SpawnSkipParticles(currentMap, targetLocation, progress);
                }
            }
            
            // 检查动画是否完成
            if (elapsedTime >= AnimationDuration)
            {
                CompleteAnimation();
            }
        }
        
        // ==================== 私有方法 ====================
        
        /// <summary>
        /// 生成折跃入口特效
        /// </summary>
        private void SpawnSkipEntryEffect(Map map, IntVec3 location)
        {
            try
            {
                if (!location.InBounds(map)) return;
                
                Vector3 drawPos = location.ToVector3Shifted();
                
                // 使用原版折跃特效
                // FleckDef: PsychicConditionCauserPulse 或类似效果
                FleckMaker.ThrowLightningGlow(drawPos, map, 4f);
                
                // 产生环形粒子（模拟传送门边缘）
                int particleCount = 16;
                for (int i = 0; i < particleCount; i++)
                {
                    float angle = (float)i / particleCount * 360f * Mathf.Deg2Rad;
                    float radius = 2f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    Vector3 particlePos = drawPos + offset;
                    
                    IntVec3 particleCell = particlePos.ToIntVec3();
                    if (particleCell.InBounds(map))
                    {
                        // 使用 psycast 效果模拟传送门
                        FleckMaker.ThrowMetaIcon(particleCell, map, FleckDefOf.PsycastAreaEffect);
                    }
                }
                
                // 中心点爆发效果
                FleckMaker.Static(drawPos, map, FleckDefOf.PsycastSkipFlashEntry, 3f);
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortalAnimationProvider] 生成折跃特效失败: {ex.Message}");
                
                // 备用效果
                FleckMaker.ThrowLightningGlow(location.ToVector3Shifted(), currentMap, 3f);
            }
        }
        
        /// <summary>
        /// 生成传送门持续粒子
        /// </summary>
        private void SpawnSkipParticles(Map map, IntVec3 location, float progress)
        {
            try
            {
                Vector3 drawPos = location.ToVector3Shifted();
                
                // 旋转的粒子环
                float rotationAngle = progress * 360f * 2; // 2圈旋转
                float radius = 1.5f + Mathf.Sin(progress * Mathf.PI) * 0.5f;
                
                for (int i = 0; i < 4; i++)
                {
                    float angle = (rotationAngle + i * 90f) * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    Vector3 particlePos = drawPos + offset;
                    
                    IntVec3 particleCell = particlePos.ToIntVec3();
                    if (particleCell.InBounds(map))
                    {
                        FleckMaker.ThrowLightningGlow(particlePos, map, 0.5f);
                    }
                }
                
                // 中心脉冲
                if (progress > 0.5f)
                {
                    float pulseIntensity = (progress - 0.5f) * 4f;
                    FleckMaker.ThrowLightningGlow(drawPos, map, pulseIntensity);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortalAnimationProvider] 生成粒子失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 播放传送门音效
        /// </summary>
        private void PlayPortalSound(NarratorPersonaDef persona, Map map, string phase)
        {
            try
            {
                // 优先使用配置的音效
                string soundDefName = persona?.descentSound;
                
                if (string.IsNullOrEmpty(soundDefName))
                {
                    // 使用原版折跃音效
                    soundDefName = phase == "entry" ? "Psycast_Skip_Entry" : "Psycast_Skip_Exit";
                }
                
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName);
                if (soundDef != null)
                {
                    SoundStarter.PlayOneShotOnCamera(soundDef, map);
                    Log.Message($"[PortalAnimationProvider] 播放音效: {soundDefName}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortalAnimationProvider] 播放音效失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 动画完成处理
        /// </summary>
        private void CompleteAnimation()
        {
            isPlaying = false;
            
            // 播放传送门消散音效
            PlayPortalSound(currentPersona, currentMap, "exit");
            
            // 播放最终爆发特效
            if (currentMap != null && targetLocation.InBounds(currentMap))
            {
                Vector3 drawPos = targetLocation.ToVector3Shifted();
                
                // 最终闪光
                FleckMaker.ThrowLightningGlow(drawPos, currentMap, 5f);
                
                try
                {
                    FleckMaker.Static(drawPos, currentMap, FleckDefOf.PsycastSkipFlashEntry, 4f);
                }
                catch
                {
                    // 备用效果
                    FleckMaker.ThrowDustPuff(targetLocation, currentMap, 3f);
                }
                
                // 降临完成特效
                effectRenderer.PlayDescentCompleteEffect(targetLocation, isHostile);
            }
            
            // 触发回调
            onCompleteCallback?.Invoke();
            onCompleteCallback = null;
            
            // 清理状态
            currentMap = null;
            currentPersona = null;
            
            Log.Message("[PortalAnimationProvider] 传送门动画完成");
        }
    }
}
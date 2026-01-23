using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ⭐ v1.7.8: 降临特效渲染器 (修复 Mote 渲染错误)
    ///
    /// 功能：
    /// - 渲染降临冲击波特效
    /// - 渲染光环特效（援助/袭击）
    /// - 渲染粒子效果
    /// - 渲染魔法阵
    /// - ⭐ v1.6.80: 使用RimWorld内置特效作为占位符
    /// - ⭐ v1.7.8: 修复 Mote_SmokeJoint 渲染冲突
    /// </summary>
    public class DescentEffectRenderer
    {
        // ⭐ v1.7.8: 安全检查 - 是否可以安全生成粒子
        private static bool CanSpawnFlecks()
        {
            try
            {
                // 必须在主线程、有地图、且不在渲染阶段
                if (Find.CurrentMap == null) return false;
                if (!UnityData.IsInMainThread) return false;
                
                // ⭐ v1.7.8: 检查游戏是否正在 tick（避免在暂停时生成）
                if (Find.TickManager?.Paused == true) return false;
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        // ⭐ v1.7.8: 安全的 Fleck 生成包装器
        private static void SafeThrowFleck(Action fleckAction)
        {
            if (!CanSpawnFlecks()) return;
            
            try
            {
                fleckAction?.Invoke();
            }
            catch (Exception ex)
            {
                // 静默忽略 Mote 渲染错误，避免日志刷屏
                if (Prefs.DevMode)
                {
                    Log.Warning($"[DescentEffectRenderer] Fleck 生成跳过: {ex.Message}");
                }
            }
        }
        
        // ==================== 配置参数 ====================
        
        private const float AURA_DURATION = 5.0f;        // 光环持续时间
        private const float IMPACT_DURATION = 1.5f;      // 冲击波持续时间
        private const float PARTICLE_DURATION = 3.0f;    // 粒子持续时间
        
        // ==================== 公共方法 ====================
        
        /// <summary>
        /// ⭐ v1.7.8: 播放冲击波特效（修复渲染冲突）
        /// </summary>
        public void PlayImpactEffect(IntVec3 location, bool isHostile)
        {
            if (!CanSpawnFlecks()) return;
            
            try
            {
                Map map = Find.CurrentMap;
                if (map == null || !location.IsValid || !location.InBounds(map))
                {
                    return;
                }
                
                Vector3 drawPos = location.ToVector3Shifted();
                
                // ⭐ v1.7.8: 使用安全包装器
                SafeThrowFleck(() => FleckMaker.ThrowLightningGlow(drawPos, map, 3f));
                SafeThrowFleck(() => FleckMaker.ThrowDustPuff(location, map, 2f));
                
                // 根据敌对/友好选择不同颜色效果
                if (isHostile)
                {
                    SafeThrowFleck(() => FleckMaker.ThrowFireGlow(drawPos, map, 2f));
                    SafeThrowFleck(() => FleckMaker.ThrowMicroSparks(drawPos, map));
                }
                else
                {
                    SafeThrowFleck(() => FleckMaker.ThrowMetaIcon(location, map, FleckDefOf.PsycastAreaEffect));
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[DescentEffectRenderer] 播放冲击波特效 at {location}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[DescentEffectRenderer] 播放冲击波特效失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.7.8: 播放光环特效（减少粒子数量避免冲突）
        /// </summary>
        public void PlayAuraEffect(IntVec3 location, bool isHostile, float duration)
        {
            if (!CanSpawnFlecks()) return;
            
            try
            {
                Map map = Find.CurrentMap;
                if (map == null || !location.IsValid || !location.InBounds(map))
                {
                    return;
                }
                
                Vector3 drawPos = location.ToVector3Shifted();
                
                // ⭐ v1.7.8: 减少粒子数量，避免渲染冲突
                int particleCount = Mathf.Min((int)(duration * 2), 8); // 最多8个粒子
                float radius = 2f;
                
                for (int i = 0; i < particleCount; i++)
                {
                    float angle = (float)i / particleCount * 360f;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                        0f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                    );
                    
                    Vector3 particlePos = drawPos + offset;
                    IntVec3 particleCell = particlePos.ToIntVec3();
                    
                    if (particleCell.InBounds(map))
                    {
                        if (isHostile)
                        {
                            // ⭐ v1.7.8: 避免使用 ThrowSmoke（会导致 Mote_SmokeJoint 错误）
                            SafeThrowFleck(() => FleckMaker.ThrowFireGlow(particlePos, map, 0.5f));
                        }
                        else
                        {
                            SafeThrowFleck(() => FleckMaker.ThrowLightningGlow(particlePos, map, 0.5f));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[DescentEffectRenderer] 播放光环特效失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.7.8: 播放魔法阵特效（减少粒子避免冲突）
        /// </summary>
        public void PlayMagicCircle(IntVec3 location, bool isHostile)
        {
            if (!CanSpawnFlecks()) return;
            
            try
            {
                Map map = Find.CurrentMap;
                if (map == null || !location.IsValid || !location.InBounds(map))
                {
                    return;
                }
                
                Vector3 drawPos = location.ToVector3Shifted();
                
                // ⭐ v1.7.8: 减少粒子数量
                float radius = 3f;
                int segments = 8; // 从16减少到8
                
                for (int i = 0; i < segments; i++)
                {
                    float angle = (float)i / segments * 360f;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                        0f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                    );
                    
                    Vector3 particlePos = drawPos + offset;
                    IntVec3 particleCell = particlePos.ToIntVec3();
                    
                    if (particleCell.InBounds(map))
                    {
                        if (isHostile)
                        {
                            SafeThrowFleck(() => FleckMaker.ThrowFireGlow(particlePos, map, 0.5f));
                        }
                        else
                        {
                            SafeThrowFleck(() => FleckMaker.ThrowLightningGlow(particlePos, map, 0.5f));
                        }
                    }
                }
                
                // 中心点特效
                SafeThrowFleck(() => FleckMaker.ThrowLightningGlow(drawPos, map, 2f));
            }
            catch (Exception ex)
            {
                Log.Warning($"[DescentEffectRenderer] 播放魔法阵失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.80: 播放实体飞掠阴影效果
        /// </summary>
        public void PlayEntityShadow(Map map, IntVec3 targetLocation, float progress)
        {
            try
            {
                if (map == null)
                {
                    return;
                }
                
                // 计算阴影位置（从地图边缘飞向目标点）
                IntVec3 mapEdge = new IntVec3(0, 0, (int)(map.Size.z * (1f - progress)));
                // 手动插值 IntVec3
                IntVec3 shadowPos = new IntVec3(
                    (int)Mathf.Lerp(mapEdge.x, targetLocation.x, progress),
                    0,
                    (int)Mathf.Lerp(mapEdge.z, targetLocation.z, progress)
                );
                
                // ⭐ v1.6.91: 移除粒子烟雾阴影，完全依赖 DragonShadowRenderer 的图片阴影
                // if (shadowPos.InBounds(map))
                // {
                //     Vector3 drawPos = shadowPos.ToVector3Shifted();
                //     FleckMaker.ThrowSmoke(drawPos, map, 5f);
                //     FleckMaker.ThrowDustPuff(shadowPos, map, 3f);
                // }
                
                // 在目标位置产生预兆效果
                if (progress > 0.8f && targetLocation.InBounds(map))
                {
                    FleckMaker.ThrowLightningGlow(targetLocation.ToVector3Shifted(), map, progress * 3f);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[DescentEffectRenderer] 播放实体阴影失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.7.8: 播放降临完成特效（移除 ThrowSmoke 避免错误）
        /// </summary>
        public void PlayDescentCompleteEffect(IntVec3 location, bool isHostile)
        {
            if (!CanSpawnFlecks()) return;
            
            try
            {
                Map map = Find.CurrentMap;
                if (map == null || !location.InBounds(map))
                {
                    return;
                }
                
                Vector3 drawPos = location.ToVector3Shifted();
                
                // ⭐ v1.7.8: 简化特效，避免 Mote 渲染冲突
                
                // 1. 核心能量爆发
                float size = 4f;
                SafeThrowFleck(() => FleckMaker.ThrowLightningGlow(drawPos, map, size));
                SafeThrowFleck(() => FleckMaker.ThrowDustPuff(location, map, 3f));
                SafeThrowFleck(() => FleckMaker.ThrowHeatGlow(location, map, size * 0.6f));

                // 2. 垂直光柱（减少数量）
                for (int i = 0; i < 3; i++)
                {
                    Vector3 offset = new Vector3(Rand.Range(-0.3f, 0.3f), 0, Rand.Range(-0.3f, 0.3f));
                    SafeThrowFleck(() => FleckMaker.ThrowMicroSparks(drawPos + offset, map));
                }

                // 3. 环形冲击波（减少粒子数量，移除 ThrowSmoke）
                int particleCount = 12; // 从24减少到12
                float radius = 3f;
                for (int i = 0; i < particleCount; i++)
                {
                    float angle = (float)i / particleCount * 360f;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                        0f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                    );
                    
                    Vector3 particlePos = drawPos + offset;
                    IntVec3 particleCell = particlePos.ToIntVec3();
                    
                    if (particleCell.InBounds(map))
                    {
                        if (isHostile)
                        {
                            // ⭐ v1.7.8: 移除 ThrowSmoke，只用 FireGlow
                            SafeThrowFleck(() => FleckMaker.ThrowFireGlow(particlePos, map, 0.8f));
                        }
                        else
                        {
                            SafeThrowFleck(() => FleckMaker.ThrowLightningGlow(particlePos, map, 0.8f));
                        }
                    }
                }
                
                // 4. 调用基础冲击波
                PlayImpactEffect(location, isHostile);
            }
            catch (Exception ex)
            {
                Log.Warning($"[DescentEffectRenderer] 播放降临完成特效失败: {ex.Message}");
            }
        }
    }
}

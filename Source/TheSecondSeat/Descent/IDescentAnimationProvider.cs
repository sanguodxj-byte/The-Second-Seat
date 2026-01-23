using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ⭐ v1.6.81: 降临动画提供者接口
    /// 
    /// 子Mod可以实现此接口来提供自定义的降临过场动画
    /// 
    /// 使用方法：
    /// 1. 在子Mod中创建类实现 IDescentAnimationProvider
    /// 2. 在 [StaticConstructorOnStartup] 中调用 DescentAnimationRegistry.Register()
    /// 3. 在 NarratorPersonaDef 中设置 descentAnimationType 为注册的类型名
    /// </summary>
    public interface IDescentAnimationProvider
    {
        /// <summary>
        /// 动画类型唯一标识符
        /// 例如: "DragonFlyby", "PortalMagic", "LightningStrike"
        /// </summary>
        string AnimationType { get; }
        
        /// <summary>
        /// 动画持续时间（秒）
        /// 降临系统会在动画完成后生成实体
        /// </summary>
        float AnimationDuration { get; }
        
        /// <summary>
        /// 开始播放降临动画
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="targetLocation">降临目标位置</param>
        /// <param name="persona">叙事者人格配置（可用于读取自定义配置）</param>
        /// <param name="isHostile">是否为敌对降临</param>
        /// <param name="onComplete">动画完成回调（可选）</param>
        void StartAnimation(Map map, IntVec3 targetLocation, NarratorPersonaDef persona, bool isHostile, Action onComplete = null);
        
        /// <summary>
        /// 停止动画（用于中断或清理）
        /// </summary>
        void StopAnimation();
        
        /// <summary>
        /// 每帧更新（用于持续动画）
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        void Update(float deltaTime);
        
        /// <summary>
        /// 检查动画是否正在播放
        /// </summary>
        bool IsPlaying { get; }
    }
    
    /// <summary>
    /// ⭐ v1.6.81: 降临动画注册表
    /// 
    /// 管理所有已注册的降临动画提供者
    /// </summary>
    public static class DescentAnimationRegistry
    {
        private static readonly Dictionary<string, IDescentAnimationProvider> providers = new Dictionary<string, IDescentAnimationProvider>();
        private static IDescentAnimationProvider defaultProvider;
        
        /// <summary>
        /// 注册降临动画提供者
        /// </summary>
        /// <param name="provider">动画提供者实例</param>
        public static void Register(IDescentAnimationProvider provider)
        {
            if (provider == null)
            {
                Log.Error("[DescentAnimationRegistry] 尝试注册空的动画提供者");
                return;
            }
            
            string type = provider.AnimationType;
            if (string.IsNullOrEmpty(type))
            {
                Log.Error("[DescentAnimationRegistry] 动画提供者的 AnimationType 不能为空");
                return;
            }
            
            if (providers.ContainsKey(type))
            {
                Log.Warning($"[DescentAnimationRegistry] 动画类型 '{type}' 已存在，将被覆盖");
            }
            
            providers[type] = provider;
            Log.Message($"[DescentAnimationRegistry] 已注册降临动画: {type}");
        }
        
        /// <summary>
        /// 注销降临动画提供者
        /// </summary>
        /// <param name="animationType">动画类型标识符</param>
        public static void Unregister(string animationType)
        {
            if (providers.Remove(animationType))
            {
                Log.Message($"[DescentAnimationRegistry] 已注销降临动画: {animationType}");
            }
        }
        
        /// <summary>
        /// 获取指定类型的动画提供者
        /// </summary>
        /// <param name="animationType">动画类型标识符</param>
        /// <returns>动画提供者，如果未找到则返回默认提供者</returns>
        public static IDescentAnimationProvider GetProvider(string animationType)
        {
            if (!string.IsNullOrEmpty(animationType) && providers.TryGetValue(animationType, out var provider))
            {
                return provider;
            }
            
            // 返回默认提供者（空投仓）
            return defaultProvider ?? (defaultProvider = new DefaultDropPodAnimationProvider());
        }
        
        /// <summary>
        /// 检查指定类型的动画提供者是否已注册
        /// </summary>
        public static bool HasProvider(string animationType)
        {
            return !string.IsNullOrEmpty(animationType) && providers.ContainsKey(animationType);
        }
        
        /// <summary>
        /// 获取所有已注册的动画类型
        /// </summary>
        public static IEnumerable<string> GetRegisteredTypes()
        {
            return providers.Keys;
        }
        
        /// <summary>
        /// 设置默认提供者（用于未配置动画类型时）
        /// </summary>
        public static void SetDefaultProvider(IDescentAnimationProvider provider)
        {
            defaultProvider = provider;
        }
    }
    
    /// <summary>
    /// ⭐ v1.6.81: 默认空投仓动画提供者
    /// 
    /// 主Mod提供的默认实现，使用RimWorld原版空投仓
    /// </summary>
    public class DefaultDropPodAnimationProvider : IDescentAnimationProvider
    {
        private bool isPlaying = false;
        private float elapsedTime = 0f;
        private Action onCompleteCallback;
        
        public string AnimationType => "DropPod";
        public float AnimationDuration => 2.5f;
        public bool IsPlaying => isPlaying;
        
        public void StartAnimation(Map map, IntVec3 targetLocation, NarratorPersonaDef persona, bool isHostile, Action onComplete = null)
        {
            isPlaying = true;
            elapsedTime = 0f;
            onCompleteCallback = onComplete;
            
            try
            {
                // 播放降临音效
                if (persona != null && !string.IsNullOrEmpty(persona.descentSound))
                {
                    SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(persona.descentSound);
                    if (soundDef != null)
                    {
                        SoundStarter.PlayOneShotOnCamera(soundDef, map);
                    }
                }
                
                // 使用RimWorld原版空投仓
                string skyfallerDefName = persona?.descentSkyfallerDef;
                if (string.IsNullOrEmpty(skyfallerDefName))
                {
                    skyfallerDefName = "DropPodIncoming";
                }
                
                ThingDef skyfallerDef = DefDatabase<ThingDef>.GetNamedSilentFail(skyfallerDefName);
                if (skyfallerDef != null && targetLocation.IsValid && targetLocation.InBounds(map))
                {
                    SkyfallerMaker.SpawnSkyfaller(skyfallerDef, targetLocation, map);
                    Log.Message($"[DefaultDropPodAnimationProvider] 空投仓动画开始: {skyfallerDefName}");
                }
                else
                {
                    // 备用效果
                    FleckMaker.ThrowLightningGlow(targetLocation.ToVector3Shifted(), map, 5f);
                    FleckMaker.ThrowDustPuff(targetLocation, map, 3f);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[DefaultDropPodAnimationProvider] 动画启动失败: {ex.Message}");
            }
        }
        
        public void StopAnimation()
        {
            isPlaying = false;
            elapsedTime = 0f;
            onCompleteCallback = null;
        }
        
        public void Update(float deltaTime)
        {
            if (!isPlaying) return;
            
            elapsedTime += deltaTime;
            if (elapsedTime >= AnimationDuration)
            {
                isPlaying = false;
                onCompleteCallback?.Invoke();
                onCompleteCallback = null;
            }
        }
    }
}
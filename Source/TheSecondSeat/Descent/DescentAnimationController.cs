using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Utils;
using TheSecondSeat.Core; // For PortraitOverlaySystem

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ? v2.0.0: 降临动画控制器
    /// 
    /// 功能：
    /// - 控制立绘姿势切换序列
    /// - 播放实体降临过场动画
    /// - 管理动画时间轴和过渡效果
    ///
    /// 动画序列：
    /// 1. 姿势切换（3秒）：ready → charging → casting
    /// 2. 过场动画（6秒）：实体飞入、盘旋、降落、着陆
    /// 3. 特效爆发（2秒）：冲击波、光环浮现
    /// </summary>
    public class DescentAnimationController
    {
        // ==================== 姿势切换配置 ====================
        
        private const float POSTURE_READY_DURATION = 0.5f;     // 准备姿势持续时间
        private const float POSTURE_CHARGING_DURATION = 1.0f;  // 蓄力姿势持续时间
        private const float POSTURE_CASTING_DURATION = 1.5f;   // 施法姿势持续时间
        private const float POSTURE_FADE_DURATION = 0.3f;      // 姿势淡入淡出时间
        
        // ==================== 过场动画配置 ====================
        
        private const float CINEMATIC_DURATION = 6.0f;         // 过场动画总时长
        private const int CINEMATIC_FPS = 15;                  // 过场动画帧率
        private const int CINEMATIC_TOTAL_FRAMES = 90;         // 过场动画总帧数
        
        // 默认过场动画文件夹（已从 DragonRider 泛化）
        // 兼容性注意：如果资源包未更新，可能需要重命名文件夹为 EntityFlyby
        private const string DEFAULT_CINEMATIC_FOLDER = "EntityFlyby";

        // ==================== 状态字段 ====================
        
        private PostureState currentPostureState = PostureState.None;
        private float postureTimer = 0f;
        private Action? postureCompleteCallback;
        
        private bool isPlayingCinematic = false;
        private float cinematicTimer = 0f;
        private int currentFrame = 0;
        private Action? cinematicCompleteCallback;
        private IntVec3 targetLocation;
        
        private bool isHostileMode = false;
        
        // ==================== 公共方法 ====================
        
        /// <summary>
        /// 开始姿势切换序列
        /// </summary>
        public void StartPostureSequence(bool isHostile, Action onComplete)
        {
            isHostileMode = isHostile;
            currentPostureState = PostureState.Ready;
            postureTimer = 0f;
            postureCompleteCallback = onComplete;
            
            // 切换到准备姿势
            SwitchToPosture(PostureType.Ready);
            
            Log.Message($"[DescentAnimationController] Started posture sequence: {(isHostile ? "Attack" : "Assist")}");
        }
        
        /// <summary>
        /// 开始过场动画
        /// </summary>
        public void StartCinematic(bool isHostile, IntVec3 location, Action onComplete)
        {
            isHostileMode = isHostile;
            targetLocation = location;
            isPlayingCinematic = true;
            cinematicTimer = 0f;
            currentFrame = 0;
            cinematicCompleteCallback = onComplete;
            
            // 预加载动画帧（前30帧，避免全部加载导致内存压力）
            PreloadCinematicFrames(0, 30);
            
            Log.Message($"[DescentAnimationController] Started cinematic: {(isHostile ? "Attack" : "Assist")} at {location}");
        }
        
        /// <summary>
        /// 更新动画（每帧调用）
        /// </summary>
        public void Update(float deltaTime)
        {
            // 更新姿势序列
            if (currentPostureState != PostureState.None && currentPostureState != PostureState.Completed)
            {
                UpdatePostureSequence(deltaTime);
            }
            
            // 更新过场动画
            if (isPlayingCinematic)
            {
                UpdateCinematic(deltaTime);
            }
        }
        
        // ==================== 私有方法 - 姿势切换 ====================
        
        /// <summary>
        /// 更新姿势序列
        /// </summary>
        private void UpdatePostureSequence(float deltaTime)
        {
            postureTimer += deltaTime;
            
            switch (currentPostureState)
            {
                case PostureState.Ready:
                    if (postureTimer >= POSTURE_READY_DURATION)
                    {
                        // 切换到蓄力姿势
                        currentPostureState = PostureState.Charging;
                        postureTimer = 0f;
                        SwitchToPosture(PostureType.Charging);
                    }
                    break;
                    
                case PostureState.Charging:
                    if (postureTimer >= POSTURE_CHARGING_DURATION)
                    {
                        // 切换到施法姿势
                        currentPostureState = PostureState.Casting;
                        postureTimer = 0f;
                        SwitchToPosture(PostureType.Casting);
                    }
                    break;
                    
                case PostureState.Casting:
                    if (postureTimer >= POSTURE_CASTING_DURATION)
                    {
                        // 姿势序列完成
                        currentPostureState = PostureState.Completed;
                        postureTimer = 0f;
                        
                        Log.Message("[DescentAnimationController] Posture sequence completed");
                        
                        // 触发回调
                        postureCompleteCallback?.Invoke();
                        postureCompleteCallback = null;
                    }
                    break;
            }
        }
        
        /// <summary>
        /// 切换到指定姿势
        /// ⚠️ v1.6.80: 使用线程安全的资源加载
        /// </summary>
        private void SwitchToPosture(PostureType type)
        {
            try
            {
                // ⚠️ v1.6.80: 确保在主线程调用
                if (!TSS_AssetLoader.IsMainThread)
                {
                    Log.Warning("[DescentAnimationController] SwitchToPosture called from non-main thread, skipping");
                    return;
                }
                
                // 获取当前人格
                var manager = Current.Game?.GetComponent<Narrator.NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                
                if (persona == null)
                {
                    Log.Warning("[DescentAnimationController] No persona found for posture switch");
                    return;
                }
                
                // 加载姿势立绘
                string postureName = type switch
                {
                    PostureType.Ready => "descent_pose_ready",
                    PostureType.Charging => "descent_pose_charging",
                    PostureType.Casting => "descent_pose_casting",
                    _ => "descent_pose_ready"
                };
                
                string personaName = GetPersonaName(persona);
                
                // ⚠️ v1.6.80: 使用线程安全的加载方法
                Texture2D postureTexture = TSS_AssetLoader.LoadDescentPosture(personaName, postureName);
                
                if (postureTexture != null)
                {
                    // 通过 FullBodyPortraitPanel 切换立绘
                    var panel = PortraitOverlaySystem.GetPanel();
                    if (panel != null)
                    {
                        // 计算持续时间
                        float duration = type switch
                        {
                            PostureType.Ready => POSTURE_READY_DURATION,
                            PostureType.Charging => POSTURE_CHARGING_DURATION,
                            PostureType.Casting => POSTURE_CASTING_DURATION,
                            _ => 1.0f
                        };
                        
                        // 触发面板动画
                        panel.TriggerPostureAnimation(postureName, null, duration);

                        // ⭐ 同步面部表情
                        ExpressionType targetExpr = isHostileMode ? ExpressionType.Angry : ExpressionType.Neutral;
                        if (type == PostureType.Casting) targetExpr = ExpressionType.Happy; // 施法高潮
                        
                        panel.TriggerExpression(targetExpr, duration, intensity: 2);
                    }
                    
                    Log.Message($"[DescentAnimationController] Switched to posture: {postureName}");
                }
                else
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[DescentAnimationController] Posture texture not found for {personaName}/{postureName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DescentAnimationController] Failed to switch posture: {ex}");
            }
        }
        
        // ==================== 私有方法 - 过场动画 ====================
        
        /// <summary>
        /// 更新过场动画
        /// </summary>
        private void UpdateCinematic(float deltaTime)
        {
            cinematicTimer += deltaTime;
            
            // 计算当前应该显示的帧
            int targetFrame = Mathf.FloorToInt((cinematicTimer / CINEMATIC_DURATION) * CINEMATIC_TOTAL_FRAMES);
            targetFrame = Mathf.Clamp(targetFrame, 0, CINEMATIC_TOTAL_FRAMES - 1);
            
            // 如果需要切换帧
            if (targetFrame != currentFrame)
            {
                currentFrame = targetFrame;
                DisplayCinematicFrame(currentFrame);
                
                // 预加载接下来的帧
                if (currentFrame % 10 == 0) // 每10帧预加载一次
                {
                    PreloadCinematicFrames(currentFrame + 1, currentFrame + 30);
                }
            }
            
            // 检查是否完成
            if (cinematicTimer >= CINEMATIC_DURATION)
            {
                isPlayingCinematic = false;
                cinematicTimer = 0f;
                currentFrame = 0;
                
                Log.Message("[DescentAnimationController] Cinematic completed");
                
                // 触发回调
                cinematicCompleteCallback?.Invoke();
                cinematicCompleteCallback = null;
            }
        }
        
        /// <summary>
        /// 显示过场动画帧
        /// ⚠️ v1.6.80: 使用线程安全的资源加载
        /// </summary>
        private void DisplayCinematicFrame(int frameIndex)
        {
            try
            {
                // ⚠️ v1.6.80: 确保在主线程调用
                if (!TSS_AssetLoader.IsMainThread)
                {
                    return; // 静默跳过，避免日志刷屏
                }
                
                // 获取帧纹理路径
                string framePath = GetCinematicFramePath(frameIndex);
                
                // ⚠️ v1.6.80: 使用线程安全的加载方法
                Texture2D frameTexture = TSS_AssetLoader.LoadTexture(framePath);
                
                if (frameTexture != null)
                {
                    // TODO: 在全屏显示过场动画帧
                    // 可以使用 Find.WindowStack.Add(new Window_Cinematic(frameTexture))
                    
                    if (Prefs.DevMode && frameIndex % 15 == 0) // 每秒输出一次
                    {
                        Log.Message($"[DescentAnimationController] Displaying frame {frameIndex}/{CINEMATIC_TOTAL_FRAMES}");
                    }
                }
                else
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[DescentAnimationController] Frame texture not found: {framePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DescentAnimationController] Failed to display frame {frameIndex}: {ex}");
            }
        }
        
        /// <summary>
        /// 获取过场动画帧路径
        /// </summary>
        private string GetCinematicFramePath(int frameIndex)
        {
            // 根据帧索引确定所属阶段
            string stage;
            int stageFrame;
            
            if (frameIndex < 40)
            {
                stage = "approach";
                stageFrame = frameIndex + 1;
            }
            else if (frameIndex < 60)
            {
                stage = "circle";
                stageFrame = frameIndex + 1;
            }
            else if (frameIndex < 90)
            {
                stage = "landing";
                stageFrame = frameIndex + 1;
            }
            else
            {
                stage = "dismount";
                stageFrame = frameIndex + 1;
            }
            
            return $"UI/Narrators/Descent/Cinematic/{DEFAULT_CINEMATIC_FOLDER}/rider_{stage}_{stageFrame:D3}";
        }
        
        /// <summary>
        /// 预加载过场动画帧
        /// ⚠️ v1.6.80: 必须在主线程调用
        /// </summary>
        private void PreloadCinematicFrames(int startFrame, int endFrame)
        {
            try
            {
                // ⚠️ v1.6.80: 确保在主线程调用
                if (!TSS_AssetLoader.IsMainThread)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("[DescentAnimationController] PreloadCinematicFrames called from non-main thread, skipping");
                    }
                    return;
                }
                
                for (int i = startFrame; i <= endFrame && i < CINEMATIC_TOTAL_FRAMES; i++)
                {
                    string framePath = GetCinematicFramePath(i);
                    // ⚠️ v1.6.80: 使用线程安全的加载方法
                    TSS_AssetLoader.LoadTexture(framePath);
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[DescentAnimationController] Preloaded frames {startFrame}-{endFrame}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[DescentAnimationController] Failed to preload frames: {ex.Message}");
            }
        }
        
        // ==================== 辅助方法 ====================
        
        /// <summary>
        /// 获取人格名称（用于路径）
        /// </summary>
        private string GetPersonaName(NarratorPersonaDef def)
        {
            if (!string.IsNullOrEmpty(def.narratorName))
            {
                return def.narratorName.Split(' ')[0].Trim();
            }
            
            return def.defName;
        }
        
        /// <summary>
        /// 停止所有动画
        /// </summary>
        public void Stop()
        {
            currentPostureState = PostureState.None;
            isPlayingCinematic = false;
            postureTimer = 0f;
            cinematicTimer = 0f;
            currentFrame = 0;
            
            postureCompleteCallback = null;
            cinematicCompleteCallback = null;
            
            Log.Message("[DescentAnimationController] All animations stopped");
        }
    }
    
    // ==================== 枚举定义 ====================
    
    /// <summary>
    /// 姿势状态
    /// </summary>
    public enum PostureState
    {
        None,       // 无
        Ready,      // 准备
        Charging,   // 蓄力
        Casting,    // 施法
        Completed   // 完成
    }
    
    /// <summary>
    /// 姿势类型
    /// </summary>
    public enum PostureType
    {
        Ready,     // 准备姿势
        Charging,  // 蓄力姿势
        Casting    // 施法姿势
    }
}

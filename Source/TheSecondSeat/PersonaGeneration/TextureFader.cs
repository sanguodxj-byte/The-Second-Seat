using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// ⭐ v1.8.1: 纹理渐变器 - 实现平滑的透明度过渡
    /// 
    /// 核心原理：
    /// 在切换纹理时，保留旧纹理和新纹理，通过 Alpha 渐变实现丝滑过渡。
    /// 
    /// 第 0.0 秒：旧图 Alpha=1.0，新图 Alpha=0.0
    /// 第 0.1 秒：旧图 Alpha=0.5，新图 Alpha=0.5
    /// 第 0.2 秒：旧图 Alpha=0.0，新图 Alpha=1.0（过渡结束）
    /// </summary>
    public class TextureFader
    {
        // === 配置常量 ===
        /// <summary>默认过渡速度（5.0 = 0.2秒完成）</summary>
        public const float DEFAULT_TRANSITION_SPEED = 5.0f;
        
        /// <summary>瞬间切换速度（用于口型动画）</summary>
        public const float INSTANT_SPEED = 999f;
        
        /// <summary>慢速过渡（用于养神等场景）</summary>
        public const float SLOW_SPEED = 2.0f;
        
        // === 状态 ===
        private Texture2D currentTex;
        private Texture2D targetTex;
        private float transitionProgress = 1.0f;
        private float transitionSpeed = DEFAULT_TRANSITION_SPEED;
        
        // === 属性 ===
        /// <summary>当前显示的纹理（过渡完成后）</summary>
        public Texture2D CurrentTexture => transitionProgress >= 1f ? targetTex : currentTex;
        
        /// <summary>目标纹理</summary>
        public Texture2D TargetTexture => targetTex;
        
        /// <summary>是否正在过渡中</summary>
        public bool IsTransitioning => transitionProgress < 1.0f;
        
        /// <summary>过渡进度（0-1）</summary>
        public float Progress => transitionProgress;
        
        /// <summary>
        /// 设置新的目标纹理
        /// </summary>
        /// <param name="newTex">新纹理</param>
        /// <param name="speed">过渡速度（可选，默认使用 DEFAULT_TRANSITION_SPEED）</param>
        public void SetTarget(Texture2D newTex, float speed = DEFAULT_TRANSITION_SPEED)
        {
            // 如果是相同纹理，跳过
            if (newTex == currentTex || newTex == targetTex)
            {
                return;
            }
            
            // 如果上一次过渡还没完成，把当前的画面作为起点
            if (transitionProgress < 1.0f && targetTex != null)
            {
                currentTex = targetTex;
            }
            
            targetTex = newTex;
            transitionProgress = 0f;
            transitionSpeed = speed;
        }
        
        /// <summary>
        /// 立即设置纹理（无过渡）
        /// </summary>
        public void SetImmediate(Texture2D newTex)
        {
            currentTex = newTex;
            targetTex = newTex;
            transitionProgress = 1f;
        }
        
        /// <summary>
        /// 更新过渡进度
        /// </summary>
        public void Update()
        {
            if (transitionProgress < 1.0f)
            {
                transitionProgress += Time.deltaTime * transitionSpeed;
                transitionProgress = Mathf.Clamp01(transitionProgress);
                
                // 过渡完成时，更新 currentTex
                if (transitionProgress >= 1.0f)
                {
                    currentTex = targetTex;
                }
            }
        }
        
        /// <summary>
        /// ⭐ 核心绘制方法：在 OnGUI 中调用
        /// 
        /// 自动处理过渡动画：
        /// - 过渡中：绘制旧图（淡出）和新图（淡入）
        /// - 静态阶段：只绘制目标图
        /// </summary>
        /// <param name="rect">绘制区域</param>
        public void Draw(Rect rect)
        {
            // 先更新进度
            Update();
            
            if (transitionProgress >= 1.0f)
            {
                // 静态阶段：只画目标图
                if (targetTex != null)
                {
                    GUI.DrawTexture(rect, targetTex, ScaleMode.ScaleToFit, true);
                }
            }
            else
            {
                // 过渡阶段：画两张
                
                // 画旧图（淡出）
                if (currentTex != null)
                {
                    Color oldColor = GUI.color;
                    GUI.color = new Color(1, 1, 1, 1.0f - transitionProgress);
                    GUI.DrawTexture(rect, currentTex, ScaleMode.ScaleToFit, true);
                    GUI.color = oldColor;
                }
                
                // 画新图（淡入）
                if (targetTex != null)
                {
                    Color oldColor = GUI.color;
                    GUI.color = new Color(1, 1, 1, transitionProgress);
                    GUI.DrawTexture(rect, targetTex, ScaleMode.ScaleToFit, true);
                    GUI.color = oldColor;
                }
            }
        }
        
        /// <summary>
        /// 绘制带偏移的纹理（用于呼吸动画等）
        /// </summary>
        public void Draw(Rect rect, float offsetX, float offsetY)
        {
            Rect offsetRect = new Rect(rect.x + offsetX, rect.y + offsetY, rect.width, rect.height);
            Draw(offsetRect);
        }
        
        /// <summary>
        /// 重置状态
        /// </summary>
        public void Reset()
        {
            currentTex = null;
            targetTex = null;
            transitionProgress = 1f;
        }
    }
    
    /// <summary>
    /// ⭐ v1.8.1: 高级眨眼控制器 - 支持普通眨眼和养神（长闭眼）
    /// </summary>
    public class AdvancedBlinkController
    {
        // === 配置 ===
        /// <summary>普通眨眼持续时间（秒）</summary>
        public float BlinkDuration = 0.15f;
        
        /// <summary>普通眨眼间隔范围（秒）</summary>
        public float BlinkIntervalMin = 3.0f;
        public float BlinkIntervalMax = 6.0f;
        
        /// <summary>养神（长闭眼）持续时间范围（秒）</summary>
        public float RestingDurationMin = 2.0f;
        public float RestingDurationMax = 5.0f;
        
        /// <summary>养神触发概率（每次眨眼后检查）</summary>
        public float RestingChance = 0.05f;
        
        /// <summary>养神间隔（秒，防止过于频繁）</summary>
        public float RestingCooldown = 30.0f;
        
        // === 状态 ===
        private float nextBlinkTime;
        private float blinkEndTime;
        private float restingEndTime;
        private float lastRestingTime;
        private bool isBlinking;
        private bool isResting;
        
        // === 属性 ===
        /// <summary>是否正在眨眼或养神（眼睛闭合）</summary>
        public bool IsEyesClosed => isBlinking || isResting;
        
        /// <summary>是否正在普通眨眼</summary>
        public bool IsBlinking => isBlinking;
        
        /// <summary>是否正在养神</summary>
        public bool IsResting => isResting;
        
        /// <summary>养神条件检查委托</summary>
        public System.Func<bool> CanRestCondition { get; set; }
        
        public AdvancedBlinkController()
        {
            ScheduleNextBlink();
        }
        
        /// <summary>
        /// 安排下一次眨眼
        /// </summary>
        private void ScheduleNextBlink()
        {
            nextBlinkTime = Time.time + Random.Range(BlinkIntervalMin, BlinkIntervalMax);
        }
        
        /// <summary>
        /// 更新眨眼状态
        /// </summary>
        public void Update()
        {
            float now = Time.time;
            
            // 1. 处理养神结束
            if (isResting && now >= restingEndTime)
            {
                isResting = false;
                ScheduleNextBlink();
            }
            
            // 2. 处理普通眨眼结束
            if (isBlinking && now >= blinkEndTime)
            {
                isBlinking = false;
                
                // 眨眼结束时，检查是否触发养神
                if (ShouldTriggerResting())
                {
                    StartResting();
                }
                else
                {
                    ScheduleNextBlink();
                }
            }
            
            // 3. 触发新的眨眼
            if (!isBlinking && !isResting && now >= nextBlinkTime)
            {
                StartBlink();
            }
        }
        
        /// <summary>
        /// 开始眨眼
        /// </summary>
        private void StartBlink()
        {
            isBlinking = true;
            blinkEndTime = Time.time + BlinkDuration;
        }
        
        /// <summary>
        /// 开始养神
        /// </summary>
        private void StartResting()
        {
            isResting = true;
            float duration = Random.Range(RestingDurationMin, RestingDurationMax);
            restingEndTime = Time.time + duration;
            lastRestingTime = Time.time;
        }
        
        /// <summary>
        /// 检查是否应该触发养神
        /// </summary>
        private bool ShouldTriggerResting()
        {
            // 检查冷却时间
            if (Time.time - lastRestingTime < RestingCooldown)
            {
                return false;
            }
            
            // 检查外部条件（好感度、说话状态等）
            if (CanRestCondition != null && !CanRestCondition())
            {
                return false;
            }
            
            // 概率检查
            return Random.value < RestingChance;
        }
        
        /// <summary>
        /// 强制打开眼睛（例如开始说话时）
        /// </summary>
        public void ForceOpenEyes()
        {
            isBlinking = false;
            isResting = false;
            ScheduleNextBlink();
        }
        
        /// <summary>
        /// 强制进入养神状态
        /// </summary>
        public void ForceResting(float duration)
        {
            isBlinking = false;
            isResting = true;
            restingEndTime = Time.time + duration;
            lastRestingTime = Time.time;
        }
        
        /// <summary>
        /// 设置眨眼间隔
        /// </summary>
        public void SetBlinkInterval(float min, float max)
        {
            BlinkIntervalMin = min;
            BlinkIntervalMax = max;
        }
    }
    
    /// <summary>
    /// ⭐ v1.8.1: 待机行为状态机 - 管理好感度驱动的待机动画
    /// </summary>
    public class IdleBehaviorController
    {
        // === 配置 ===
        /// <summary>高好感度阈值</summary>
        public float HighAffinityThreshold = 60f;
        
        /// <summary>享受表情（闭眼微笑）概率</summary>
        public float ContentmentChance = 0.4f;
        
        /// <summary>享受表情持续时间（秒）</summary>
        public float ContentmentDurationMin = 2.0f;
        public float ContentmentDurationMax = 5.0f;
        
        // === 状态 ===
        private bool isInContentmentState;
        private float contentmentEndTime;
        
        // === 外部状态获取器 ===
        /// <summary>获取当前好感度</summary>
        public System.Func<float> GetAffinity { get; set; }
        
        /// <summary>检查是否正在说话</summary>
        public System.Func<bool> IsSpeaking { get; set; }
        
        /// <summary>检查是否处于战斗</summary>
        public System.Func<bool> IsInCombat { get; set; }
        
        // === 属性 ===
        /// <summary>是否处于享受状态（闭眼微笑）</summary>
        public bool IsInContentment => isInContentmentState && Time.time < contentmentEndTime;
        
        /// <summary>是否处于待机状态（可以触发特殊动画）</summary>
        public bool IsIdle
        {
            get
            {
                if (IsSpeaking != null && IsSpeaking()) return false;
                if (IsInCombat != null && IsInCombat()) return false;
                return true;
            }
        }
        
        /// <summary>是否为高好感度</summary>
        public bool IsHighAffinity => GetAffinity != null && GetAffinity() >= HighAffinityThreshold;
        
        /// <summary>
        /// 更新待机行为
        /// </summary>
        public void Update()
        {
            // 检查享受状态结束
            if (isInContentmentState && Time.time >= contentmentEndTime)
            {
                isInContentmentState = false;
            }
        }
        
        /// <summary>
        /// 尝试触发享受状态（由眨眼控制器在养神开始时调用）
        /// </summary>
        public bool TryTriggerContentment()
        {
            if (!IsIdle || !IsHighAffinity)
            {
                return false;
            }
            
            if (Random.value > ContentmentChance)
            {
                return false;
            }
            
            isInContentmentState = true;
            contentmentEndTime = Time.time + Random.Range(ContentmentDurationMin, ContentmentDurationMax);
            return true;
        }
        
        /// <summary>
        /// 强制结束享受状态（例如开始说话时）
        /// </summary>
        public void EndContentment()
        {
            isInContentmentState = false;
        }
    }
    
    /// <summary>
    /// ⭐ v1.8.1: 集成表情控制器 - 整合 Fader、Blink、Idle
    /// </summary>
    public class IntegratedExpressionController
    {
        // === 子系统 ===
        public readonly TextureFader EyeFader = new TextureFader();
        public readonly TextureFader MouthFader = new TextureFader();
        public readonly TextureFader BrowFader = new TextureFader();
        public readonly AdvancedBlinkController BlinkController = new AdvancedBlinkController();
        public readonly IdleBehaviorController IdleController = new IdleBehaviorController();
        
        // === 纹理缓存 ===
        private Texture2D closedEyesTex;
        private Texture2D contentmentEyesTex;
        private Texture2D contentmentMouthTex;
        private Texture2D currentEmotionEyesTex;
        private Texture2D currentEmotionMouthTex;
        
        // === 状态 ===
        private bool isSpeaking;
        private string personaName;
        
        /// <summary>
        /// 初始化控制器
        /// </summary>
        public void Initialize(string personaDefName)
        {
            personaName = personaDefName;
            
            // 加载闭眼纹理
            closedEyesTex = ContentFinder<Texture2D>.Get($"UI/Narrators/9x16/Layered/{personaDefName}/Closed_eyes", false);
            
            // 加载享受表情纹理（闭眼微笑）
            contentmentEyesTex = closedEyesTex; // 复用闭眼
            contentmentMouthTex = ContentFinder<Texture2D>.Get($"UI/Narrators/9x16/Layered/{personaDefName}/Smile_mouth", false);
            
            // 设置眨眼控制器的养神条件
            BlinkController.CanRestCondition = () => !isSpeaking && IdleController.IsIdle && IdleController.IsHighAffinity;
            
            // 连接待机控制器
            IdleController.IsSpeaking = () => isSpeaking;
        }
        
        /// <summary>
        /// 设置情绪纹理（由 ExpressionSystem 调用）
        /// </summary>
        public void SetEmotionTextures(Texture2D eyesTex, Texture2D mouthTex, bool useFade = true)
        {
            currentEmotionEyesTex = eyesTex;
            currentEmotionMouthTex = mouthTex;
            
            float speed = useFade ? TextureFader.DEFAULT_TRANSITION_SPEED : TextureFader.INSTANT_SPEED;
            
            // 只有不在眨眼/养神时才更新眼睛
            if (!BlinkController.IsEyesClosed)
            {
                EyeFader.SetTarget(eyesTex, speed);
            }
            
            // 只有不在说话时才更新嘴巴（情绪嘴型）
            if (!isSpeaking)
            {
                MouthFader.SetTarget(mouthTex, speed);
            }
        }
        
        /// <summary>
        /// 设置说话口型（由 MouthAnimationSystem 调用）
        /// 口型切换不使用渐变，直接跳变
        /// </summary>
        public void SetSpeakingMouth(Texture2D mouthTex)
        {
            isSpeaking = true;
            MouthFader.SetTarget(mouthTex, TextureFader.INSTANT_SPEED);
        }
        
        /// <summary>
        /// 停止说话（恢复情绪嘴型）
        /// </summary>
        public void StopSpeaking()
        {
            isSpeaking = false;
            
            // 恢复情绪嘴型，使用渐变
            if (currentEmotionMouthTex != null)
            {
                MouthFader.SetTarget(currentEmotionMouthTex, TextureFader.DEFAULT_TRANSITION_SPEED);
            }
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Update()
        {
            // 1. 更新眨眼控制器
            BlinkController.Update();
            
            // 2. 更新待机控制器
            IdleController.Update();
            
            // 3. 处理眼睛状态
            if (BlinkController.IsEyesClosed || IdleController.IsInContentment)
            {
                // 闭眼
                if (IdleController.IsInContentment && contentmentEyesTex != null)
                {
                    EyeFader.SetTarget(contentmentEyesTex, TextureFader.SLOW_SPEED);
                }
                else if (closedEyesTex != null)
                {
                    EyeFader.SetTarget(closedEyesTex, TextureFader.DEFAULT_TRANSITION_SPEED);
                }
            }
            else
            {
                // 睁眼
                if (currentEmotionEyesTex != null)
                {
                    EyeFader.SetTarget(currentEmotionEyesTex, TextureFader.DEFAULT_TRANSITION_SPEED);
                }
            }
            
            // 4. 处理享受状态的嘴型
            if (IdleController.IsInContentment && !isSpeaking && contentmentMouthTex != null)
            {
                MouthFader.SetTarget(contentmentMouthTex, TextureFader.SLOW_SPEED);
            }
            
            // 5. 更新所有 Fader
            EyeFader.Update();
            MouthFader.Update();
            BrowFader.Update();
        }
        
        /// <summary>
        /// 在 OnGUI 中绘制
        /// </summary>
        public void DrawOnGUI(Rect eyeRect, Rect mouthRect, Rect browRect)
        {
            // 先更新
            Update();
            
            // 绘制
            EyeFader.Draw(eyeRect);
            MouthFader.Draw(mouthRect);
            BrowFader.Draw(browRect);
        }
        
        /// <summary>
        /// 重置所有状态
        /// </summary>
        public void Reset()
        {
            EyeFader.Reset();
            MouthFader.Reset();
            BrowFader.Reset();
            BlinkController.ForceOpenEyes();
            IdleController.EndContentment();
            isSpeaking = false;
        }
    }
}
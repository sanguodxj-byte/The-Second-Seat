using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using TheSecondSeat.Narrator;
using TheSecondSeat.Settings;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// v1.6.82: 主动对话系统
    /// 功能：
    /// 1. 空闲检测（现实时间5分钟无操作触发主动对话）
    /// 2. 事件监听（监听 Letters 信件触发即时评论）
    /// </summary>
    public class ProactiveDialogueSystem : GameComponent
    {
        // ==================== 配置常量 ====================
        
        /// <summary>空闲触发时间（秒，默认5分钟）</summary>
        private const float IdleTimeoutSeconds = 300f;
        
        /// <summary>检查间隔（ticks，每秒检查一次）</summary>
        private const int CheckIntervalTicks = 60;
        
        /// <summary>主动对话冷却时间（秒）</summary>
        private const float ProactiveDialogueCooldown = 60f;
        
        /// <summary>是否启用主动对话（从设置读取）</summary>
        private bool IsEnabled
        {
            get
            {
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                // 如果设置不存在或未显式禁用，默认启用
                return modSettings?.enableProactiveDialogue ?? true;
            }
        }
        
        // ==================== 状态追踪 ====================
        
        /// <summary>上次用户操作的现实时间</summary>
        private DateTime lastUserActionTime;
        
        /// <summary>上次主动对话的现实时间</summary>
        private DateTime lastProactiveDialogueTime;
        
        /// <summary>上次检测到的信件数量</summary>
        private int lastLetterCount = 0;
        
        /// <summary>是否已触发空闲对话（防止重复触发）</summary>
        private bool hasTriggeredIdleDialogue = false;
        
        /// <summary>上次鼠标位置（用于检测操作）</summary>
        private Vector2 lastMousePosition;
        
        /// <summary>检查计时器</summary>
        private int ticksSinceLastCheck = 0;
        
        // ==================== 构造函数 ====================
        
        public ProactiveDialogueSystem(Game game) : base()
        {
            lastUserActionTime = DateTime.Now;
            lastProactiveDialogueTime = DateTime.Now.AddMinutes(-10); // 允许立即触发
            lastMousePosition = Vector2.zero;
        }
        
        // ==================== 生命周期 ====================
        
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            ticksSinceLastCheck++;
            if (ticksSinceLastCheck < CheckIntervalTicks) return;
            ticksSinceLastCheck = 0;
            
            // ⭐ 检查是否启用
            if (!IsEnabled) return;
            
            // 1. 检测用户操作（即使禁用也继续检测，保持空闲计时器准确）
            CheckUserActivity();
            
            // 2. 检测空闲状态
            CheckIdleState();
            
            // 3. 监听信件事件
            CheckLetterEvents();
        }
        
        // ==================== 用户活动检测 ====================
        
        /// <summary>
        /// 检测用户是否有操作
        /// ⭐ v1.7.7: 移除 Input/Event.current 依赖，使用 RimWorld 兼容方式
        /// </summary>
        private void CheckUserActivity()
        {
            try
            {
                // ⭐ v1.7.7: 使用游戏状态变化来推断用户活动
                // 检测游戏速度变化（用户按了1/2/3/4/空格）
                if (Find.TickManager != null)
                {
                    float currentSpeed = Find.TickManager.TickRateMultiplier;
                    bool isPaused = Find.TickManager.Paused;
                    
                    // 如果游戏状态发生变化，说明用户有操作
                    // 这里我们简单地假设：如果游戏没有暂停，用户就是活跃的
                    // 真正的空闲检测依赖于对话系统的调用来重置计时器
                    if (!isPaused && currentSpeed > 0f)
                    {
                        // 游戏正在运行 - 不自动重置空闲计时器
                        // 只有明确的用户操作（对话、交互）才会重置
                    }
                }
                
                // ⭐ v1.7.7: 检测选中单位变化（用户点击了小人）
                if (Find.Selector?.SelectedObjects?.Count > 0)
                {
                    // 有选中对象，可能是用户在操作
                    // 但不自动重置，避免误判
                }
            }
            catch
            {
                // 静默忽略异常
            }
        }
        
        /// <summary>
        /// 记录用户操作（重置空闲计时器）
        /// </summary>
        public void RecordUserAction(string actionType = "未知")
        {
            lastUserActionTime = DateTime.Now;
            hasTriggeredIdleDialogue = false; // 重置空闲触发标记
            
            if (Prefs.DevMode)
            {
                // 静默记录，不输出日志（太频繁）
            }
        }
        
        /// <summary>
        /// 外部调用：记录对话操作（聊天窗口发送消息）
        /// </summary>
        public void RecordDialogueAction()
        {
            RecordUserAction("对话");
            lastProactiveDialogueTime = DateTime.Now; // 对话后重置冷却
        }
        
        /// <summary>
        /// 外部调用：记录立绘交互（触摸、点击头像等）
        /// </summary>
        public void RecordPortraitInteraction()
        {
            RecordUserAction("立绘交互");
        }
        
        // ==================== 空闲检测 ====================
        
        /// <summary>
        /// 检查是否达到空闲阈值
        /// </summary>
        private void CheckIdleState()
        {
            if (hasTriggeredIdleDialogue) return; // 已触发，等待用户操作
            
            double idleSeconds = (DateTime.Now - lastUserActionTime).TotalSeconds;
            double cooldownSeconds = (DateTime.Now - lastProactiveDialogueTime).TotalSeconds;
            
            // 检查是否满足条件：空闲超过5分钟 且 冷却完成
            if (idleSeconds >= IdleTimeoutSeconds && cooldownSeconds >= ProactiveDialogueCooldown)
            {
                TriggerIdleDialogue();
            }
        }
        
        /// <summary>
        /// 触发空闲对话
        /// </summary>
        private void TriggerIdleDialogue()
        {
            hasTriggeredIdleDialogue = true;
            lastProactiveDialogueTime = DateTime.Now;
            
            Log.Message("[ProactiveDialogueSystem] 触发空闲对话（5分钟无操作）");
            
            // 构建空闲对话上下文
            string idleContext = BuildIdleContext();
            
            // 调用 NarratorController 触发对话
            var controller = Current.Game?.GetComponent<NarratorController>();
            if (controller != null && !controller.IsProcessing)
            {
                controller.TriggerNarratorUpdate(idleContext);
            }
        }
        
        /// <summary>
        /// 构建空闲对话上下文
        /// </summary>
        private string BuildIdleContext()
        {
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            float affinity = narrator?.Favorability ?? 0f;
            
            string context = "[系统提示: 玩家已经5分钟没有操作了，请主动发起对话]\n";
            context += "[注意: 不要说'我注意到你5分钟没操作'，而是自然地找话题]\n";
            
            // 根据好感度选择对话方向
            if (affinity >= 600)
            {
                context += "建议话题: 关心玩家状态、轻微撒娇、询问是否需要帮助\n";
            }
            else if (affinity >= 100)
            {
                context += "建议话题: 汇报殖民地近况、提出建议、闲聊\n";
            }
            else if (affinity >= -100)
            {
                context += "建议话题: 简短问候、观察评论\n";
            }
            else
            {
                context += "建议话题: 冷淡的提醒、讽刺性评论\n";
            }
            
            return context;
        }
        
        // ==================== 事件监听 ====================
        
        /// <summary>
        /// 监听信件事件（Letters）
        /// </summary>
        private void CheckLetterEvents()
        {
            if (Find.LetterStack == null) return;
            
            int currentLetterCount = Find.LetterStack.LettersListForReading.Count;
            
            // 检测新信件
            if (currentLetterCount > lastLetterCount)
            {
                int newLetterCount = currentLetterCount - lastLetterCount;
                
                // 获取最新的信件
                var letters = Find.LetterStack.LettersListForReading;
                for (int i = letters.Count - newLetterCount; i < letters.Count; i++)
                {
                    if (i >= 0 && i < letters.Count)
                    {
                        ProcessNewLetter(letters[i]);
                    }
                }
            }
            
            lastLetterCount = currentLetterCount;
        }
        
        /// <summary>
        /// 处理新信件，决定是否触发评论
        /// </summary>
        private void ProcessNewLetter(Letter letter)
        {
            if (letter == null) return;
            
            // 检查冷却
            double cooldownSeconds = (DateTime.Now - lastProactiveDialogueTime).TotalSeconds;
            if (cooldownSeconds < 30f) return; // 信件评论冷却30秒
            
            // 判断事件重要性
            LetterImportance importance = ClassifyLetterImportance(letter);
            
            // ⭐ 修复：Low 和 Ignore 都不触发评论（避免过多对话）
            if (importance <= LetterImportance.Low) return;
            
            Log.Message($"[ProactiveDialogueSystem] 检测到重要信件: {letter.Label} (重要性: {importance})");
            
            // 触发事件评论
            TriggerEventComment(letter, importance);
        }
        
        /// <summary>
        /// 分类信件重要性
        /// </summary>
        private LetterImportance ClassifyLetterImportance(Letter letter)
        {
            // 根据 LetterDef 分类
            if (letter.def == LetterDefOf.ThreatBig || letter.def == LetterDefOf.ThreatSmall)
            {
                return LetterImportance.Critical; // 威胁事件
            }
            
            if (letter.def == LetterDefOf.Death)
            {
                return LetterImportance.Critical; // 死亡事件
            }
            
            if (letter.def == LetterDefOf.PositiveEvent)
            {
                return LetterImportance.Medium; // 正面事件
            }
            
            if (letter.def == LetterDefOf.NegativeEvent)
            {
                return LetterImportance.High; // 负面事件
            }
            
            if (letter.def == LetterDefOf.NeutralEvent)
            {
                return LetterImportance.Low; // 中性事件
            }
            
            // 根据标签关键词判断
            string label = (letter.Label != null ? letter.Label.ToString() : "").ToLower();
            if (label.Contains("raid") || label.Contains("袭击") || label.Contains("attack") || label.Contains("攻击"))
            {
                return LetterImportance.Critical;
            }
            
            if (label.Contains("death") || label.Contains("死亡") || label.Contains("killed") || label.Contains("杀死"))
            {
                return LetterImportance.Critical;
            }
            
            if (label.Contains("trade") || label.Contains("交易") || label.Contains("caravan") || label.Contains("商队"))
            {
                return LetterImportance.Medium;
            }
            
            return LetterImportance.Low;
        }
        
        /// <summary>
        /// 触发事件评论
        /// </summary>
        private void TriggerEventComment(Letter letter, LetterImportance importance)
        {
            lastProactiveDialogueTime = DateTime.Now;
            
            // 构建事件评论上下文
            string eventContext = BuildEventContext(letter, importance);
            
            // 调用 NarratorController 触发对话
            var controller = Current.Game?.GetComponent<NarratorController>();
            if (controller != null && !controller.IsProcessing)
            {
                controller.TriggerNarratorUpdate(eventContext);
            }
        }
        
        /// <summary>
        /// 构建事件评论上下文
        /// </summary>
        private string BuildEventContext(Letter letter, LetterImportance importance)
        {
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            float affinity = narrator?.Favorability ?? 0f;
            
            string urgency = importance switch
            {
                LetterImportance.Critical => "紧急",
                LetterImportance.High => "重要",
                LetterImportance.Medium => "一般",
                _ => "轻微"
            };
            
            string context = $"[游戏事件通知 - {urgency}]\n";
            context += $"事件类型: {letter.def?.defName ?? "未知"}\n";
            context += $"事件标题: {letter.Label}\n";
            context += $"事件描述: {TruncateText(GetLetterDescription(letter), 200)}\n";

            // ⭐ 战术情报注入：对于高威胁事件，自动扫描并提供位置信息
            if (importance >= LetterImportance.High)
            {
                string tacticalInfo = GetTacticalAnalysis();
                if (!string.IsNullOrEmpty(tacticalInfo))
                {
                    context += $"\n[战术情报补全]\n{tacticalInfo}\n";
                }
            }

            context += "\n[请对这个事件发表简短评论（1-2句话），根据你的人格和好感度做出反应]\n";
            
            // 根据好感度调整反应建议
            if (importance == LetterImportance.Critical)
            {
                if (affinity >= 300)
                {
                    context += "建议反应: 担忧、警告、准备帮助\n";
                }
                else if (affinity >= -100)
                {
                    context += "建议反应: 提醒、评论\n";
                }
                else
                {
                    context += "建议反应: 讽刺、冷漠或幸灾乐祸\n";
                }
            }
            
            return context;
        }
        
        /// <summary>
        /// 获取信件描述文本
        /// </summary>
        private string GetLetterDescription(Letter letter)
        {
            if (letter == null) return "";
            
            // 尝试获取信件的文本内容
            try
            {
                // 对于 ChoiceLetter，使用 Text 属性
                if (letter is ChoiceLetter choiceLetter)
                {
                    string text = choiceLetter.Text.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
                
                // 其他类型使用标签
                string label = letter.Label.ToString();
                return !string.IsNullOrEmpty(label) ? label : "";
            }
            catch
            {
                try
                {
                    return letter.Label.ToString();
                }
                catch
                {
                    return "";
                }
            }
        }
        
        /// <summary>
        /// 截断文本
        /// </summary>
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// 获取战术分析（简化版的 ScanMap 逻辑）
        /// </summary>
        private string GetTacticalAnalysis()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return "";

                // 1. 确定锚点（居住区中心）
                IntVec3 homeCenter = map.Center;
                var homeCells = map.areaManager.Home.ActiveCells;
                if (homeCells.Any())
                {
                    long sumX = 0; long sumZ = 0; int count = 0;
                    foreach (var cell in homeCells) { sumX += cell.x; sumZ += cell.z; count++; }
                    homeCenter = new IntVec3((int)(sumX / count), 0, (int)(sumZ / count));
                }

                // 2. 扫描威胁
                var hostiles = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Downed && !p.Dead)
                    .ToList();

                // 3. 扫描我方战力
                var colonists = map.mapPawns.FreeColonists;
                int totalColonists = colonists.Count;
                int activeColonists = colonists.Count(c => !c.Downed && !c.Dead && c.health.State == PawnHealthState.Mobile);
                int downedColonists = totalColonists - activeColonists;

                string friendlyStatus = $"我方状态: 总人数 {totalColonists}, 可战斗 {activeColonists}, 倒地/无法行动 {downedColonists}。";

                if (hostiles.Any())
                {
                    // 计算重心
                    long hSumX = 0; long hSumZ = 0;
                    foreach (var h in hostiles) { hSumX += h.Position.x; hSumZ += h.Position.z; }
                    var threatCenter = new IntVec3((int)(hSumX / hostiles.Count), 0, (int)(hSumZ / hostiles.Count));

                    // 计算方向
                    string direction = GetDirection(homeCenter, threatCenter);
                    
                    return $"扫描结果: 发现 {hostiles.Count} 个敌对目标，位于基地{direction} ({threatCenter.x}, {threatCenter.z})。\n{friendlyStatus}";
                }
                
                return friendlyStatus; // 即使没有敌人，也返回我方状态（可能是其他灾难）
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProactiveDialogueSystem] 战术分析失败: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 计算相对方向（复用自 ScanMapCommand）
        /// </summary>
        private string GetDirection(IntVec3 from, IntVec3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            double angle = Math.Atan2(dz, dx) * (180 / Math.PI);

            if (angle > -22.5 && angle <= 22.5) return "东侧(右)";
            if (angle > 22.5 && angle <= 67.5) return "东北侧(右上)";
            if (angle > 67.5 && angle <= 112.5) return "北侧(上)";
            if (angle > 112.5 && angle <= 157.5) return "西北侧(左上)";
            if (angle > 157.5 || angle <= -157.5) return "西侧(左)";
            if (angle > -157.5 && angle <= -112.5) return "西南侧(左下)";
            if (angle > -112.5 && angle <= -67.5) return "南侧(下)";
            if (angle > -67.5 && angle <= -22.5) return "东南侧(右下)";
            return "未知方位";
        }
        
        // ==================== 公共接口 ====================
        
        /// <summary>
        /// 获取空闲时间（秒）
        /// </summary>
        public double GetIdleTimeSeconds()
        {
            return (DateTime.Now - lastUserActionTime).TotalSeconds;
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            double idleSeconds = GetIdleTimeSeconds();
            double cooldownRemaining = Math.Max(0, ProactiveDialogueCooldown - (DateTime.Now - lastProactiveDialogueTime).TotalSeconds);
            
            return $"空闲: {idleSeconds:F0}s / {IdleTimeoutSeconds}s\n" +
                   $"冷却: {cooldownRemaining:F0}s\n" +
                   $"信件数: {lastLetterCount}\n" +
                   $"已触发空闲: {hasTriggeredIdleDialogue}";
        }
        
        // ==================== 存档 ====================
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastLetterCount, "lastLetterCount", 0);
            Scribe_Values.Look(ref hasTriggeredIdleDialogue, "hasTriggeredIdleDialogue", false);
            
            // 加载时重置时间戳
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lastUserActionTime = DateTime.Now;
                lastProactiveDialogueTime = DateTime.Now.AddMinutes(-2); // 加载后2分钟可以触发
            }
        }
    }
    
    /// <summary>
    /// 信件重要性等级
    /// </summary>
    public enum LetterImportance
    {
        Ignore = 0,    // 忽略
        Low = 1,       // 低（不触发评论）
        Medium = 2,    // 中（可选触发）
        High = 3,      // 高（触发评论）
        Critical = 4   // 紧急（立即触发）
    }
}
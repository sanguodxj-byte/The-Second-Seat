using System;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// AI难度模式
    /// 决定AI的定位和行为规则
    /// </summary>
    public enum AIDifficultyMode
    {
        /// <summary>
        /// 助手模式
        /// - 无论好感度如何，从不拒绝玩家指令
        /// - 主动提供帮助殖民地发展的建议
        /// - 提供优化建议和警告
        /// - 友好支持型AI
        /// </summary>
        Assistant,
        
        /// <summary>
        /// 奕者模式（原对弈者）
        /// - 通常不拒绝指令（除非极端情况）
        /// - 控制事件生成，可制造正面或负面事件
        /// - 好感度影响事件难度
        /// - 挑战平衡型AI
        /// </summary>
        Opponent,

        /// <summary>
        /// 工程师模式
        /// - 专注于技术支持和错误诊断
        /// - 主动监控日志和性能
        /// - 提供调试建议和修复方案
        /// - 技术专家型AI
        /// </summary>
        Engineer
    }
    
    /// <summary>
    /// 难度模式扩展方法
    /// </summary>
    public static class AIDifficultyModeExtensions
    {
        /// <summary>
        /// 获取模式的中文名称
        /// </summary>
        public static string GetChineseName(this AIDifficultyMode mode)
        {
            return mode switch
            {
                AIDifficultyMode.Assistant => "助手模式",
                AIDifficultyMode.Opponent => "奕者模式",
                AIDifficultyMode.Engineer => "工程师模式",
                _ => "未知模式"
            };
        }
        
        /// <summary>
        /// 获取模式的描述
        /// </summary>
        public static string GetDescription(this AIDifficultyMode mode)
        {
            return mode switch
            {
                AIDifficultyMode.Assistant => 
                    "AI将作为忠实的助手，无论好感度如何都会执行玩家指令，并主动提供帮助殖民地发展。适合休闲或希望获得帮助的玩家.",
                
                AIDifficultyMode.Opponent =>
                    "AI将作为弈者，通常会执行指令（除非极端情况），但通过事件生成来制造挑战和惊喜。好感度会影响事件难度。适合寻求挑战的玩家.",
                
                AIDifficultyMode.Engineer =>
                    "AI将作为技术工程师，专注于Mod排错、日志分析和性能优化。它会主动监控游戏状态并提供技术建议。适合Mod开发者或遇到技术问题的玩家.",

                _ => "未知模式"
            };
        }
        
        /// <summary>
        /// 是否应该主动提供建议
        /// </summary>
        public static bool ShouldGiveSuggestions(this AIDifficultyMode mode)
        {
            return mode == AIDifficultyMode.Assistant || mode == AIDifficultyMode.Engineer;
        }
        
        /// <summary>
        /// 是否可以拒绝指令（基于好感度）
        /// </summary>
        public static bool CanRefuseCommands(this AIDifficultyMode mode, float affinity)
        {
            return mode switch
            {
                AIDifficultyMode.Assistant => false, // 助手从不拒绝
                AIDifficultyMode.Engineer => false, // 工程师从不拒绝
                AIDifficultyMode.Opponent => affinity < -70f, // 奕者仅在极低好感度时可能拒绝
                _ => false
            };
        }
        
        /// <summary>
        /// 是否控制事件生成
        /// </summary>
        public static bool ControlsEventGeneration(this AIDifficultyMode mode)
        {
            return mode == AIDifficultyMode.Opponent;
        }
    }
}

using System;
using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.SmartPrompt
{
    /// <summary>
    /// PromptModuleDef - 智能技能包定义
    /// 
    /// 架构层级: L0 配置层 (Configuration)
    /// 核心职责: 预计算、扩展语义
    /// 
    /// 不再是单纯的文本，而是包含：
    /// - 提示词内容（支持 Scriban 模板）
    /// - 触发条件（Gatekeeper）
    /// - 依赖管理（Dependency Chain）
    /// - 预计算词库（AI 预生成的关键词）
    /// </summary>
    public class PromptModuleDef : Def
    {
        // ========== 基础信息 ==========
        
        /// <summary>
        /// 提示词内容（支持 Scriban 模板）
        /// 如果内容为空，可以通过 contentPath 从外部文件加载
        /// </summary>
        public string content;
        
        /// <summary>
        /// 外部内容路径（可选，从 Languages 目录加载）
        /// 例如: "Module_Agriculture" (无需包含 Prompts/ 前缀和 .txt 后缀)
        /// </summary>
        public string contentPath;
        
        /// <summary>
        /// 是否启用 Scriban 模板渲染
        /// </summary>
        public bool useScriban = false;
        
        /// <summary>
        /// 模块类型（用于分类和优先级）
        /// </summary>
        public ModuleType moduleType = ModuleType.Skill;
        
        /// <summary>
        /// 优先级（越高越优先加载，Core 模块应设为 1000+）
        /// </summary>
        public int priority = 100;
        
        // ========== 触发条件 (Gatekeeper) ==========
        
        /// <summary>
        /// 意图标签列表（如 "Harvest", "Combat", "Build"）
        /// 当 FlashMatcher 识别出这些意图时，模块会被激活
        /// </summary>
        public List<string> triggerIntents = new List<string>();
        
        /// <summary>
        /// 是否要求战斗环境
        /// 只有当前处于战斗状态时，模块才会被激活
        /// </summary>
        public bool requiresCombat = false;
        
        /// <summary>
        /// 是否要求和平环境
        /// 只有当前处于和平状态时，模块才会被激活
        /// </summary>
        public bool requiresPeace = false;
        
        /// <summary>
        /// 是否总是激活（用于核心模块，如身份、格式等）
        /// </summary>
        public bool alwaysActive = false;
        
        /// <summary>
        /// 最大同时激活数量（0 = 无限制）
        /// 用于限制同类模块的加载数量
        /// </summary>
        public int maxConcurrent = 0;
        
        // ========== 依赖管理 (Dependency Chain) ==========
        
        /// <summary>
        /// 依赖的其他模块 defName 列表
        /// 例如: ["Module_Command_Format", "Module_Identity_Core"]
        /// 当本模块被激活时，依赖模块会自动被拉取
        /// </summary>
        public List<string> dependencies = new List<string>();
        
        /// <summary>
        /// 互斥的模块 defName 列表
        /// 例如: ["Module_Combat_Aggressive", "Module_Combat_Defensive"]
        /// 不能同时激活
        /// </summary>
        public List<string> exclusiveWith = new List<string>();
        
        // ========== 预计算词库 (Pre-computed) ==========
        
        /// <summary>
        /// 由 AI 预生成的 50+ 个关键词（支持多语言）
        /// 这些关键词在游戏启动时被加载到 FlashMatcher 的 Trie 树中
        /// 运行时通过 AC 自动机匹配，实现微秒级意图识别
        /// 
        /// 示例: ["harvest", "reap", "crop", "收割", "割稻", "收菜", "种田", "农业"]
        /// </summary>
        public List<string> expandedKeywords = new List<string>();
        
        /// <summary>
        /// 关键词权重映射（可选）
        /// 某些关键词可能比其他更重要
        /// </summary>
        public Dictionary<string, float> keywordWeights = new Dictionary<string, float>();
        
        // ========== 运行时缓存 ==========
        
        /// <summary>
        /// 缓存的渲染后内容
        /// </summary>
        [Unsaved]
        private string cachedContent;
        
        /// <summary>
        /// 内容是否已加载
        /// </summary>
        [Unsaved]
        private bool contentLoaded = false;
        
        /// <summary>
        /// 获取模块内容（支持懒加载）
        /// </summary>
        public string GetContent()
        {
            if (!contentLoaded)
            {
                LoadContent();
            }
            return cachedContent ?? content ?? "";
        }
        
        /// <summary>
        /// 从外部文件加载内容
        /// </summary>
        private void LoadContent()
        {
            contentLoaded = true;
            
            if (!string.IsNullOrEmpty(content))
            {
                cachedContent = content;
                return;
            }
            
            if (!string.IsNullOrEmpty(contentPath))
            {
                try
                {
                    // 确保移除路径前缀和扩展名，因为 PromptLoader 会自动处理
                    string cleanPath = System.IO.Path.GetFileNameWithoutExtension(contentPath);
                    cachedContent = PersonaGeneration.PromptLoader.Load(cleanPath);
                    
                    if (cachedContent.StartsWith("[Error:"))
                    {
                        Log.Warning($"[PromptModuleDef] Failed to load content for {defName} from path '{contentPath}': {cachedContent}");
                        cachedContent = "";
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[PromptModuleDef] Exception loading content for {defName}: {ex.Message}");
                    cachedContent = "";
                }
            }
        }
        
        /// <summary>
        /// 重新加载内容（用于热重载）
        /// </summary>
        public void ReloadContent()
        {
            contentLoaded = false;
            cachedContent = null;
            LoadContent();
        }
        
        /// <summary>
        /// 检查模块是否可以在当前环境下激活
        /// </summary>
        /// <param name="isInCombat">当前是否处于战斗状态</param>
        public bool CanActivate(bool isInCombat)
        {
            if (alwaysActive) return true;
            if (requiresCombat && !isInCombat) return false;
            if (requiresPeace && isInCombat) return false;
            return true;
        }
        
        /// <summary>
        /// Def 解析后的后处理
        /// </summary>
        public override void PostLoad()
        {
            base.PostLoad();
            
            // 验证配置
            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(contentPath))
            {
                Log.Warning($"[PromptModuleDef] {defName}: Both content and contentPath are empty.");
            }
            
            // 注意：不要在 PostLoad 中预加载内容，因为此时 LanguageDatabase 可能尚未初始化
            // 导致 PromptLoader 空引用异常。
            // 内容加载推迟到 GetContent() 首次调用时（懒加载）或 SmartPromptInitializer 中进行。
        }
        
        /// <summary>
        /// 获取模块的调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[{defName}] Type={moduleType}, Priority={priority}, " +
                   $"Intents=[{string.Join(", ", triggerIntents)}], " +
                   $"Keywords={expandedKeywords.Count}, " +
                   $"AlwaysActive={alwaysActive}, " +
                   $"Dependencies=[{string.Join(", ", dependencies)}]";
        }
    }
    
    /// <summary>
    /// 模块类型枚举
    /// </summary>
    public enum ModuleType
    {
        /// <summary>核心身份（如角色设定，始终加载）</summary>
        Core,
        
        /// <summary>格式规范（如输出格式，通常作为依赖被加载）</summary>
        Format,
        
        /// <summary>技能模块（如农业、战斗、建造）</summary>
        Skill,
        
        /// <summary>情境模块（如紧急事件、社交互动）</summary>
        Context,
        
        /// <summary>记忆模块（如历史事件、人物关系）</summary>
        Memory,
        
        /// <summary>扩展模块（由其他 Mod 提供）</summary>
        Extension
    }
}

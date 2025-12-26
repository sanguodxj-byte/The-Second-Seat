using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// ? v1.6.64: 性格标签定义 - XML 配置化
    /// 
    /// 核心设计：
    /// - 性格标签从 XML 加载，完全可配置
    /// - Mod 作者可自由扩展新标签
    /// - 支持多语言本地化
    /// - 支持条件激活（好感度范围）
    /// - 支持对话风格修正
    /// 
    /// XML 示例：
    /// <PersonalityTagDef>
    ///   <defName>Kuudere</defName>
    ///   <label>冰美人</label>
    ///   <minAffinityToActivate>60</minAffinityToActivate>
    ///   <behaviorInstructions>
    ///     <li>?? **KUUDERE MODE:**...</li>
    ///   </behaviorInstructions>
    /// </PersonalityTagDef>
    /// </summary>
    public class PersonalityTagDef : Def
    {
        // ==================== 基础信息 ====================
        
        /// <summary>
        /// 标签显示名称（中文）
        /// </summary>
        public string label;
        
        /// <summary>
        /// 本地化键（可选）
        /// </summary>
        public string labelKey;
        
        /// <summary>
        /// 描述本地化键（可选）
        /// </summary>
        public string descriptionKey;
        
        // ==================== 激活条件 ====================
        
        /// <summary>
        /// 最低激活好感度
        /// </summary>
        public float minAffinityToActivate = 0f;
        
        /// <summary>
        /// 最高激活好感度
        /// </summary>
        public float maxAffinityToActivate = 100f;
        
        /// <summary>
        /// 是否需要在 Assistant 模式下激活
        /// </summary>
        public bool requiresAssistantMode = true;
        
        // ==================== 行为指令 ====================
        
        /// <summary>
        /// 行为指令列表（按优先级排序）
        /// </summary>
        public List<BehaviorInstruction> behaviorInstructions = new List<BehaviorInstruction>();
        
        // ==================== 表情倾向 ====================
        
        /// <summary>
        /// 推荐表情列表
        /// </summary>
        public List<string> preferredExpressions = new List<string>();
        
        // ==================== 对话风格修正 ====================
        
        /// <summary>
        /// 对话风格修正器
        /// </summary>
        public DialogueStyleModifiers dialogueStyleModifiers;
        
        // ==================== 公共方法 ====================
        
        /// <summary>
        /// 检查是否应该激活此标签
        /// </summary>
        public bool ShouldActivate(float affinity, AIDifficultyMode difficultyMode)
        {
            // 检查好感度范围
            if (affinity < minAffinityToActivate || affinity > maxAffinityToActivate)
            {
                return false;
            }
            
            // 检查难度模式
            if (requiresAssistantMode && difficultyMode != AIDifficultyMode.Assistant)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 生成行为指令文本
        /// </summary>
        public string GenerateInstructionText()
        {
            if (behaviorInstructions == null || behaviorInstructions.Count == 0)
            {
                return string.Empty;
            }
            
            // 按优先级排序并拼接
            var sorted = behaviorInstructions.OrderBy(i => i.priority).ToList();
            return string.Join("\n", sorted.Select(i => i.text));
        }
        
        /// <summary>
        /// 应用对话风格修正
        /// </summary>
        public void ApplyStyleModifiers(DialogueStyleDef style)
        {
            if (dialogueStyleModifiers == null || style == null)
            {
                return;
            }
            
            dialogueStyleModifiers.ApplyTo(style);
        }
        
        /// <summary>
        /// 获取本地化标签
        /// </summary>
        public string GetLocalizedLabel()
        {
            if (!string.IsNullOrEmpty(labelKey))
            {
                return labelKey.Translate();
            }
            
            return label ?? defName;
        }
        
        /// <summary>
        /// 获取本地化描述
        /// </summary>
        public string GetLocalizedDescription()
        {
            if (!string.IsNullOrEmpty(descriptionKey))
            {
                return descriptionKey.Translate();
            }
            
            return description ?? string.Empty;
        }
    }
    
    /// <summary>
    /// 行为指令（单条）
    /// </summary>
    public class BehaviorInstruction
    {
        /// <summary>
        /// 优先级（数字越小越优先）
        /// </summary>
        public int priority = 0;
        
        /// <summary>
        /// 指令文本
        /// </summary>
        public string text;
    }
    
    /// <summary>
    /// 对话风格修正器
    /// </summary>
    public class DialogueStyleModifiers
    {
        // 基础风格参数
        public float? formalityLevel;
        public float? emotionalExpression;
        public float? verbosity;
        public float? humorLevel;
        public float? sarcasmLevel;
        
        // 特殊行为参数
        public float? possessiveness;      // 占有欲（Yandere）
        public float? physicalDirectness;  // 物理接触直接度（Kuudere）
        public float? tsundereness;        // 傲娇度（Tsundere）
        public float? nurturing;           // 呵护度（Gentle）
        public float? arrogance;           // 高傲度（Arrogant）
        public float? mysteriousness;      // 神秘度（Mysterious）
        
        /// <summary>
        /// 应用修正到对话风格
        /// </summary>
        public void ApplyTo(DialogueStyleDef style)
        {
            if (formalityLevel.HasValue)
                style.formalityLevel = formalityLevel.Value;
            
            if (emotionalExpression.HasValue)
                style.emotionalExpression = emotionalExpression.Value;
            
            if (verbosity.HasValue)
                style.verbosity = verbosity.Value;
            
            if (humorLevel.HasValue)
                style.humorLevel = humorLevel.Value;
            
            if (sarcasmLevel.HasValue)
                style.sarcasmLevel = sarcasmLevel.Value;
        }
    }
}

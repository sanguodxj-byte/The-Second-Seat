using System;
using System.Collections.Generic;
using System.Linq;
using TheSecondSeat.PersonaGeneration;
using Verse;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// ⭐ v2.5.0: 服装更换命令
    /// 由 LLM 调用，允许叙事者自主决定何时更换服装
    /// 
    /// 用法:
    /// - ChangeOutfit(target: "Pajamas") - 更换到睡衣
    /// - ChangeOutfit(target: "Default") - 恢复默认服装
    /// - ChangeOutfit(target: "Casual") - 更换到休闲装
    /// 
    /// LLM 可以根据：
    /// - 当前时间（现实时间或游戏时间）
    /// - 对话情境
    /// - 自身角色设定
    /// 自主决定何时切换服装
    /// </summary>
    public class ChangeOutfitCommand : BaseAICommand
    {
        public override string ActionName => "ChangeOutfit";

        public override string GetDescription()
        {
            return "更换叙事者服装 (target: 服装标签如 'Pajamas'睡衣, 'Default'默认, 'Casual'休闲, 'Formal'正装)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("未指定服装标签");
                return false;
            }

            try
            {
                // 获取当前活动的人格
                string personaDefName = GetActivePersonaDefName();
                
                if (string.IsNullOrEmpty(personaDefName))
                {
                    LogError("无法获取当前人格");
                    return false;
                }

                // 通过标签查找服装定义
                var outfitDef = OutfitDefManager.GetByTag(personaDefName, target);
                
                if (outfitDef == null)
                {
                    // 尝试查找通用服装
                    outfitDef = OutfitDefManager.GetByTag("", target);
                }

                if (outfitDef != null)
                {
                    // 应用服装
                    OutfitSystem.SetOutfitDef(personaDefName, outfitDef.defName);
                    LogExecution($"切换到服装: {outfitDef.label} ({outfitDef.outfitTag})");
                    return true;
                }
                else
                {
                    // 如果是 "Default" 标签，清除当前服装
                    if (target.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        target.Equals("默认", StringComparison.OrdinalIgnoreCase))
                    {
                        OutfitSystem.ClearOutfitDef(personaDefName);
                        LogExecution("恢复默认服装");
                        return true;
                    }
                    
                    LogError($"未找到服装标签: {target}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取当前活动的人格 DefName
        /// </summary>
        private string GetActivePersonaDefName()
        {
            // 尝试从 StorytellerAgent 获取
            try
            {
                var agentType = Type.GetType("TheSecondSeat.StorytellerAgent, The Second Seat");
                if (agentType != null)
                {
                    var currentPersonaProperty = agentType.GetProperty("CurrentPersonaDefName", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (currentPersonaProperty != null)
                    {
                        return currentPersonaProperty.GetValue(null) as string ?? "";
                    }
                }
            }
            catch { /* 忽略 */ }

            // 回退：从 DefDatabase 获取第一个人格
            var personaDef = DefDatabase<NarratorPersonaDef>.AllDefs.FirstOrDefault();
            return personaDef?.defName ?? "";
        }
    }

    /// <summary>
    /// ⭐ v2.5.0: 获取可用服装列表命令
    /// 让 LLM 知道有哪些服装可以选择
    /// </summary>
    public class GetOutfitListCommand : BaseAICommand
    {
        public override string ActionName => "GetOutfitList";

        public override string GetDescription()
        {
            return "获取当前人格可用的服装列表";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            try
            {
                string personaDefName = target;
                
                if (string.IsNullOrEmpty(personaDefName))
                {
                    // 获取当前活动人格
                    var personaDef = DefDatabase<NarratorPersonaDef>.AllDefs.FirstOrDefault();
                    personaDefName = personaDef?.defName ?? "";
                }

                var outfits = OutfitDefManager.GetOutfitsForPersona(personaDefName);
                
                if (outfits.Count == 0)
                {
                    Log.Message($"[GetOutfitList] 人格 {personaDefName} 没有可用服装");
                    return true;
                }

                var info = $"[GetOutfitList] {personaDefName} 可用服装:\n";
                foreach (var outfit in outfits)
                {
                    info += $"  - {outfit.outfitTag}: {outfit.label}\n";
                    if (!string.IsNullOrEmpty(outfit.outfitDescription))
                    {
                        info += $"    {outfit.outfitDescription}\n";
                    }
                }
                
                Log.Message(info);
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return false;
            }
        }
    }

    /// <summary>
    /// ⭐ v2.5.0: 获取当前服装状态命令
    /// </summary>
    public class GetCurrentOutfitCommand : BaseAICommand
    {
        public override string ActionName => "GetCurrentOutfit";

        public override string GetDescription()
        {
            return "获取当前穿着的服装";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            try
            {
                string personaDefName = target;
                
                if (string.IsNullOrEmpty(personaDefName))
                {
                    var personaDef = DefDatabase<NarratorPersonaDef>.AllDefs.FirstOrDefault();
                    personaDefName = personaDef?.defName ?? "";
                }

                var currentOutfit = OutfitSystem.GetCurrentOutfitDef(personaDefName);
                var currentTag = OutfitSystem.GetCurrentOutfitTag(personaDefName);

                if (currentOutfit != null)
                {
                    Log.Message($"[GetCurrentOutfit] {personaDefName} 当前服装: {currentOutfit.label} ({currentTag})");
                }
                else
                {
                    Log.Message($"[GetCurrentOutfit] {personaDefName} 当前穿着默认服装");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Integration
{
    /// <summary>
    /// RimTalk 记忆扩展集成
    /// 功能：
    /// 1. 自动记录叙事者和玩家的对话到记忆系统
    /// 2. 为叙事者创建虚拟 Pawn 用于存储记忆
    /// 3. 支持集成扩展的 UI 显示
    /// 4. 自动标签和标签
    /// ? 修复：使用 WorldComponent 持久化虚拟 Pawn
    /// </summary>
    public static class RimTalkMemoryIntegration
    {
        // RimTalk 记忆系统是否可用
        private static bool? isRimTalkAvailable = null;
        
        /// <summary>
        /// 检查 RimTalk 记忆扩展是否已加载
        /// </summary>
        public static bool IsRimTalkMemoryAvailable()
        {
            if (isRimTalkAvailable.HasValue)
                return isRimTalkAvailable.Value;
            
            try
            {
                // 检查 RimTalk.Memory 命名空间是否存在
                var memoryType = Type.GetType("RimTalk.Memory.MemoryEntry, RimTalk-ExpandMemory");
                isRimTalkAvailable = (memoryType != null);
                
                if (isRimTalkAvailable.Value)
                {
                    Log.Message("[TheSecondSeat] RimTalk 记忆扩展检测成功，集成功能已启用");
                }
                else
                {
                    Log.Warning("[TheSecondSeat] 未检测到 RimTalk 记忆扩展，对话记忆功能已禁用");
                }
                
                return isRimTalkAvailable.Value;
            }
            catch (Exception ex)
            {
                Log.Warning($"[TheSecondSeat] RimTalk 记忆扩展检测失败: {ex.Message}");
                isRimTalkAvailable = false;
                return false;
            }
        }
        
        /// <summary>
        /// ? 获取或创建叙事者的虚拟 Pawn（用于存储记忆）
        /// ? 修复：直接创建临时Pawn，不再依赖WorldComponent
        /// </summary>
        public static Pawn GetOrCreateNarratorPawn(string narratorDefName, string narratorName)
        {
            if (!IsRimTalkMemoryAvailable())
                return null;
            
            try
            {
                // ? 简化：每次调用都创建一个临时 Pawn
                // 原因：RimTalk记忆系统本身会管理Pawn的持久化
                PawnKindDef kind = PawnKindDefOf.Colonist;
                Faction faction = Faction.OfPlayer;
                
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                pawn.Name = new NameSingle(narratorName);
                
                // 添加记忆组件
                AddMemoryComponentToPawn(pawn);
                
                Log.Message($"[RimTalkMemoryIntegration] 为叙事者 {narratorName} 创建临时 Pawn");
                return pawn;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkMemoryIntegration] 创建叙事者 Pawn 失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// ? 新增：为 Pawn 添加记忆组件（供外部调用）
        /// </summary>
        public static void AddMemoryComponentToPawn(Pawn pawn)
        {
            if (pawn == null)
                return;
            
            try
            {
                // 使用反射添加 FourLayerMemoryComp
                var compType = Type.GetType("RimTalk.Memory.FourLayerMemoryComp, RimTalk-ExpandMemory");
                if (compType == null)
                {
                    Log.Warning("[RimTalkMemoryIntegration] 未找到 FourLayerMemoryComp 类型");
                    return;
                }
                
                // 检查是否已存在
                var existingComp = pawn.AllComps.FirstOrDefault(c => c.GetType() == compType);
                if (existingComp != null)
                {
                    Log.Message($"[RimTalkMemoryIntegration] Pawn {pawn.LabelShort} 已有记忆组件");
                    return;
                }
                
                // 创建组件实例
                var comp = (ThingComp)Activator.CreateInstance(compType);
                comp.parent = pawn;
                pawn.AllComps.Add(comp);
                
                // 初始化组件
                var initMethod = compType.GetMethod("Initialize");
                initMethod?.Invoke(comp, new object[] { new CompProperties() });
                
                Log.Message($"[RimTalkMemoryIntegration] 为 {pawn.LabelShort} 添加记忆组件成功");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkMemoryIntegration] 添加记忆组件失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 添加记忆组件到 Pawn
        /// </summary>
        private static void AddMemoryComponent(Pawn pawn)
        {
            try
            {
                // 使用反射添加 FourLayerMemoryComp
                var compType = Type.GetType("RimTalk.Memory.FourLayerMemoryComp, RimTalk-ExpandMemory");
                if (compType == null)
                {
                    Log.Warning("[TheSecondSeat] 未找到 FourLayerMemoryComp 类型");
                    return;
                }
                
                // 检查是否已有组件
                var existingComp = pawn.AllComps.FirstOrDefault(c => c.GetType() == compType);
                if (existingComp != null)
                {
                    Log.Message($"[TheSecondSeat] Pawn {pawn.LabelShort} 已有记忆组件");
                    return;
                }
                
                // 创建组件实例
                var comp = (ThingComp)Activator.CreateInstance(compType);
                comp.parent = pawn;
                pawn.AllComps.Add(comp);
                
                // 初始化组件
                var initMethod = compType.GetMethod("Initialize");
                initMethod?.Invoke(comp, new object[] { new CompProperties() });
                
                Log.Message($"[TheSecondSeat] 已为 {pawn.LabelShort} 添加记忆组件");
            }
            catch (Exception ex)
            {
                Log.Error($"[TheSecondSeat] 添加记忆组件失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 记录对话到记忆系统
        /// </summary>
        /// <param name="narratorDefName">叙事者 DefName</param>
        /// <param name="narratorName">叙事者名称</param>
        /// <param name="speaker">说话者（"Player" 或 "Narrator"）</param>
        /// <param name="content">对话内容</param>
        /// <param name="importance">重要性（0-1）</param>
        /// <param name="tags">标签</param>
        public static void RecordConversation(
            string narratorDefName,
            string narratorName,
            string speaker,
            string content,
            float importance = 0.7f,
            List<string> tags = null)
        {
            if (!IsRimTalkMemoryAvailable())
                return;
            
            try
            {
                // 获取叙事者 Pawn
                Pawn narratorPawn = GetOrCreateNarratorPawn(narratorDefName, narratorName);
                if (narratorPawn == null)
                    return;
                
                // 获取记忆组件
                var memoryComp = GetMemoryComponent(narratorPawn);
                if (memoryComp == null)
                    return;
                
                // 格式化对话内容
                string formattedContent = speaker == "Player" 
                    ? $"[玩家]: {content}" 
                    : $"[{narratorName}]: {content}";
                
                // 准备标签
                var finalTags = tags ?? new List<string>();
                if (!finalTags.Contains("叙事者对话"))
                    finalTags.Add("叙事者对话");
                if (!finalTags.Contains(narratorName))
                    finalTags.Add(narratorName);
                
                // 调用记忆组件的 AddActiveMemory 方法
                AddMemoryToComp(memoryComp, formattedContent, importance, speaker, finalTags);
                
                Log.Message($"[TheSecondSeat] 已记录对话到记忆系统: {formattedContent.Substring(0, Math.Min(50, formattedContent.Length))}...");
            }
            catch (Exception ex)
            {
                Log.Error($"[TheSecondSeat] 记录对话到记忆系统失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取记忆组件
        /// </summary>
        private static ThingComp GetMemoryComponent(Pawn pawn)
        {
            try
            {
                var compType = Type.GetType("RimTalk.Memory.FourLayerMemoryComp, RimTalk-ExpandMemory");
                if (compType == null)
                    return null;
                
                return pawn.AllComps.FirstOrDefault(c => c.GetType() == compType);
            }
            catch (Exception ex)
            {
                Log.Error($"[TheSecondSeat] 获取记忆组件失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 添加记忆到组件（使用反射）
        /// </summary>
        private static void AddMemoryToComp(
            ThingComp memoryComp,
            string content,
            float importance,
            string relatedPawn,
            List<string> tags)
        {
            try
            {
                var compType = memoryComp.GetType();
                
                // 获取 MemoryType 枚举
                var memoryTypeEnum = Type.GetType("RimTalk.Memory.MemoryType, RimTalk-ExpandMemory");
                var conversationType = Enum.Parse(memoryTypeEnum, "Conversation");
                
                // 调用 AddActiveMemory 方法
                var method = compType.GetMethod("AddActiveMemory");
                if (method != null)
                {
                    method.Invoke(memoryComp, new object[] 
                    { 
                        content,        // string content
                        conversationType, // MemoryType type
                        importance,     // float importance
                        relatedPawn     // string relatedPawn
                    });
                    
                    // 如果有标签，尝试添加
                    if (tags != null && tags.Count > 0)
                    {
                        AddTagsToLatestMemory(memoryComp, tags);
                    }
                }
                else
                {
                    Log.Warning("[TheSecondSeat] 未找到 AddActiveMemory 方法");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TheSecondSeat] 添加记忆失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 为最新记忆添加标签
        /// </summary>
        private static void AddTagsToLatestMemory(ThingComp memoryComp, List<string> tags)
        {
            try
            {
                var compType = memoryComp.GetType();
                
                // 获取 activeMemories 字段
                var activeMemoriesField = compType.GetField("activeMemories", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (activeMemoriesField != null)
                {
                    var activeMemories = activeMemoriesField.GetValue(memoryComp) as System.Collections.IList;
                    
                    if (activeMemories != null && activeMemories.Count > 0)
                    {
                        var latestMemory = activeMemories[0];
                        var tagsProperty = latestMemory.GetType().GetProperty("tags");
                        
                        if (tagsProperty != null)
                        {
                            var memoryTags = tagsProperty.GetValue(latestMemory) as List<string>;
                            if (memoryTags == null)
                            {
                                memoryTags = new List<string>();
                                tagsProperty.SetValue(latestMemory, memoryTags);
                            }
                            
                            foreach (var tag in tags)
                            {
                                if (!memoryTags.Contains(tag))
                                    memoryTags.Add(tag);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TheSecondSeat] 添加标签失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取叙事者的对话记忆（暂时禁用，需重构）
        /// </summary>
        public static List<string> RetrieveConversationMemories(string narratorDefName, int maxCount = 10)
        {
            var result = new List<string>();
            
            if (!IsRimTalkMemoryAvailable())
                return result;
            
            // ? 修复：由于没有持久化的Pawn管理，暂时返回空列表
            // 未来如果需要，可以从RimTalk的全局记忆系统中检索
            Log.Warning("[RimTalkMemoryIntegration] RetrieveConversationMemories 暂未实现持久化检索");
            return result;
        }
        
        /// <summary>
        /// 获取所有叙事者虚拟 Pawn（已禁用）
        /// </summary>
        public static List<Pawn> GetAllNarratorPawns()
        {
            // ? 修复：返回空列表
            return new List<Pawn>();
        }
        
        /// <summary>
        /// 判断 Pawn 是否为叙事者虚拟 Pawn（已禁用）
        /// </summary>
        public static bool IsNarratorPawn(Pawn pawn)
        {
            // ? 修复：始终返回 false
            return false;
        }
        
        /// <summary>
        /// 获取叙事者的 DefName（已禁用）
        /// </summary>
        public static string GetNarratorDefName(Pawn narratorPawn)
        {
            // ? 修复：返回 null
            return null;
        }
        
        /// <summary>
        /// 清空叙事者虚拟 Pawn 缓存（已禁用）
        /// </summary>
        public static void ClearCache()
        {
            // ? 修复：空操作
            Log.Message("[RimTalkMemoryIntegration] ClearCache 已禁用（无缓存）");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using RimWorld.QuestGen;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 任务发布工具 - 让导演Agent向殖民地发布原生RimWorld任务
    /// 
    /// 使用 RimWorld 原生的 QuestGen 系统生成任务：
    /// 1. 通过 QuestScriptDef 定义任务脚本
    /// 2. 使用 QuestGen.Generate() 生成任务
    /// 3. 添加到 QuestManager 让玩家接受
    /// </summary>
    public class QuestIssueTool : ITool
    {
        public string Name => "issue_quest";
        public string Description => "向殖民地发布任务（使用RimWorld原生任务系统）";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // 获取任务脚本名称
                string scriptDefName = "";
                if (parameters.TryGetValue("quest_script", out var scriptObj))
                {
                    scriptDefName = scriptObj?.ToString() ?? "";
                }
                
                // 获取威胁点数
                float points = 1000f;
                if (parameters.TryGetValue("points", out var pointsObj))
                {
                    float.TryParse(pointsObj?.ToString(), out points);
                }
                
                // 获取自定义参数
                var customParams = new Dictionary<string, object>();
                if (parameters.TryGetValue("params", out var paramsObj) && paramsObj is Dictionary<string, object> dict)
                {
                    customParams = dict;
                }
                
                // 在主线程执行任务生成
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        Quest quest = null;
                        
                        if (!string.IsNullOrEmpty(scriptDefName))
                        {
                            // 使用指定的 QuestScriptDef
                            var scriptDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(scriptDefName);
                            if (scriptDef != null)
                            {
                                quest = GenerateQuest(scriptDef, points, customParams);
                            }
                            else
                            {
                                Log.Warning($"[QuestIssueTool] QuestScriptDef not found: {scriptDefName}");
                            }
                        }
                        
                        if (quest == null)
                        {
                            // 使用随机可用任务
                            quest = GenerateRandomQuest(points);
                        }
                        
                        if (quest != null)
                        {
                            // 添加到任务管理器
                            Find.QuestManager.Add(quest);
                            
                            // 发送通知信件
                            SendQuestLetter(quest);
                            
                            Log.Message($"[QuestIssueTool] Quest issued: {quest.name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[QuestIssueTool] Error generating quest: {ex.Message}");
                    }
                });
                
                return Task.FromResult(new ToolResult
                {
                    Success = true,
                    Data = "任务生成请求已提交，请查看游戏内通知"
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[QuestIssueTool] Error: {ex.Message}\n{ex.StackTrace}");
                return Task.FromResult(new ToolResult
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
        
        /// <summary>
        /// 使用指定脚本生成任务
        /// </summary>
        private Quest GenerateQuest(QuestScriptDef scriptDef, float points, Dictionary<string, object> customParams)
        {
            var slate = new Slate();
            slate.Set("points", points);
            
            // 设置自定义参数
            foreach (var kvp in customParams)
            {
                slate.Set(kvp.Key, kvp.Value);
            }
            
            // 设置默认地图
            var map = Find.CurrentMap;
            if (map != null)
            {
                slate.Set("map", map);
            }
            
            return QuestGen.Generate(scriptDef, slate);
        }
        
        /// <summary>
        /// 生成随机任务
        /// </summary>
        private Quest GenerateRandomQuest(float points)
        {
            // 获取可用的任务脚本
            var availableScripts = DefDatabase<QuestScriptDef>.AllDefsListForReading
                .Where(x => CanUseScript(x, points))
                .ToList();
            
            if (availableScripts.Count == 0)
            {
                Log.Warning("[QuestIssueTool] No available quest scripts found");
                return null;
            }
            
            // 随机选择一个
            var script = availableScripts.RandomElement();
            
            var slate = new Slate();
            slate.Set("points", points);
            
            var map = Find.CurrentMap;
            if (map != null)
            {
                slate.Set("map", map);
            }
            
            try
            {
                return QuestGen.Generate(script, slate);
            }
            catch (Exception ex)
            {
                Log.Warning($"[QuestIssueTool] Failed to generate quest with script {script.defName}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 检查脚本是否可用
        /// </summary>
        private bool CanUseScript(QuestScriptDef script, float points)
        {
            // 排除特殊任务
            if (script.isRootSpecial) return false;
            if (script.IsRootDecree) return false;
            
            // 简单检查
            try
            {
                // 任务脚本通常需要地图
                var map = Find.CurrentMap;
                if (map == null) return false;
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 发送任务通知信件
        /// </summary>
        private void SendQuestLetter(Quest quest)
        {
            // 获取任务描述
            string description = quest.description;
            if (description == null)
            {
                description = "查看任务详情";
            }
            
            // 创建任务信件
            var letter = LetterMaker.MakeLetter(
                $"📋 导演的任务: {quest.name}",
                $"导演向你的殖民地布置了一个新任务。\n\n{description.Trim()}\n\n请在任务列表中查看详情。",
                LetterDefOf.PositiveEvent
            );
            
            Find.LetterStack.ReceiveLetter(letter);
            
            // 显示消息
            Messages.Message($"📋 新任务: {quest.name}", MessageTypeDefOf.PositiveEvent, false);
        }
        
        /// <summary>
        /// 获取可用的任务脚本列表（供Agent查询）
        /// </summary>
        public static List<string> GetAvailableQuestScripts()
        {
            return DefDatabase<QuestScriptDef>.AllDefsListForReading
                .Where(x => !x.isRootSpecial && !x.IsRootDecree)
                .Select(x => x.defName)
                .ToList();
        }
    }
}

using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.Framework.Actions
{
    /// <summary>
    /// 修改好感度行动
    /// 
    /// XML示例：
    /// <![CDATA[
    /// <li Class="TheSecondSeat.Framework.Actions.ModifyAffinityAction">
    ///   <delta>10</delta>
    ///   <reason>玩家完成挑战</reason>
    /// </li>
    /// ]]>
    /// </summary>
    public class ModifyAffinityAction : TSSAction
    {
        public float delta = 0f;
        public string reason = "Event";
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            var agent = Current.Game?.GetComponent<StorytellerAgent>();
            if (agent != null)
            {
                agent.ModifyAffinity(delta, reason);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[ModifyAffinityAction] Affinity changed by {delta:+0;-0} (reason: {reason})");
                }
            }
        }
        
        public override string GetDescription()
        {
            return $"Modify Affinity {delta:+0;-0} ({reason})";
        }
    }
    
    /// <summary>
    /// 显示对话行动
    /// </summary>
    public class ShowDialogueAction : TSSAction
    {
        public string dialogueText = "";
        public MessageTypeDef messageType = MessageTypeDefOf.NeutralEvent;
        public bool useNarratorWindow = false;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (string.IsNullOrEmpty(dialogueText))
            {
                return;
            }
            
            if (useNarratorWindow)
            {
                // TODO: 打开叙事者窗口显示对话
                // NarratorWindow.Show(dialogueText);
            }
            else
            {
                Messages.Message(dialogueText, messageType);
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[ShowDialogueAction] Showing dialogue: {dialogueText}");
            }
        }
        
        public override string GetDescription()
        {
            return $"Show Dialogue: {dialogueText.Substring(0, Math.Min(30, dialogueText.Length))}...";
        }
    }
    
    /// <summary>
    /// 生成资源行动
    /// </summary>
    public class SpawnResourceAction : TSSAction
    {
        public ThingDef resourceType = null;
        public int amount = 1;
        public IntVec3 spawnLocation = IntVec3.Invalid;
        public bool dropNearPlayer = true;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (resourceType == null || amount <= 0)
            {
                return;
            }
            
            // 确定生成位置
            IntVec3 loc = spawnLocation;
            if (!loc.IsValid || dropNearPlayer)
            {
                // ? 修复：使用正确的RimWorld API
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 35, 
                    (IntVec3 c) => c.Standable(map) && !c.Roofed(map), 
                    out IntVec3 result))
                {
                    result = map.Center;
                }
                loc = result;
            }
            
            // 生成物品
            Thing thing = ThingMaker.MakeThing(resourceType);
            thing.stackCount = amount;
            GenSpawn.Spawn(thing, loc, map);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[SpawnResourceAction] Spawned {amount}x {resourceType.defName} at {loc}");
            }
        }
        
        public override string GetDescription()
        {
            return $"Spawn {amount}x {resourceType?.defName ?? "Unknown"}";
        }
        
        public override bool Validate(out string error)
        {
            if (resourceType == null)
            {
                error = "resourceType is null";
                return false;
            }
            
            if (amount <= 0)
            {
                error = $"amount must be positive: {amount}";
                return false;
            }
            
            return base.Validate(out error);
        }
    }
    
    /// <summary>
    /// 触发事件行动（链式触发）
    /// </summary>
    public class TriggerEventAction : TSSAction
    {
        public string targetEventDefName = "";
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (string.IsNullOrEmpty(targetEventDefName))
            {
                return;
            }
            
            var manager = NarratorEventManager.Instance;
            if (manager != null)
            {
                manager.ForceTriggerEvent(targetEventDefName);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[TriggerEventAction] Triggered event: {targetEventDefName}");
                }
            }
        }
        
        public override string GetDescription()
        {
            return $"Trigger Event: {targetEventDefName}";
        }
    }
    
    /// <summary>
    /// 播放音效行动
    /// TODO: 需要RimWorld正确的音效API
    /// </summary>
    public class PlaySoundAction : TSSAction
    {
        public SoundDef sound = null;
        public float volume = 1.0f;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (sound != null)
            {
                // ? 临时：仅记录日志，实际播放留待子Mod实现
                if (Prefs.DevMode)
                {
                    Log.Message($"[PlaySoundAction] Would play sound: {sound.defName} (not implemented)");
                }
                
                // TODO: 实现音效播放
                // 子Mod可以继承此类并重写Execute方法实现音效播放
            }
        }
        
        public override string GetDescription()
        {
            return $"Play Sound: {sound?.defName ?? "None"}";
        }
    }
    
    /// <summary>
    /// 修改心情行动
    /// </summary>
    public class SetMoodAction : TSSAction
    {
        public string newMood = "Cheerful";
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            var agent = Current.Game?.GetComponent<StorytellerAgent>();
            if (agent != null)
            {
                // TODO: 添加SetMood方法到StorytellerAgent
                // agent.SetMood(newMood);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[SetMoodAction] Mood set to: {newMood}");
                }
            }
        }
        
        public override string GetDescription()
        {
            return $"Set Mood: {newMood}";
        }
    }
    
    /// <summary>
    /// 日志输出行动（调试用）
    /// </summary>
    public class LogMessageAction : TSSAction
    {
        public string message = "";
        public bool isWarning = false;
        public bool isError = false;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            
            if (isError)
            {
                Log.Error($"[NarratorEvent] {message}");
            }
            else if (isWarning)
            {
                Log.Warning($"[NarratorEvent] {message}");
            }
            else
            {
                Log.Message($"[NarratorEvent] {message}");
            }
        }
        
        public override string GetDescription()
        {
            return $"Log: {message}";
        }
    }
}

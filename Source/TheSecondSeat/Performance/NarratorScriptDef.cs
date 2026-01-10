using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Performance
{
    public class NarratorScriptDef : Def
    {
        public List<ScriptAction> actions = new List<ScriptAction>();
        
        // 剧本完成后的自动操作
        public bool repeat = false;
        public string nextScriptDef = "";
    }

    /// <summary>
    /// 剧本动作基类
    /// </summary>
    public abstract class ScriptAction
    {
        // 动作执行前的等待时间（秒）
        public float delay = 0f;
        
        // 是否阻塞后续动作直到完成（例如等待对话显示完毕）
        public bool waitForCompletion = true;

        // 执行动作的方法
        public abstract void Execute();
        
        // 检查动作是否完成（用于 waitForCompletion）
        public virtual bool IsCompleted() 
        { 
            return true; 
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref delay, "delay", 0f);
            Scribe_Values.Look(ref waitForCompletion, "waitForCompletion", true);
        }
    }

    /// <summary>
    /// 对话动作
    /// </summary>
    public class ScriptAction_Dialogue : ScriptAction
    {
        public string text;
        public string expressionStr; // 可选：同时切换表情
        public float duration = 3f; // 对话显示的基础持续时间

        public override void Execute()
        {
            // 通过 NarratorWindow 显示对话
            UI.NarratorWindow.AddAIMessage(text);
            
            // 如果有表情，切换表情
            if (!string.IsNullOrEmpty(expressionStr))
            {
                if (Enum.TryParse<ExpressionType>(expressionStr, true, out var expr))
                {
                    ExpressionSystem.SetExpression(
                        Core.NarratorController.CurrentPersonaDefName ?? "Cassandra_Classic",
                        expr,
                        (int)(duration * 60),
                        "Script Action"
                    );
                }
            }
            
            // 尝试播放 TTS
            // 注意：这里需要一种方式访问 Controller 来触发 TTS，或者直接调用 TTSService
            // 为了简单，我们假设 TTS 服务会监听 UI 消息或由 Manager 处理
            // 实际上 NarratorController.AutoPlayTTS 是私有的，我们可能需要公开它或由 PerformanceManager 处理
        }
        
        // 简单的计时器模拟完成
        private int tickStarted = -1;
        
        public override bool IsCompleted()
        {
            if (tickStarted == -1) tickStarted = Find.TickManager.TicksGame;
            return Find.TickManager.TicksGame > tickStarted + (duration * 60);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref expressionStr, "expressionStr");
            Scribe_Values.Look(ref duration, "duration", 3f);
        }
    }

    /// <summary>
    /// 表情切换动作
    /// </summary>
    public class ScriptAction_Expression : ScriptAction
    {
        public string expression;
        public float duration = 2f;

        public override void Execute()
        {
            if (Enum.TryParse<ExpressionType>(expression, true, out var expr))
            {
                ExpressionSystem.SetExpression(
                    Core.NarratorController.CurrentPersonaDefName ?? "Cassandra_Classic",
                    expr,
                    (int)(duration * 60),
                    "Script Action"
                );
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expression, "expression");
            Scribe_Values.Look(ref duration, "duration", 2f);
        }
    }

    /// <summary>
    /// 等待动作
    /// </summary>
    public class ScriptAction_Wait : ScriptAction
    {
        public float seconds;
        private int targetTick = -1;

        public override void Execute()
        {
            targetTick = Find.TickManager.TicksGame + (int)(seconds * 60);
        }

        public override bool IsCompleted()
        {
            return Find.TickManager.TicksGame >= targetTick;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref seconds, "seconds", 1f);
        }
    }
    
    /// <summary>
    /// 事件触发动作
    /// </summary>
    public class ScriptAction_Event : ScriptAction
    {
        public string incidentDef;
        public float points = -1f;

        public override void Execute()
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(incidentDef);
            if (def != null)
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, Find.CurrentMap);
                if (points > 0) parms.points = points;
                
                def.Worker.TryExecute(parms);
                Log.Message($"[Performance] Triggered event: {incidentDef}");
            }
            else
            {
                Log.Warning($"[Performance] Could not find incident def: {incidentDef}");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref incidentDef, "incidentDef");
            Scribe_Values.Look(ref points, "points", -1f);
        }
    }
}

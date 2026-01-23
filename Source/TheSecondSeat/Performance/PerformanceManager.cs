using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Performance
{
    /// <summary>
    /// 管理剧本的播放
    /// </summary>
    public class PerformanceManager : GameComponent
    {
        private NarratorScriptDef currentScript;
        private int currentActionIndex = -1;
        private bool isPlaying = false;
        private bool isPaused = false;
        
        // 当前动作状态
        private ScriptAction currentAction;
        private bool waitingForDelay = false;
        private int delayStartTick = -1;
        
        public bool IsPlaying => isPlaying;
        public NarratorScriptDef CurrentScript => currentScript;

        public PerformanceManager(Game game) : base()
        {
        }

        public void StartScript(string scriptDefName)
        {
            NarratorScriptDef def = DefDatabase<NarratorScriptDef>.GetNamedSilentFail(scriptDefName);
            if (def != null)
            {
                StartScript(def);
            }
            else
            {
                Log.Error($"[PerformanceManager] Could not find script: {scriptDefName}");
            }
        }

        public void StartScript(NarratorScriptDef script)
        {
            StopScript();
            currentScript = script;
            currentActionIndex = -1;
            isPlaying = true;
            isPaused = false;
            Log.Message($"[PerformanceManager] Started script: {script.defName}");
            AdvanceToNextAction();
        }

        public void StopScript()
        {
            currentScript = null;
            currentAction = null;
            currentActionIndex = -1;
            isPlaying = false;
            isPaused = false;
        }
        
        public void PauseScript()
        {
            if (isPlaying) isPaused = true;
        }
        
        public void ResumeScript()
        {
            if (isPlaying) isPaused = false;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            if (!isPlaying || isPaused || currentScript == null) return;
            
            // 检查当前动作是否正在等待前置延迟
            if (waitingForDelay)
            {
                if (Find.TickManager.TicksGame >= delayStartTick + (currentAction.delay * 60))
                {
                    waitingForDelay = false;
                    currentAction.Execute();
                    
                    // 如果不需要等待完成，立即进入下一个动作
                    if (!currentAction.waitForCompletion)
                    {
                        AdvanceToNextAction();
                    }
                }
                return;
            }
            
            // 检查当前动作是否完成
            if (currentAction != null)
            {
                if (currentAction.IsCompleted())
                {
                    AdvanceToNextAction();
                }
            }
            else
            {
                // 如果当前没有动作，可能是刚开始或出错了
                AdvanceToNextAction();
            }
        }

        private void AdvanceToNextAction()
        {
            currentActionIndex++;
            
            if (currentActionIndex >= currentScript.actions.Count)
            {
                // 剧本结束
                FinishScript();
                return;
            }
            
            currentAction = currentScript.actions[currentActionIndex];
            
            // 设置延迟
            if (currentAction.delay > 0)
            {
                waitingForDelay = true;
                delayStartTick = Find.TickManager.TicksGame;
            }
            else
            {
                waitingForDelay = false;
                currentAction.Execute();
                
                // 如果不需要等待完成，递归调用进入下一个动作
                // 注意防止无限递归
                if (!currentAction.waitForCompletion)
                {
                    // 使用 GameComponentTick 的下一次循环来处理，避免栈溢出
                    // 或者在这里简单地调用，但要注意
                    // 为安全起见，我们在 Tick 中处理非阻塞动作的推进
                }
            }
        }

        private void FinishScript()
        {
            Log.Message($"[PerformanceManager] Finished script: {currentScript.defName}");
            
            if (currentScript.repeat)
            {
                StartScript(currentScript);
            }
            else if (!string.IsNullOrEmpty(currentScript.nextScriptDef))
            {
                StartScript(currentScript.nextScriptDef);
            }
            else
            {
                StopScript();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref currentScript, "currentScript");
            Scribe_Values.Look(ref currentActionIndex, "currentActionIndex", -1);
            Scribe_Values.Look(ref isPlaying, "isPlaying", false);
            Scribe_Values.Look(ref isPaused, "isPaused", false);
            
            // 注意：不保存 currentAction 对象本身，因为它在 script.actions 中
            // 加载时需要根据 index 恢复引用
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (currentScript != null && currentActionIndex >= 0 && currentActionIndex < currentScript.actions.Count)
                {
                    currentAction = currentScript.actions[currentActionIndex];
                }
            }
        }
    }
}

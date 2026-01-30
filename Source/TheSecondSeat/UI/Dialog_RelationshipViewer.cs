using UnityEngine;
using Verse;
using RimWorld;
using System.Linq;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 关系轴查看/编辑器
    /// </summary>
    public class Dialog_RelationshipViewer : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        
        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public Dialog_RelationshipViewer()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "关系轴查看器 (Relationship Viewer)");
            Text.Font = GameFont.Small;

            var manager = NarratorManager.Instance;
            if (manager == null || manager.StorytellerAgent == null)
            {
                Widgets.Label(new Rect(0f, 40f, inRect.width, 30f), "未找到活跃的叙事者 (NarratorManager not active)");
                return;
            }

            var agent = manager.StorytellerAgent;
            var persona = manager.GetCurrentPersona();

            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 50f);
            float viewHeight = 100f + (persona?.relationshipAxes?.Count ?? 0) * 80f;
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float y = 0f;

            // 1. 基础好感度 (Affinity)
            DrawAxisRow(ref y, viewRect.width, "Affinity", "好感度", agent.affinity, -100f, 100f, 
                (val) => agent.ModifyAffinity(val - agent.affinity, "Debug"));

            // 2. 自定义关系轴
            if (persona != null && persona.relationshipAxes != null)
            {
                foreach (var axis in persona.relationshipAxes)
                {
                    float currentVal = agent.GetRelationship(axis.key);
                    DrawAxisRow(ref y, viewRect.width, axis.key, axis.label, currentVal, axis.min, axis.max,
                        (val) => agent.ModifyRelationship(axis.key, val - currentVal, "Debug"));
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(10f, y, viewRect.width, 24f), "(无自定义关系轴)");
                GUI.color = Color.white;
            }

            Widgets.EndScrollView();
        }

        private void DrawAxisRow(ref float y, float width, string key, string label, float value, float min, float max, System.Action<float> onUpdate)
        {
            float rowHeight = 70f;
            Rect rect = new Rect(0f, y, width, rowHeight);
            
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            
            // 标题
            string title = $"{label} ({key}): {value:F1}";
            Widgets.Label(new Rect(10f, y + 5f, width - 20f, 24f), title);
            
            // 滑块
            float newValue = Widgets.HorizontalSlider(new Rect(10f, y + 30f, width - 20f, 24f), value, min, max, true);
            if (Mathf.Abs(newValue - value) > 0.01f)
            {
                onUpdate(newValue);
            }
            
            y += rowHeight + 5f;
        }
    }
}
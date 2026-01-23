using Verse;
using RimWorld;
using System;

namespace TheSecondSeat
{
    public class CompProperties_DraftableDragon : CompProperties
    {
        public CompProperties_DraftableDragon()
        {
            this.compClass = typeof(CompDraftableDragon);
        }
    }

    public class CompDraftableDragon : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            try
            {
                base.PostSpawnSetup(respawningAfterLoad);
                
                Pawn pawn = parent as Pawn;
                if (pawn == null) return;
                
                // 空值防护：Faction 可能为 null (例如野生动物)
                if (pawn.Faction == null) return;

                // 确保属于玩家派系
                if (!pawn.Faction.IsPlayer) return;
                
                // 注入 DraftController
                if (pawn.drafter == null)
                {
                    pawn.drafter = new Pawn_DraftController(pawn);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TSS] Error in CompDraftableDragon.PostSpawnSetup for {parent?.LabelShort ?? "Unknown"}: {ex.Message}");
            }
        }

        /// <summary>
        /// 偶尔检查以处理驯服后的情况 (从野生变为玩家派系)
        /// </summary>
        public override void CompTickRare()
        {
            base.CompTickRare();
            
            try 
            {
                Pawn pawn = parent as Pawn;
                if (pawn == null) return;
                
                if (pawn.Faction != null && pawn.Faction.IsPlayer && pawn.drafter == null)
                {
                    pawn.drafter = new Pawn_DraftController(pawn);
                }
            }
            catch (Exception) 
            {
                // 忽略 Tick 中的错误以防止刷屏
            }
        }
    }
}

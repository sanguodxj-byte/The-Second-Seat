using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Components
{
    public class GameComponent_TimeStop : GameComponent
    {
        // Tracks all active casters of time stop.
        // Using HashSet for O(1) lookups.
        private HashSet<Pawn> activeCasters = new HashSet<Pawn>();
        
        public static GameComponent_TimeStop Instance;

        public GameComponent_TimeStop(Game game)
        {
            Instance = this;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // Cleanup invalid casters (dead, despawned, etc)
            // We iterate backwards or use a remove list to modify collection while iterating
            if (activeCasters.Count > 0)
            {
                activeCasters.RemoveWhere(p => p == null || p.Dead || !p.Spawned || p.Destroyed);
            }
        }

        public void RegisterCaster(Pawn caster)
        {
            if (caster != null && !activeCasters.Contains(caster))
            {
                activeCasters.Add(caster);
            }
        }

        public void UnregisterCaster(Pawn caster)
        {
            if (caster != null && activeCasters.Contains(caster))
            {
                activeCasters.Remove(caster);
            }
        }

        public bool IsTimeStopped(Map map)
        {
            if (activeCasters.Count == 0) return false;

            // Time is stopped on a map if there is at least one active caster on that map.
            foreach (var caster in activeCasters)
            {
                if (caster.Map == map)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CanTick(Thing thing)
        {
            // Optimization: Fast check if no time stop is active globally
            if (activeCasters.Count == 0) return true;

            // If time is not stopped on this thing's map, it can tick
            if (!IsTimeStopped(thing.Map)) return true;

            // If time IS stopped:
            
            // 1. If thing is a Pawn
            if (thing is Pawn p)
            {
                // Active casters can tick
                if (activeCasters.Contains(p)) return true;
                // Everyone else is frozen
                return false;
            }

            // 2. Projectiles are ALWAYS frozen during time stop (as requested)
            if (thing is Projectile)
            {
                return false;
            }

            // 3. Fire is frozen
            if (thing is Fire)
            {
                return false;
            }
            
            // 4. Motes (Effects) are frozen (optional but recommended for visual consistency)
            if (thing is Mote)
            {
                // We might want to allow Motes attached to the Caster? 
                // But Mote usually doesn't reference caster easily. 
                // Freezing all motes is safer visual style.
                return false;
            }

            // Default: allow tick (Buildings, Plants, etc)
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref activeCasters, "activeCasters", LookMode.Reference);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (activeCasters == null) activeCasters = new HashSet<Pawn>();
            }
        }
    }
}

using System;

namespace TheSecondSeat
{
    /// <summary>
    /// Main mod static initializer (separate from the Mod class)
    /// ? 重命名为 TheSecondSeatInit 避免与 Settings.TheSecondSeatMod 冲突
    /// </summary>
    [Verse.StaticConstructorOnStartup]
    public static class TheSecondSeatInit
    {
        static TheSecondSeatInit()
        {
            Verse.Log.Message("[The Second Seat] AI Narrator Assistant initialized");
        }
    }
}

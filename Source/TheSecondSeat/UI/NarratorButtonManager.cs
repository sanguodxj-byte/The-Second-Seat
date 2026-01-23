using Verse;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 管理屏幕按钮的显示
    /// </summary>
    public class NarratorButtonManager : MapComponent
    {
        private static NarratorScreenButton? screenButton;

        public NarratorButtonManager(Map map) : base(map)
        {
        }

        private int logTick = 0;

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            
            if (Prefs.DevMode && logTick++ > 600)
            {
                logTick = 0;
                Log.Message($"[The Second Seat] NarratorButtonManager running on map {map.uniqueID}. Button exists: {screenButton != null}");
            }

            // 确保按钮始终显示
            if (screenButton == null || !Find.WindowStack.IsOpen(screenButton))
            {
                if (Prefs.DevMode) Log.Message("[The Second Seat] NarratorButtonManager attempting to show button.");
                ShowButton();
            }
        }

        private static void ShowButton()
        {
            if (Current.ProgramState == ProgramState.Playing)
            {
                screenButton = new NarratorScreenButton();
                Find.WindowStack.Add(screenButton);
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            
            // 地图移除时关闭按钮
            if (screenButton != null)
            {
                Find.WindowStack.TryRemove(screenButton);
                screenButton = null;
            }
        }
    }
}

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

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            
            // 确保按钮始终显示
            if (screenButton == null || !Find.WindowStack.IsOpen(screenButton))
            {
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

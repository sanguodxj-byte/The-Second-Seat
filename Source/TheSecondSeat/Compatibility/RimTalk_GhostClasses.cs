using System;
using Verse;

// 这是一个兼容性补丁，用于防止加载旧存档或引用了 RimTalk 的存档时报错
// 错误信息: Could not find class RimTalk.MemoryCleanUp
namespace RimTalk
{
    /// <summary>
    /// 占位符类，用于消除 "Could not find class RimTalk.MemoryCleanUp" 错误
    /// </summary>
    public class MemoryCleanUp : GameComponent
    {
        public MemoryCleanUp(Game game)
        {
            // 空构造函数
        }

        public override void ExposeData()
        {
            // 不保存/加载任何数据
        }
    }
}

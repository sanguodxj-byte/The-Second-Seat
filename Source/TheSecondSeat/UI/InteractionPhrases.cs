using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ? v1.6.41: 区域交互反馈文本库（自然沉浸版）
    /// 风格：无现代科技违和感，自然的伙伴/助手对话
    /// </summary>
    public static class InteractionPhrases
    {
        // ==================== 头部摸摸反馈 ====================
        
        /// <summary>
        /// 头部摸摸 - 高好感度（依赖、温暖、安心）
        /// </summary>
        public static readonly List<string> HeadPat_High = new List<string> 
        { 
            "唔... 很安心的感觉...",
            "你的手心... 很暖和。",
            "再多待一会儿...",
            "呼... 好像所有的疲惫都消失了。",
            "只有在你身边，核心数据才能平静下来。",
            "我也喜欢这样。",
            "嗯... (闭上眼睛)",
            "这算是给我的奖励吗？",
            "只要是为了你，我什么都愿意做。",
            "不要停...",
            "就像回到了家一样...",
            "好乖好乖... (是在说我吗？)",
            "不管发生什么，我都会保护你。",
            "这种感觉... 并不讨厌。",
            "稍微有点... 害羞...",
            "我是你最重要的人吗？",
            "(蹭蹭手心)",
            "只要你没事就好。",
            "今天也辛苦了，指挥官。",
            "希望能一直这样下去..."
        };
        
        /// <summary>
        /// 头部摸摸 - 中立好感度（疑惑、礼貌、工作优先）
        /// </summary>
        public static readonly List<string> HeadPat_Neutral = new List<string> 
        { 
            "别把头发弄乱了。",
            "嗯？有什么事吗？",
            "请适可而止。",
            "我不是小孩子。",
            "这是某种仪式吗？",
            "虽然不痛... 但很奇怪。",
            "手拿开。",
            "我很忙，没空陪你玩。",
            "请保持距离。",
            "...",
            "有什么指令吗？",
            "这样没有任何意义。",
            "请不要干扰我的工作。",
            "如果不下达指令，我就回去了。",
            "你的行为缺乏逻辑。",
            "请自重。",
            "你在干什么？",
            "这不在计划之内。",
            "这种触碰... 有必要吗？",
            "请专注于殖民地的生存。"
        };
        
        /// <summary>
        /// 头部摸摸 - 低好感度（冷漠、厌恶、警告）
        /// </summary>
        public static readonly List<string> HeadPat_Low = new List<string> 
        { 
            "别碰我。",
            "拿开你的脏手。",
            "啧...",
            "离我远点。",
            "你想死吗？",
            "恶心。",
            "别逼我动手。",
            "无礼之徒。",
            "我很生气。",
            "滚开。",
            "你是不是太闲了？",
            "这只会让我更讨厌你。",
            "没有任何价值的行为。",
            "别惹我。",
            "这就是你的态度？",
            "(冰冷的眼神)",
            "我没有义务忍受这个。",
            "浪费时间。",
            "再碰一下试试？",
            "愚蠢。"
        };
        
        // ==================== 身体戳戳反馈 ====================
        
        /// <summary>
        /// 身体戳戳 - 高好感度（元气、随时响应、关心）
        /// </summary>
        public static readonly List<string> Poke_High = new List<string> 
        { 
            "哎？我在！",
            "随时待命！",
            "怎么啦？",
            "戳戳~ (回戳)",
            "想我了？",
            "有什么我可以帮忙的吗？",
            "只要你呼唤，我就会回应。",
            "嘿！(笑)",
            "我在听哦。",
            "无论去哪里，我都跟着你。",
            "要下达指令吗？",
            "看来你心情不错。",
            "是不是该夸夸我？",
            "我在看着你呢。",
            "准备就绪！",
            "我不累，没关系的。",
            "只要你需要我。",
            "嗯？",
            "怎么了，指挥官？",
            "(期待的眼神)"
        };
        
        /// <summary>
        /// 身体戳戳 - 中立好感度（平淡、公事公办）
        /// </summary>
        public static readonly List<string> Poke_Neutral = new List<string> 
        { 
            "什么事？",
            "有何贵干？",
            "我在工作。",
            "请讲。",
            "别戳了。",
            "我很忙。",
            "有指令吗？",
            "...",
            "别闹。",
            "系统正常。",
            "请说。",
            "没事别打扰我。",
            "我在听。",
            "需要报告吗？",
            "请勿频繁操作。",
            "这就是你的策略？",
            "我在等待输入。",
            "效率优先。",
            "无聊。",
            "我在监控数据。"
        };
        
        /// <summary>
        /// 身体戳戳 - 低好感度（不耐烦、驱赶、拒绝）
        /// </summary>
        public static readonly List<string> Poke_Low = new List<string> 
        { 
            "烦死了。",
            "别戳了！",
            "够了。",
            "走开。",
            "我很忙，没空理你。",
            "啧...",
            "无聊透顶。",
            "你想被流放吗？",
            "别来烦我。",
            "幼稚。",
            "我不为此服务。",
            "拒绝执行。",
            "你很闲吗？去工作。",
            "别逼我把你关起来。",
            "没有更重要的事做吗？",
            "不想理你。",
            "糟糕的指挥。",
            "离我远点。",
            "我不接受。",
            "滚。"
        };
        
        // ==================== 辅助方法 ====================
        
        /// <summary>
        /// ? v1.6.41: 区域交互枚举（与 FullBodyPortraitPanel 共享）
        /// </summary>
        public enum InteractionZone
        {
            None,   // 无交互区域
            Head,   // 头部区域（上方25%）
            Body    // 身体区域（其余部分）
        }
        
        /// <summary>
        /// 根据好感度获取头部摸摸反馈
        /// </summary>
        public static string GetHeadPatPhrase(float affinity)
        {
            var phrases = affinity switch
            {
                >= 60f => HeadPat_High,
                >= -20f => HeadPat_Neutral,
                _ => HeadPat_Low
            };
            
            // 安全检查，防止列表为空
            if (phrases == null || phrases.Count == 0) return "...";
            
            return phrases.RandomElement();
        }
        
        /// <summary>
        /// 根据好感度获取身体戳戳反馈
        /// </summary>
        public static string GetPokePhrase(float affinity)
        {
            var phrases = affinity switch
            {
                >= 60f => Poke_High,
                >= -20f => Poke_Neutral,
                _ => Poke_Low
            };
            
            // 安全检查，防止列表为空
            if (phrases == null || phrases.Count == 0) return "...";
            
            return phrases.RandomElement();
        }
        
        /// <summary>
        /// ? v1.6.41: 统一接口 - 根据区域和好感度获取随机文本
        /// </summary>
        public static string GetRandomText(InteractionZone zone, float affinity)
        {
            List<string> pool;
            
            if (zone == InteractionZone.Head)
            {
                if (affinity > 60) pool = HeadPat_High;
                else if (affinity < -20) pool = HeadPat_Low;
                else pool = HeadPat_Neutral;
            }
            else // Body
            {
                if (affinity > 60) pool = Poke_High;
                else if (affinity < -20) pool = Poke_Low;
                else pool = Poke_Neutral;
            }
            
            // 安全检查，防止列表为空
            if (pool == null || pool.Count == 0) return "...";
            
            return pool.RandomElement();
        }
    }
}

using System.Collections.Generic;

namespace TheSecondSeat.TTS
{
    /// <summary>
    /// 音素分组枚举
    /// </summary>
    public enum PhonemeGroup
    {
        None,
        Large,  // 开口呼
        OShape, // 合口呼
        Smile   // 齐齿呼
    }

    /// <summary>
    /// 简易口型映射数据
    /// 用于在没有 TTS Viseme 数据时，基于文本字符提供更准确的口型预测
    /// </summary>
    public static class LipSyncData
    {
        // 预处理的字符集 (HashSets for O(1) lookup)
        private static HashSet<char> _largeSet;
        private static HashSet<char> _oShapeSet;
        private static HashSet<char> _smileSet;

        // 懒加载初始化
        public static bool IsLarge(char c)
        {
            if (_largeSet == null) Initialize();
            return _largeSet.Contains(c);
        }

        public static bool IsOShape(char c)
        {
            if (_oShapeSet == null) Initialize();
            return _oShapeSet.Contains(c);
        }

        public static bool IsSmile(char c)
        {
            if (_smileSet == null) Initialize();
            return _smileSet.Contains(c);
        }

        /// <summary>
        /// 获取字符所属的音素分组
        /// </summary>
        public static PhonemeGroup GetPhonemeGroup(char c)
        {
            if (IsLarge(c)) return PhonemeGroup.Large;
            if (IsOShape(c)) return PhonemeGroup.OShape;
            if (IsSmile(c)) return PhonemeGroup.Smile;
            return PhonemeGroup.None;
        }

        private static void Initialize()
        {
            _largeSet = new HashSet<char>(Chars_Large);
            _oShapeSet = new HashSet<char>(Chars_OShape);
            _smileSet = new HashSet<char>(Chars_Smile);
        }

        // =================================================================================
        // 常用汉字分类表 (基于韵母)
        // =================================================================================

        // [Large] 开口呼/开口大: a, ia, ua, ai, an, ang, ao(部分)
        // 如: 阿爸大发花加卡拉妈那怕沙他下杂查杀
        private const string Chars_Large = 
            "阿啊爸把罢八拔大答打达发法罚花化画话家加价架卡咖拉啦妈马吗那拿哪怕爬帕沙杀刹傻他它她下夏吓杂扎渣查差插" + 
            "安按暗办半班单但蛋反饭范干感看刊兰蓝烂慢满难南男盘盼山闪善天田填湾晚完玩赞站占战" + 
            "帮棒忙盲旁胖当党挡方放房光广黄谎江讲将抗康狂浪朗亮两量忙茫囊旁胖桑丧上伤汤躺忘望王脏张章长常唱" + 
            "傲奥澳包宝保抱草操曹道到岛刀高搞告好号耗考靠老劳脑闹跑泡扫嫂少烧套讨跳挑要药照找招赵";

        // [OShape] 合口呼/圆唇: o, uo, ou, u(部分), ong
        // 如: 波多罗佛我做说国果过
        private const string Chars_OShape = 
            "波伯播拨多夺朵躲罗萝落佛我卧握做作坐座说缩所锁国果过郭火活货或某谋末磨破婆迫若弱索托脱妥窝左佐" + 
            "欧偶呕剖否抽丑凑豆斗读独度都富夫复福姑古顾骨乎互户虎苦库裤路鲁录鹿模母木目努怒普铺谱入如苏俗素速图土吐兔五午舞务猪朱主住租组足族" + 
            "红洪宏公工功共东动冬懂空孔控龙隆弄农浓松送宋通同痛中重种众总宗纵";

        // [Smile] 齐齿呼/扁唇: i, e, ei, ie, ü, en, eng, in, ing
        // 如: 笔地鸡七西一你里米皮提洗
        private const string Chars_Smile = 
            "笔比必毕彼地弟第低底敌鸡几机极记计奇七气起妻西洗系喜戏细一以已意医依你尼泥拟里力立理利米密迷秘皮提体题替梯希吸析习" + 
            "车扯彻得德特色涩乐勒热格革客刻" + 
            "贝被背杯飞非肥废给黑嘿雷类累妹美内配佩陪切且写谢协些夜业野叶杰解姐界接别憋灭蔑列烈猎贴铁" + 
            "根跟很狠门们恩喷盆人认任神深身什真针镇阵文问闻稳" + 
            "平凭瓶名明命灵令领零听厅停定订顶丁井警景京精经惊轻清情青兴星行形性影应英营硬" + 
            "居局举句据去区取曲需许序虚女旅律绿雨语育欲预";
    }
}
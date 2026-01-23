using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.PersonaGeneration
{
    // ✅ 使用 TheSecondSeat.Narrator.AffinityTier（避免重复定义）

    /// <summary>
    /// 短语触发类型枚举
    /// </summary>
    public enum PhraseCategory
    {
        /// <summary>摸头反应</summary>
        HeadPat,
        /// <summary>戳身体反应</summary>
        BodyPoke,
        /// <summary>问候语</summary>
        Greeting,
        /// <summary>事件反馈（通用）</summary>
        EventReaction,
        /// <summary>好事发生</summary>
        GoodEventReaction,
        /// <summary>坏事发生</summary>
        BadEventReaction,
        /// <summary>战斗开始</summary>
        CombatStart,
        /// <summary>战斗胜利</summary>
        CombatVictory,
        /// <summary>受伤反应</summary>
        TakeDamage,
        /// <summary>治愈反应</summary>
        Healed,
        /// <summary>闲聊</summary>
        Idle,
        /// <summary>告别</summary>
        Farewell
    }

    /// <summary>
    /// 单个好感等级的短语集合
    /// </summary>
    public class AffinityTierPhrases
    {
        /// <summary>好感等级</summary>
        public AffinityTier tier = AffinityTier.Indifferent;
        
        /// <summary>摸头反应短语 (30+)</summary>
        public List<string> headPatPhrases = new List<string>();
        
        /// <summary>戳身体反应短语 (30+)</summary>
        public List<string> bodyPokePhrases = new List<string>();
        
        /// <summary>问候语短语 (30+)</summary>
        public List<string> greetingPhrases = new List<string>();
        
        /// <summary>事件反馈短语 (30+)</summary>
        public List<string> eventReactionPhrases = new List<string>();
        
        /// <summary>好事件反馈短语 (15+)</summary>
        public List<string> goodEventPhrases = new List<string>();
        
        /// <summary>坏事件反馈短语 (15+)</summary>
        public List<string> badEventPhrases = new List<string>();
        
        /// <summary>战斗开始短语 (10+)</summary>
        public List<string> combatStartPhrases = new List<string>();
        
        /// <summary>战斗胜利短语 (10+)</summary>
        public List<string> combatVictoryPhrases = new List<string>();
        
        /// <summary>受伤反应短语 (10+)</summary>
        public List<string> takeDamagePhrases = new List<string>();
        
        /// <summary>治愈反应短语 (10+)</summary>
        public List<string> healedPhrases = new List<string>();
        
        /// <summary>闲聊短语 (20+)</summary>
        public List<string> idlePhrases = new List<string>();
        
        /// <summary>告别短语 (10+)</summary>
        public List<string> farewellPhrases = new List<string>();

        /// <summary>
        /// 根据类别获取短语列表
        /// </summary>
        public List<string> GetPhrasesByCategory(PhraseCategory category)
        {
            return category switch
            {
                PhraseCategory.HeadPat => headPatPhrases,
                PhraseCategory.BodyPoke => bodyPokePhrases,
                PhraseCategory.Greeting => greetingPhrases,
                PhraseCategory.EventReaction => eventReactionPhrases,
                PhraseCategory.GoodEventReaction => goodEventPhrases,
                PhraseCategory.BadEventReaction => badEventPhrases,
                PhraseCategory.CombatStart => combatStartPhrases,
                PhraseCategory.CombatVictory => combatVictoryPhrases,
                PhraseCategory.TakeDamage => takeDamagePhrases,
                PhraseCategory.Healed => healedPhrases,
                PhraseCategory.Idle => idlePhrases,
                PhraseCategory.Farewell => farewellPhrases,
                _ => eventReactionPhrases
            };
        }

        /// <summary>
        /// 获取该等级的短语总数
        /// </summary>
        public int GetTotalPhraseCount()
        {
            return headPatPhrases.Count + bodyPokePhrases.Count + greetingPhrases.Count +
                   eventReactionPhrases.Count + goodEventPhrases.Count + badEventPhrases.Count +
                   combatStartPhrases.Count + combatVictoryPhrases.Count + takeDamagePhrases.Count +
                   healedPhrases.Count + idlePhrases.Count + farewellPhrases.Count;
        }
    }

    /// <summary>
    /// 短语库定义 - RimWorld Def类型
    /// 存储一个人格的所有好感等级短语
    /// 
    /// XML 结构示例:
    /// <![CDATA[
    /// <TheSecondSeat.PersonaGeneration.PhraseLibraryDef>
    ///   <defName>Phrases_Sideria</defName>
    ///   <personaDefName>Sideria_Persona</personaDefName>
    ///   <affinityPhrases>
    ///     <li>
    ///       <tier>Warm</tier>
    ///       <headPatPhrases>
    ///         <li>嗯...还不错~</li>
    ///         <li>再摸一下也可以...</li>
    ///       </headPatPhrases>
    ///       <bodyPokePhrases>...</bodyPokePhrases>
    ///     </li>
    ///   </affinityPhrases>
    /// </TheSecondSeat.PersonaGeneration.PhraseLibraryDef>
    /// ]]>
    /// </summary>
    public class PhraseLibraryDef : Def
    {
        /// <summary>关联的人格 Def 名称</summary>
        public string personaDefName = "";
        
        /// <summary>各好感等级的短语集合</summary>
        public List<AffinityTierPhrases> affinityPhrases = new List<AffinityTierPhrases>();
        
        /// <summary>生成时间戳（用于缓存验证）</summary>
        public long generatedTimestamp = 0;
        
        /// <summary>生成版本（用于兼容性检查）</summary>
        public string generatorVersion = "1.0.0";
        
        /// <summary>是否完整生成（所有等级都有30+短语）</summary>
        public bool isComplete = false;
        
        // 运行时缓存
        [Unsaved]
        private Dictionary<AffinityTier, AffinityTierPhrases> tierCache;
        
        /// <summary>
        /// 获取指定好感等级的短语集合
        /// </summary>
        public AffinityTierPhrases GetTierPhrases(AffinityTier tier)
        {
            if (tierCache == null)
            {
                BuildCache();
            }
            
            if (tierCache.TryGetValue(tier, out var phrases))
            {
                return phrases;
            }
            
            // 返回最接近的等级
            return GetClosestTier(tier);
        }
        
        /// <summary>
        /// 根据好感度数值获取对应的短语等级
        /// </summary>
        public AffinityTier GetTierFromAffinity(float affinity)
        {
            if (affinity <= -75f) return AffinityTier.Hatred;
            if (affinity <= -50f) return AffinityTier.Hostile;
            if (affinity <= -25f) return AffinityTier.Cold;
            if (affinity <= 25f) return AffinityTier.Indifferent;
            if (affinity <= 50f) return AffinityTier.Warm;
            if (affinity <= 75f) return AffinityTier.Devoted;
            if (affinity <= 95f) return AffinityTier.Adoration;
            return AffinityTier.SoulBound;
        }
        
        /// <summary>
        /// 获取随机短语
        /// </summary>
        public string GetRandomPhrase(float affinity, PhraseCategory category)
        {
            var tier = GetTierFromAffinity(affinity);
            var tierPhrases = GetTierPhrases(tier);
            
            if (tierPhrases == null)
            {
                Log.Warning($"[PhraseLibrary] No phrases for tier {tier} in {defName}");
                return "";
            }
            
            var phrases = tierPhrases.GetPhrasesByCategory(category);
            if (phrases == null || phrases.Count == 0)
            {
                // 回退到事件反馈
                phrases = tierPhrases.eventReactionPhrases;
            }
            
            if (phrases == null || phrases.Count == 0)
            {
                return "";
            }
            
            return phrases.RandomElement();
        }
        
        /// <summary>
        /// 获取所有短语总数
        /// </summary>
        public int GetTotalPhraseCount()
        {
            int total = 0;
            foreach (var tier in affinityPhrases)
            {
                total += tier.GetTotalPhraseCount();
            }
            return total;
        }
        
        /// <summary>
        /// 验证短语库完整性
        /// </summary>
        public bool ValidateCompleteness(out string report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Phrase Library Validation: {defName} ===");
            
            bool allComplete = true;
            
            foreach (AffinityTier tier in Enum.GetValues(typeof(AffinityTier)))
            {
                var tierPhrases = affinityPhrases.FirstOrDefault(t => t.tier == tier);
                if (tierPhrases == null)
                {
                    sb.AppendLine($"  [MISSING] Tier: {tier}");
                    allComplete = false;
                    continue;
                }
                
                // 检查每个类别的短语数量
                var issues = new List<string>();
                
                if (tierPhrases.headPatPhrases.Count < 30)
                    issues.Add($"HeadPat: {tierPhrases.headPatPhrases.Count}/30");
                if (tierPhrases.bodyPokePhrases.Count < 30)
                    issues.Add($"BodyPoke: {tierPhrases.bodyPokePhrases.Count}/30");
                if (tierPhrases.greetingPhrases.Count < 30)
                    issues.Add($"Greeting: {tierPhrases.greetingPhrases.Count}/30");
                if (tierPhrases.eventReactionPhrases.Count < 30)
                    issues.Add($"EventReaction: {tierPhrases.eventReactionPhrases.Count}/30");
                
                if (issues.Count > 0)
                {
                    sb.AppendLine($"  [INCOMPLETE] Tier {tier}: {string.Join(", ", issues)}");
                    allComplete = false;
                }
                else
                {
                    sb.AppendLine($"  [OK] Tier {tier}: {tierPhrases.GetTotalPhraseCount()} phrases");
                }
            }
            
            sb.AppendLine($"=== Total: {GetTotalPhraseCount()} phrases ===");
            
            report = sb.ToString();
            return allComplete;
        }
        
        private void BuildCache()
        {
            tierCache = new Dictionary<AffinityTier, AffinityTierPhrases>();
            foreach (var tier in affinityPhrases)
            {
                tierCache[tier.tier] = tier;
            }
        }
        
        private AffinityTierPhrases GetClosestTier(AffinityTier targetTier)
        {
            // 向上查找
            for (int i = (int)targetTier + 1; i <= (int)AffinityTier.SoulBound; i++)
            {
                if (tierCache.TryGetValue((AffinityTier)i, out var upper))
                    return upper;
            }
            
            // 向下查找
            for (int i = (int)targetTier - 1; i >= 0; i--)
            {
                if (tierCache.TryGetValue((AffinityTier)i, out var lower))
                    return lower;
            }
            
            // 返回任意一个
            return affinityPhrases.FirstOrDefault();
        }
        
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            
            affinityPhrases ??= new List<AffinityTierPhrases>();
            
            foreach (var tier in affinityPhrases)
            {
                tier.headPatPhrases ??= new List<string>();
                tier.bodyPokePhrases ??= new List<string>();
                tier.greetingPhrases ??= new List<string>();
                tier.eventReactionPhrases ??= new List<string>();
                tier.goodEventPhrases ??= new List<string>();
                tier.badEventPhrases ??= new List<string>();
                tier.combatStartPhrases ??= new List<string>();
                tier.combatVictoryPhrases ??= new List<string>();
                tier.takeDamagePhrases ??= new List<string>();
                tier.healedPhrases ??= new List<string>();
                tier.idlePhrases ??= new List<string>();
                tier.farewellPhrases ??= new List<string>();
            }
            
            BuildCache();
        }
    }
    
    /// <summary>
    /// 短语库 DefOf 引用类
    /// </summary>
    [DefOf]
    public static class PhraseLibraryDefOf
    {
        // 可在此添加静态引用
        // public static PhraseLibraryDef Phrases_Sideria;
        
        static PhraseLibraryDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PhraseLibraryDefOf));
        }
    }
}

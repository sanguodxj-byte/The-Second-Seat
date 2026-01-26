using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using TheSecondSeat.Core;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Descent; // ⭐ 引入降临系统

namespace TheSecondSeat.CharacterCard
{
    /// <summary>
    /// ⭐ 负责维护 NarratorStateCard 的单例系统
    /// </summary>
    public static class CharacterCardSystem
    {
        private static NarratorStateCard _cachedCard;
        private static int _lastUpdateTick = -1;

        /// <summary>
        /// 获取当前缓存的角色卡
        /// 注意：此方法仅返回缓存，不触发更新，因此在后台线程调用是安全的。
        /// 请确保在主线程（如 GameComponentTick 或 TriggerUpdate）中调用 UpdateCard()。
        /// </summary>
        public static NarratorStateCard GetCurrentCard()
        {
            if (_cachedCard == null)
            {
                // 如果缓存为空（尚未初始化），返回一个空卡片以防崩溃
                // 注意：不能在这里调用 UpdateCard，因为可能处于后台线程
                return new NarratorStateCard();
            }
            return _cachedCard;
        }

        /// <summary>
        /// 强制刷新卡片数据
        /// </summary>
        public static void UpdateCard()
        {
            var manager = Current.Game?.GetComponent<NarratorManager>();
            if (manager == null) return;

            var persona = manager.GetCurrentPersona();
            var agent = manager.StorytellerAgent;
            var bio = Current.Game.GetComponent<NarratorBioRhythm>();
            var descent = Current.Game.GetComponent<NarratorDescentSystem>();

            var card = new NarratorStateCard();

            // 1. 基础信息
            if (persona != null)
            {
                card.Name = persona.narratorName;
                card.Label = !string.IsNullOrEmpty(persona.label) ? persona.label : persona.narratorName;
                card.Role = manager.CurrentNarratorMode.ToString();
            }

            // 2. 心理状态 (From Agent)
            if (agent != null)
            {
                card.Mind.AffinityValue = agent.affinity;
                card.Mind.AffinityTier = GetAffinityLabel(agent.affinity);
                card.Mind.CurrentEmotion = agent.currentMood.ToString();
                card.Mind.ActiveTraits = agent.activePersonalityTags ?? new List<string>();
            }

            // 3. 生物几律 (From BioRhythm)
            if (bio != null)
            {
                card.Bio.EnergyLevel = GetEnergyLabel(bio.CurrentEnergy);
                // NarratorBioRhythm 目前没有 HungerLevel，使用默认值
                card.Bio.HungerLevel = "Full"; 
                // 修复：确保 Find.CurrentMap 不为空
                if (Find.CurrentMap != null)
                {
                    card.Bio.TimeOfDay = GetTimePeriod(GenLocalDate.HourOfDay(Find.CurrentMap));
                }
                else
                {
                    card.Bio.TimeOfDay = "Unknown";
                }
                card.Bio.IsSleepy = bio.CurrentEnergy < 20f;
            }

            // 4. 降临状态 (From DescentSystem)
            if (descent != null)
            {
                bool isActive = descent.IsDescentActive;
                bool hasPawn = descent.GetDescentPawn() != null;
                string cooldown = descent.GetCooldownRemaining();
                
                card.Descent.IsDescentActive = hasPawn;
                card.Descent.IsDescending = isActive && !hasPawn;
                card.Descent.CooldownRemaining = cooldown;
                
                if (isActive || hasPawn)
                {
                    card.Descent.CurrentForm = "Physical";
                    card.Descent.FormDescription = "你当前以【实体形态】存在于游戏世界中。你已经从玩家身边离开，降临到了殖民地的土地上，成为一个可以行动的实体。";
                }
                else
                {
                    card.Descent.CurrentForm = "Portrait";
                    if (cooldown == "Ready")
                    {
                        card.Descent.FormDescription = "你当前以【立绘形态】存在，陪伴在玩家身边。如果你想要亲自降临到游戏世界中，可以使用 Descent 命令。";
                    }
                    else
                    {
                        card.Descent.FormDescription = $"你当前以【立绘形态】存在，陪伴在玩家身边。降临能力正在冷却中（{cooldown}）。";
                    }
                }
            }

            // 5. 视觉感知 (From MultimodalAnalysisService + Texture Filename + Apparel System)
            if (persona != null)
            {
                // A. 优先检查动态服装 (Apparel System)
                string activeApparelTexture = null;
                bool hasActiveApparel = false;

                if (bio != null && !string.IsNullOrEmpty(bio.CurrentApparelTag))
                {
                    // 获取当前服装对应的纹理名称
                    var renderTree = RenderTreeDefManager.GetRenderTree(persona.defName);
                    if (renderTree != null)
                    {
                        // 假设默认姿态是 "Standing"
                        activeApparelTexture = renderTree.GetBodyTexture("Standing", bio.CurrentApparelTag);
                        if (!string.IsNullOrEmpty(activeApparelTexture))
                        {
                            hasActiveApparel = true;
                        }
                    }
                }

                // B. 从纹理文件名提取特征 (Priority 1)
                // 如果有动态服装，使用服装纹理名；否则使用默认立绘文件名
                List<string> fileTags = new List<string>();
                string texturePathToParse = hasActiveApparel ? activeApparelTexture : persona.portraitPath;
                bool isVariant = hasActiveApparel; // 如果是动态换装，肯定是变体

                if (!string.IsNullOrEmpty(texturePathToParse))
                {
                    try
                    {
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(texturePathToParse);
                        
                        // 判断是否为变体（如果不包含 Base/Default）
                        if (!hasActiveApparel &&
                            fileName.IndexOf("Base", StringComparison.OrdinalIgnoreCase) < 0 &&
                            fileName.IndexOf("Default", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            isVariant = true;
                        }

                        // 移除常见前缀/后缀
                        string cleanName = fileName.Replace("Portrait_", "").Replace("_Base", "");
                        var tags = cleanName.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var tag in tags)
                        {
                            if (tag.Length > 2 && !int.TryParse(tag, out _))
                            {
                                fileTags.Add(tag);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CharacterCard] Failed to parse texture filename: {ex.Message}");
                    }
                }

                // C. 多模态分析缓存 (Priority 2)
                // 如果是变体（换装），则不信任基础缓存中的 VisualTags（避免混入法袍标签）
                var visionData = MultimodalAnalysisService.Instance.GetCachedResult(persona.portraitPath)
                              ?? MultimodalAnalysisService.Instance.GetCachedResult(persona.defName)
                              ?? persona.GetAnalysis(); // Fallback to XML defined data

                if (visionData != null)
                {
                    card.Appearance.HasVisualContext = true;
                    
                    // 仅当不是变体，或者文件名 Tags 很少时，才使用 Vision Tags
                    // 且 Vision Tags 中的内容可能会被文件名 Tags 覆盖（虽然 List 只是追加）
                    // 关键策略：变体状态下，丢弃基础分析中的 VisualTags
                    bool trustVisionTags = !isVariant;

                    if (trustVisionTags && visionData.VisualTags != null)
                    {
                        card.Appearance.VisualTags.AddRange(visionData.VisualTags);
                    }
                    
                    // Description 保留
                    card.Appearance.Description = visionData.VisualDescription;
                }

                // D. 合并 Tags (文件名 Tags 优先级最高，用于补充或覆盖)
                foreach (var tag in fileTags)
                {
                    if (!card.Appearance.VisualTags.Contains(tag))
                    {
                        card.Appearance.VisualTags.Add(tag);
                    }
                }
            }

            // 6. ⭐ 身份与设定 (Identity Injection)
            if (persona != null)
            {
                // 🛡️ 增强空值保护：防止 Def 中的字段为 null 导致下游逻辑异常
                card.Identity.Biography = persona.biography ?? string.Empty;
                card.Identity.CustomSystemPrompt = persona.customSystemPrompt ?? string.Empty;
                
                // 优先使用覆盖的人格类型
                // 逻辑：如果 overridePersonality 有值，则使用它；否则使用 personalityType；如果都为空，默认 "Unknown"
                string baseType = persona.personalityType ?? string.Empty;
                string overrideType = persona.overridePersonality ?? string.Empty;
                
                card.Identity.PersonalityType = !string.IsNullOrEmpty(overrideType)
                    ? overrideType
                    : (string.IsNullOrEmpty(baseType) ? "Unknown" : baseType);

                // 数值类型直接赋值
                card.Identity.ChaosLevel = persona.narratorChaosLevel;
                card.Identity.MercyLevel = persona.mercyLevel;
                card.Identity.DominanceLevel = persona.dominanceLevel;
                
                // 列表安全拷贝
                if (persona.selectedTraits != null)
                {
                    card.Identity.SelectedTraits.AddRange(persona.selectedTraits);
                }

                if (persona.personalityTags != null)
                {
                    card.Identity.PersonalityTags.AddRange(persona.personalityTags);
                }

                if (persona.toneTags != null)
                {
                    card.Identity.ToneTags.AddRange(persona.toneTags);
                }

                if (persona.forbiddenWords != null)
                {
                    card.Identity.ForbiddenWords.AddRange(persona.forbiddenWords);
                }

                if (persona.specialAbilities != null)
                {
                    card.Identity.SpecialAbilities.AddRange(persona.specialAbilities);
                }
            }

            // 7. ⭐ 一致性检查 (Consistency Validation)
            ValidateConsistency(card, persona?.defName);

            _cachedCard = card;
            _lastUpdateTick = Find.TickManager.TicksGame;
        }
        
        /// <summary>
        /// ⭐ 验证表情与心情的一致性
        /// 用于智能掩盖策略：只在检测到严重不一致时才切换思考表情
        /// </summary>
        private static void ValidateConsistency(NarratorStateCard card, string personaDefName)
        {
            if (string.IsNullOrEmpty(personaDefName))
            {
                card.Appearance.Consistency.IsConsistent = true;
                return;
            }
            
            // 获取当前表情状态
            var exprState = ExpressionSystem.GetExpressionState(personaDefName);
            var currentExpression = exprState.CurrentExpression;
            
            // 获取当前心情/好感度
            string currentEmotion = card.Mind.CurrentEmotion ?? "Neutral";
            float affinity = card.Mind.AffinityValue;
            
            // 记录当前表情
            card.Appearance.Consistency.CurrentExpression = currentExpression.ToString();
            
            // 根据好感度/心情计算期望表情
            ExpressionType expectedExpression = GetExpectedExpression(affinity, currentEmotion);
            card.Appearance.Consistency.ExpectedExpression = expectedExpression.ToString();
            
            // 检查一致性
            bool isConsistent = true;
            float severityLevel = 0f;
            string warningMessage = "";
            
            // ⭐ 核心逻辑：检测"崩坏"情况
            // 情况1：高好感度（>=60）但显示负面表情
            if (affinity >= 60f && IsNegativeExpression(currentExpression))
            {
                isConsistent = false;
                severityLevel = 0.8f;
                warningMessage = $"高好感度({affinity:F0})但显示负面表情({currentExpression})";
            }
            // 情况2：低好感度（<0）但显示正面表情
            else if (affinity < 0f && IsPositiveExpression(currentExpression))
            {
                isConsistent = false;
                severityLevel = 0.7f;
                warningMessage = $"低好感度({affinity:F0})但显示正面表情({currentExpression})";
            }
            // 情况3：心情与表情严重不匹配
            else if (!IsEmotionExpressionMatch(currentEmotion, currentExpression))
            {
                // 这种情况严重程度较低，因为可能是过渡状态
                isConsistent = false;
                severityLevel = 0.4f;
                warningMessage = $"心情({currentEmotion})与表情({currentExpression})不匹配";
            }
            
            // 更新一致性状态
            card.Appearance.Consistency.IsConsistent = isConsistent;
            card.Appearance.Consistency.SeverityLevel = severityLevel;
            card.Appearance.Consistency.WarningMessage = warningMessage;
        }
        
        /// <summary>
        /// 根据好感度和心情获取期望的表情类型
        /// </summary>
        private static ExpressionType GetExpectedExpression(float affinity, string emotion)
        {
            // 优先根据心情判断
            if (!string.IsNullOrEmpty(emotion))
            {
                switch (emotion.ToLower())
                {
                    case "happy":
                    case "joyful":
                        return ExpressionType.Happy;
                    case "sad":
                    case "depressed":
                        return ExpressionType.Sad;
                    case "angry":
                    case "furious":
                        return ExpressionType.Angry;
                    case "worried":
                    case "anxious":
                        return ExpressionType.Worried;
                    case "surprised":
                    case "shocked":
                        return ExpressionType.Surprised;
                }
            }
            
            // 根据好感度判断基础表情
            if (affinity >= 60f) return ExpressionType.Happy;
            if (affinity >= 30f) return ExpressionType.Happy;
            if (affinity >= 0f) return ExpressionType.Neutral;
            if (affinity >= -30f) return ExpressionType.Annoyed;
            return ExpressionType.Angry;
        }
        
        /// <summary>
        /// 判断是否为负面表情
        /// </summary>
        private static bool IsNegativeExpression(ExpressionType expr)
        {
            return expr == ExpressionType.Sad ||
                   expr == ExpressionType.Angry ||
                   expr == ExpressionType.Disappointed ||
                   expr == ExpressionType.Annoyed;
        }
        
        /// <summary>
        /// 判断是否为正面表情
        /// </summary>
        private static bool IsPositiveExpression(ExpressionType expr)
        {
            return expr == ExpressionType.Happy ||
                   expr == ExpressionType.Playful ||
                   expr == ExpressionType.Smug;
        }
        
        /// <summary>
        /// 检查心情与表情是否匹配
        /// </summary>
        private static bool IsEmotionExpressionMatch(string emotion, ExpressionType expression)
        {
            if (string.IsNullOrEmpty(emotion)) return true;
            
            string emotionLower = emotion.ToLower();
            
            // 定义心情-表情的兼容映射
            switch (emotionLower)
            {
                case "happy":
                case "joyful":
                    return expression == ExpressionType.Happy ||
                           expression == ExpressionType.Playful ||
                           expression == ExpressionType.Smug ||
                           expression == ExpressionType.Neutral;
                           
                case "sad":
                case "depressed":
                    return expression == ExpressionType.Sad ||
                           expression == ExpressionType.Disappointed ||
                           expression == ExpressionType.Worried ||
                           expression == ExpressionType.Neutral;
                           
                case "angry":
                case "furious":
                    return expression == ExpressionType.Angry ||
                           expression == ExpressionType.Annoyed ||
                           expression == ExpressionType.Neutral;
                           
                case "neutral":
                case "calm":
                    // 中立心情兼容大多数表情
                    return true;
                    
                default:
                    return true;
            }
        }

        private static string GetAffinityLabel(float val)
        {
            if (val >= 90) return "Soulmate";
            if (val >= 60) return "Partner";
            if (val >= 20) return "Friend";
            return "Neutral";
        }

        private static string GetTimePeriod(int hour)
        {
            if (hour < 6) return "Night";
            if (hour < 12) return "Morning";
            if (hour < 18) return "Afternoon";
            return "Evening";
        }

        private static string GetEnergyLabel(float energy)
        {
            if (energy > 80f) return "Energetic";
            if (energy > 40f) return "Normal";
            if (energy > 20f) return "Tired";
            return "Exhausted";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Verse;

namespace TheSecondSeat.LLM
{
    /// <summary>
    /// LLM响应解析器
    /// 从LLMService中拆分出来的解析逻辑
    /// 支持JSON格式和Tag格式的响应解析
    /// </summary>
    public static class LLMResponseParser
    {
        /// <summary>
        /// 解析 LLM 响应（JSON 或 Tag 格式）
        /// </summary>
        public static LLMResponse? Parse(string messageContent)
        {
            if (string.IsNullOrEmpty(messageContent))
                return null;

            // 1. 尝试解析 JSON
            var jsonResponse = TryParseJson(messageContent);
            if (jsonResponse != null)
            {
                jsonResponse.rawContent = messageContent;
                return jsonResponse;
            }

            // 2. 尝试解析 Tag 格式
            var tagResponse = TryParseTagFormat(messageContent);
            if (tagResponse != null)
            {
                tagResponse.rawContent = messageContent;
                return tagResponse;
            }

            // 3. 回退到纯文本
            return new LLMResponse
            {
                thought = "",
                dialogue = messageContent,
                command = null,
                rawContent = messageContent
            };
        }

        /// <summary>
        /// 尝试从响应中解析JSON
        /// </summary>
        private static LLMResponse? TryParseJson(string content)
        {
            try
            {
                string jsonContent = ExtractJsonFromMarkdown(content);
                if (jsonContent.Trim().StartsWith("{"))
                {
                    var settings = new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        Error = (sender, args) => { args.ErrorContext.Handled = true; }
                    };

                    var llmResponse = JsonConvert.DeserializeObject<LLMResponse>(jsonContent, settings);
                    if (llmResponse != null)
                    {
                        return llmResponse;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LLMResponseParser] JSON Parse Warning: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 尝试解析 Tag 格式响应
        /// 支持 [THOUGHT], [DIALOGUE], [EXPRESSION], [AFFINITY], [ACTION]
        /// </summary>
        private static LLMResponse? TryParseTagFormat(string content)
        {
            // 如果不包含任何标签，则不认为是 Tag 格式
            if (!content.Contains("[") || !content.Contains("]")) 
                return null;

            var response = new LLMResponse();
            bool hasTag = false;

            // 解析 Thought
            var thoughtMatch = Regex.Match(content, @"\[THOUGHT\]:\s*(.+?)(?=\[|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (thoughtMatch.Success)
            {
                response.thought = thoughtMatch.Groups[1].Value.Trim();
                hasTag = true;
            }

            // 解析 Dialogue
            var dialogueMatch = Regex.Match(content, @"\[DIALOGUE\]:\s*(.+?)(?=\[|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (dialogueMatch.Success)
            {
                response.dialogue = dialogueMatch.Groups[1].Value.Trim();
                hasTag = true;
            }
            else
            {
                // 如果没有显式 DIALOGUE 标签，尝试提取剩余文本
                string cleanText = Regex.Replace(content, @"\[\w+\]:.*?(?=\[|$)", "", RegexOptions.Singleline).Trim();
                if (!string.IsNullOrEmpty(cleanText))
                {
                    response.dialogue = cleanText;
                }
            }

            // 解析 Expression
            var exprMatch = Regex.Match(content, @"\[EXPRESSION\]:\s*(\w+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (exprMatch.Success)
            {
                response.expression = exprMatch.Groups[1].Value.Trim();
                hasTag = true;
            }

            // 解析 Emotion
            var emotionMatch = Regex.Match(content, @"\[EMOTION\]:\s*(\w+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (emotionMatch.Success)
            {
                response.emotion = emotionMatch.Groups[1].Value.Trim();
                hasTag = true;
            }

            // 解析 Affinity
            var affinityMatch = Regex.Match(content, @"\[AFFINITY\]:\s*([+\-]?\d+(?:\.\d+)?)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (affinityMatch.Success && float.TryParse(affinityMatch.Groups[1].Value, out float delta))
            {
                response.affinityDelta = delta;
                hasTag = true;
            }

            // 解析 Action
            var actionMatch = Regex.Match(content, @"\[ACTION\]:\s*(\w+)\((.*?)\)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (actionMatch.Success)
            {
                string actionName = actionMatch.Groups[1].Value.Trim();
                string paramsString = actionMatch.Groups[2].Value.Trim();
                
                var command = new LLMCommand
                {
                    action = actionName,
                    target = "Map",
                    parameters = ParseActionParams(paramsString)
                };

                // 尝试从 parameters 中提取 target
                if (command.parameters is Dictionary<string, object> dict)
                {
                    if (dict.ContainsKey("target"))
                    {
                        command.target = dict["target"]?.ToString();
                        dict.Remove("target");
                    }
                }

                response.command = command;
                hasTag = true;
            }

            return hasTag ? response : null;
        }

        /// <summary>
        /// 解析Action参数字符串
        /// </summary>
        private static Dictionary<string, object> ParseActionParams(string paramsString)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(paramsString)) return result;

            // 匹配 key="value" 或 key=value
            var matches = Regex.Matches(paramsString, @"(\w+)\s*=\s*(?:""([^""]*)""|'([^']*)'|(\S+))");
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Success ? match.Groups[2].Value :
                              match.Groups[3].Success ? match.Groups[3].Value :
                              match.Groups[4].Value;
                result[key] = value;
            }
            return result;
        }

        /// <summary>
        /// 从 markdown 代码块中提取 JSON
        /// </summary>
        public static string ExtractJsonFromMarkdown(string content)
        {
            // 如果包含 ```json 代码块，提取其中的内容
            if (content.Contains("```json"))
            {
                int startIndex = content.IndexOf("```json") + 7;
                int endIndex = content.IndexOf("```", startIndex);
                if (endIndex > startIndex)
                {
                    return content.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            else if (content.Contains("```"))
            {
                int startIndex = content.IndexOf("```") + 3;
                int endIndex = content.IndexOf("```", startIndex);
                if (endIndex > startIndex)
                {
                    return content.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            
            return content.Trim();
        }

        /// <summary>
        /// 解析紧凑情绪序列 (emotions字段)
        /// 格式: "happy|worried|angry"
        /// </summary>
        public static List<string> ParseCompactEmotions(string emotionsString)
        {
            if (string.IsNullOrEmpty(emotionsString))
                return new List<string> { "neutral" };

            return new List<string>(emotionsString.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// 将情绪缩写转换为完整名称
        /// </summary>
        public static string ExpandEmotionAbbreviation(string emotion)
        {
            return emotion?.ToLower() switch
            {
                "h" => "happy",
                "s" => "sad",
                "a" => "angry",
                "su" => "surprised",
                "w" => "worried",
                "c" => "confused",
                "n" => "neutral",
                _ => emotion ?? "neutral"
            };
        }
    }
}

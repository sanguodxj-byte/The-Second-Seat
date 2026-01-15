using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// RenderTree 配置输入/输出工具
    /// 用于将运行时配置保存为 XML 文件，或从 XML 文件加载配置
    /// </summary>
    public static class RenderTreeConfigIO
    {
        /// <summary>
        /// 将 RenderTreeDef 保存为 XML 文件
        /// </summary>
        /// <param name="def">要保存的 Def</param>
        /// <param name="filePath">保存路径</param>
        public static void SaveToXml(RenderTreeDef def, string filePath)
        {
            try
            {
                XDocument doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("Defs",
                        new XElement(typeof(RenderTreeDef).FullName,
                            new XElement("defName", def.defName),
                            
                            // 表情映射
                            new XElement("expressions",
                                (def.expressions ?? new System.Collections.Generic.List<ExpressionMapping>()).ConvertAll(expr => 
                                    new XElement("li",
                                        new XElement("expression", expr.expression),
                                        new XElement("suffix", expr.suffix),
                                        new XElement("variants",
                                            (expr.variants ?? new System.Collections.Generic.List<RenderTreeExpressionVariant>()).ConvertAll(v =>
                                                new XElement("li",
                                                    new XElement("variant", v.variant),
                                                    new XElement("suffix", v.suffix),
                                                    !string.IsNullOrEmpty(v.textureName) ? new XElement("textureName", v.textureName) : null,
                                                    !string.IsNullOrEmpty(v.eyesTexture) ? new XElement("eyesTexture", v.eyesTexture) : null
                                                )
                                            )
                                        )
                                    )
                                )
                            ),
                            
                            // 说话配置
                            new XElement("speaking",
                                // Viseme Map
                                new XElement("visemeMap",
                                    (def.speaking?.visemeMap ?? new System.Collections.Generic.List<VisemeTextureMapping>()).ConvertAll(m =>
                                        new XElement("li",
                                            new XElement("viseme", m.viseme),
                                            new XElement("textureName", m.textureName)
                                        )
                                    )
                                ),
                                // Openness Thresholds
                                new XElement("opennessThresholds",
                                    (def.speaking?.opennessThresholds ?? new System.Collections.Generic.List<OpennessThreshold>()).ConvertAll(t =>
                                        new XElement("li",
                                            new XElement("threshold", t.threshold.ToString("F2")),
                                            new XElement("viseme", t.viseme)
                                        )
                                    )
                                ),
                                // Azure Viseme Map
                                new XElement("azureVisemeMap",
                                    (def.speaking?.azureVisemeMap ?? new System.Collections.Generic.List<AzureVisemeMapping>()).ConvertAll(m =>
                                        new XElement("li",
                                            new XElement("azureId", m.azureId),
                                            new XElement("textureName", m.textureName)
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

                doc.Save(filePath);
                Messages.Message($"配置已保存至: {filePath}", MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[RenderTreeConfigIO] 保存失败: {ex.Message}");
                Messages.Message($"保存失败: {ex.Message}", MessageTypeDefOf.NegativeEvent, false);
            }
        }

        /// <summary>
        /// 获取默认保存路径
        /// </summary>
        public static string GetDefaultSavePath(string defName)
        {
            string configDir = GenFilePaths.ConfigFolderPath;
            return Path.Combine(configDir, $"{defName}.xml");
        }

        /// <summary>
        /// 从 XML 文件加载 RenderTreeDef 的数据
        /// </summary>
        public static RenderTreeDef LoadFromXml(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                XDocument doc = XDocument.Load(filePath);
                XElement root = doc.Root?.Element(typeof(RenderTreeDef).FullName);
                if (root == null) return null;

                var def = new RenderTreeDef();
                def.defName = root.Element("defName")?.Value;

                // Load expressions
                def.expressions = root.Element("expressions")?.Elements("li").Select(li => new ExpressionMapping
                {
                    expression = li.Element("expression")?.Value,
                    suffix = li.Element("suffix")?.Value,
                    variants = li.Element("variants")?.Elements("li").Select(v => new RenderTreeExpressionVariant
                    {
                        variant = (int?)v.Element("variant") ?? 0,
                        suffix = v.Element("suffix")?.Value,
                        textureName = v.Element("textureName")?.Value,
                        eyesTexture = v.Element("eyesTexture")?.Value
                    }).ToList() ?? new List<RenderTreeExpressionVariant>()
                }).ToList() ?? new List<ExpressionMapping>();

                // Load speaking config
                XElement speakingEl = root.Element("speaking");
                if (speakingEl != null)
                {
                    def.speaking = new SpeakingConfig
                    {
                        visemeMap = speakingEl.Element("visemeMap")?.Elements("li").Select(li => new VisemeTextureMapping
                        {
                            viseme = li.Element("viseme")?.Value,
                            textureName = li.Element("textureName")?.Value
                        }).ToList() ?? new List<VisemeTextureMapping>(),
                        
                        opennessThresholds = speakingEl.Element("opennessThresholds")?.Elements("li").Select(li => new OpennessThreshold
                        {
                            threshold = float.TryParse(li.Element("threshold")?.Value, out float th) ? th : 0f,
                            viseme = li.Element("viseme")?.Value
                        }).ToList() ?? new List<OpennessThreshold>(),

                        azureVisemeMap = speakingEl.Element("azureVisemeMap")?.Elements("li").Select(li => new AzureVisemeMapping
                        {
                            azureId = int.TryParse(li.Element("azureId")?.Value, out int id) ? id : 0,
                            textureName = li.Element("textureName")?.Value
                        }).ToList() ?? new List<AzureVisemeMapping>()
                    };
                }

                return def;
            }
            catch (Exception ex)
            {
                Log.Error($"[RenderTreeConfigIO] 加载失败: {ex.Message}");
                return null;
            }
        }
    }
}

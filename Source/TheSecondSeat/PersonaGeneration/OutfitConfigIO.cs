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
    /// OutfitDef 配置输入/输出工具
    /// 用于将运行时服装配置保存为 XML 文件，或从 XML 文件加载配置
    /// </summary>
    public static class OutfitConfigIO
    {
        /// <summary>
        /// 将 OutfitDef 列表保存为 XML 文件
        /// </summary>
        public static void SaveOutfitsToXml(string personaDefName, List<OutfitDef> outfits, string filePath)
        {
            try
            {
                XDocument doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("Defs",
                        outfits.ConvertAll(def => 
                            new XElement(typeof(OutfitDef).FullName,
                                new XElement("defName", def.defName),
                                new XElement("label", def.label),
                                new XElement("personaDefName", personaDefName),
                                new XElement("outfitTag", def.outfitTag),
                                new XElement("priority", def.priority),
                                new XElement("bodyTexture", def.bodyTexture),
                                new XElement("outfitDescription", def.outfitDescription),
                                new XElement("layers",
                                    (def.layers ?? new List<OutfitLayer>()).ConvertAll(l =>
                                        new XElement("li",
                                            new XElement("name", l.name),
                                            new XElement("textureName", l.textureName),
                                            new XElement("zOrder", l.zOrder),
                                            new XElement("replaceBody", l.replaceBody)
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

                doc.Save(filePath);
                Messages.Message($"服装配置已保存至: {filePath}", MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[OutfitConfigIO] 保存失败: {ex.Message}");
                Messages.Message($"保存失败: {ex.Message}", MessageTypeDefOf.NegativeEvent, false);
            }
        }

        /// <summary>
        /// 从 XML 文件加载 OutfitDef 列表
        /// </summary>
        public static List<OutfitDef> LoadOutfitsFromXml(string filePath)
        {
            var loadedOutfits = new List<OutfitDef>();
            try
            {
                if (!File.Exists(filePath)) return loadedOutfits;

                XDocument doc = XDocument.Load(filePath);
                if (doc.Root == null) return loadedOutfits;

                foreach (var el in doc.Root.Elements(typeof(OutfitDef).FullName))
                {
                    var def = new OutfitDef();
                    def.defName = el.Element("defName")?.Value ?? $"Outfit_{Guid.NewGuid()}";
                    def.label = el.Element("label")?.Value ?? "New Outfit";
                    def.personaDefName = el.Element("personaDefName")?.Value ?? "";
                    def.outfitTag = el.Element("outfitTag")?.Value ?? "Casual";
                    
                    if (int.TryParse(el.Element("priority")?.Value, out int p)) def.priority = p;
                    
                    def.bodyTexture = el.Element("bodyTexture")?.Value ?? "";
                    def.outfitDescription = el.Element("outfitDescription")?.Value ?? "";
                    
                    def.layers = el.Element("layers")?.Elements("li").Select(li => new OutfitLayer
                    {
                        name = li.Element("name")?.Value ?? "",
                        textureName = li.Element("textureName")?.Value ?? "",
                        zOrder = int.TryParse(li.Element("zOrder")?.Value, out int z) ? z : 0,
                        replaceBody = bool.TryParse(li.Element("replaceBody")?.Value, out bool r) ? r : false
                    }).ToList() ?? new List<OutfitLayer>();

                    loadedOutfits.Add(def);
                }

                return loadedOutfits;
            }
            catch (Exception ex)
            {
                Log.Error($"[OutfitConfigIO] 加载失败: {ex.Message}");
                return loadedOutfits;
            }
        }

        /// <summary>
        /// 获取默认保存路径
        /// </summary>
        public static string GetDefaultSavePath(string personaDefName)
        {
            string configDir = GenFilePaths.ConfigFolderPath;
            return Path.Combine(configDir, $"{personaDefName}_Outfits.xml");
        }
    }
}
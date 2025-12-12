using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 图层类型枚举
    /// 定义分层立绘系统支持的所有图层类型
    /// </summary>
    public enum LayerType
    {
        Background,     // 背景层（最底层，如背景光晕）
        Body,           // 身体层（基础人物轮廓）
        Outfit,         // 服装层（可更换）
        Face,           // 面部层（表情专用）
        Eyes,           // ? 新增：眼睛层（眨眼动画专用）
        Mouth,          // ? 新增：嘴巴层（张嘴动画专用）
        Hair,           // 头发层
        Accessories,    // 配饰层（眼镜、帽子等）
        ForegroundFX,   // 前景特效层（如魔法光效）
        Overlay         // 覆盖层（最顶层，如UI边框）
    }

    /// <summary>
    /// 图层混合模式
    /// 控制图层如何与下层混合
    /// </summary>
    public enum BlendMode
    {
        Normal,         // 标准 Alpha 混合
        Multiply,       // 正片叠底（颜色变暗）
        Screen,         // 滤色（颜色变亮）
        Overlay,        // 叠加（保留明暗对比）
        Additive        // 加法混合（光效专用）
    }

    /// <summary>
    /// 单个图层定义
    /// 描述一个图层的所有属性和加载规则
    /// </summary>
    public class LayerDefinition
    {
        /// <summary>
        /// 图层类型
        /// </summary>
        public LayerType Type { get; set; } = LayerType.Body;

        /// <summary>
        /// 图层名称（用于识别和日志）
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 图层优先级（0-100，数字越大越靠前）
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 纹理路径模板
        /// 可以使用占位符：{persona}, {expression}, {outfit}
        /// 例如：UI/Narrators/9x16/Layered/{persona}/face_{expression}.png
        /// </summary>
        public string TexturePath { get; set; } = "";

        /// <summary>
        /// 混合模式
        /// </summary>
        public BlendMode Blend { get; set; } = BlendMode.Normal;

        /// <summary>
        /// 不透明度（0.0-1.0）
        /// </summary>
        public float Opacity { get; set; } = 1.0f;

        /// <summary>
        /// 是否必需（如果图层缺失是否中止加载）
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// 是否启用（可用于临时禁用某些图层）
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 依赖的表情类型（如果指定，仅在该表情时加载）
        /// null 表示所有表情都加载
        /// </summary>
        public ExpressionType? ExpressionFilter { get; set; } = null;

        /// <summary>
        /// 依赖的服装 ID（如果指定，仅在该服装时加载）
        /// null 表示所有服装都加载
        /// </summary>
        public string? OutfitFilter { get; set; } = null;

        /// <summary>
        /// 偏移量（像素）
        /// 用于微调图层位置
        /// </summary>
        public Vector2 Offset { get; set; } = Vector2.zero;

        /// <summary>
        /// 缩放系数
        /// 默认 1.0，可用于调整特定图层大小
        /// </summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>
        /// 色调调整（RGBA 乘数）
        /// 默认 (1,1,1,1)，可用于染色
        /// </summary>
        public Color Tint { get; set; } = Color.white;

        /// <summary>
        /// 解析纹理路径中的变量
        /// </summary>
        public string ResolveTexturePath(string personaName, ExpressionType expression, string outfit = "default")
        {
            if (string.IsNullOrEmpty(TexturePath))
            {
                return "";
            }

            string resolved = TexturePath
                .Replace("{persona}", personaName)
                .Replace("{expression}", expression.ToString().ToLower())
                .Replace("{outfit}", outfit);

            return resolved;
        }

        /// <summary>
        /// 检查图层是否应该在当前条件下加载
        /// </summary>
        public bool ShouldLoad(ExpressionType expression, string outfit)
        {
            if (!Enabled)
            {
                return false;
            }

            if (ExpressionFilter.HasValue && ExpressionFilter.Value != expression)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(OutfitFilter) && OutfitFilter != outfit)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 克隆图层定义
        /// </summary>
        public LayerDefinition Clone()
        {
            return new LayerDefinition
            {
                Type = this.Type,
                Name = this.Name,
                Priority = this.Priority,
                TexturePath = this.TexturePath,
                Blend = this.Blend,
                Opacity = this.Opacity,
                Required = this.Required,
                Enabled = this.Enabled,
                ExpressionFilter = this.ExpressionFilter,
                OutfitFilter = this.OutfitFilter,
                Offset = this.Offset,
                Scale = this.Scale,
                Tint = this.Tint
            };
        }
    }

    /// <summary>
    /// 分层立绘配置
    /// 定义一个人格的完整图层堆栈
    /// </summary>
    public class LayeredPortraitConfig
    {
        /// <summary>
        /// 人格 defName
        /// </summary>
        public string PersonaDefName { get; set; } = "";

        /// <summary>
        /// 图层列表（按优先级自动排序）
        /// </summary>
        public List<LayerDefinition> Layers { get; set; } = new List<LayerDefinition>();

        /// <summary>
        /// 输出尺寸（像素）
        /// </summary>
        public Vector2Int OutputSize { get; set; } = new Vector2Int(1024, 1572);

        /// <summary>
        /// 是否启用图层缓存
        /// 启用后，相同配置的图层组合会被缓存
        /// </summary>
        public bool EnableCache { get; set; } = true;

        /// <summary>
        /// 添加图层
        /// </summary>
        public void AddLayer(LayerDefinition layer)
        {
            Layers.Add(layer);
            SortLayers();
        }

        /// <summary>
        /// 移除图层
        /// </summary>
        public void RemoveLayer(string layerName)
        {
            Layers.RemoveAll(l => l.Name == layerName);
        }

        /// <summary>
        /// 按优先级排序图层
        /// </summary>
        public void SortLayers()
        {
            Layers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// 获取当前条件下应加载的图层
        /// </summary>
        public List<LayerDefinition> GetActiveLayersForCondition(ExpressionType expression, string outfit = "default")
        {
            var activeLayers = new List<LayerDefinition>();

            foreach (var layer in Layers)
            {
                if (layer.ShouldLoad(expression, outfit))
                {
                    activeLayers.Add(layer);
                }
            }

            return activeLayers;
        }

        /// <summary>
        /// 创建默认分层配置
        /// ? 新逻辑：base_body.png 作为底图（包含身体+默认表情）
        /// ? 表情拆分为 eyes/mouth/flush 三个独立部件进行拼接
        /// </summary>
        public static LayeredPortraitConfig CreateDefault(string personaName)
        {
            return new LayeredPortraitConfig
            {
                PersonaDefName = personaName,
                Layers = new List<LayerDefinition>
                {
                    // ? 1. 底图层：base_body.png（包含身体+默认表情）
                    new LayerDefinition
                    {
                        Name = "base_body",
                        Type = LayerType.Body,
                        Priority = 0,  // 最底层
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/base_body.png",
                        Blend = BlendMode.Normal,
                        Opacity = 1.0f,
                        Required = true
                    },
                    
                    // 2. 背景层（可选）
                    new LayerDefinition
                    {
                        Name = "background",
                        Type = LayerType.Background,
                        Priority = 5,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/background.png",
                        Blend = BlendMode.Normal,
                        Opacity = 0.3f,
                        Required = false
                    },
                    
                    // 3. 服装层（覆盖在身体上）
                    new LayerDefinition
                    {
                        Name = "outfit",
                        Type = LayerType.Outfit,
                        Priority = 10,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/outfit_{outfit}.png",
                        Blend = BlendMode.Normal,
                        Opacity = 1.0f,
                        Required = false
                    },
                    
                    // ? 4. 脸红层（表情部件1 - 可选）
                    new LayerDefinition
                    {
                        Name = "flush",
                        Type = LayerType.Face,
                        Priority = 20,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/{expression}_flush.png",
                        Blend = BlendMode.Normal,
                        Opacity = 1.0f,
                        Required = false
                    },
                    
                    // ? 5. 眼睛层（表情部件2 - 必需）
                    new LayerDefinition
                    {
                        Name = "eyes",
                        Type = LayerType.Face,
                        Priority = 30,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/{expression}_eyes.png",
                        Blend = BlendMode.Normal,
                        Opacity = 1.0f,
                        Required = true
                    },
                    
                    // ? 6. 嘴巴层（表情部件3 - 必需）
                    new LayerDefinition
                    {
                        Name = "mouth",
                        Type = LayerType.Face,
                        Priority = 40,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/{expression}_mouth.png",
                        Blend = BlendMode.Normal,
                        Opacity = 1.0f,
                        Required = true
                    },
                    
                    // 7. 头发层（覆盖在表情上）
                    new LayerDefinition
                    {
                        Name = "hair",
                        Type = LayerType.Hair,
                        Priority = 50,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/hair.png",
                        Blend = BlendMode.Normal,
                        Opacity = 1.0f,
                        Required = false
                    },
                    
                    // 8. 配饰层
                    new LayerDefinition
                    {
                        Name = "accessories",
                        Type = LayerType.Accessories,
                        Priority = 60,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/accessories.png",
                        Blend = BlendMode.Normal,
                        Opacity = 1.0f,
                        Required = false
                    },
                    
                    // 9. 特效层（可选）
                    new LayerDefinition
                    {
                        Name = "fx",
                        Type = LayerType.ForegroundFX,
                        Priority = 70,
                        TexturePath = "UI/Narrators/9x16/Layered/{persona}/fx.png",
                        Blend = BlendMode.Additive,
                        Opacity = 0.8f,
                        Required = false
                    }
                }
            };
        }

        /// <summary>
        /// 验证配置完整性
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(PersonaDefName))
            {
                error = "PersonaDefName is empty";
                return false;
            }

            if (Layers.Count == 0)
            {
                error = "No layers defined";
                return false;
            }

            // 检查是否至少有一个必需图层
            bool hasRequiredLayer = Layers.Exists(l => l.Required);
            if (!hasRequiredLayer)
            {
                error = "No required layers defined (at least Body or Face should be required)";
                return false;
            }

            error = "";
            return true;
        }

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            var info = $"[LayeredPortraitConfig] {PersonaDefName}\n";
            info += $"  Output Size: {OutputSize.x}×{OutputSize.y}\n";
            info += $"  Total Layers: {Layers.Count}\n";
            info += $"  Cache Enabled: {EnableCache}\n";
            info += "  Layers:\n";

            foreach (var layer in Layers)
            {
                string status = layer.Enabled ? "?" : "?";
                string required = layer.Required ? "[Required]" : "";
                info += $"    {status} [{layer.Priority}] {layer.Name} ({layer.Type}) {required}\n";
                info += $"       Path: {layer.TexturePath}\n";
                info += $"       Blend: {layer.Blend}, Opacity: {layer.Opacity}\n";
            }

            return info;
        }
    }
}

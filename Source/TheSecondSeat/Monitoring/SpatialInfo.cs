using System;
using Newtonsoft.Json;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// 空间位置信息 - 用于描述物体、建筑、Pawn的方位
    /// v2.0.0: 新增空间感知系统核心数据结构
    /// </summary>
    [Serializable]
    public class SpatialInfo
    {
        /// <summary>
        /// X坐标（东西方向）
        /// </summary>
        public int x { get; set; }
        
        /// <summary>
        /// Z坐标（南北方向，RimWorld使用z作为垂直轴）
        /// </summary>
        public int z { get; set; }
        
        /// <summary>
        /// 相对于殖民地中心的方向
        /// 可能的值：North, South, East, West, Northeast, Northwest, Southeast, Southwest, Center
        /// </summary>
        public string direction { get; set; } = "Unknown";
        
        /// <summary>
        /// 距离殖民地中心的格子数
        /// </summary>
        public int distanceFromCenter { get; set; }
        
        /// <summary>
        /// 距离等级：VeryClose, Close, Medium, Far, VeryFar
        /// </summary>
        public string distanceLevel { get; set; } = "Unknown";
        
        /// <summary>
        /// 所在区域名称（Home, Stockpile, Growing, Wilderness等）
        /// </summary>
        public string zone { get; set; } = "Unknown";
        
        /// <summary>
        /// 是否在家园区内
        /// </summary>
        public bool isInHomeArea { get; set; }
        
        /// <summary>
        /// 是否在室内
        /// </summary>
        public bool isIndoors { get; set; }
        
        /// <summary>
        /// 返回友好的位置描述（用于叙事）
        /// </summary>
        [JsonIgnore]
        public string FriendlyDescription
        {
            get
            {
                if (direction == "Center")
                    return "殖民地中心";
                
                string distanceDesc = distanceLevel switch
                {
                    "VeryClose" => "非常近的",
                    "Close" => "附近的",
                    "Medium" => "中等距离的",
                    "Far" => "较远的",
                    "VeryFar" => "遥远的",
                    _ => ""
                };
                
                string directionDesc = direction switch
                {
                    "North" => "北方",
                    "South" => "南方",
                    "East" => "东方",
                    "West" => "西方",
                    "Northeast" => "东北方",
                    "Northwest" => "西北方",
                    "Southeast" => "东南方",
                    "Southwest" => "西南方",
                    _ => "未知方向"
                };
                
                return $"{distanceDesc}{directionDesc}";
            }
        }
        
        /// <summary>
        /// 返回英文友好描述（用于AI Prompt）
        /// </summary>
        [JsonIgnore]
        public string FriendlyDescriptionEN
        {
            get
            {
                if (direction == "Center")
                    return "at colony center";
                
                string distanceDesc = distanceLevel switch
                {
                    "VeryClose" => "very close to the",
                    "Close" => "near the",
                    "Medium" => "at medium distance",
                    "Far" => "far to the",
                    "VeryFar" => "very far to the",
                    _ => "to the"
                };
                
                return $"{distanceDesc} {direction.ToLower()}";
            }
        }
    }
    
    /// <summary>
    /// 建筑信息 - 包含位置的建筑数据
    /// </summary>
    [Serializable]
    public class BuildingInfo
    {
        /// <summary>
        /// 建筑名称
        /// </summary>
        public string name { get; set; } = "";
        
        /// <summary>
        /// 建筑类型（Storage, Production, Defense, Power, Medical, Recreation, Research）
        /// </summary>
        public string type { get; set; } = "";
        
        /// <summary>
        /// 建筑defName（用于代码引用）
        /// </summary>
        public string defName { get; set; } = "";
        
        /// <summary>
        /// 位置信息
        /// </summary>
        public SpatialInfo location { get; set; } = new SpatialInfo();
        
        /// <summary>
        /// 建筑尺寸（占用格子数）
        /// </summary>
        public int size { get; set; } = 1;
        
        /// <summary>
        /// 是否正在工作/运行
        /// </summary>
        public bool isOperational { get; set; }
        
        /// <summary>
        /// 当前工作者（如果有）
        /// </summary>
        public string? currentWorker { get; set; }
    }
    
    /// <summary>
    /// 物品堆信息 - 包含位置的物品数据
    /// </summary>
    [Serializable]
    public class ItemStackInfo
    {
        /// <summary>
        /// 物品名称
        /// </summary>
        public string name { get; set; } = "";
        
        /// <summary>
        /// 物品defName
        /// </summary>
        public string defName { get; set; } = "";
        
        /// <summary>
        /// 数量
        /// </summary>
        public int count { get; set; }
        
        /// <summary>
        /// 物品类别（Food, Material, Weapon, Medicine等）
        /// </summary>
        public string category { get; set; } = "";
        
        /// <summary>
        /// 位置信息
        /// </summary>
        public SpatialInfo location { get; set; } = new SpatialInfo();
    }
    
    /// <summary>
    /// 威胁实体信息 - 包含位置的敌人/威胁数据
    /// </summary>
    [Serializable]
    public class ThreatEntityInfo
    {
        /// <summary>
        /// 实体名称
        /// </summary>
        public string name { get; set; } = "";
        
        /// <summary>
        /// 威胁类型（Raider, MechanoidCluster, Animal, Infestation等）
        /// </summary>
        public string threatType { get; set; } = "";
        
        /// <summary>
        /// 派系名称
        /// </summary>
        public string? faction { get; set; }
        
        /// <summary>
        /// 位置信息
        /// </summary>
        public SpatialInfo location { get; set; } = new SpatialInfo();
        
        /// <summary>
        /// 威胁等级（1-5）
        /// </summary>
        public int threatLevel { get; set; } = 1;
        
        /// <summary>
        /// 是否正在战斗
        /// </summary>
        public bool isInCombat { get; set; }
        
        /// <summary>
        /// 携带的武器
        /// </summary>
        public string? weapon { get; set; }
    }
}

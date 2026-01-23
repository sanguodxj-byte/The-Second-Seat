using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 集中管理所有 Agent 工具的注册表
    /// 自动扫描并注册实现了 ITool 接口的类
    /// </summary>
    public static class ToolRegistry
    {
        private static Dictionary<string, ITool> _tools;

        /// <summary>
        /// 获取所有已注册的工具
        /// </summary>
        public static Dictionary<string, ITool> GetAllTools()
        {
            if (_tools == null)
            {
                Initialize();
            }
            return _tools;
        }

        /// <summary>
        /// 初始化工具注册表
        /// 使用反射查找所有 ITool 实现
        /// </summary>
        public static void Initialize()
        {
            _tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase); // 使用不区分大小写的比较器

            try
            {
                // 获取当前程序集中的所有类型
                var types = Assembly.GetExecutingAssembly().GetTypes();

                foreach (var type in types)
                {
                    // 查找实现了 ITool 接口的非抽象类
                    if (typeof(ITool).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        try
                        {
                            // 实例化工具
                            var tool = (ITool)Activator.CreateInstance(type);
                            
                            // 注册工具
                            if (!string.IsNullOrEmpty(tool.Name))
                            {
                                if (!_tools.ContainsKey(tool.Name))
                                {
                                    _tools.Add(tool.Name, tool);
                                    // Log.Message($"[The Second Seat] Registered tool: {tool.Name}");
                                }
                                else
                                {
                                    Log.Warning($"[The Second Seat] Duplicate tool name found: {tool.Name} in {type.Name}. Skipping.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[The Second Seat] Failed to instantiate tool {type.Name}: {ex.Message}");
                        }
                    }
                }
                
                Log.Message($"[The Second Seat] ToolRegistry initialized. {_tools.Count} tools registered.");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] ToolRegistry initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据名称获取工具 (不区分大小写)
        /// </summary>
        public static ITool GetTool(string name)
        {
            if (_tools == null) Initialize();
            
            if (_tools.TryGetValue(name, out var tool))
            {
                return tool;
            }
            return null;
        }
    }
}

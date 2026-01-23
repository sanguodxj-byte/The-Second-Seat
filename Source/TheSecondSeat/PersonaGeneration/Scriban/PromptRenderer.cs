using System;
using Scriban;
using Scriban.Runtime;
using Verse;

namespace TheSecondSeat.PersonaGeneration.Scriban
{
    /// <summary>
    /// Scriban 模板渲染器
    /// 封装 Scriban 引擎的调用逻辑，配置 MemberRenamer 和 TemplateLoader
    /// </summary>
    public static class PromptRenderer
    {
        private static readonly ModPromptTemplateLoader _loader = new ModPromptTemplateLoader();

        static PromptRenderer()
        {
            // 输出 Scriban 版本以验证升级是否生效
            var scribanVersion = typeof(Template).Assembly.GetName().Version;
            Log.Message($"[The Second Seat] Initialized Scriban Renderer. Version: {scribanVersion} (ILRepack Integrated)");
        }

        /// <summary>
        /// 手动初始化以触发静态构造函数
        /// </summary>
        public static void Init() { }

        /// <summary>
        /// 渲染指定的模板文件
        /// </summary>
        /// <param name="templateName">模板名称（不含 .txt 后缀），如 "SystemPrompt_Master"</param>
        /// <param name="context">数据上下文</param>
        /// <returns>渲染后的字符串</returns>
        public static string Render(string templateName, PromptContext context)
        {
            try
            {
                // 1. 加载模板内容
                string templateContent = PromptLoader.Load(templateName);
                if (string.IsNullOrEmpty(templateContent) || templateContent.StartsWith("[Error:"))
                {
                    Log.Error($"[The Second Seat] Failed to load template: {templateName}");
                    return $"Error: Template {templateName} missing.";
                }

                // 2. 解析模板
                var template = Template.Parse(templateContent, templateName);
                if (template.HasErrors)
                {
                    foreach (var error in template.Messages)
                    {
                        Log.Error($"[The Second Seat] Template Parse Error ({templateName}): {error}");
                    }
                    return $"Error: Template {templateName} has syntax errors.";
                }

                // 3. 配置渲染上下文
                // 使用 MemberRenamer 将 C# 的 PascalCase 属性映射为模板中的 snake_case (可选，或者保持原样)
                // 这里为了保持一致性，我们允许标准 C# 属性访问，同时也支持 snake_case
                var scriptObject = new ScriptObject();
                
                // 将整个 PromptContext 对象导入 scriptObject
                // Import 会将属性名转换为 snake_case (默认行为)
                // 例如 context.Narrator.Name -> {{ narrator.name }}
                scriptObject.Import(context, renamer: member => member.Name.ToLowerInvariant()); // 简单转小写，或者使用 StandardMemberRenamer

                var templateContext = new TemplateContext();
                templateContext.PushGlobal(scriptObject);
                
                // 配置 Loader 以支持 include
                templateContext.TemplateLoader = _loader;
                
                // 允许使用 C# 风格的成员访问 (可选，如果 Import 已经处理了)
                templateContext.MemberRenamer = member => member.Name; // 保持原名访问 (例如 {{ Narrator.Name }})
                // 如果想支持 snake_case: member => StandardMemberRenamer.Rename(member)

                // 实际上 Scriban 默认比较灵活。为了简单起见，我们让模板使用 snake_case (narrator.name)
                // 或者我们让用户使用与 C# 属性一致的命名 (Narrator.Name)
                // 考虑到模板编写者可能习惯 snake_case，我们可以做一个混合配置或者明确约定。
                
                // 这里我们使用 Scriban 默认的 MemberRenamer，它支持 snake_case
                // 上面的 Import 已经把对象导入了。
                // 让我们重新配置一下，确保最简单的用法。
                
                // 重新创建 context 以确保干净
                var finalContext = new TemplateContext();
                finalContext.TemplateLoader = _loader;
                
                // 使用 PushGlobal 将 context 对象的属性直接暴露给模板
                // 例如 {{ Narrator.Name }} 或 {{ narrator.name }} (取决于 renamer)
                var globalObj = new ScriptObject();
                globalObj.Import(context); // Import 默认使用 snake_case
                finalContext.PushGlobal(globalObj);

                // 4. 渲染
                return template.Render(finalContext);
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Render Error ({templateName}): {ex}");
                return $"Error: Render failed for {templateName}. {ex.Message}";
            }
        }
        
        /// <summary>
        /// 渲染 System Prompt（便捷方法，自动拼接 Scriban 后缀）
        /// </summary>
        /// <param name="promptType">提示词类型："Master" 或 "EventDirector"</param>
        /// <param name="context">数据上下文</param>
        /// <returns>渲染后的 System Prompt</returns>
        public static string RenderSystemPrompt(string promptType, PromptContext context)
        {
            string templateName = $"SystemPrompt_{promptType}_Scriban";
            return Render(templateName, context);
        }
    }
}

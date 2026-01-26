using System.Threading.Tasks;
using Scriban;
using Scriban.Parsing;
using Scriban.Syntax;
using Scriban.Runtime;
using Verse;

namespace TheSecondSeat.PersonaGeneration.Scriban
{
    /// <summary>
    /// Scriban 模板加载器
    /// 允许在模板中使用 include 语句加载其他提示词文件
    /// 复用 PromptLoader 的加载逻辑（支持用户覆盖、语言回退）
    /// </summary>
    public class ModPromptTemplateLoader : ITemplateLoader
    {
        public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
        {
            // 在这里 templateName 就是 include 'Name' 中的 Name
            // 不需要路径解析，因为 PromptLoader 通过名称查找
            return templateName;
        }

        public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            // 尝试从上下文获取 Narrator 信息
            string personaName = GetPersonaNameFromContext(context);

            // 同步加载
            // templatePath 即 GetPath 返回的名称
            return PromptLoader.Load(templatePath, personaName);
        }

        public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            // 尝试从上下文获取 Narrator 信息
            string personaName = GetPersonaNameFromContext(context);

            // Scriban 支持异步，但我们的 PromptLoader 是同步的
            return new ValueTask<string>(PromptLoader.Load(templatePath, personaName));
        }

        private string GetPersonaNameFromContext(TemplateContext context)
        {
            try
            {
                // 尝试获取 Narrator 对象
                // 注意：Scriban 的变量访问可能需要根据具体的导入方式调整
                // PromptRenderer 中我们是 scriptObject.Import(context);
                // 所以 Narrator 是全局变量
                var narratorObj = context.GetValue(new ScriptVariableGlobal("Narrator"));
                
                if (narratorObj is NarratorInfo narratorInfo)
                {
                    return narratorInfo.DefName;
                }
            }
            catch
            {
                // 忽略错误，回退到默认加载
            }
            return null;
        }
    }
}
using System.Threading.Tasks;
using Scriban;
using Scriban.Parsing;
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
            // 同步加载
            // templatePath 即 GetPath 返回的名称
            return PromptLoader.Load(templatePath);
        }

        public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            // Scriban 支持异步，但我们的 PromptLoader 是同步的
            return new ValueTask<string>(PromptLoader.Load(templatePath));
        }
    }
}
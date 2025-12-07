# 修复中文翻译文件编码
# 使用 UTF-8 with BOM

$filePath = "Languages\ChineseSimplified\Keyed\TheSecondSeat_Keys.xml"

$utf8WithBom = New-Object System.Text.UTF8Encoding $true
$writer = New-Object System.IO.StreamWriter($filePath, $false, $utf8WithBom)

# 写入 XML 头部
$writer.WriteLine('<?xml version="1.0" encoding="utf-8"?>')
$writer.WriteLine('<LanguageData>')

# UI 元素
$writer.WriteLine('  <!-- UI Elements -->')
$writer.WriteLine('  <TSS_NarratorWindowTitle>AI 叙事者助手</TSS_NarratorWindowTitle>')
$writer.WriteLine('  <TSS_NarratorButton>AI 旁白</TSS_NarratorButton>')
$writer.WriteLine('  <TSS_SendButton>发送消息</TSS_SendButton>')
$writer.WriteLine('  <TSS_InputPlaceholder>在这里输入消息...</TSS_InputPlaceholder>')
$writer.WriteLine('  <TSS_Favorability>好感度</TSS_Favorability>')
$writer.WriteLine('  <TSS_SwitchPersona>切换人格</TSS_SwitchPersona>')
$writer.WriteLine('')

# 设置部分
$writer.WriteLine('  <!-- Settings -->')
$writer.WriteLine('  <TSS_Settings_Title>The Second Seat 设置</TSS_Settings_Title>')
$writer.WriteLine('  <TSS_Settings_LLM_Title>LLM 设置</TSS_Settings_LLM_Title>')
$writer.WriteLine('  <TSS_Settings_WebSearch_Title>网络搜索设置</TSS_Settings_WebSearch_Title>')
$writer.WriteLine('  <TSS_Settings_Multimodal_Title>多模态分析设置</TSS_Settings_Multimodal_Title>')
$writer.WriteLine('  <TSS_Settings_Debug_Title>调试设置</TSS_Settings_Debug_Title>')
$writer.WriteLine('')

# LLM 设置
$writer.WriteLine('  <!-- LLM Settings -->')
$writer.WriteLine('  <TSS_Settings_LLMProvider>LLM 提供者</TSS_Settings_LLMProvider>')
$writer.WriteLine('  <TSS_Settings_LLMProvider_Local>本地模型 (LM Studio / Ollama)</TSS_Settings_LLMProvider_Local>')
$writer.WriteLine('  <TSS_Settings_LLMProvider_OpenAI>OpenAI (GPT-4 / GPT-3.5)</TSS_Settings_LLMProvider_OpenAI>')
$writer.WriteLine('  <TSS_Settings_LLMProvider_DeepSeek>DeepSeek (中文优化)</TSS_Settings_LLMProvider_DeepSeek>')
$writer.WriteLine('  <TSS_Settings_LLMProvider_Gemini>Google Gemini</TSS_Settings_LLMProvider_Gemini>')
$writer.WriteLine('  <TSS_Settings_APIEndpoint>API 端点</TSS_Settings_APIEndpoint>')
$writer.WriteLine('  <TSS_Settings_APIKey>API 密钥</TSS_Settings_APIKey>')
$writer.WriteLine('  <TSS_Settings_ModelName>模型名称</TSS_Settings_ModelName>')
$writer.WriteLine('  <TSS_Settings_Temperature>温度</TSS_Settings_Temperature>')
$writer.WriteLine('  <TSS_Settings_MaxTokens>最大 Token 数</TSS_Settings_MaxTokens>')
$writer.WriteLine('')

# 网络搜索
$writer.WriteLine('  <!-- Web Search -->')
$writer.WriteLine('  <TSS_Settings_EnableWebSearch>启用网络搜索</TSS_Settings_EnableWebSearch>')
$writer.WriteLine('  <TSS_Settings_SearchEngine>搜索引擎</TSS_Settings_SearchEngine>')
$writer.WriteLine('  <TSS_Settings_SearchEngine_DuckDuckGo>DuckDuckGo（免费，无需 API Key）</TSS_Settings_SearchEngine_DuckDuckGo>')
$writer.WriteLine('  <TSS_Settings_SearchEngine_Bing>Bing（需要 API Key）</TSS_Settings_SearchEngine_Bing>')
$writer.WriteLine('  <TSS_Settings_SearchEngine_Google>Google（需要 API Key）</TSS_Settings_SearchEngine_Google>')
$writer.WriteLine('  <TSS_Settings_BingAPIKey>Bing API 密钥</TSS_Settings_BingAPIKey>')
$writer.WriteLine('  <TSS_Settings_GetAPIKey_Bing>获取: https://bing.com/dev</TSS_Settings_GetAPIKey_Bing>')
$writer.WriteLine('  <TSS_Settings_GoogleAPIKey>Google API 密钥</TSS_Settings_GoogleAPIKey>')
$writer.WriteLine('  <TSS_Settings_GoogleSearchEngineID>Google 搜索引擎 ID</TSS_Settings_GoogleSearchEngineID>')
$writer.WriteLine('  <TSS_Settings_GetAPIKey_Google>获取: https://developers.google.com/custom-search</TSS_Settings_GetAPIKey_Google>')
$writer.WriteLine('')

# 多模态分析
$writer.WriteLine('  <!-- Multimodal -->')
$writer.WriteLine('  <TSS_Settings_EnableMultimodal>启用多模态分析</TSS_Settings_EnableMultimodal>')
$writer.WriteLine('  <TSS_Settings_MultimodalProvider>多模态提供者</TSS_Settings_MultimodalProvider>')
$writer.WriteLine('  <TSS_Settings_MultimodalProvider_OpenAI>OpenAI (gpt-4-vision-preview)</TSS_Settings_MultimodalProvider_OpenAI>')
$writer.WriteLine('  <TSS_Settings_MultimodalProvider_DeepSeek>DeepSeek (deepseek-vl)</TSS_Settings_MultimodalProvider_DeepSeek>')
$writer.WriteLine('  <TSS_Settings_MultimodalProvider_Gemini>Gemini (gemini-pro-vision)</TSS_Settings_MultimodalProvider_Gemini>')
$writer.WriteLine('  <TSS_Settings_MultimodalAPIKey>多模态 API 密钥</TSS_Settings_MultimodalAPIKey>')
$writer.WriteLine('  <TSS_Settings_VisionModel>视觉模型</TSS_Settings_VisionModel>')
$writer.WriteLine('  <TSS_Settings_TextAnalysisModel>文本分析模型</TSS_Settings_TextAnalysisModel>')
$writer.WriteLine('')

# 按钮
$writer.WriteLine('  <!-- Buttons -->')
$writer.WriteLine('  <TSS_Settings_Apply>应用设置</TSS_Settings_Apply>')
$writer.WriteLine('  <TSS_Settings_TestConnection>测试连接</TSS_Settings_TestConnection>')
$writer.WriteLine('  <TSS_Settings_ClearSearchCache>清空搜索缓存</TSS_Settings_ClearSearchCache>')
$writer.WriteLine('  <TSS_Settings_CacheCleared>搜索缓存已清空</TSS_Settings_CacheCleared>')
$writer.WriteLine('  <TSS_Settings_Testing>正在测试连接...</TSS_Settings_Testing>')
$writer.WriteLine('  <TSS_Settings_TestSuccess>连接测试成功！</TSS_Settings_TestSuccess>')
$writer.WriteLine('  <TSS_Settings_TestFailed>连接测试失败</TSS_Settings_TestFailed>')
$writer.WriteLine('  <TSS_Settings_Applied>设置已应用</TSS_Settings_Applied>')
$writer.WriteLine('  <TSS_Settings_DebugMode>调试模式</TSS_Settings_DebugMode>')
$writer.WriteLine('')

# 好感度等级
$writer.WriteLine('  <!-- Favorability -->')
$writer.WriteLine('  <TSS_Tier_Hostile>敌对</TSS_Tier_Hostile>')
$writer.WriteLine('  <TSS_Tier_Cold>冷漠</TSS_Tier_Cold>')
$writer.WriteLine('  <TSS_Tier_Neutral>中立</TSS_Tier_Neutral>')
$writer.WriteLine('  <TSS_Tier_Warm>温暖</TSS_Tier_Warm>')
$writer.WriteLine('  <TSS_Tier_Devoted>忠诚</TSS_Tier_Devoted>')
$writer.WriteLine('  <TSS_Tier_Infatuated>痴迷</TSS_Tier_Infatuated>')
$writer.WriteLine('')

# 消息
$writer.WriteLine('  <!-- Messages -->')
$writer.WriteLine('  <TSS_Message_CommandSuccess>AI 叙事者：命令执行成功</TSS_Message_CommandSuccess>')
$writer.WriteLine('  <TSS_Message_CommandFailed>AI 叙事者：命令执行失败</TSS_Message_CommandFailed>')
$writer.WriteLine('')

# 结束
$writer.WriteLine('</LanguageData>')
$writer.Close()

Write-Host "? 完整的中文翻译文件已创建（UTF-8 with BOM）" -ForegroundColor Green
Write-Host "?? 文件路径: $filePath" -ForegroundColor Cyan

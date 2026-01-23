// Edge TTS 独立测试程序
// 基于 edge-tts 7.2.7 Python 库的协议实现
// 使用 dotnet 编译运行

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class EdgeTTSStandaloneTest
{
    // Edge TTS 端点 (来自 edge-tts 7.2.7)
    private const string BASE_URL = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";
    private const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    
    // WebSocket URL
    private static readonly string WSS_URL = $"wss://{BASE_URL}/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}";
    
    // Windows 文件时间纪元 (1601-01-01 到 1970-01-01 的秒数)
    private const long WIN_EPOCH = 11644473600;
    
    // 音频输出格式
    private const string OUTPUT_FORMAT = "audio-24khz-48kbitrate-mono-mp3";
    
    // Chrome 版本信息 (来自 edge-tts 7.2.7)
    private const string CHROMIUM_FULL_VERSION = "143.0.3650.75";
    private static readonly string CHROMIUM_MAJOR_VERSION = CHROMIUM_FULL_VERSION.Split('.')[0];
    private static readonly string SEC_MS_GEC_VERSION = $"1-{CHROMIUM_FULL_VERSION}";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Edge TTS WebSocket 测试 (edge-tts 7.2.7 协议) ===");
        Console.WriteLine();
        
        string text = args.Length > 0 ? args[0] : "你好，这是一个测试。Edge TTS 正在工作。";
        string voice = args.Length > 1 ? args[1] : "zh-CN-XiaoxiaoNeural";
        string outputFile = args.Length > 2 ? args[2] : "edge_tts_test.mp3";
        
        Console.WriteLine($"文本: {text}");
        Console.WriteLine($"语音: {voice}");
        Console.WriteLine($"输出: {outputFile}");
        Console.WriteLine();
        
        // 生成 Sec-MS-GEC token
        string secMsGec = GenerateSecMsGec();
        Console.WriteLine($"Sec-MS-GEC: {secMsGec}");
        
        // 生成连接 ID 和 MUID
        string connectId = GenerateConnectId();
        string muid = GenerateMuid();
        Console.WriteLine($"ConnectionId: {connectId}");
        Console.WriteLine($"MUID: {muid}");
        
        // 构建 WebSocket URL (来自 edge-tts 7.2.7)
        string wssUrl = $"{WSS_URL}&ConnectionId={connectId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={SEC_MS_GEC_VERSION}";
        Console.WriteLine($"URL: {wssUrl.Substring(0, Math.Min(120, wssUrl.Length))}...");
        Console.WriteLine();
        
        try
        {
            var startTime = DateTime.Now;
            byte[] audioData = await SynthesizeAsync(wssUrl, muid, text, voice, "+0%", "+0%");
            var elapsed = DateTime.Now - startTime;
            
            if (audioData != null && audioData.Length > 0)
            {
                Console.WriteLine($"成功! 生成了 {audioData.Length} 字节的音频数据");
                Console.WriteLine($"耗时: {elapsed.TotalSeconds:F2} 秒");
                
                File.WriteAllBytes(outputFile, audioData);
                Console.WriteLine($"已保存到: {Path.GetFullPath(outputFile)}");
                
                // 尝试播放
                Console.WriteLine();
                Console.WriteLine("按 Enter 播放音频，或按其他键退出...");
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Enter)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo();
                    psi.FileName = outputFile;
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                }
            }
            else
            {
                Console.WriteLine("失败! 未能生成音频数据");
            }
        }
        catch (AggregateException ae)
        {
            Console.WriteLine($"异常: {ae.InnerException?.Message ?? ae.Message}");
            Console.WriteLine($"堆栈: {ae.InnerException?.StackTrace ?? ae.StackTrace}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异常: {ex.Message}");
            Console.WriteLine($"堆栈: {ex.StackTrace}");
        }
        
        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    
    /// <summary>
    /// 生成 Sec-MS-GEC token (来自 edge-tts 7.2.7 drm.py)
    /// </summary>
    static string GenerateSecMsGec()
    {
        // 获取当前 Unix 时间戳（秒）
        double ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // 转换为 Windows 文件时间纪元
        ticks += WIN_EPOCH;
        
        // 向下取整到最近的 5 分钟（300 秒）
        ticks -= ticks % 300;
        
        // 转换为 100 纳秒间隔（Windows 文件时间格式）
        ticks *= 1e9 / 100;
        
        // 创建要哈希的字符串
        string strToHash = $"{ticks:F0}{TRUSTED_CLIENT_TOKEN}";
        
        // 计算 SHA256 哈希并返回大写的十六进制字符串
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(strToHash));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
        }
    }
    
    /// <summary>
    /// 生成随机 MUID (来自 edge-tts 7.2.7)
    /// </summary>
    static string GenerateMuid()
    {
        byte[] bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
    }
    
    /// <summary>
    /// 生成连接 ID (来自 edge-tts 7.2.7)
    /// </summary>
    static string GenerateConnectId()
    {
        return Guid.NewGuid().ToString("N");
    }
    
    /// <summary>
    /// 生成 JavaScript 风格的日期字符串 (来自 edge-tts 7.2.7)
    /// </summary>
    static string DateToString()
    {
        return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) 
               + " GMT+0000 (Coordinated Universal Time)";
    }
    
    static async Task<byte[]> SynthesizeAsync(string wssUrl, string muid, string text, string voice, string rate, string volume)
    {
        using (var webSocket = new ClientWebSocket())
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            
            // 设置请求头 - 完全模拟 Edge 浏览器 (来自 edge-tts 7.2.7 constants.py)
            webSocket.Options.SetRequestHeader("User-Agent", 
                $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{CHROMIUM_MAJOR_VERSION}.0.0.0 Safari/537.36 Edg/{CHROMIUM_MAJOR_VERSION}.0.0.0");
            webSocket.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
            webSocket.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
            webSocket.Options.SetRequestHeader("Pragma", "no-cache");
            webSocket.Options.SetRequestHeader("Cache-Control", "no-cache");
            webSocket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
            
            // 添加 MUID Cookie (来自 edge-tts 7.2.7)
            webSocket.Options.SetRequestHeader("Cookie", $"muid={muid};");
            
            Console.WriteLine($"正在连接到 Edge TTS 服务...");
            
            try
            {
                await webSocket.ConnectAsync(new Uri(wssUrl), cts.Token);
            }
            catch (WebSocketException wse)
            {
                Console.WriteLine($"WebSocket 连接失败: {wse.Message}");
                Console.WriteLine($"WebSocket 错误代码: {wse.WebSocketErrorCode}");
                if (wse.InnerException != null)
                {
                    Console.WriteLine($"内部异常: {wse.InnerException.Message}");
                }
                return null;
            }
            
            if (webSocket.State != WebSocketState.Open)
            {
                Console.WriteLine($"连接失败! 状态: {webSocket.State}");
                return null;
            }
            Console.WriteLine("连接成功!");
            
            // 发送配置消息 (来自 edge-tts 7.2.7)
            Console.WriteLine("发送配置消息...");
            await SendConfigMessageAsync(webSocket, cts.Token);
            
            // 发送 SSML 消息 (来自 edge-tts 7.2.7)
            Console.WriteLine("发送 SSML 消息...");
            string ssml = BuildSSML(text, voice, rate, volume);
            await SendSSMLMessageAsync(webSocket, ssml, cts.Token);
            
            // 接收音频数据
            Console.WriteLine("接收音频数据...");
            byte[] audioData = await ReceiveAudioAsync(webSocket, cts.Token);
            
            return audioData;
        }
    }
    
    static async Task SendConfigMessageAsync(ClientWebSocket webSocket, CancellationToken ct)
    {
        string timestamp = DateToString();
        
        // JSON 结构 (来自 edge-tts 7.2.7 communicate.py)
        string configMessage = 
            $"X-Timestamp:{timestamp}\r\n" +
            "Content-Type:application/json; charset=utf-8\r\n" +
            "Path:speech.config\r\n\r\n" +
            "{\"context\":{\"synthesis\":{\"audio\":{" +
            "\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\"}," +
            $"\"outputFormat\":\"{OUTPUT_FORMAT}\"" +
            "}}}}\r\n";
        
        Console.WriteLine($"配置消息:\n{configMessage}\n");
        
        byte[] buffer = Encoding.UTF8.GetBytes(configMessage);
        await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, ct);
    }
    
    static async Task SendSSMLMessageAsync(ClientWebSocket webSocket, string ssml, CancellationToken ct)
    {
        string timestamp = DateToString();
        string requestId = GenerateConnectId();
        
        // 注意：X-Timestamp 后面有个 Z，这是 Microsoft Edge 的 bug (来自 edge-tts 7.2.7 注释)
        string ssmlMessage = 
            $"X-RequestId:{requestId}\r\n" +
            "Content-Type:application/ssml+xml\r\n" +
            $"X-Timestamp:{timestamp}Z\r\n" +
            "Path:ssml\r\n\r\n" +
            ssml;
        
        Console.WriteLine($"SSML 消息:\n{ssmlMessage}\n");
        
        byte[] buffer = Encoding.UTF8.GetBytes(ssmlMessage);
        await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, ct);
    }
    
    static async Task<byte[]> ReceiveAudioAsync(ClientWebSocket webSocket, CancellationToken ct)
    {
        var audioChunks = new List<byte[]>();
        byte[] buffer = new byte[16384];
        int chunkCount = 0;
        bool audioReceived = false;
        
        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"\n收到关闭消息: {webSocket.CloseStatus} - {webSocket.CloseStatusDescription}");
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (result.Count > 2)
                    {
                        int headerLength = (buffer[0] << 8) | buffer[1];
                        if (result.Count > headerLength + 2)
                        {
                            // 解析头部检查 Path
                            string header = Encoding.UTF8.GetString(buffer, 2, headerLength);
                            if (header.Contains("Path:audio"))
                            {
                                // 检查 Content-Type
                                if (header.Contains("Content-Type:audio/mpeg"))
                                {
                                    int audioStart = headerLength + 2;
                                    int audioLength = result.Count - audioStart;
                                    if (audioLength > 0)
                                    {
                                        byte[] audioChunk = new byte[audioLength];
                                        Array.Copy(buffer, audioStart, audioChunk, 0, audioLength);
                                        audioChunks.Add(audioChunk);
                                        chunkCount++;
                                        audioReceived = true;
                                        Console.Write($"\r收到 {chunkCount} 个音频块 ({audioChunks.Sum(c => c.Length)} 字节)...");
                                    }
                                }
                            }
                        }
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    if (message.Contains("Path:turn.start"))
                    {
                        Console.WriteLine("\n收到 turn.start");
                    }
                    else if (message.Contains("Path:turn.end"))
                    {
                        Console.WriteLine("\n收到 turn.end (结束标记)");
                        break;
                    }
                    else if (message.Contains("Path:audio.metadata"))
                    {
                        Console.WriteLine("\n收到 audio.metadata");
                    }
                    else if (message.Contains("Path:response") && message.Contains("error"))
                    {
                        Console.WriteLine($"\n服务器错误: {message}");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"\n收到文本消息: {message.Substring(0, Math.Min(200, message.Length))}...");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n操作超时");
                break;
            }
            catch (WebSocketException wse)
            {
                Console.WriteLine($"\nWebSocket 错误: {wse.Message}");
                break;
            }
        }
        
        if (!audioReceived)
        {
            Console.WriteLine("没有收到音频数据");
            return null;
        }
        
        int totalLength = audioChunks.Sum(c => c.Length);
        byte[] audioData = new byte[totalLength];
        int offset = 0;
        foreach (var chunk in audioChunks)
        {
            Array.Copy(chunk, 0, audioData, offset, chunk.Length);
            offset += chunk.Length;
        }
        
        Console.WriteLine($"\n总共收到 {chunkCount} 个音频块，{totalLength} 字节");
        
        return audioData;
    }
    
    static string BuildSSML(string text, string voice, string rate, string volume)
    {
        text = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                   .Replace("\"", "&quot;").Replace("'", "&apos;");
        
        // SSML 格式 (来自 edge-tts 7.2.7)
        return "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
               $"<voice name='{voice}'>" +
               $"<prosody pitch='+0Hz' rate='{rate}' volume='{volume}'>" +
               text +
               "</prosody>" +
               "</voice>" +
               "</speak>";
    }
}

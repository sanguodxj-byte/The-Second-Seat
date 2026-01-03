using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// ? v1.6.77: �������־��ȡ���� - ֻ�����޷��գ���������
    /// 
    /// ���ܣ�
    /// - �Զ���λ Player.log �ļ�
    /// - ��ȡ��� 50 �У��㹻��ϱ�����
    /// - ֻ�����������κθ�����
    /// 
    /// ʹ�ó�����
    /// - �û�����"���ֱ���"ʱ��AI �Զ���ȡ��־����
    /// - �����Ϸ����ԭ��
    /// - �鿴����ľ�����Ϣ
    /// </summary>
    public class LogReaderTool : ITool
    {
        public string Name => "read_log";
        
        public string Description => "��ȡ��Ϸ��־����󲿷��Է������� (read_tail). ����������Զ���λ Player.log ����ȡ��� 50 �С�";

        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            await Task.CompletedTask; // 保持异步签名
            try
            {
                // 1. 自动定位 Player.log
                string logPath = Path.Combine(GenFilePaths.ConfigFolderPath, "..", "Logs", "Player.log");
                
                // �淶��·��
                logPath = Path.GetFullPath(logPath);
                
                if (!File.Exists(logPath))
                {
                    return new ToolResult 
                    { 
                        Success = false, 
                        Error = "Log file not found at: " + logPath 
                    };
                }

                // 2. ֻ��ȡ��� 50 �� (�㹻��������)
                int linesToRead = 50;
                
                // ����������ȡ�������ļ���������
                string[] allLines;
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    var lines = new List<string>();
                    while (!sr.EndOfStream)
                    {
                        lines.Add(sr.ReadLine());
                    }
                    allLines = lines.ToArray();
                }
                
                int startLine = Math.Max(0, allLines.Length - linesToRead);
                string tailContent = string.Join("\n", allLines.Skip(startLine));

                // 3. ͳ�ƴ���;�������
                int errorCount = allLines.Count(line => line.Contains("Exception") || line.Contains("ERROR") || line.Contains("Error"));
                int warningCount = allLines.Count(line => line.Contains("WARNING") || line.Contains("Warning"));

                return new ToolResult 
                { 
                    Success = true, 
                    Data = $"[Player.log Last {linesToRead} Lines]\n" +
                           $"Total lines in log: {allLines.Length}\n" +
                           $"Errors in full log: {errorCount}\n" +
                           $"Warnings in full log: {warningCount}\n" +
                           $"\n--- Last {linesToRead} Lines ---\n{tailContent}" 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"Error reading log: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 从流末尾倒序读取指定行数
        /// </summary>
        private List<string> ReadLastLines(FileStream fs, int count)
        {
            var lines = new List<string>();
            if (fs.Length == 0) return lines;

            long position = fs.Length - 1;
            int linesFound = 0;
            var buffer = new List<byte>();
            
            // 总是读取最后一行
            while (position >= 0)
            {
                fs.Seek(position, SeekOrigin.Begin);
                int byteVal = fs.ReadByte();
                
                if (byteVal == '\n')
                {
                    // 找到换行符，处理缓冲区
                    if (buffer.Count > 0)
                    {
                        buffer.Reverse();
                        lines.Insert(0, System.Text.Encoding.UTF8.GetString(buffer.ToArray()).Trim());
                        buffer.Clear();
                        linesFound++;
                        
                        if (linesFound >= count) break;
                    }
                }
                else if (byteVal != '\r') // 忽略 \r
                {
                    buffer.Add((byte)byteVal);
                }
                
                position--;
            }
            
            // 处理最后剩余的缓冲区（即文件的第一行）
            if (buffer.Count > 0)
            {
                buffer.Reverse();
                lines.Insert(0, System.Text.Encoding.UTF8.GetString(buffer.ToArray()).Trim());
            }
            
            return lines;
        }
    }
}

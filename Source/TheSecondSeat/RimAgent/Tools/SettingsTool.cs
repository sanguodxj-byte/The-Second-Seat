using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using TheSecondSeat.Settings;
using TheSecondSeat.Commands;
using Newtonsoft.Json;
using UnityEngine;

namespace TheSecondSeat.RimAgent.Tools
{
    public class SettingsTool : BaseAICommand
    {
        public override string ActionName => "update_settings";

        // 定义该工具能修改的“白名单”，防止 AI 把游戏搞崩
        private static readonly HashSet<string> AllowedPrefs = new HashSet<string> {
            "DevMode", "VolumeGame", "VolumeMusic", "RunInBackground", "TestMapSizes"
        };

        public override string GetDescription()
        {
            return "Update game or mod settings. Usage: target='Game'|'Mod', key='SettingKey', value='NewValue'";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            try
            {
                // 解析参数
                var args = ParseArgs(target, parameters);
                if (args == null)
                {
                    LogError("Failed to parse arguments.");
                    return false;
                }

                if (args.target == "Game")
                {
                    return UpdateGameSettings(args);
                }
                else if (args.target == "Mod")
                {
                    return UpdateModSettings(args);
                }
                else
                {
                    LogError($"Unknown target: {args.target}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error executing SettingsTool: {ex.Message}");
                return false;
            }
        }

        private bool UpdateGameSettings(SettingArgs args)
        {
            // 修改 RimWorld 原生设置 (Prefs)
            if (AllowedPrefs.Contains(args.key))
            {
                try
                {
                    if (args.key == "DevMode")
                    {
                        bool val = bool.Parse(args.value);
                        if (Prefs.DevMode != val)
                        {
                            Prefs.DevMode = val;
                            LogExecution($"Set DevMode to {val}");
                        }
                    }
                    else if (args.key == "VolumeGame")
                    {
                        float val = float.Parse(args.value);
                        Prefs.VolumeGame = Mathf.Clamp01(val);
                        LogExecution($"Set VolumeGame to {Prefs.VolumeGame}");
                    }
                    else if (args.key == "VolumeMusic")
                    {
                        float val = float.Parse(args.value);
                        Prefs.VolumeMusic = Mathf.Clamp01(val);
                        LogExecution($"Set VolumeMusic to {Prefs.VolumeMusic}");
                    }
                    else if (args.key == "RunInBackground")
                    {
                        bool val = bool.Parse(args.value);
                        Prefs.RunInBackground = val;
                        LogExecution($"Set RunInBackground to {val}");
                    }
                    
                    Prefs.Save(); // 强制保存
                    return true;
                }
                catch (Exception ex)
                {
                    LogError($"Failed to update game setting {args.key}: {ex.Message}");
                    return false;
                }
            }
            else
            {
                LogError($"Game setting '{args.key}' is not allowed to be modified.");
                return false;
            }
        }

        private bool UpdateModSettings(SettingArgs args)
        {
            // 修改本模组设置
            var settings = TheSecondSeatMod.Settings;
            if (settings == null)
            {
                LogError("Mod settings not available.");
                return false;
            }

            try
            {
                if (args.key == "ChatFrequency") // 注意：Settings 中可能没有这个字段，这里仅作为示例，需根据实际 Settings 类调整
                {
                    // 假设这是一个 float 值，并且需要限制范围
                    // 由于 TheSecondSeatSettings 中目前没有 ChatFrequency，我将使用一个存在的字段作为示例，或者添加注释
                    // 检查是否存在相关字段，如果不存在则记录警告
                    
                    // 这里我们以 ttsVolume 为例，因为它是 float 且在 Settings 中
                     if (args.key == "ttsVolume")
                     {
                         float val = float.Parse(args.value);
                         settings.ttsVolume = Mathf.Clamp01(val);
                         LogExecution($"Set ttsVolume to {settings.ttsVolume}");
                     }
                     else if (args.key == "ttsSpeechRate")
                     {
                         float val = float.Parse(args.value);
                         settings.ttsSpeechRate = Mathf.Clamp(val, 0.5f, 2.0f); // 限制语速范围
                         LogExecution($"Set ttsSpeechRate to {settings.ttsSpeechRate}");
                     }
                     else
                     {
                         // 尝试通过反射设置，但要小心
                         // 为了安全起见，仅支持明确定义的字段
                         LogError($"Mod setting '{args.key}' is not supported or not implemented yet.");
                         return false;
                     }
                }
                else if (args.key == "ttsVolume")
                {
                    float val = float.Parse(args.value);
                    settings.ttsVolume = Mathf.Clamp01(val);
                    LogExecution($"Set ttsVolume to {settings.ttsVolume}");
                }
                else if (args.key == "ttsSpeechRate")
                {
                    float val = float.Parse(args.value);
                    settings.ttsSpeechRate = Mathf.Clamp(val, 0.5f, 2.0f);
                    LogExecution($"Set ttsSpeechRate to {settings.ttsSpeechRate}");
                }
                 else if (args.key == "enableTTS")
                {
                    bool val = bool.Parse(args.value);
                    settings.enableTTS = val;
                    LogExecution($"Set enableTTS to {val}");
                }
                else
                {
                     LogError($"Mod setting '{args.key}' is not explicitly supported via SettingsTool.");
                     return false;
                }

                LoadedModManager.GetMod<TheSecondSeatMod>().WriteSettings(); // 保存
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to update mod setting {args.key}: {ex.Message}");
                return false;
            }
        }

        private SettingArgs? ParseArgs(string? target, object? parameters)
        {
            var args = new SettingArgs();

            // 1. 尝试从 parameters 中解析
            if (parameters is Dictionary<string, object> paramDict)
            {
                if (paramDict.ContainsKey("target")) args.target = paramDict["target"]?.ToString();
                if (paramDict.ContainsKey("key")) args.key = paramDict["key"]?.ToString();
                if (paramDict.ContainsKey("value")) args.value = paramDict["value"]?.ToString();
            }
            else if (parameters is string json)
            {
                try
                {
                    args = JsonConvert.DeserializeObject<SettingArgs>(json);
                }
                catch
                {
                    // JSON 解析失败
                }
            }

            // 2. 如果 target 参数不为空，覆盖 args.target
            if (!string.IsNullOrEmpty(target))
            {
                args.target = target;
            }

            // 验证必要参数
            if (string.IsNullOrEmpty(args.target) || string.IsNullOrEmpty(args.key) || args.value == null)
            {
                return null;
            }

            return args;
        }

        private class SettingArgs
        {
            public string target;
            public string key;
            public string value;
        }
    }
}
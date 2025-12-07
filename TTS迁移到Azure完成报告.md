# TTS 迁移到 Azure 完成报告

**版本**: v1.7.4  
**日期**: 2025-12-06  
**状态**: ? **编译成功，等待游戏关闭后部署**

---

## ?? 修改摘要

### 已完成的工作

1. ? **移除 Edge TTS 支持**
   - 删除 `GenerateEdgeTTSAsync()` 方法
   - 强制使用 Azure TTS

2. ? **简化 TTSService.cs**
   - `Configure()` 方法：强制 `ttsProvider = "azure"`
   - `SpeakAsync()` 方法：只调用 Azure TTS
   - 移除 Edge TTS 端点和逻辑

3. ? **简化设置菜单**
   - 移除 Edge TTS 和 Local TTS 选项
   - 只显示 "TTS 提供商: Azure TTS"
   - 添加清晰的 API Key 获取提示

4. ? **自动播放 TTS**
   - 叙事者发言时自动生成语音
   - 保存为 WAV 格式
   - 显示成功提示

---

## ?? 修改的文件

| 文件 | 修改内容 |
|------|----------|
| `Source\TheSecondSeat\TTS\TTSService.cs` | 移除 Edge TTS，只保留 Azure TTS |
| `Source\TheSecondSeat\Settings\ModSettings.cs` | 移除 TTS 提供商选项 |
| `Source\TheSecondSeat\Core\NarratorController.cs` | 添加 AutoPlayTTS 方法 |

---

## ?? 部署步骤

### 前提条件

?? **游戏正在运行！** 需要先关闭游戏才能部署。

### 部署命令

```powershell
# 1. 关闭游戏

# 2. 部署 DLL
Copy-Item "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\" -Force

# 3. 验证
Get-Item "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll" | Select-Object Name, LastWriteTime, Length
```

---

## ??? 用户配置指南

### Azure TTS 配置步骤

#### 步骤 1：获取 Azure API 密钥

1. 访问 [Azure 门户](https://azure.microsoft.com/)
2. 创建 Azure 账号（新用户有免费额度）
3. 创建 **Speech Services** 资源：
   - 搜索 "语音服务" 或 "Speech Services"
   - 点击 **创建**
   - 选择区域（如 `eastus`, `westeurope`）
   - 选择定价层（F0 免费层每月 50万字符）
4. 创建后，进入资源页面
5. 点击 **密钥和终结点**
6. 复制 **密钥 1** 和 **区域**

#### 步骤 2：在游戏中配置

1. 打开 RimWorld
2. **选项** → **模组设置** → **The Second Seat**
3. 展开 **"语音合成（TTS）"**
4. 配置：
   - ? 启用语音合成（TTS）
   - **API 密钥**: 粘贴复制的密钥
   - **区域**: 输入区域代码（如 `eastus`）
   - **语音**: 选择 `zh-CN-XiaoxiaoNeural`（默认中文女声）
   - **语速**: `1.00x`
   - **音量**: `100%`

#### 步骤 3：测试

1. 点击 **"测试 TTS"** 按钮
2. **预期结果**：
   - ? 生成 WAV 文件
   - ? 显示 "TTS 测试成功！音频文件已保存。"
   - ? 音频保存在：`RimWorld\SaveData\TheSecondSeat\TTS\`

---

## ?? 功能说明

### 自动播放 TTS

**触发条件**：叙事者每次发言时

**工作流程**：

```
叙事者发言
    ↓
自动调用 AutoPlayTTS()
    ↓
清除动作标记 (括号内的内容)
    ↓
调用 Azure TTS API
    ↓
生成 WAV 文件
    ↓
保存到 TTS 文件夹
    ↓
显示提示："语音已生成: tts_xxxxxx.wav"
```

### 文件保存位置

**路径**: `C:\Users\<你的用户名>\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\SaveData\TheSecondSeat\TTS\`

**文件命名**: `tts_yyyyMMdd_HHmmss.wav`

**示例**: `tts_20251206_143022.wav`

---

## ?? Azure TTS 定价

| 功能 | 免费层 (F0) | 标准层 (S0) |
|------|-------------|-------------|
| **月度配额** | 50万字符 | 无限制 |
| **费用** | ? 免费 | $1/10万字符 |
| **标准语音** | ? 支持 | ? 支持 |
| **神经语音** | ? 支持 | ? 支持 |
| **SSML** | ? 支持 | ? 支持 |

### 使用量估算

**假设**：
- 每次对话平均 50 个字符
- 每天玩 2 小时，对话 20 次

**计算**：
```
每天: 50 字符/次 × 20 次 = 1000 字符
每月: 1000 字符/天 × 30 天 = 30000 字符
```

**结论**: ? **免费层完全够用**（50万/月 >> 3万/月）

---

## ?? Azure TTS vs Edge TTS 对比

| 特性 | Azure TTS | Edge TTS（已移除） |
|------|-----------|-------------------|
| **价格** | ?? 付费（免费层50万字符/月） | ? 完全免费 |
| **配置难度** | ? 简单（只需 API Key） | ? 复杂（需本地服务器） |
| **音质** | ? 高质量 | ? 高质量 |
| **延迟** | ? 1-2 秒 | ? 2-3 秒 |
| **格式** | ? WAV（Unity 原生支持） | ? MP3（需转换） |
| **依赖** | ? 仅需网络 | ? 需 Python + Flask |
| **稳定性** | ? 企业级 | ?? 自建服务器 |
| **维护成本** | ? 无需维护 | ? 需维护服务器 |

---

## ? 验证清单

### 编译验证

- [x] 代码编译成功（0 错误，101 警告）
- [x] DLL 文件生成：`TheSecondSeat.dll`
- [x] 文件大小：约 200KB

### 功能验证（部署后）

- [ ] 关闭游戏
- [ ] 部署 DLL
- [ ] 启动游戏
- [ ] 打开模组设置
- [ ] 检查 TTS 设置界面
- [ ] 输入 Azure API 密钥
- [ ] 点击"测试 TTS"
- [ ] 验证 WAV 文件生成
- [ ] 与叙事者对话
- [ ] 验证自动生成语音

---

## ?? 已知问题

### 警告（非致命）

```
CS8603: 可能返回 null 引用（PersonaDefExporter.cs）
CS0414: 字段已赋值但从未使用（ttsProvider）
```

**影响**: ? **无影响**，可以正常运行

**解决方案**（可选）：
- 添加 `#pragma warning disable` 抑制警告
- 或者在下次更新时修复

---

## ?? 用户文档

### 快速开始

**5 分钟配置 Azure TTS**：

1. 访问 https://azure.microsoft.com/
2. 创建免费账号（需要信用卡，但不会扣费）
3. 创建 Speech Services 资源
4. 复制 API 密钥和区域
5. 在游戏中粘贴配置
6. 点击"测试 TTS"验证
7. 开始游戏，叙事者发言时自动生成语音！

### 常见问题

**Q1: Azure TTS 收费吗？**
A: 免费层每月 50万字符，足够个人使用。

**Q2: 需要信用卡吗？**
A: 是的，但免费层不会扣费。

**Q3: 音频文件在哪里？**
A: `%LocalAppData%Low\Ludeon Studios\RimWorld by Ludeon Studios\SaveData\TheSecondSeat\TTS\`

**Q4: 可以用其他语音吗？**
A: 可以，点击语音选择按钮，支持 400+ 语音。

**Q5: Edge TTS 去哪了？**
A: 已移除，因为需要额外配置本地服务器，复杂度高。

---

## ?? 总结

### 完成的改进

? **用户体验**：
- 无需配置本地服务器
- 一键测试，即时反馈
- 自动生成语音，无需手动点击

? **代码质量**：
- 移除 Edge TTS 复杂逻辑
- 代码更简洁，易维护
- 统一使用 Azure TTS

? **稳定性**：
- 企业级 API，高可用
- 无需维护本地服务
- WAV 格式，兼容性好

---

## ?? 下一步

### 立即行动

**关闭游戏 → 运行部署命令 → 启动游戏 → 配置 Azure TTS → 开始使用！**

### 未来改进（可选）

1. **语音缓存**：相同文本不重复生成
2. **语音队列**：多条消息顺序播放
3. **情感语音**：根据好感度调整语音风格
4. **多人格语音**：不同人格使用不同语音

---

**创建时间**: 2025-12-06 14:45  
**编译状态**: ? 成功（0 错误，101 警告）  
**部署状态**: ? 等待游戏关闭  
**文档版本**: v1.7.4

---

**部署命令（游戏关闭后运行）**:

```powershell
Copy-Item "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\" -Force ; Write-Host "[TTS 迁移完成] 已部署 - 只使用 Azure TTS" -ForegroundColor Green
```

# 第二席位 - AI旁白助手

一个为RimWorld设计的AI驱动旁白助手模组，具有基于好感度机制的动态人格，可以执行批量命令来帮助管理你的殖民地。

## 功能特性

### ?? AI驱动的旁白
- 集成OpenAI API或本地LLM端点
- 观察你的殖民地状态并提供评论
- 可以根据殖民地需求执行游戏命令

### ?? 好感度系统
旁白的个性会根据你们的关系而改变：
- **敌对** (< -50): 嘲讽，拒绝帮助
- **冷淡** (-50 到 -10): 疏远且不乐于助人
- **中立** (-10 到 30): 专业且客观
- **温暖** (30 到 60): 友好且乐于助人
- **忠诚** (60 到 85): 充满保护欲且主动
- **痴迷** (> 85): 调情且过度热心帮助

### ? 批量命令
旁白可以执行强大的批量操作：
- **批量收获**: 指定所有成熟作物进行收获
- **批量装备**: 为殖民者装备最佳可用武器
- **优先修复**: 将受损建筑设置为优先修复
- **紧急撤退**: 征召所有殖民者进行撤退
- **更改政策**: 建议修改殖民地政策

## 安装

1. 下载并解压到你的RimWorld模组文件夹
2. 在模组设置中启用
3. 在模组选项中配置你的API端点和密钥

## 配置

### API设置

#### OpenAI API
1. 从 https://platform.openai.com/api-keys 获取API密钥
2. 在RimWorld模组设置中输入：
   - 端点：`https://api.openai.com/v1/chat/completions`
   - API密钥：你的OpenAI API密钥

#### 本地LLM（LM Studio、Ollama等）
1. 设置你的本地LLM服务器
2. 在RimWorld模组设置中输入：
   - 端点：你的本地端点（例如：`http://localhost:1234/v1/chat/completions`）
   - API密钥：留空或输入你的本地密钥

### 更新设置
- **自动更新**：启用/禁用自动旁白更新
- **更新间隔**：旁白检查殖民地状态的频率（1-60分钟）

## 使用方法

### 打开旁白界面
1. 选择任何殖民者
2. 点击按钮栏中的"AI旁白"按钮
3. 旁白窗口将打开

### 与旁白互动
- 在输入框中输入消息直接对话
- 点击"与旁白交谈"发送你的消息
- 点击"请求状态更新"快速获得殖民地概况

### 理解好感度
- 好感度基于旁白互动而变化
- 成功执行命令会增加好感度
- 失败的命令或忽略建议会降低好感度
- 观察彩色条来跟踪你们的关系

## 技术架构

### 阶段1：通信层
- `LLMService`：用于LLM通信的异步HTTP客户端
- 非阻塞以防止游戏冻结
- 支持OpenAI格式和兼容端点

### 阶段2：观察层
- `GameStateObserver`：捕获殖民地状态
- 令牌高效的JSON序列化
- 跟踪殖民者、资源、威胁、天气

### 阶段3：好感度系统
- `NarratorManager`：管理AI人格
- 6个具有独特行为的人格等级
- 动态系统提示生成

### 阶段4：命令执行
- 命令模式实现
- 5个内置批量操作
- 可扩展自定义命令

## 开发

### 添加自定义命令

```csharp
using TheSecondSeat.Commands;

public class MyCustomCommand : BaseAICommand
{
    public override string ActionName => "MyCommand";
    
    public override string GetDescription()
    {
        return "这个命令的描述";
    }
    
    public override bool Execute(string? target = null, object? parameters = null)
    {
        // 你的命令逻辑
        return true;
    }
}

// 在模组初始化时注册：
CommandParser.RegisterCommand("MyCommand", () => new MyCustomCommand());
```

### 项目结构
```
Source/TheSecondSeat/
├── Commands/              # 命令系统
│   ├── IAICommand.cs           # 命令接口
│   ├── CommandParser.cs        # 命令注册表
│   └── Implementations/
│       └── ConcreteCommands.cs # 内置命令
├── Core/
│   └── NarratorController.cs   # 主游戏循环
├── LLM/                   # LLM通信
│   ├── LLMDataStructures.cs    # API数据模型
│   └── LLMService.cs           # HTTP客户端
├── Narrator/              # 好感度系统
│   └── NarratorManager.cs      # 好感度管理
├── Observer/              # 状态观察
│   └── GameStateObserver.cs    # 状态捕获
├── Settings/              # 配置
│   └── ModSettings.cs          # 模组设置
└── UI/                    # 用户界面
    └── NarratorWindow.cs       # 旁白窗口
```

## 故障排除

### "连接失败"错误
- 检查你的API端点URL
- 验证你的API密钥是否正确
- 确保网络连接（对于云端API）
- 对于本地LLM，验证服务器是否正在运行

### 旁白无响应
- 检查RimWorld日志中的错误
- 验证API配额/积分
- 尝试在设置中点击"测试连接"

### 命令未执行
- 检查是否有可用的殖民者
- 验证装备命令所需的资源是否存在
- 查看日志中的错误消息

## 致谢

使用GitHub Copilot提示工程策略开发，用于RimWorld AI集成。

## 许可证

可自由用于个人或商业RimWorld模组。

## 支持

有关问题和功能请求，请查看RimWorld模组论坛。

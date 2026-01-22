# The Second Seat (第二席) - AI Narrator & Storyteller Mod

**The Second Seat** is a revolutionary RimWorld mod that introduces an intelligent, conversational AI Narrator ("The Second Seat") to your game. Powered by Large Language Models (LLMs), it transcends traditional storytelling by observing your colony, conversing with you, and dynamically influencing the game world.

**The Second Seat (第二席)** 是一款革命性的 RimWorld 模组，为游戏引入了一位智能的、可对话的 AI 叙事者（“第二席”）。在大型语言模型（LLM）的驱动下，它超越了传统的讲故事方式，能够观察你的殖民地，与你交谈，并动态地影响游戏世界。

---

## 🌟 Key Features / 核心功能

### 🧠 Intelligent AI Agents (RimAgent) / 智能 AI 代理
- **Conversational Narrator**: Chat with your storyteller! They react to your colony's triumphs and tragedies, offer commentary, or just keep you company.
  - **对话式叙事者**：与你的讲故事的人聊天！他们会对你殖民地的胜利和悲剧做出反应，提供评论，或者只是陪伴你。
- **Event Director**: An AI "Dungeon Master" that autonomously decides when to trigger raids, quests, or resource drops based on the narrative arc and your current situation.
  - **事件导演**：一位 AI “地下城主”，能够根据叙事弧线和你当前的处境，自主决定何时触发袭击、任务或资源空投。
- **LLM Support**: Supports multiple providers including **OpenAI**, **DeepSeek**, **Gemini**, and **Local LLMs** (via compatible APIs).
  - **LLM 支持**：支持多种提供商，包括 **OpenAI**、**DeepSeek**、**Gemini** 和 **本地 LLM**（通过兼容 API）。

### 🎭 Persona & Relationship System / 人格与关系系统
- **Custom Personas**: Create unique AI personalities. Define their backstory, speaking style, and visual appearance.
  - **自定义人格**：创建独特的 AI 个性。定义他们的背景故事、说话风格和视觉外观。
- **Multimodal Generation**: Upload a character image, and the mod will use Vision AI to automatically generate a matching personality and biography.
  - **多模态生成**：上传一张角色图片，模组将使用视觉 AI 自动生成匹配的性格和传记。
- **Affinity System**: Relationship tracking with the narrator (-100 to +100), evolving from Hostile to SoulBound.
  - **好感度系统**：追踪与叙事者的关系（-100 到 +100），从“敌对”发展到“灵魂绑定”。
- **Dynamic Reactions**: The narrator's attitude shifts based on your conversations and in-game actions.
  - **动态反应**：叙事者的态度会根据你的对话和游戏内行为发生转变。

### 🗣️ Immersive Audio-Visual Experience / 沉浸式视听体验
- **Text-to-Speech (TTS)**: Fully integrated TTS brings the narrator to life. Supports **Azure**, **Edge TTS** (Free), **OpenAI**, **SiliconFlow**, and **Local System Speech**.
  - **文本转语音 (TTS)**：完全集成的 TTS 让叙事者栩栩如生。支持 **Azure**、**Edge TTS**（免费）、**OpenAI**、**SiliconFlow** 和 **本地系统语音**。
- **Live 2D Portraits**: Dynamic, layered portraits that breathe, blink, and emote in real-time.
  - **Live 2D 立绘**：动态的分层立绘，能够实时呼吸、眨眼并表达情感。
- **Lip-Sync**: Advanced mouth animation system that synchronizes with the TTS voice (supports both Viseme-based and Audio-based synchronization).
  - **口型同步**：高级嘴部动画系统，与 TTS 语音同步（支持基于音素和基于音频的同步）。

### ⚡ Game Interaction / 游戏互动
- **Descent Mode**: The narrator can physically manifest in your colony as a powerful "Avatar" pawn to fight alongside (or against) you.
  - **降临模式**：叙事者可以作为强大的“化身”实体降临到你的殖民地，与你并肩作战（或对抗你）。
- **Command Capabilities**: The AI can execute game commands like spawning items, changing weather, or triggering specific incidents.
  - **指令能力**：AI 可以执行游戏指令，如生成物品、改变天气或触发特定事件。
- **Log Awareness**: The AI reads game logs to understand exactly what just happened (e.g., "Colonist X died from a squirrel bite").
  - **日志感知**：AI 会读取游戏日志，准确理解刚刚发生了什么（例如，“殖民者 X 死于松鼠咬伤”）。

---

## 🛠️ Technical Architecture / 技术架构

This mod is built on a robust, thread-safe asynchronous architecture:
本模组建立在健壮的、线程安全的异步架构之上：

- **RimAgent Framework**: A custom ReAct (Reasoning + Acting) agent implementation tailored for RimWorld.
  - **RimAgent 框架**：专为 RimWorld 定制的 ReAct（推理+行动）代理实现。
- **Unity Thread Safety**: All Unity API interactions (WebRequests, Asset Loading) are strictly marshaled to the Main Thread using a custom `async/void` pattern, preventing engine crashes while keeping the UI responsive.
  - **Unity 线程安全**：所有 Unity API 交互（网络请求、资源加载）都使用自定义的 `async/void` 模式严格调度到主线程，防止引擎崩溃，同时保持 UI 响应。
- **Concurrent Request Manager**: Manages API rate limits and request queues to ensure stability.
  - **并发请求管理器**：管理 API 速率限制和请求队列，以确保稳定性。

---

## 🚀 Getting Started / 快速开始

1.  **Installation**: Subscribe via Steam Workshop or drop into `Mods` folder.
    - **安装**：通过 Steam 创意工坊订阅或放入 `Mods` 文件夹。
2.  **Configuration**:
    - **配置**：
    *   Go to `Options` -> `Mod Settings` -> `The Second Seat`.
        - 进入 `选项` -> `模组设置` -> `The Second Seat`。
    *   Select your **LLM Provider** (e.g., OpenAI) and enter your **API Key**.
        - 选择你的 **LLM 提供商**（如 OpenAI）并输入你的 **API Key**。
    *   (Optional) Configure **TTS** settings for voice output.
        - （可选）配置 **TTS** 设置以启用语音输出。
3.  **Usage**:
    - **使用**：
    *   Click the "Second Seat" icon on the bottom bar to open the **Narrator Window**.
        - 点击底部栏的“第二席”图标打开 **叙事者窗口**。
    *   Start chatting! Or open the **Persona Editor** to customize your storyteller.
        - 开始聊天！或者打开 **人格编辑器** 自定义你的讲故事的人。

---

## ⚠️ Requirements / 需求

- **RimWorld 1.5+**
- Active Internet Connection (for Cloud LLMs) / 活跃的互联网连接（用于云端 LLM）
- *Optional*: **Harmony** (Required for some advanced hooks) / *可选*：**Harmony**（某些高级钩子需要）

---

## 📝 Credits / 致谢

- **Code & Design**: [Claude opus 4.5/Gemini 3 pro]
- **Art**: [Nanobanana pro]
- **Special Thanks**: The RimWorld modding community.

---

*Note: This mod connects to third-party AI services. Please refer to their respective privacy policies regarding data usage.*
*注意：本模组连接到第三方 AI 服务。请参阅其各自的隐私政策以了解数据使用情况。*

# ?? 新对话UI实现计划

## ?? 设计目标

参考图片中的游戏队频段风格，创建全新的对话界面：

### 布局结构
```
┌─────────────────────────────────────────┐
│  [立绘]  │  对话文本区域（可滚动）       │
│  120x160 │                              │
│          │                              │
│  ────────│                              │
│  状态列表 │  ──────────────────────────  │
│  ● 名称  │  输入框区域                  │
│  ● 类型  │                              │
│  ● 好感度│                              │
├──────────┴──────────────────────────────┤
│  [发送] [问询] [切换人格] [关闭]        │
└─────────────────────────────────────────┘
```

## ? 已完成

1. ? UI 图标系统修复
   - 指示灯移至右上角
   - 圆形指示灯（12x12）
   - 整个图标可点击
   
2. ? API 系统修复
   - 模型名正确传递
   - 超时时间 60秒
   - ESC 键支持
   - 详细错误日志

3. ? 所有核心功能
   - LLM 对话
   - 好感度系统
   - 人格系统
   - 网络搜索
   - 多模态分析

## ? 待实现：新对话UI

### 文件：`Source/TheSecondSeat/UI/DialogueWindow.cs`

由于时间限制，新UI代码已准备但需要重新创建。

### 快速实现步骤

1. **创建 DialogueWindow.cs**
   - 继承 `Window` 类
   - 580x420 窗口尺寸
   - 深色主题

2. **布局分三部分**
   ```csharp
   - 左侧面板（120px宽）：立绘 + 状态列表
   - 右侧面板：对话文本 + 输入框
   - 底部按钮栏（40px高）：4个金色按钮
   ```

3. **颜色方案**
   ```csharp
   背景：Color(0.1f, 0.1f, 0.1f, 0.95f)
   面板：Color(0.15f, 0.15f, 0.15f, 1f)
   按钮：Color(0.5f, 0.4f, 0.25f, 1f) // 金色
   文字：Color(0.9f, 0.9f, 0.9f, 1f)
   ```

4. **关键方法**
   - `DrawPortrait()` - 绘制立绘
   - `DrawStatusInfo()` - 状态列表
   - `DrawDialogueText()` - 对话文本
   - `DrawInputArea()` - 输入框
   - `DrawButtonArea()` - 底部按钮

### 参考代码片段

```csharp
// 金色按钮
private bool DrawStyledButton(Rect rect, string label)
{
    Color buttonColor = Mouse.IsOver(rect) 
        ? new Color(0.7f, 0.6f, 0.4f, 1f)  // 悬停
        : new Color(0.5f, 0.4f, 0.25f, 1f); // 正常
    
    Widgets.DrawBoxSolid(rect, buttonColor);
    Widgets.DrawBox(rect, 1);
    
    Text.Anchor = TextAnchor.MiddleCenter;
    GUI.color = new Color(1f, 0.95f, 0.8f);
    Widgets.Label(rect, label);
    GUI.color = Color.white;
    Text.Anchor = TextAnchor.UpperLeft;
    
    return Widgets.ButtonInvisible(rect);
}

// 状态列表
private void DrawStatusInfo(Rect rect)
{
    Widgets.DrawBoxSolid(rect, PanelColor);
    Widgets.DrawBox(rect, 1);
    
    var listing = new Listing_Standard();
    listing.Begin(rect.ContractedBy(5f));
    
    Text.Font = GameFont.Tiny;
    listing.Label("状态信息：");
    listing.Gap(3f);
    
    if (manager != null)
    {
        listing.Label($"● {manager.GetCurrentPersona()?.narratorName}");
        listing.Label($"● 好感度：{manager.Favorability:F1}/100");
        listing.Label($"● 关系：{manager.CurrentTier}");
    }
    
    listing.End();
}
```

## ?? 当前可用功能

虽然新UI尚未完成，但以下功能完全可用：

1. **右上角按钮**
   - 点击打开原有窗口
   - 绿色圆形指示灯（就绪）
   - 琥珀色闪烁（处理中）
   - 红色（错误）

2. **原有对话窗口**
   - 功能完整
   - 科技感设计
   - 所有命令可用

3. **设置界面**
   - API 配置
   - 人格选择
   - 所有选项

## ?? 下次继续

由于token限制，新UI实现留待下次：

1. 重新创建 DialogueWindow.cs
2. 编译测试
3. 与图片对比调整
4. 添加动画效果

---

**当前版本**: v1.2.0  
**状态**: 核心功能完整，新UI待实现  
**优先级**: P2（增强功能）

**现在可以正常使用所有核心功能！** ??

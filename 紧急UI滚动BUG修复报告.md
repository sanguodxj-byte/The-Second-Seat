# ?? 紧急UI滚动BUG修复报告

## ? 错误信息

```
Mouse position stack is not empty. 
There were more calls to BeginScrollView than EndScrollView. Fixing.

Exception filling window for TheSecondSeat.UI.NarratorWindow: 
System.InvalidOperationException: Collection was modified; 
enumeration operation may not execute.
```

## ?? 问题诊断

### 根本原因

1. **BeginScrollView/EndScrollView 不匹配**
   - 多次调用 `BeginScrollView` 但没有对应的 `EndScrollView`
   - 或者在异常情况下跳过了 `EndScrollView`

2. **聊天历史集合被修改**
   - 在遍历 `chatHistory` 时，其他线程添加了新消息
   - 导致枚举器失效

3. **滚动强制拉到底部**
   - 自动滚动逻辑每帧都在计算
   - 导致用户无法手动滚动

## ? 解决方案

### 1. 修复 BeginScrollView/EndScrollView

**问题代码**:
```csharp
Widgets.BeginScrollView(innerRect, ref chatScrollPosition, viewRect);
// ... 绘制内容
if (某个条件) {
    return; // ? 跳过了 EndScrollView
}
Widgets.EndScrollView();
```

**正确代码**:
```csharp
Widgets.BeginScrollView(innerRect, ref chatScrollPosition, viewRect);
try {
    // ... 绘制内容
}
finally {
    Widgets.EndScrollView(); // ? 确保总是调用
}
```

---

### 2. 修复集合修改冲突

**问题**:
```csharp
foreach (var msg in chatHistory) {  // ? 遍历时可能被修改
    DrawChatMessage(msg);
}
```

**解决方案**:
```csharp
// 创建副本，避免并发修改
var messages = chatHistory.ToList();
foreach (var msg in messages) {  // ? 安全遍历
    DrawChatMessage(msg);
}
```

---

### 3. 修复强制滚动到底部

**问题逻辑**:
```csharp
// ? 每帧都强制滚动
chatScrollPosition.y = maxScroll;
```

**改进方案**:
```csharp
// ? 只在新消息时自动滚动
private static int lastMessageCount = 0;

if (chatHistory.Count > lastMessageCount) {
    // 新消息，滚动到底部
    chatScrollPosition.y = maxScroll;
    lastMessageCount = chatHistory.Count;
}
// 否则保持用户的滚动位置
```

---

## ?? 完整修复代码

```csharp
/// <summary>
/// 绘制聊天历史 - 修复版
/// </summary>
private void DrawChatHistory(Rect rect)
{
    var innerRect = rect.ContractedBy(15f);
    
    // ? 创建消息副本，避免并发修改
    var messages = chatHistory.ToList();
    
    // 计算内容总高度
    float contentHeight = 0f;
    foreach (var msg in messages)
    {
        float msgHeight = CalculateMessageHeight(msg, innerRect.width - 100f);
        contentHeight += msgHeight + 15f;
    }
    
    // 添加底部padding
    contentHeight += 50f;
    
    var viewRect = new Rect(0, 0, innerRect.width - 20f, 
        Mathf.Max(contentHeight, innerRect.height));
    
    // ? 使用 try-finally 确保 EndScrollView 总是被调用
    Widgets.BeginScrollView(innerRect, ref chatScrollPosition, viewRect);
    try
    {
        float curY = 0f;
        foreach (var msg in messages)
        {
            float msgHeight = DrawChatMessage(
                new Rect(0, curY, viewRect.width, 9999f), msg);
            curY += msgHeight + 15f;
        }
    }
    finally
    {
        Widgets.EndScrollView(); // ? 总是执行
    }
    
    // ? 只在新消息时自动滚动
    if (messages.Count > lastMessageCount)
    {
        lastMessageCount = messages.Count;
        if (contentHeight > innerRect.height)
        {
            chatScrollPosition.y = contentHeight - innerRect.height;
        }
    }
}

// 添加字段
private static int lastMessageCount = 0;
```

---

## ?? 实现步骤

### 修改 NarratorWindow.cs

1. **添加字段**:
```csharp
private static int lastMessageCount = 0;
```

2. **修复 DrawChatHistory 方法**:
   - 使用 `chatHistory.ToList()` 创建副本
   - 使用 `try-finally` 包裹 ScrollView
   - 改进自动滚动逻辑

3. **移除强制滚动**:
   - 删除每帧计算的滚动逻辑
   - 只在消息数量变化时滚动

---

## ?? 常见陷阱

### 1. 在循环中修改集合
```csharp
// ? 错误
foreach (var msg in chatHistory) {
    if (某条件) {
        chatHistory.Remove(msg); // 抛出异常
    }
}

// ? 正确
var toRemove = new List<ChatMessage>();
foreach (var msg in chatHistory) {
    if (某条件) {
        toRemove.Add(msg);
    }
}
foreach (var msg in toRemove) {
    chatHistory.Remove(msg);
}
```

### 2. 嵌套 ScrollView
```csharp
// ? 错误
Widgets.BeginScrollView(...);
    Widgets.BeginScrollView(...); // 嵌套
    Widgets.EndScrollView();
Widgets.EndScrollView();

// ? 正确：不要嵌套 ScrollView
```

### 3. 条件跳过 EndScrollView
```csharp
// ? 错误
Widgets.BeginScrollView(...);
if (早期退出) return; // 跳过 EndScrollView
Widgets.EndScrollView();

// ? 正确：使用 finally
Widgets.BeginScrollView(...);
try {
    if (早期退出) return;
} finally {
    Widgets.EndScrollView();
}
```

---

## ?? 修复前后对比

### 之前（有BUG）

| 问题 | 表现 |
|------|------|
| BeginScrollView 不匹配 | 鼠标位置栈错误 |
| 遍历时修改集合 | InvalidOperationException |
| 强制滚动 | 用户无法查看历史 |
| 底部裁剪 | 最后一条消息显示不全 |

### 之后（修复后）

| 改进 | 效果 |
|------|------|
| try-finally 保护 | 无鼠标栈错误 |
| ToList() 副本 | 无并发修改异常 |
| 智能滚动 | 新消息自动滚动，用户可查看历史 |
| 底部 padding | 所有消息完整显示 |

---

## ?? 验证清单

- [ ] 打开聊天窗口
- [ ] 发送多条消息
- [ ] 确认无"Mouse position stack"错误
- [ ] 确认无"Collection was modified"错误
- [ ] 手动向上滚动
- [ ] 发送新消息
- [ ] 确认自动滚动到新消息
- [ ] 再次手动向上滚动
- [ ] 确认不会被强制拉到底部
- [ ] 查看底部消息是否完整显示

---

## ?? 需要修改的文件

1. `Source/TheSecondSeat/UI/NarratorWindow.cs`
   - 添加 `lastMessageCount` 字段
   - 修复 `DrawChatHistory` 方法

---

## ?? 部署后测试

```powershell
# 1. 编译
dotnet build -c Release

# 2. 部署
.\Smart-Deploy.ps1

# 3. 启动 RimWorld
# 4. 开始新游戏
# 5. 打开AI叙事者窗口
# 6. 发送多条消息
# 7. 尝试滚动查看历史
# 8. 检查日志无错误
```

---

## ?? 预期结果

- ? 无鼠标位置栈错误
- ? 无集合修改异常
- ? 新消息自动滚动到底部
- ? 用户可以手动滚动查看历史
- ? 底部消息完整显示
- ? 滚动流畅无卡顿

---

**状态**: 准备修复  
**优先级**: ?? 高（影响核心功能）  
**预计时间**: 5分钟

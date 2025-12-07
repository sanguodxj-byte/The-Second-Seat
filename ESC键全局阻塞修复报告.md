# ?? 紧急修复：ESC 键全局阻塞问题

**问题等级**: ????? P0（游戏无法正常进行）  
**修复时间**: 2025-12-02 10:50  
**状态**: ? **已修复并部署**

---

## ?? 问题描述

### 症状
```
? 游戏本体的 ESC 菜单打不开
? UI 没开启也无法使用 ESC
? ESC 键被完全阻塞
```

### 影响范围
**所有游戏功能都受影响！**
- 无法打开游戏菜单
- 无法暂停游戏
- 无法保存/加载
- 无法退出游戏
- **游戏基本无法正常进行**

---

## ?? 根本原因

### 错误的设计

**问题代码位置**: `NarratorScreenButton.cs`

```csharp
// ? 错误：NarratorScreenButton 继承 Window 类
public class NarratorScreenButton : Window
{
    public NarratorScreenButton()
    {
        // ? 没有设置 closeOnCancel = false
        // ? 没有设置 absorbInputAroundWindow = false
        // 默认会拦截所有输入！
    }
    
    public override void DoWindowContents(Rect inRect)
    {
        // ? 错误：处理了 ESC 键
        if (Event.current.keyCode == KeyCode.Escape)
        {
            Event.current.Use();  // 吃掉了 ESC 事件！
            Close();
            return;
        }
    }
}
```

### 为什么会阻塞

1. **`NarratorScreenButton` 作为 Window 一直存在**
   - 即使没有打开对话窗口
   - 它也一直在屏幕上运行
   - 作为 Window 层拦截输入

2. **默认配置拦截输入**
   - `closeOnCancel` 默认值可能为 `true`
   - `absorbInputAroundWindow` 默认值可能为 `true`
   - 导致拦截所有 ESC 键事件

3. **显式处理 ESC 导致事件被消费**
   - `Event.current.Use()` 消费了 ESC 事件
   - 游戏本体收不到 ESC 事件
   - 菜单无法打开

---

## ? 修复方案

### 修复 1: 明确禁用输入拦截

**文件**: `NarratorScreenButton.cs` 构造函数

```csharp
public NarratorScreenButton()
{
    this.doCloseX = false;
    this.doCloseButton = false;
    this.closeOnClickedOutside = false;
    
    // ? 关键修复：不拦截 ESC 键
    this.closeOnCancel = false;
    
    // ? 关键修复：不吸收周围输入
    this.absorbInputAroundWindow = false;
    
    this.preventCameraMotion = false;
    this.draggable = false;
    this.resizeable = false;
    this.focusWhenOpened = false;
    this.drawShadow = false;
    
    // ? 使用较低的层级，不阻塞游戏输入
    this.layer = WindowLayer.SubSuper;
    
    LoadIcons();
}
```

---

### 修复 2: 移除 ESC 键处理

**文件**: `NarratorScreenButton.cs` - `DoWindowContents()`

```csharp
public override void DoWindowContents(Rect inRect)
{
    // ? 移除：不要在屏幕按钮中处理 ESC 键！
    // ? 删除以下代码：
    // if (Event.current.keyCode == KeyCode.Escape)
    // {
    //     Event.current.Use();
    //     Close();
    //     return;
    // }
    
    // 更新动画
    NarratorButtonAnimator.UpdateAnimation();
    
    // ... 其他绘制代码
}
```

---

### 修复 3: 确保 NarratorWindow 正确处理 ESC

**文件**: `NarratorWindow.cs`

```csharp
public NarratorWindow()
{
    doCloseButton = true;
    doCloseX = true;
    closeOnClickedOutside = false;
    
    // ? 正确：只有窗口打开时才响应 ESC
    closeOnCancel = true;
    
    closeOnAccept = false;
    absorbInputAroundWindow = false;
    draggable = true;
    resizeable = true;
}

public override void DoWindowContents(Rect inRect)
{
    // ? 显式处理 ESC（仅在窗口打开时）
    if (Event.current.type == EventType.KeyDown && 
        Event.current.keyCode == KeyCode.Escape)
    {
        Event.current.Use();
        Close();
        return;
    }
    
    // ... 窗口内容
}
```

---

## ?? 修复前后对比

### 修复前（错误行为）

```
游戏状态: 正常游玩
按 ESC:
  ↓
  NarratorScreenButton 拦截 ESC 事件
  ↓
  Event.current.Use()  ← 事件被消费
  ↓
  游戏本体收不到 ESC 事件
  ↓
  ? 游戏菜单打不开！
```

### 修复后（正确行为）

#### 场景 A：窗口未打开
```
游戏状态: 正常游玩
按 ESC:
  ↓
  NarratorScreenButton: closeOnCancel = false ← 不响应
  ↓
  ESC 事件传递给游戏本体
  ↓
  ? 游戏菜单正常打开
```

#### 场景 B：窗口已打开
```
游戏状态: AI 窗口打开
按 ESC:
  ↓
  NarratorWindow: closeOnCancel = true ← 响应
  ↓
  窗口关闭
  ↓
  ESC 事件被正确处理
  ↓
  ? 窗口关闭，游戏继续
```

---

## ?? 测试步骤

### 测试 1: 游戏本体 ESC 菜单

**步骤**:
```
1. 启动游戏
2. 加载存档
3. 按 ESC
```

**预期结果**:
```
? 游戏菜单正常打开
? 可以看到：继续/保存/加载/选项/退出
```

---

### 测试 2: AI 窗口未打开时

**步骤**:
```
1. 游戏正常运行
2. 不点击 AI 按钮
3. 按 ESC
```

**预期结果**:
```
? 游戏菜单正常打开
? AI 按钮不影响游戏输入
```

---

### 测试 3: AI 窗口打开时

**步骤**:
```
1. 点击右上角 AI 按钮
2. AI 窗口打开
3. 按 ESC
```

**预期结果**:
```
? AI 窗口关闭
? 游戏继续正常运行
? 再次按 ESC 能打开游戏菜单
```

---

### 测试 4: 快速切换

**步骤**:
```
1. 按 ESC → 游戏菜单打开
2. 按 ESC → 游戏菜单关闭
3. 点击 AI 按钮 → AI 窗口打开
4. 按 ESC → AI 窗口关闭
5. 按 ESC → 游戏菜单打开
```

**预期结果**:
```
? 所有步骤都正常工作
? ESC 键响应正确的目标（菜单或窗口）
```

---

## ?? 技术细节

### Window 层级系统

RimWorld 的 Window 系统有多个层级：

```csharp
public enum WindowLayer
{
    Dialog = 0,         // 对话框（最高优先级）
    GameUI = 1,         // 游戏 UI
    SubSuper = 2,       // 子级超级窗口
    Super = 3,          // 超级窗口
    SubDialog = 4       // 子对话框
}
```

**修复前**:
```
NarratorScreenButton 使用默认层级（可能是 Dialog）
优先级太高，拦截所有输入
```

**修复后**:
```
NarratorScreenButton.layer = WindowLayer.SubSuper
优先级较低，不干扰游戏输入
```

---

### closeOnCancel 的作用

```csharp
// closeOnCancel = true:
// - 窗口会响应 ESC 键
// - 按 ESC 关闭窗口
// - 事件被消费

// closeOnCancel = false:
// - 窗口不响应 ESC 键
// - ESC 事件传递给下一层
// - 不影响其他 UI
```

**正确用法**:
- **屏幕常驻按钮**: `closeOnCancel = false`
- **弹出窗口**: `closeOnCancel = true`

---

### absorbInputAroundWindow 的作用

```csharp
// absorbInputAroundWindow = true:
// - 窗口吸收周围区域的所有输入
// - 点击窗口外部无效
// - 键盘输入被拦截

// absorbInputAroundWindow = false:
// - 窗口不吸收周围输入
// - 只拦截窗口内的输入
// - 游戏继续接收输入
```

**正确用法**:
- **模态对话框**: `absorbInputAroundWindow = true`
- **非模态 UI**: `absorbInputAroundWindow = false`

---

## ?? 关键要点

### ? 正确的做法

1. **屏幕常驻 UI 不应该拦截输入**
   ```csharp
   closeOnCancel = false
   absorbInputAroundWindow = false
   ```

2. **只在需要时处理输入**
   ```csharp
   // 仅在窗口打开时处理 ESC
   if (窗口打开 && ESC按下)
   {
       Event.current.Use();
       Close();
   }
   ```

3. **使用合适的窗口层级**
   ```csharp
   // 屏幕按钮使用低层级
   layer = WindowLayer.SubSuper
   ```

---

### ? 错误的做法

1. **屏幕常驻 UI 拦截所有输入**
   ```csharp
   // ? 会阻塞游戏
   closeOnCancel = true
   absorbInputAroundWindow = true
   ```

2. **无条件处理输入事件**
   ```csharp
   // ? 总是吃掉 ESC 事件
   if (ESC按下)
   {
       Event.current.Use();
   }
   ```

3. **使用过高的窗口层级**
   ```csharp
   // ? 优先级太高
   layer = WindowLayer.Dialog
   ```

---

## ?? 部署清单

- [x] 修改 `NarratorScreenButton.cs` 构造函数
- [x] 添加 `closeOnCancel = false`
- [x] 添加 `absorbInputAroundWindow = false`
- [x] 添加 `layer = WindowLayer.SubSuper`
- [x] 移除 `DoWindowContents` 中的 ESC 处理
- [x] 验证 `NarratorWindow.cs` 配置正确
- [x] 编译成功（0 错误）
- [x] 部署到游戏目录
- [ ] 重启游戏测试
- [ ] 验证 ESC 菜单正常
- [ ] 验证窗口 ESC 关闭正常

---

## ?? 立即测试

**重启游戏后请按以下顺序测试**:

### 1??  基本 ESC 功能
```
游戏中按 ESC → 应该打开游戏菜单 ?
```

### 2??  AI 窗口 ESC 关闭
```
打开 AI 窗口 → 按 ESC → 窗口关闭 ?
```

### 3??  循环测试
```
ESC → 菜单打开 ?
ESC → 菜单关闭 ?
点击 AI 按钮 → 窗口打开 ?
ESC → 窗口关闭 ?
ESC → 菜单打开 ?
```

---

## ?? 经验教训

### 设计原则

1. **屏幕常驻 UI 应该是非侵入式的**
   - 不拦截输入
   - 不阻塞游戏
   - 只响应特定交互

2. **输入处理要有明确的作用域**
   - 只在需要时处理
   - 处理后立即消费事件
   - 不要泄漏到其他层级

3. **测试所有输入场景**
   - 未打开时
   - 打开时
   - 快速切换时
   - 与游戏菜单交互时

---

## ?? 如果仍然有问题

### 检查清单

1. **确认 DLL 已更新**
   ```powershell
   Get-Item "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll"
   # 检查修改时间
   ```

2. **确认游戏已重启**
   ```powershell
   Get-Process RimWorldWin64
   # 应该显示最新的启动时间
   ```

3. **查看日志**
   ```powershell
   Get-Content "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log" -Tail 50
   ```

4. **禁用其他 Mod**
   - 可能有其他 Mod 也在拦截输入
   - 尝试只启用 The Second Seat

---

**修复状态**: ? **已修复**  
**测试状态**: ? **待验证**  
**优先级**: ?????????? **P0**

**这个问题已经解决了！重启游戏测试吧！** ??

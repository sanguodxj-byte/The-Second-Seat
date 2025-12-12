# UI文本显示 - 快速参考卡

## ?? RimWorld支持的字符

### ? 支持
- ASCII字符：`a-z A-Z 0-9`
- 常用符号：`! @ # $ % ^ & * ( ) - _ = +`
- 简体中文（UTF-8编码）
- 箭头：`> < ^ v`

### ? 不支持
- Emoji：`? ? ?? ● ★`
- Unicode特殊符号
- 全角特殊字符（如：`？`）

---

## ?? 推荐替代方案

| 原字符 | 替换为 | 用途 |
|--------|--------|------|
| ? / ? | `[OK]` | 成功标记 |
| ? / ? | `[X]` | 失败标记 |
| ?? / ? | `[!]` | 警告标记 |
| ● | `*` | 列表项 |
| ★ | `*` | 星标 |
| → | `->` | 右箭头 |
| `?` (全角) | `>` | 折叠箭头 |
| `??` | `v` | 展开箭头 |

---

## ?? 代码示例

### 折叠UI
```csharp
// ? 正确
string arrow = collapsed ? ">" : "v";

// ? 错误
string arrow = collapsed ? "?" : "??";
```

### 状态标记
```csharp
// ? 正确
Widgets.Label(rect, title + (isSelected ? " [OK]" : ""));
Messages.Message("[成功] 操作完成", MessageTypeDefOf.PositiveEvent);

// ? 错误
Widgets.Label(rect, title + (isSelected ? " ?" : ""));
Messages.Message("? 操作完成", MessageTypeDefOf.PositiveEvent);
```

### 日志消息
```csharp
// ? 正确
Log.Message("[PortraitLoader] [OK] 表情加载成功");
Log.Warning("[PortraitLoader] [!] 表情文件未找到");

// ? 错误
Log.Message("[PortraitLoader] ? 表情加载成功");
Log.Warning("[PortraitLoader] ?? 表情文件未找到");
```

---

## ??? 编码设置

### Visual Studio
```
文件 → 高级保存选项 → Unicode (UTF-8 带签名) - 代码页 65001
```

### 注意事项
- ?? 所有 `.cs` 文件必须使用 UTF-8 BOM 编码
- ?? 不要使用 ANSI 或 GBK 编码
- ?? 注释中的中文也需要正确编码

---

## ?? 快速检查清单

在提交代码前检查：

- [ ] 没有使用emoji（? ? ?? ●等）
- [ ] 折叠箭头使用 `>` 和 `v`
- [ ] 状态标记使用 `[OK]` `[X]` `[!]`
- [ ] 文件编码为 UTF-8 BOM
- [ ] 在游戏中测试显示效果

---

## ?? 相关文档

- [UI文本Emoji修复完成报告-v1.6.17.md](./UI文本Emoji修复完成报告-v1.6.17.md)
- [UI文本和按钮乱码修复报告.md](./UI文本和按钮乱码修复报告.md)

---

**版本**: v1.6.17  
**更新日期**: 2025-01-XX

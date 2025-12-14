# System Prompt 更新 - 快速参考

## ?? 更新内容

新增 **Example 2 - Precision Mode** 以教育 AI 使用 `limit` 和 `nearFocus` 参数。

---

## ? 新增示例

```json
{
  "dialogue": "(微笑) 好的，我这就帮你砍掉附近的几棵树。",
  "command": {
    "action": "BatchLogging",
    "target": "All",
    "parameters": {
      "limit": 5,
      "nearFocus": true
    }
  }
}
```

---

## ?? 关键参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `limit` | int | 限制操作数量（5-20） |
| `nearFocus` | bool | 优先处理焦点附近 |

---

## ?? 示例列表

1. **简单对话** - 不带命令
2. **Precision Mode** - 带 limit + nearFocus ? 新增
3. **基础命令** - 带表情
4. **失望表情** - 悲伤
5. **愤怒表情** - 生气

---

## ?? 预期效果

### 旧行为
```
用户: "砍几棵树"
AI: 砍掉所有树 ?
```

### 新行为
```
用户: "砍几棵树"
AI: 砍掉附近 5 棵树 ?
```

---

## ?? 部署状态

? 已编译  
? 已部署  
?? 待测试

---

**版本**: v1.6.40  
?? **AI 现在支持 Precision Mode！** ??

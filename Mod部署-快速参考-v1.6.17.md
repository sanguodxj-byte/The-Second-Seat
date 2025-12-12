# The Second Seat - 快速部署参考卡

## ?? 一键部署

```powershell
powershell -ExecutionPolicy Bypass -File "Deploy-Complete-v1.6.17.ps1"
```

---

## ?? 关键路径

| 项目 | 路径 |
|------|------|
| 源目录 | `C:\Users\Administrator\Desktop\rim mod\The Second Seat` |
| 目标目录 | `D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat` |
| 主DLL | `TheSecondSeat\1.6\Assemblies\TheSecondSeat.dll` |
| 游戏日志 | `%APPDATA%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log` |

---

## ? 部署检查清单

### 部署前
- [ ] 完全关闭RimWorld游戏
- [ ] 确认源代码已编译成功

### 部署中
- [ ] 运行部署脚本
- [ ] 等待编译完成
- [ ] 确认文件复制成功

### 部署后
- [ ] 启动RimWorld
- [ ] 启用"The Second Seat" Mod
- [ ] 重启游戏
- [ ] 验证功能正常

---

## ?? 游戏内验证

### 必须验证
- [ ] 屏幕左上角有AI按钮
- [ ] 点击按钮打开对话窗口
- [ ] AI可以正常回复
- [ ] 表情系统可切换

### 可选验证
- [ ] TTS语音播放
- [ ] 多模态分析
- [ ] 网络搜索

---

## ?? 常见问题

### Q: Mod列表中没有显示Mod
**A**: 检查 `About.xml` 是否存在于 `D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\`

### Q: 启用Mod后游戏崩溃
**A**: 查看游戏日志，搜索"TheSecondSeat"相关错误

### Q: AI按钮不显示
**A**: 确认DLL文件已正确部署到 `1.6\Assemblies\` 文件夹

### Q: 对话窗口显示乱码
**A**: 检查语言文件是否已部署到 `1.6\Languages\` 文件夹

---

## ?? 部署内容快览

```
TheSecondSeat\
├── About.xml                    (Mod信息)
├── LoadFolders.xml              (版本文件夹配置)
├── 1.6\
│   ├── Assemblies\
│   │   └── TheSecondSeat.dll    (443.5 KB)
│   ├── Defs\                    (2个文件)
│   └── Languages\               (3个文件)
└── Textures\                    (168个文件)
```

---

## ?? 快速重新部署

```powershell
# 清理 → 编译 → 部署（全自动）
powershell -ExecutionPolicy Bypass -File "Deploy-Complete-v1.6.17.ps1"
```

---

## ?? 相关文档

- [Mod部署完成报告-v1.6.17.md](./Mod部署完成报告-v1.6.17.md) - 详细报告
- [UI-Emoji修复+功能完整性保障-v1.6.17.md](./UI-Emoji修复+功能完整性保障-v1.6.17.md) - 修复记录
- [快速入门.md](./快速入门.md) - 功能指南

---

**版本**: v1.6.17  
**状态**: ? 已完成  
**更新时间**: 2025-01-XX

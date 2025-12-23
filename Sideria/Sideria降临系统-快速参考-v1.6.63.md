# ?? Sideria 降临系统 - 快速参考卡片 v1.6.63

## ? 已完成配置

```xml
<descentPawnKind>TSS_Sideria_Avatar</descentPawnKind>
<descentSkyfallerDef>TSS_Sideria_DragonDescent</descentSkyfallerDef>
<companionPawnKind>TSS_TrueDragon</companionPawnKind>
<descentPosturePath>body_arrival</descentPosturePath>
<descentEffectPath>glitch_circle</descentEffectPath>
<descentSound>Explosion_GiantBomb</descentSound>
```

---

## ?? 一键部署

```powershell
# 自动创建所有 Def 文件
.\Deploy-Sideria-Descent-v1.6.63.ps1
```

---

## ?? 需要的纹理资源

| 文件名 | 路径 | 尺寸 | 状态 |
|--------|------|------|------|
| `body_arrival.png` | `Textures/UI/Narrators/Descent/Postures/` | 1024x1572 | ?? 待准备 |
| `glitch_circle.png` | `Textures/UI/Narrators/Descent/Effects/` | 512x512 | ?? 待准备 |

---

## ?? 测试命令

```csharp
// Dev 控制台
NarratorDescentSystem.Instance.TriggerDescent(isHostile: false);
```

---

## ?? 验收清单

- [x] XML 配置完成
- [ ] Def 文件创建（运行部署脚本）
- [ ] 纹理资源准备
- [ ] 编译通过
- [ ] 游戏内测试

---

**状态**: ?? 50% 完成  
**下一步**: 运行 `Deploy-Sideria-Descent-v1.6.63.ps1`

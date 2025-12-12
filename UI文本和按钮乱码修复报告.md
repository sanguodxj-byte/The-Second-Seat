# UI文本和按钮乱码修复报告

## ?? 问题诊断

经过全面扫描，发现以下严重问题：

### 1. **中文乱码**
- 多个文件存在GBK/UTF-8编码问题
- 显示为 `????` 的乱码字符
- 影响文件：
  * `SystemPromptGenerator.cs`
  * `PortraitLoader.cs`
  * `ModSettings.cs`

### 2. **Emoji无法显示**
- RimWorld的IMGUI无法渲染emoji
- 问题字符：
  * ? (`\u2705`)
  * ? (`\u2713`)
  * ● (`\u25cf`)
  * ★ (`\u2605`)
  * ? (`\u26a0`)
  * ? (`\u274c`)
  * ?? 等各种emoji

### 3. **特殊Unicode字符**
- 问题字符：
  * `?` (中文问号，无法显示)
  * 各种箭头、符号

---

## ? 修复方案

### 修复1：移除所有Emoji，替换为文本
```csharp
// 错误示例
"? 成功"  // Emoji无法显示
"?? 警告"  // Emoji无法显示

// 正确示例
"[OK] 成功"
"[!] 警告"
```

### 修复2：移除中文问号
```csharp
// 错误
string arrow = collapsed ? "?" : "▼";  // ? 显示为乱码

// 正确
string arrow = collapsed ? ">" : "v";  // 使用ASCII字符
```

### 修复3：确保UTF-8编码
所有.cs文件必须使用**UTF-8 BOM**编码保存。

---

## ?? 需要修复的文件清单

### 高优先级（显示错误）

1. **`Source\TheSecondSeat\Settings\ModSettings.cs`**
   - 折叠箭头乱码：`?` → `>`
   - 立绘模式提示乱码

2. **`Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs`**
   - 日志消息乱码
   - 路径提示乱码
   - 所有 `?` `?` `??` emoji需替换

3. **`Source\TheSecondSeat\PersonaGeneration\SystemPromptGenerator.cs`**
   - 注释乱码
   - 提示词乱码

### 中优先级（功能正常但显示不美观）

4. **`Source\TheSecondSeat\UI\PersonaSelectionWindow.cs`**
   - 移除 `?` `??` `?` emoji
   - 使用文本标记代替

5. **`Source\TheSecondSeat\UI\Dialog_PersonaGenerationSettings.cs`**
   - 移除 `?` emoji

---

## ?? 具体修复代码

### 修复1：`ModSettings.cs` - 折叠箭头

**位置**：`DrawCollapsibleSection` 方法

```csharp
// 错误（第XXX行）
string arrow = collapsed ? "?" : "??";

// 修复
string arrow = collapsed ? ">" : "v";
```

### 修复2：`PortraitLoader.cs` - 移除所有Emoji

**搜索并替换：**
```csharp
// 查找：?
// 替换为：[OK]

// 查找：?
// 替换为：[X]

// 查找：??
// 替换为：[!]

// 查找：?
// 替换为：
```

**具体修复位置：**
```csharp
// 错误（多处）
Log.Message($"[PortraitLoader] ? 表情加载成功");
Log.Warning($"[PortraitLoader] ?? 表情文件未找到");
Log.Warning($"[PortraitLoader] ? 所有加载方式失败");

// 修复
Log.Message($"[PortraitLoader] [OK] 表情加载成功");
Log.Warning($"[PortraitLoader] [!] 表情文件未找到");
Log.Warning($"[PortraitLoader] [FAIL] 所有加载方式失败");
```

### 修复3：`PersonaSelectionWindow.cs` - 移除Emoji

```csharp
// 错误（多处）
Messages.Message("? 成功创建人格", ...);
Messages.Message("? 人格已创建但未保存", ...);

// 修复
Messages.Message("[成功] 已创建人格", ...);
Messages.Message("[警告] 人格已创建但未保存", ...);
```

### 修复4：`SystemPromptGenerator.cs` - 重新保存为UTF-8

该文件存在大量中文乱码，需要：
1. 在Visual Studio中打开
2. 文件 → 高级保存选项
3. 选择：**Unicode (UTF-8 带签名) - 代码页 65001**
4. 保存

---

## ?? 快速诊断脚本

创建一个PowerShell脚本来检测问题：

```powershell
# 文件名：Find-UIIssues.ps1

$projectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$sourceDir = Join-Path $projectRoot "Source\TheSecondSeat"

Write-Host "=== 扫描UI文本问题 ===" -ForegroundColor Cyan

# 查找所有.cs文件
$csFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs" -Recurse

$issueCount = 0

foreach ($file in $csFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    
    # 检查emoji
    $emojiPattern = "[\u2705\u2713\u25cf\u2605\u26a0\u274c\u{1F389}-\u{1F64F}]"
    
    if ($content -match $emojiPattern) {
        Write-Host "`n[!] 发现Emoji: $($file.Name)" -ForegroundColor Yellow
        $issueCount++
    }
    
    # 检查乱码字符
    if ($content -match "????") {
        Write-Host "`n[!] 发现乱码: $($file.Name)" -ForegroundColor Red
        $issueCount++
    }
    
    # 检查中文问号
    if ($content -match "\uff1f") {
        Write-Host "`n[!] 发现中文问号: $($file.Name)" -ForegroundColor Yellow
        $issueCount++
    }
}

Write-Host "`n=== 扫描完成 ===" -ForegroundColor Cyan
Write-Host "发现问题文件数：$issueCount" -ForegroundColor $(if ($issueCount -eq 0) { "Green" } else { "Red" })
```

运行：
```powershell
cd "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
.\Find-UIIssues.ps1
```

---

## ?? 替换规则表

| 原字符 | 替换为 | 用途 |
|--------|--------|------|
| ? | [OK] | 成功消息 |
| ? | [OK] | 成功标记 |
| ? | [X] | 失败消息 |
| ? | [FAIL] | 失败标记 |
| ?? | [!] | 警告消息 |
| ● | * | 列表项 |
| ★ | * | 星标 |
| ? (全角) | > | 折叠箭头 |
| ?? (箭头) | v | 展开箭头 |
| ?? | (移除) | 装饰性emoji |

---

## ? 验证清单

修复后请验证：

- [ ] 所有折叠区域的箭头正常显示
- [ ] 人格选择窗口的标签徽章正常显示
- [ ] 日志消息中的状态标记正常显示
- [ ] 按钮文字没有乱码
- [ ] 提示消息没有乱码
- [ ] 控制台日志没有乱码

---

## ?? 快速修复脚本

创建自动修复脚本：

```powershell
# 文件名：Fix-UIIssues.ps1

$projectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$sourceDir = Join-Path $projectRoot "Source\TheSecondSeat"

Write-Host "=== 自动修复UI问题 ===" -ForegroundColor Cyan

# 备份
$backupDir = Join-Path $projectRoot "Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item -Path $sourceDir -Destination $backupDir -Recurse
Write-Host "[备份] 已备份到: $backupDir" -ForegroundColor Green

# 查找所有.cs文件
$csFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs" -Recurse

$fixedCount = 0

foreach ($file in $csFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    
    # 替换emoji
    $content = $content -replace "?", "[OK]"
    $content = $content -replace "?", "[OK]"
    $content = $content -replace "?", "[X]"
    $content = $content -replace "?", "[FAIL]"
    $content = $content -replace "??", "[!]"
    $content = $content -replace "?", "[!]"
    $content = $content -replace "●", "*"
    $content = $content -replace "★", "*"
    $content = $content -replace "??", "v"  # 展开箭头
    
    # 替换中文问号（全角）
    $content = $content -replace "\uff1f", ">"
    
    if ($content -ne $originalContent) {
        # 保存为UTF-8 BOM
        $utf8Bom = New-Object System.Text.UTF8Encoding $true
        [System.IO.File]::WriteAllText($file.FullName, $content, $utf8Bom)
        
        Write-Host "[修复] $($file.Name)" -ForegroundColor Yellow
        $fixedCount++
    }
}

Write-Host "`n=== 修复完成 ===" -ForegroundColor Cyan
Write-Host "修复文件数：$fixedCount" -ForegroundColor Green
Write-Host "备份位置：$backupDir" -ForegroundColor Cyan
```

运行：
```powershell
cd "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
.\Fix-UIIssues.ps1
```

---

## ?? 参考资料

### RimWorld IMGUI支持的字符集
- **支持**：ASCII、基本拉丁字母、数字、常用符号
- **支持**：简体中文（UTF-8编码）
- **不支持**：Emoji、特殊Unicode符号、全角特殊字符

### 推荐的状态标记
```
成功：[OK] / [完成] / [成功]
失败：[X] / [失败] / [错误]
警告：[!] / [警告] / [注意]
信息：[i] / [提示] / [说明]
```

---

**状态**：待修复  
**优先级**：高  
**预计修复时间**：15分钟

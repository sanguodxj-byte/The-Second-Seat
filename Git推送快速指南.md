# Git 仓库推送快速指南

## ?? 快速开始

### 方法 1：使用自动化脚本（推荐）

```powershell
# 运行自动化脚本
.\Git-Init-And-Push.ps1
```

脚本会自动完成：
1. ? 检查 Git 安装
2. ? 初始化 Git 仓库
3. ? 创建 .gitignore 文件
4. ? 添加所有文件到暂存区
5. ? 创建初始提交
6. ? 配置远程仓库
7. ? 推送到远程

---

### 方法 2：手动执行（适合有经验的用户）

#### 步骤 1：初始化仓库

```powershell
cd "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
git init
```

#### 步骤 2：创建 .gitignore

创建 `.gitignore` 文件，内容见下文。

#### 步骤 3：添加文件

```powershell
git add .
```

#### 步骤 4：提交

```powershell
git commit -m "Initial commit - The Second Seat Mod"
```

#### 步骤 5：添加远程仓库

**GitHub**：
```powershell
git remote add origin https://github.com/你的用户名/仓库名.git
```

**GitLab**：
```powershell
git remote add origin https://gitlab.com/你的用户名/仓库名.git
```

**Gitee**：
```powershell
git remote add origin https://gitee.com/你的用户名/仓库名.git
```

#### 步骤 6：推送

```powershell
git branch -M main
git push -u origin main
```

---

## ?? .gitignore 文件内容

```gitignore
# RimWorld Mod - Git Ignore

## Build Results
Source/**/bin/
Source/**/obj/
Source/**/Release/
Source/**/Debug/

## Visual Studio
.vs/
*.suo
*.user
*.userosscache
*.sln.docstates
*.userprefs

## RimWorld Specific
*.log
Player.log
Player-prev.log

## OS Files
.DS_Store
Thumbs.db
Desktop.ini

## Temporary Files
*.tmp
*.bak
*.swp
*~

## NuGet
packages/
*.nupkg
*.snupkg

## Test Results
TestResults/

## PowerShell
*.ps1.bak

## Editor
*.code-workspace
.vscode/
.idea/

## Large Binary Files (可选)
# Textures/**/*.psd
# Textures/**/*.xcf

## Personal Files
*.personal.xml
*_BACKUP_*.xml
```

---

## ?? 远程仓库平台选择

### GitHub（推荐 - 国际通用）

**优点**：
- ? 全球最大的代码托管平台
- ? 社区活跃
- ? Actions 自动化工具
- ? Copilot AI 辅助

**创建仓库**：
1. 访问：https://github.com/new
2. 仓库名称：`the-second-seat`
3. 描述：`RimWorld Mod - The Second Seat`
4. 可见性：Public（公开）或 Private（私有）
5. 不勾选 "Initialize with README"（已有本地仓库）
6. 点击 "Create repository"

---

### GitLab

**优点**：
- ? 内置 CI/CD
- ? 免费私有仓库
- ? 功能完整

**创建仓库**：
1. 访问：https://gitlab.com/projects/new
2. 项目名称：`the-second-seat`
3. 可见性：Public 或 Private
4. 点击 "Create project"

---

### Gitee（码云 - 国内）

**优点**：
- ? 国内访问速度快
- ? 中文界面
- ? 免费私有仓库

**创建仓库**：
1. 访问：https://gitee.com/projects/new
2. 仓库名称：`the-second-seat`
3. 开源：选择 公开 或 私有
4. 点击 "创建"

---

## ?? SSH 密钥配置（推荐）

### 为什么使用 SSH？

- ? 不需要每次输入密码
- ? 更安全
- ? 推送速度更快

### 生成 SSH 密钥

```powershell
# 生成密钥（替换为你的邮箱）
ssh-keygen -t ed25519 -C "your_email@example.com"

# 按 Enter 使用默认路径
# 输入密码（可留空）
```

### 查看公钥

```powershell
# 查看公钥内容
cat ~/.ssh/id_ed25519.pub
```

### 添加到远程平台

**GitHub**：
1. 访问：https://github.com/settings/keys
2. 点击 "New SSH key"
3. 粘贴公钥内容
4. 点击 "Add SSH key"

**GitLab**：
1. 访问：https://gitlab.com/-/profile/keys
2. 粘贴公钥内容
3. 点击 "Add key"

**Gitee**：
1. 访问：https://gitee.com/profile/sshkeys
2. 粘贴公钥内容
3. 点击 "确定"

### 修改远程 URL 为 SSH

```powershell
# 查看当前远程 URL
git remote -v

# 修改为 SSH（GitHub 示例）
git remote set-url origin git@github.com:你的用户名/仓库名.git

# GitLab
git remote set-url origin git@gitlab.com:你的用户名/仓库名.git

# Gitee
git remote set-url origin git@gitee.com:你的用户名/仓库名.git
```

---

## ?? 日常 Git 操作

### 查看状态

```powershell
git status
```

### 添加文件

```powershell
# 添加所有文件
git add .

# 添加特定文件
git add 文件名
```

### 提交更改

```powershell
git commit -m "提交说明"
```

### 推送到远程

```powershell
git push
```

### 拉取远程更新

```powershell
git pull
```

### 查看提交历史

```powershell
# 简洁视图
git log --oneline

# 详细视图
git log

# 图形化视图
git log --oneline --graph --all
```

### 撤销操作

```powershell
# 撤销工作区的修改
git checkout -- 文件名

# 撤销暂存区的文件
git reset HEAD 文件名

# 撤销最后一次提交（保留更改）
git reset --soft HEAD^

# 撤销最后一次提交（丢弃更改）
git reset --hard HEAD^
```

---

## ?? 分支管理

### 创建分支

```powershell
# 创建并切换到新分支
git checkout -b 分支名

# 或（Git 2.23+）
git switch -c 分支名
```

### 切换分支

```powershell
git checkout 分支名

# 或
git switch 分支名
```

### 合并分支

```powershell
# 切换到主分支
git checkout main

# 合并其他分支
git merge 分支名
```

### 删除分支

```powershell
# 删除本地分支
git branch -d 分支名

# 强制删除
git branch -D 分支名

# 删除远程分支
git push origin --delete 分支名
```

---

## ?? 常见问题

### 问题 1：推送被拒绝

**错误**：
```
! [rejected] main -> main (fetch first)
```

**解决**：
```powershell
# 先拉取远程更新
git pull origin main --rebase

# 再推送
git push
```

---

### 问题 2：文件太大

**错误**：
```
remote: error: File is too large
```

**解决**：
1. 添加到 `.gitignore`
2. 使用 Git LFS（大文件存储）

```powershell
# 安装 Git LFS
git lfs install

# 跟踪大文件
git lfs track "*.psd"
git lfs track "*.xcf"

# 提交 .gitattributes
git add .gitattributes
git commit -m "Add Git LFS"
```

---

### 问题 3：忘记添加 .gitignore

**解决**：
```powershell
# 删除已跟踪的文件（但保留本地文件）
git rm -r --cached .

# 重新添加（会应用 .gitignore）
git add .

# 提交
git commit -m "Apply .gitignore"
```

---

## ?? 推荐的提交消息规范

### 格式

```
<type>: <subject>

<body>

<footer>
```

### Type 类型

- `feat`: 新功能
- `fix`: 修复 Bug
- `docs`: 文档更新
- `style`: 代码格式（不影响功能）
- `refactor`: 重构
- `test`: 测试相关
- `chore`: 构建/工具相关

### 示例

```
feat: 添加害羞表情和随机变体系统

- 新增 Shy 表情类型
- 实现随机表情变体选择
- 立绘和头像同步支持
- TTS 情感映射（gentle 风格）

Closes #123
```

---

## ? 完整推送检查清单

### 推送前

- [ ] 确保远程仓库已创建
- [ ] 检查 .gitignore 文件
- [ ] 检查是否有敏感信息（API 密钥等）
- [ ] 测试代码编译通过
- [ ] 编写有意义的提交消息

### 推送后

- [ ] 在远程平台验证文件
- [ ] 检查 README 显示正常
- [ ] 设置仓库描述和标签
- [ ] 添加许可证（License）
- [ ] 设置仓库主页

---

## ?? 下一步建议

1. **添加 README**：
   - 项目介绍
   - 功能列表
   - 安装指南
   - 使用说明

2. **添加许可证**：
   - MIT License（宽松）
   - GPL（开源）
   - 自定义许可证

3. **设置 CI/CD**：
   - GitHub Actions
   - GitLab CI
   - 自动化测试和部署

4. **项目管理**：
   - Issues（问题追踪）
   - Projects（项目看板）
   - Wiki（文档）

---

**准备好了就运行脚本吧！** ??

```powershell
.\Git-Init-And-Push.ps1
```

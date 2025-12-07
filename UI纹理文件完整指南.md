# ?? UI 纹理文件完整指南

## ?? 纹理文件放置位置

### 项目开发目录
```
C:\Users\Administrator\Desktop\rim mod\The Second Seat\Textures\UI\
├── NarratorButton_Ready.png           ← 就绪状态按钮（256x256）
├── NarratorButton_Processing.png      ← 处理中状态（256x256）
├── NarratorButton_Error.png           ← 错误状态（256x256）
├── NarratorButton_Disabled.png        ← 禁用状态（256x256）
│
├── Narrator\                          ← 状态面板纹理
│   ├── StatusPanel.png                ← 悬浮面板背景（512x512）
│   └── MainWindow.png                 ← 主窗口背景（1024x1024）
│
└── StatusIcons\                       ← 状态图标
    ├── Online.png                     ← 在线指示灯（64x64）
    ├── Offline.png                    ← 离线指示灯（64x64）
    ├── Sync.png                       ← 同步环形（128x128）
    ├── Link.png                       ← 连接齿轮（128x128）
    └── Error.png                      ← 错误标志（64x64）
```

### 游戏部署目录（自动复制）
```
D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Textures\UI\
（与开发目录结构相同）
```

---

## ?? 纹理文件规格总表

| 文件名 | 尺寸 | 用途 | 状态 | 动画 |
|--------|------|------|------|------|
| NarratorButton_Ready.png | 256x256 | 就绪按钮 | 灰白色 | 无 |
| NarratorButton_Processing.png | 256x256 | 处理中按钮 | 琥珀色 | 脉冲+旋转 |
| NarratorButton_Error.png | 256x256 | 错误按钮 | 红色 | 闪烁 |
| NarratorButton_Disabled.png | 256x256 | 禁用按钮 | 灰色半透明 | 无 |
| StatusPanel.png | 512x512 | 悬浮面板 | 深色半透明 | - |
| MainWindow.png | 1024x1024 | 主窗口 | 深色科技 | - |
| Online.png | 64x64 | 在线灯 | 绿色圆点 | - |
| Sync.png | 128x128 | 同步环 | 青色圆环 | - |
| Link.png | 128x128 | 连接齿轮 | 绿色齿轮 | - |
| Error.png | 64x64 | 错误标志 | 红色三角 | - |

---

## ?? 设计规范

### 颜色代码（RGB）

#### Ready（就绪）
```css
主色: rgb(204, 204, 204)  /* #CCCCCC 浅灰 */
发光: rgb(255, 255, 255)  /* #FFFFFF 白色（可选）*/
指示灯: rgb(0, 255, 136)    /* #00FF88 绿色 */
```

#### Processing（处理中）
```css
主色: rgb(255, 184, 77)   /* #FFB84D 琥珀色 */
发光: rgb(255, 204, 102)  /* #FFCC66 黄色 */
指示灯: rgb(255, 165, 0)    /* #FFA500 橙色 */
```

#### Error（错误）
```css
主色: rgb(255, 51, 51)    /* #FF3333 鲜红 */
发光: rgb(255, 102, 102)  /* #FF6666 红色发光 */
指示灯: rgb(255, 0, 0)      /* #FF0000 纯红 */
```

#### Disabled（禁用）
```css
主色: rgb(128, 128, 128)  /* #808080 灰色 */
透明度: 50% (Alpha 127)
指示灯: rgb(102, 102, 102)  /* #666666 暗灰 */
```

---

## ?? 设计模板（文字版）

### Ready 按钮设计
```
256x256 像素，PNG 格式

背景层：
- 深色半透明矩形（#1A1A1A, Alpha 90%）
- 圆角 16px

图标层：
- 中心圆形：直径 180px
- 电路纹路：浅灰色 #CCCCCC
- AI 符号或齿轮

指示灯（右上角）：
- 绿色圆点：16x16px
- 位置：(220, 20)
- 颜色：#00FF88
```

### Processing 按钮设计
```
256x256 像素，PNG 格式

背景层：
- 深色半透明矩形（#1A1A1A, Alpha 90%）
- 琥珀色发光边缘 #FFB84D

图标层：
- 中心圆形：直径 180px
- 电路纹路：琥珀色 #FFB84D
- 动态元素（齿轮、圆环）

发光层：
- 外发光效果：黄色 #FFCC66
- 模糊半径：8px

指示灯（右上角）：
- 橙色圆点：16x16px
- 位置：(220, 20)
- 颜色：#FFA500
```

### Error 按钮设计
```
256x256 像素，PNG 格式

背景层：
- 深色半透明矩形（#1A1A1A, Alpha 90%）
- 红色边框 #FF3333

图标层：
- 中心圆形：直径 180px
- 错误符号：X 或 ！
- 颜色：红色 #FF3333

指示灯（右上角）：
- 红色 X：16x16px
- 位置：(220, 20)
- 颜色：#FF0000
```

---

## ??? Photoshop/GIMP 快速设置

### Photoshop
```
1. 新建文档
   - 宽度：256 像素
   - 高度：256 像素
   - 分辨率：72 或 96 DPI
   - 颜色模式：RGB
   - 背景：透明

2. 创建图层
   - 图层 1：背景矩形
   - 图层 2：主图标
   - 图层 3：发光效果
   - 图层 4：指示灯

3. 导出设置
   - 格式：PNG-24
   - 透明度：是
   - 保存位置：Textures/UI/
```

### GIMP
```
1. 文件 → 新建
   - 宽度：256
   - 高度：256
   - 填充：透明

2. 图层操作
   - 添加图层 → 透明图层
   - 绘制主图标
   - 添加发光效果（滤镜 → 光照效果 → 发光）

3. 导出
   - 文件 → 导出为 PNG
   - 文件名：NarratorButton_Ready.png
   - 保存位置：Textures/UI/
```

---

## ?? 快速测试步骤

### 1. 添加单个纹理测试
```powershell
# 复制测试纹理
Copy-Item "你的设计文件.png" "Textures\UI\NarratorButton_Ready.png"

# 运行部署
.\一键部署.ps1

# 启动游戏测试
```

### 2. 验证纹理加载
```
启动游戏后：
1. 查看右上角按钮
2. 如果显示自定义图标 → 成功 ?
3. 如果显示默认图标 → 检查文件名和路径
```

### 3. 调试纹理问题
```
检查清单：
□ 文件名拼写正确
□ 文件格式为 PNG
□ 文件有 Alpha 通道
□ 文件尺寸正确（256x256）
□ 文件路径正确（Textures/UI/）
```

---

## ??? 在线工具推荐

### 免费设计工具
1. **Figma** (在线)
   - https://figma.com
   - 专业 UI 设计
   - 免费版功能完整

2. **Photopea** (在线)
   - https://photopea.com
   - 在线 Photoshop
   - 支持 PSD/PNG

3. **Pixlr** (在线)
   - https://pixlr.com
   - 简单易用
   - 快速编辑

### 图标资源
1. **Flaticon**
   - https://flaticon.com
   - 免费图标库
   - 可商用（需注明）

2. **Icon8**
   - https://icons8.com
   - 科技风格图标
   - 部分免费

3. **Game-icons.net**
   - https://game-icons.net
   - 游戏图标专用
   - 完全免费

---

## ?? 设计技巧

### 创建发光效果
```
Photoshop：
1. 复制图标图层
2. 滤镜 → 模糊 → 高斯模糊（半径 8px）
3. 图层混合模式 → 外发光
4. 不透明度 → 60%

GIMP：
1. 复制图层
2. 滤镜 → 模糊 → 高斯模糊（半径 8）
3. 图层模式 → 添加
4. 不透明度 → 60%
```

### 创建电路纹理
```
1. 使用钢笔工具绘制线条
2. 描边：1-2px
3. 颜色：根据状态选择
4. 添加圆点端点（科技感）
5. 可选：添加闪光效果
```

### 创建齿轮图标
```
1. 绘制外圆（直径 100px）
2. 绘制内圆（直径 60px）
3. 绘制 8 个齿（矩形）
4. 合并图层
5. 添加发光效果
```

---

## ?? 文件命名检查表

复制以下内容，创建你的纹理文件：

```
待创建文件清单：

□ NarratorButton_Ready.png          (256x256, 灰白色)
□ NarratorButton_Processing.png     (256x256, 琥珀色)
□ NarratorButton_Error.png          (256x256, 红色)
□ NarratorButton_Disabled.png       (256x256, 灰色半透明)

可选文件：
□ StatusPanel.png                   (512x512, 面板背景)
□ MainWindow.png                    (1024x1024, 窗口背景)
□ Online.png                        (64x64, 绿色圆点)
□ Sync.png                          (128x128, 青色圆环)
□ Link.png                          (128x128, 绿色齿轮)
□ Error.png                         (64x64, 红色三角)
```

---

## ?? 快速部署流程

### 一键添加和测试
```powershell
# 1. 复制你的纹理文件到 Textures/UI/
Copy-Item "你的文件夹\*.png" "Textures\UI\" -Force

# 2. 运行验证脚本
.\Verify-AnimationButton.ps1

# 3. 部署到游戏
.\一键部署.ps1

# 4. 启动游戏测试
# （手动启动 RimWorld）
```

---

## ?? 最小可用方案

### 只需 4 个按钮纹理
```
优先级：
1. NarratorButton_Ready.png       ← 必需
2. NarratorButton_Processing.png  ← 必需
3. NarratorButton_Error.png       ← 必需
4. NarratorButton_Disabled.png    ← 可选

其他纹理可以稍后添加！
```

### 临时占位符
```
如果暂时没有设计：
? 系统使用 RimWorld 内置图标
? 动画效果完全正常
? 添加纹理后自动替换
```

---

## ?? 获取帮助

### 纹理问题排查
```
问题：纹理不显示
检查：
1. 文件名是否完全匹配（区分大小写）
2. 文件格式是否为 PNG
3. 文件是否有透明通道
4. 文件路径是否正确
5. 查看 Player.log 错误信息
```

### 日志位置
```
RimWorld\Player.log
搜索关键词："ContentFinder" 或 "Texture"
```

---

## ?? 完成检查

完成以下步骤后，纹理系统即可使用：

- [ ] 创建 4 个按钮纹理文件
- [ ] 放入 `Textures/UI/` 文件夹
- [ ] 运行 `一键部署.ps1`
- [ ] 启动游戏验证
- [ ] 查看右上角按钮显示
- [ ] 测试 Processing 动画
- [ ] 测试 Error 状态显示

---

**创建时间**：2025-01-XX  
**适用版本**：The Second Seat v1.0+  
**难度**：??☆☆☆（中等）

?? **开始创作你的科技感 UI 纹理吧！** ??

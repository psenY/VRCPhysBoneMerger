# VRC PhysBone Merger (动骨合并与压缩工具)

[![VRChat](https://img.shields.io/badge/VRChat-Avatar%203.0-blue.svg)](https://vrchat.com)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

一个专为 VRChat Avatar 3.0 设计的高性能、**非破坏性 (Non-Destructive)** 动骨合并与优化工具。支持零风险严格匹配、智能碰撞体去重、实时性能等级预测及上传时极晚期自动构建。

---

## ?? 核心特性 (Key Features)

- ?? **非破坏性工作流 (Non-Destructive Workflow)**：
  - 挂载 `PhysBoneAutoMerger` 组件后，模型源文件、Prefabs 均保持 100% 原样不变。
  - 在点击 VRChat 上传或进入 Play 测试时，在内存临时副本中自动合并，上传后自动销毁标记，彻底杜绝 Missing Script 报错。
- ??? **极晚期执行与全框架兼容 (Order 999999)**：
  - 在 NDMF、Modular Avatar、VRCFury 和面捕框架（Triturbo FaceTracking 等）完全生成完动画层后才执行合并，杜绝动画参数失效与 NullReferenceException 崩溃。
- ?? **多级策略系统 (Strategy Presets)**：
  - **Strict (零风险严格策略 - 推荐)**：仅合并物理手感与属性完全一致的同层级动骨，绝不影响动效。
  - **Aggressive (激进策略)**：允许微小容差，大幅度减少动骨总数。
  - **Custom (自定义策略)**：支持自由调节数值容差、曲线容差、忽略旋转与端点等。
- ?? **性能等级实时预览 (Performance Rank Preview)**：
  - 实时分析模型当前与预测的动骨数量（PhysBone Components & Transforms & Colliders），直观展示 Very Poor -> Poor / Medium / Good 变化。
- ?? **碰撞体智能去重与冗余清理 (Collider Deduplication & Cleanup)**：
  - 自动去重合并后动骨列表中的重复碰撞体，智能清理被破坏或未生效的冗余引用。
- ?? **原生中英双语无缝切换 (Bilingual UI)**：
  - 支持在窗口与菜单栏随时一键切换简体中文与英文。

---

## ?? 安装方式 (Installation)

### 方式 1：通过 Unity Package Manager (UPM) 本地添加 (推荐)
1. 打开 Unity 顶部菜单：`Window` -> `Package Manager`。
2. 点击左上角的 `+` 号，选择 **"Add package from disk..."**。
3. 选中本插件根目录下的 `package.json` 即可完成安装。

### 方式 2：修改 Packages/manifest.json
在工程的 `Packages/manifest.json` 的 `dependencies` 中添加：
```json
"pseny7.vrc.physbone-merger": "file:C:/Users/psenY7/Downloads/VRCPhysBoneMerger"
```

---

## ?? 使用指南 (Usage)

### 1. 非破坏性自动合并（推荐）
1. 选中场景中的 Avatar 根节点。
2. 点击 Inspector 下方的 `Add Component` -> 搜索并添加 `PhysBone Auto Merger (动骨非破坏性自动合并组件)`。
3. 在策略下拉框中选择 **Strict (严格策略)** 或自定义策略。
4. 正常点击 VRChat SDK 的 **Build & Publish** 或进入 **Play 模式**，插件将自动完成合并，源文件完好无损！

### 2. 交互式可视化合并窗口
1. 点击 Unity 顶部菜单：`Tools` -> `VRC 动骨合并器 (PhysBone Merger)`。
2. 将模型拖入 Avatar 槽位。
3. 点击 **"扫描动骨层次结构"**，审查合并候选组及性能评分变化。
4. 点击 **"仅合并选中项"**，支持一键撤销 (Undo)。

---

## ?? 开源许可 (License)
本项目采用 [MIT License](LICENSE) 开源。


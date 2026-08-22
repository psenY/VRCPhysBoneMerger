# 🦴 VRCPhysBoneMerger

**VRChat 动骨非破坏性自动合并与性能优化工具 (Non-Destructive PhysBone Merger & Optimizer)**

[![Unity](https://img.shields.io/badge/Unity-2019.4+-black.svg?style=flat&logo=unity)](https://unity.com/)
[![VRChat](https://img.shields.io/badge/VRChat-Avatar%203.0-blue.svg?style=flat&logo=vrchat)](https://vrchat.com)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![VPM Listing](https://img.shields.io/badge/VPM-Repository-2ea44f?style=flat&logo=github)](https://pseny.github.io/vpm-repository/)
[![Release](https://img.shields.io/github/v/release/psenY/VRCPhysBoneMerger?color=orange&logo=github)](https://github.com/psenY/VRCPhysBoneMerger/releases)

---

## 📖 项目简介 (Introduction)

**VRCPhysBoneMerger** 是一款专为 VRChat Avatar 3.0 设计的高性能、**纯非破坏性 (Non-Destructive)** 动骨合并与性能优化工具。  
支持零风险严格匹配、智能碰撞体去重、实时性能等级预测及上传极晚期自动构建，帮助你在完全不破坏模型原始骨骼与动画的前提下大幅降低动骨开销，提升 Avatar 性能评级（Performance Rank）。

---

## 🌟 核心特性 (Key Features)

- 🔒 **非破坏性工作流 (Non-Destructive Workflow)**：
  - 挂载 `PhysBoneAutoMerger` 组件即可生效，源模型、Prefabs 均保持 100% 原始状态。
  - 在点击 VRChat 上传或进入 Play 测试时，在内存临时副本中自动合并，上传后自动销毁标记，彻底杜绝 Missing Script 报错。
- 🛡️ **极晚期执行与全框架兼容 (Order 999999)**：
  - 在 NDMF、Modular Avatar、VRCFury 和面捕框架（Triturbo FaceTracking 等）完全生成完动画层后才执行合并，杜绝动画参数失效与 NullReferenceException 崩溃。
- 🎯 **多级策略系统 (Strategy Presets)**：
  - **Strict (零风险严格策略 - 推荐)**：仅合并物理手感与属性完全一致的同层级动骨，绝不影响动效。
  - **Aggressive (激进策略)**：同父节点动骨批量合并，大幅减少动骨总数。
  - **Custom (自定义策略)**：支持自由调节数值容差、曲线容差、忽略旋转等。
- 📊 **性能等级实时预览 (Performance Rank Preview)**：
  - 实时分析模型当前与预测的动骨数量，直观展示 Very Poor -> Poor / Medium / Good 变化。
- 🧹 **碰撞体智能去重与冗余清理 (Collider Deduplication & Cleanup)**：
  - 自动去重合并后动骨列表中的重复碰撞体，智能清理被破坏或未生效的冗余引用。
- 🏷️ **Hierarchy 视图动骨数量实时显示 (Hierarchy PhysBone Badges)**：
  - 在 Unity Hierarchy 层级面板中直接为每个物件（头发、衣服、饰品、骨骼分支等）标注动骨数量徽章（如 `PB: 8` / `PB`），一眼掌握各个组件与子层级的动骨分布，支持随时一键开关。
- 🌐 **原生中英双语无缝切换 (Bilingual UI)**：
  - 支持在 Inspector 面板一键切换简体中文与英文。

---

## 🚀 安装方式 (Installation)

### 方式 1：通过 VCC / ALCOM 一键安装 (推荐 ⭐)
1. 打开 **[psenY7's VPM Listing](https://pseny.github.io/vpm-repository/)** 仓库主页。
2. 点击 **Add to VCC** 按钮一键导入，或在 VCC / ALCOM 设置中添加 VPM 仓库源：
   ```text
   https://pseny.github.io/vpm-repository/index.json
   ```
3. 在工程管理页面搜索 **VRC PhysBone Merger**，点击安装即可。

### 方式 2：通过 Unity Package Manager (UPM Git URL)
1. 打开 Unity，在顶部菜单栏选择 **Window** -> **Package Manager**。
2. 点击左上角的 **`+`** 按钮，选择 **`Add package from git URL...`**。
3. 粘贴仓库地址：
   ```text
   https://github.com/psenY/VRCPhysBoneMerger.git
   ```
4. 点击 **Add** 即可完成自动安装与后续一键更新。

### 方式 3：从本地磁盘安装 (UPM Disk / 源码复制)
- **UPM 本地包**：下载仓库后在 Package Manager 中选择 `Add package from disk...` 并指向 `package.json`。
- **源码导入**：将仓库中的 `Runtime` 与 `Editor` 文件夹直接复制到 Unity 项目的 `Assets/` 目录下。

---

## 📖 使用指南 (Usage)

1. **添加组件**：选中场景中的 Avatar 根节点，点击 Inspector 下方的 `Add Component` -> 搜索并添加 `PhysBone Auto Merger (动骨自动合并组件)`。
2. **选择策略**：在策略下拉框中推荐选择 **Strict (严格策略)**。
3. **一键上传 / 测试**：正常点击 VRChat SDK 的 **Build & Publish** 或进入 **Play 模式**，插件将在后台自动完成合并构建，源工程完好无损！

---

## 🔗 系列插件 (Related Tools)

- 🦴 **[VRCPhysBoneMerger](https://github.com/psenY/VRCPhysBoneMerger)**：专为 VRChat 设计的非破坏性动骨自动合并与优化组件。
- 📦 **[VRCPackageInspector](https://github.com/psenY/VRCPackageInspector)**：UnityPackage 极速免导入资源与动骨分析器。
- 🌐 **[psenY7's VPM Listing](https://pseny.github.io/vpm-repository/)**：VRChat 创作者插件与工具统一订阅源。

---

## 📄 开源许可 (License)

本项目基于 [GNU General Public License v3.0 (GPL-3.0)](LICENSE) 开源。欢迎提交 Issue 或 Pull Request！
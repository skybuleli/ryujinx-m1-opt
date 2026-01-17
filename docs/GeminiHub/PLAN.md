# Gemini Hub 实施规划指南

## 项目愿景
构建一个基于 Tauri 的 macOS 桌面伴侣应用，用于增强 Gemini CLI 的交互体验。它将作为 Gemini 的“第二大脑”，提供可视化的记忆管理、会话回溯和技能配置功能。

## 技术栈
- **核心架构**: Tauri (Rust + WebView)
- **前端框架**: Vue 3 + TypeScript
- **样式方案**: Tailwind CSS
- **通信机制**: Tauri Command / IPC
- **数据源**: 本地文件系统 (`~/.gemini/`)

## 阶段规划

### Phase 1: 基础设施 (Infrastructure) ✅
- [x] 初始化 Tauri 项目结构
- [x] 配置 Vue 3 和 Tailwind CSS
- [x] 解决 Rust 2024 版本兼容性问题
- [x] 实现 Rust 与前端的基础 IPC 通信 (Greet Test)
- [x] 解决 macOS App Bundle 图标编译问题

### Phase 2: 会话时光机 (Session Time Machine) 🚧
- **目标**: 实现无感的 CLI 会话记录与可视化的回溯浏览。
- **核心功能**:
    - [x] **Shell 钩子**: 拦截 `gemini` 命令并将输出流式传输/保存到日志文件。
    - [x] **日志读取**: Rust 后端扫描 `~/.gemini/hub/logs` 并解析会话。
    - [x] **Markdown 渲染**: 前端完美还原 CLI 的富文本输出（代码高亮、表格等）。
    - [ ] **UI 优化**: 提供美观、现代的阅读体验 (流式布局)。
    - [ ] **搜索/过滤**: 按时间、关键词搜索历史会话。

### Phase 3: 皮层管理器 (Cortex Manager)
- **目标**: 可视化管理长期记忆 (`GEMINI.md`)。
- **核心功能**:
    - [ ] **文件解析**: 将 Markdown 记忆文件解析为结构化数据 (JSON)。
    - [ ] **可视化编辑**: 提供增删改查记忆条目的 UI。
    - [ ] **冲突解决**: 处理 CLI 和 GUI 同时修改文件的并发情况。

### Phase 4: 扩展坞 (Extension Dock)
- **目标**: 管理 MCP 工具和技能配置。
- **核心功能**:
    - [ ] **技能开关**: 启用/禁用特定工具。
    - [ ] **配置表单**: 可视化编辑 `config.json`。
    - [ ] **市场 (可选)**: 浏览可用的扩展。

## 目录结构规范
```
GeminiHub/
├── src-tauri/       # Rust 后端
│   ├── src/
│   │   ├── main.rs  # 主逻辑与命令定义
│   │   └── lib.rs
│   └── tauri.conf.json
├── src/             # Vue 前端
│   ├── components/  # UI 组件
│   ├── assets/      # 静态资源
│   └── App.vue      # 主视图
└── ...
```

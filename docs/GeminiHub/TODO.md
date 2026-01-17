# Gemini Hub 开发任务清单

## 当前状态
- **阶段**: Phase 2 (UI Polishing)
- **最近更新**: 2026-01-16

## 待办事项 (Todo)

### Phase 1: 基础建设
- [x] 初始化项目 (Vue + Tauri)
- [x] 配置 Tailwind CSS
- [x] 修复 Rust 编译错误 (edition 2024)
- [x] 解决图标缺失导致的构建失败
- [x] 验证前端 <-> Rust 通信

### Phase 2: 会话时光机
#### 核心逻辑
- [x] 定义 Shell 函数 `gemini()` 用于记录日志
- [x] Rust: 实现 `get_sessions` (扫描目录)
- [x] Rust: 实现 `read_session` (读取内容)
- [x] Frontend: 集成 `markdown-it` 和 `highlight.js`

#### UI/UX 交互
- [x] 初步列表展示
- [x] 初步 Markdown 渲染
- [x] **[CRITICAL] UI 重构 (Amp Code Style)**: 列表页一比一复刻完成。
- [ ] **会话详情页深度美化**: 复刻 Amp 的对话流和工具使用展示。
- [ ] 优化滚动条样式
- [ ] 增加空状态提示

### Phase 3: 记忆管理 (Cortex)
- [ ] Rust: 读取 `GEMINI.md`
- [ ] Rust: 实现简单的记忆解析器 (Regex/AST)
- [ ] UI: 记忆卡片视图
- [ ] UI: 编辑/保存功能

### Phase 4: 设置与扩展
- [ ] 读取 `~/.gemini/config`
- [ ] 扩展列表展示

## 已知问题 (Bugs)
1. 浏览器环境无法调用 Tauri API (预期行为，已确认)
2. 当前深色主题对比度不佳，被评价为"丑陋" -> **正在修复**

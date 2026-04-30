# Requirements: SwitchPro

**Defined:** 2026-04-24
**Core Value:** 在 M1 8GB 内存设备上，以低于 4.5GB 的常驻内存实现稳定 30+ FPS 的主流游戏运行体验。

## v1 Requirements

### 内存优化

- [ ] **MEM-01**: 实现内存预算监控系统，实时追踪 App Resident Memory
- [ ] **MEM-02**: 常驻内存稳定在 4.5GB 以下（目标 3.5-4GB）
- [ ] **MEM-03**: Swap 使用量趋近于 0（避免内存交换导致的卡顿）
- [ ] **MEM-04**: Memoryless 深度/模板缓冲实现，减少显存占用 30%+
- [ ] **MEM-05**: 统一内存优化，减少 CPU-GPU 数据传输拷贝
- [ ] **MEM-06**: .NET GC 调优，减少长时间游戏过程中的 GC 卡顿

### 图形性能

- [ ] **GPU-01**: Metal 原生渲染管线替换 MoltenVK 间接层
- [ ] **GPU-02**: 利用 TBDR 架构优化渲染通路
- [ ] **GPU-03**: Shader 编译时间减少 50%
- [ ] **GPU-04**: Shader 编译缓存持久化，避免重复编译
- [ ] **GPU-05**: 分辨率缩放性能优化，1080p 稳定运行
- [ ] **GPU-06**: 帧率稳定性提升，1% Low FPS 提升 20%+

### CPU 性能

- [ ] **CPU-01**: ARMeilleure JIT 进一步优化 ARM64 翻译效率
- [ ] **CPU-02**: Profiled Persistent Translation Cache 增强，加载时间减少 40%+
- [ ] **CPU-03**: 热点路径零分配优化（Span/Memory/ArrayPool）
- [ ] **CPU-04**: 多线程调度优化，减少线程同步开销

### 工程工作流

- [ ] **QA-01**: 建立 BenchmarkDotNet 微基准测试套件
- [ ] **QA-02**: 实现自动化宏基准测试（运行 3000 帧自动截图对比）
- [ ] **QA-03**: 黄金镜像对比测试（像素级差异检测）
- [ ] **QA-04**: 性能回归测试 CI 流水线（每次提交自动运行）
- [ ] **QA-05**: CSV 日志输出 FPS、RAM、帧时间指标

### 开发者工具

- [ ] **DEV-01**: 开发者诊断看板（实时内存、帧率、ASTC 命中率）
- [ ] **DEV-02**: Metal Performance HUD 集成
- [ ] **DEV-03**: Tracy Profiler 深度分析支持
- [ ] **DEV-04**: 逐帧对比模式（基准版 vs 优化版左右分屏）
- [ ] **DEV-05**: Instruments 性能模板集成指南

### 用户体验

- [ ] **UX-01**: 启动时间优化（NativeAOT 编译准备）
- [ ] **UX-02**: 游戏库加载异步化，避免 UI 阻塞
- [ ] **UX-03**: 设置选项持久化和配置验证
- [ ] **UX-04**: 崩溃报告和诊断信息自动收集

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Hypervisor 虚拟化

- **HV-01**: 接入 macOS Hypervisor.framework
- **HV-02**: 消除 ARMeilleure JIT 翻译开销
- **HV-03**: CPU 负载降低 50%，能效提升

### NativeAOT 全编译

- **NAT-01**: 完整 NativeAOT 编译迁移
- **NAT-02**: 消除 .NET JIT 运行时内存占用
- **NAT-03**: 启动时间进一步降低

### 高级图形特性

- **GFX-01**: 光线追踪增强（Metal Ray Tracing）
- **GFX-02**: DLSS/FSR 3 帧生成支持
- **GFX-03**: 多显示器输出支持

## Out of Scope

| Feature | Reason |
|---------|--------|
| Windows/Linux 支持 | 专注 macOS Apple Silicon，放弃跨平台冗余 |
| x86_64 Mac 支持 | Apple 已停产 Intel Mac，资源集中在 ARM64 |
| 在线多人联机 | 网络协议模拟法律风险高，非核心体验 |
| Amiibo 硬件模拟 | 保持现有软件模拟方案，无需硬件 |
| Joy-Con 体感直连 | 依赖第三方驱动，保持现有 SDL2 方案 |
| Android/iOS 移植 | 完全不同的平台架构和输入方式 |
| 游戏 ROM 分发 | 严格遵守版权法律，不提供游戏来源 |
| 云端存档同步 | 需要后端基础设施，偏离性能优化核心目标 |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| MEM-01 | Phase 1 | Pending |
| MEM-02 | Phase 1 | Pending |
| MEM-03 | Phase 1 | Pending |
| MEM-04 | Phase 2 | Pending |
| MEM-05 | Phase 3 | Pending |
| MEM-06 | Phase 3 | Pending |
| GPU-01 | Phase 2 | Pending |
| GPU-02 | Phase 2 | Pending |
| GPU-03 | Phase 4 | Pending |
| GPU-04 | Phase 4 | Pending |
| GPU-05 | Phase 5 | Pending |
| GPU-06 | Phase 5 | Pending |
| CPU-01 | Phase 3 | Pending |
| CPU-02 | Phase 3 | Pending |
| CPU-03 | Phase 1 | Pending |
| CPU-04 | Phase 5 | Pending |
| QA-01 | Phase 1 | Pending |
| QA-02 | Phase 6 | Pending |
| QA-03 | Phase 6 | Pending |
| QA-04 | Phase 6 | Pending |
| QA-05 | Phase 6 | Pending |
| DEV-01 | Phase 7 | Pending |
| DEV-02 | Phase 7 | Pending |
| DEV-03 | Phase 7 | Pending |
| DEV-04 | Phase 7 | Pending |
| DEV-05 | Phase 7 | Pending |
| UX-01 | Phase 8 | Pending |
| UX-02 | Phase 8 | Pending |
| UX-03 | Phase 8 | Pending |
| UX-04 | Phase 8 | Pending |

**Coverage:**
- v1 requirements: 28 total
- Mapped to phases: 28
- Unmapped: 0 ✓

---
*Requirements defined: 2026-04-24*
*Last updated: 2026-04-24 after initial definition*

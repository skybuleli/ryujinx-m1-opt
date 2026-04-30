# Roadmap: SwitchPro

**Created:** 2026-04-24
**Phases:** 8 | **Requirements:** 28 mapped | **Granularity:** Fine

## Overview

| # | Phase | Goal | Requirements | Success Criteria |
|---|-------|------|--------------|------------------|
| 1 | 内存止血 | 建立内存监控，消除基础分配开销 | MEM-01~03, CPU-03, QA-01 | 5 |
| 2 | Metal 基础 | 原生 Metal 管线替换 MoltenVK | MEM-04, GPU-01~02 | 3 |
| 3 | CPU 与 GC 优化 | JIT 效率提升，GC 调优 | CPU-01~02, MEM-05~06 | 4 |
| 4 | Shader 优化 | 编译缓存和着色器性能 | GPU-03~04 | 2 |
| 5 | 帧率稳定 | 分辨率缩放和多线程优化 | GPU-05~06, CPU-04 | 3 |
| 6 | 基准测试体系 | 自动化回归测试和黄金镜像 | QA-02~05 | 4 |
| 7 | 开发者工具 | 诊断看板和逐帧分析 | DEV-01~05 | 5 |
| 8 | 体验打磨 | 启动优化和 UX 完善 | UX-01~04 | 4 |

---

## Phase 1: 内存止血 (Stop the Bleeding)

**Goal:** 建立全面的内存监控体系，消除热点路径的堆分配，确保基础内存占用达标。

**Requirements:** MEM-01, MEM-02, MEM-03, CPU-03, QA-01

**Status:** ✅ Complete | All 4 plans completed (2026-04-24)

**Success Criteria:**
1. 内存监控系统可实时显示 App Resident Memory（精度 ±10MB）
2. 空闲状态下内存占用 < 2GB
3. 运行《塞尔达传说：荒野之息》30 分钟后内存 < 4.5GB，Swap = 0
4. 热点解码路径（BCnDecoder, ASTCDecoder）零堆分配
5. BenchmarkDotNet 微基准套件可运行并输出基准数据

**Key Tasks:**
- ✅ 添加运行时内存统计 API（RSS, Swap, GC Heap）— Plan 01 complete
- ✅ 实现内存预算管理器（Memory Budget Manager）— Plan 03 complete
- ✅ 集成 `Microsoft.IO.RecyclableMemoryStream` 全面替换 `MemoryStream` — Plan 02 complete
- ✅ 在 `Ryujinx.Graphics.Texture` 中消除 AstcDecoder 和 LayoutConverter 堆分配 — Plan 02 complete
- ✅ 创建 BCnDecoder / ASTCDecoder / ETC2Decoder / MemoryBlock 的 BenchmarkDotNet 基准测试 — Plan 04 complete

**Duration Estimate:** 2-3 weeks
**Dependencies:** None

---

## Phase 2: Metal 基础 (Metal Foundation)

**Goal:** 构建原生 Metal 渲染管线，替换 MoltenVK 间接层，利用 Apple Silicon GPU 架构优势。

**Requirements:** MEM-04, GPU-01, GPU-02

**Success Criteria:**
1. Metal 后端可渲染至少 3 款主流游戏画面正确
2. 相比 MoltenVK 后端，GPU 时间降低 20%+
3. Depth/Stencil Buffer 使用 Memoryless 存储，显存占用降低 30%+
4. 通过黄金镜像测试（与 MoltenVK 版本像素级一致）

**Key Tasks:**
- 扩展 `Ryujinx.Graphics.Metal.SharpMetalExtensions` 为完整 Metal 后端
- 实现 Metal Command Buffer、Render Pipeline State 管理
- 适配 Switch Maxwell GPU 特性到 Metal API（Compute Shader, Tessellation）
- 实现 Memoryless Render Target（Depth/Stencil）
- 性能对比：Metal vs MoltenVK vs OpenGL

**Duration Estimate:** 3-4 weeks
**Dependencies:** Phase 1

---

## Phase 3: CPU 与 GC 优化 (CPU & GC Tuning)

**Goal:** 提升 JIT 翻译效率，优化 .NET GC 行为，减少 CPU 瓶颈和 GC 卡顿。

**Requirements:** CPU-01, CPU-02, MEM-05, MEM-06

**Success Criteria:**
1. ARMeilleure JIT 翻译吞吐量提升 15%+
2. Profiled Persistent Translation Cache 命中率 > 95%
3. 游戏加载时间（标题画面）减少 40%+
4. GC 暂停时间 < 5ms（99th percentile）
5. CPU-GPU 数据传输减少 50%（统一内存优化）

**Key Tasks:**
- ARMeilleure IR 优化和 ARM64 代码生成改进
- Translation Cache 持久化格式优化（压缩/快速加载）
- .NET GC 配置调优（Server GC, Latency Mode, LOH 压缩）
- 实现统一内存缓冲区（避免 CPU-GPU 显式拷贝）
- GC 压力测试和内存泄漏检测

**Duration Estimate:** 2-3 weeks
**Dependencies:** Phase 1

---

## Phase 4: Shader 优化 (Shader Optimization)

**Goal:** 减少 Shader 编译时间和内存开销，实现持久化缓存避免重复编译。

**Requirements:** GPU-03, GPU-04

**Success Criteria:**
1. Shader 编译时间（首次启动游戏）减少 50%+
2. Shader 缓存持久化到磁盘，二次启动编译时间 < 10% 首次
3. Shader 缓存大小压缩 30%+
4. 无 Shader 编译导致的帧率骤降（stall）

**Key Tasks:**
- 优化 `Ryujinx.Graphics.Shader` 翻译器性能
- 实现磁盘 Shader Cache（二进制序列化，快速加载）
- Shader 缓存压缩（LZ4 或 ZSTD）
- 异步 Shader 编译管线（避免阻塞渲染线程）
- 缓存失效和版本管理策略

**Duration Estimate:** 2 weeks
**Dependencies:** Phase 2

---

## Phase 5: 帧率稳定 (Frame Rate Stability)

**Goal:** 优化分辨率缩放和多线程调度，实现稳定的帧率输出。

**Requirements:** GPU-05, GPU-06, CPU-04

**Success Criteria:**
1. 1080p 分辨率下主流游戏稳定 30 FPS+
2. 1% Low FPS 相比基线提升 20%+
3. 帧时间抖动（Frame Time Variance）< 10%
4. 多线程 CPU 模拟负载均衡（所有核心利用率 > 70%）

**Key Tasks:**
- 分辨率缩放算法优化（Bicubic/FSR）
- 多线程 JIT 编译调度优化
- 渲染线程与模拟线程同步优化
- 帧率限制器和自适应 VSync
- 热点游戏帧率剖析和针对性优化

**Duration Estimate:** 2-3 weeks
**Dependencies:** Phase 3, Phase 4

---

## Phase 6: 基准测试体系 (Benchmarking Infrastructure)

**Goal:** 建立完整的自动化性能回归测试体系，确保每次优化可衡量、不引入回归。

**Requirements:** QA-02, QA-03, QA-04, QA-05

**Success Criteria:**
1. 自动化基准测试脚本 `run_benchmark.py` 可一键运行
2. 每次 PR 自动运行基准测试并生成报告
3. 黄金镜像对比差异分值 = 0（像素级一致）
4. CSV 日志包含 FPS、RAM、帧时间、Shader 编译时间
5. 性能衰退自动告警（阈值：FPS 下降 > 3%，内存增加 > 5%）

**Key Tasks:**
- 开发 `run_benchmark.py` 测试驱动脚本
- 集成 Headless 模式自动截图（特定帧）
- ImageMagick 像素对比流水线
- GitHub Actions / CI 基准测试工作流
- 性能数据存储和历史趋势分析

**Duration Estimate:** 2 weeks
**Dependencies:** Phase 1

---

## Phase 7: 开发者工具 (Developer Tooling)

**Goal:** 构建开发者诊断和调试工具集，便于性能分析和优化验证。

**Requirements:** DEV-01, DEV-02, DEV-03, DEV-04, DEV-05

**Success Criteria:**
1. 模拟器内开发者看板显示实时内存、帧率、ASTC 命中率
2. Metal Performance HUD 可通过快捷键启用
3. Tracy Profiler 可捕获完整帧的 CPU/GPU 时间线
4. 逐帧对比模式可左右分屏显示基准版和优化版
5. Instruments 性能分析模板文档化

**Key Tasks:**
- 在 Avalonia UI 中集成开发者看板（Overlay 或侧边栏）
- Metal Performance HUD 快捷键绑定
- Tracy Profiler C# 集成（Zone 标记）
- 逐帧对比模式 UI（双窗口同步渲染）
- Instruments 模板和文档编写

**Duration Estimate:** 2 weeks
**Dependencies:** Phase 2, Phase 5, Phase 6

---

## Phase 8: 体验打磨 (UX Polish)

**Goal:** 优化启动时间和用户界面体验，完善错误处理和诊断。

**Requirements:** UX-01, UX-02, UX-03, UX-04

**Success Criteria:**
1. 启动时间（点击图标到主界面）< 3 秒
2. 游戏库 1000+ 游戏加载时间 < 5 秒
3. 设置更改即时生效，无需重启
4. 崩溃时自动收集诊断日志并提示上报

**Key Tasks:**
- NativeAOT 编译准备（修剪配置、反射替代方案）
- 游戏库异步加载和虚拟化列表
- 设置系统重构（热重载，验证）
- 崩溃处理（未捕获异常、信号处理）
- 诊断日志自动打包

**Duration Estimate:** 2 weeks
**Dependencies:** Phase 3, Phase 7

---

## Parallelization Plan

**可并行执行的阶段组：**

- **Wave 1**: Phase 1（内存）+ Phase 6（基准测试）— 基准测试不依赖内存优化完成
- **Wave 2**: Phase 2（Metal）+ Phase 3（CPU/GC）— 两者依赖 Phase 1，但互不依赖
- **Wave 3**: Phase 4（Shader）+ Phase 5（帧率）— 依赖 Wave 2
- **Wave 4**: Phase 7（开发者工具）+ Phase 8（UX）— 依赖 Wave 3

**关键路径**: Phase 1 → Phase 2/3 → Phase 4/5 → Phase 7/8

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Metal 后端开发复杂度超预期 | 高 | 保留 MoltenVK 作为 fallback，分阶段替换 |
| NativeAOT 兼容性问题 | 中 | 渐进式迁移，保留 JIT 编译路径 |
| 游戏兼容性回归 | 高 | 黄金镜像测试，CI 自动化验证 |
| 8GB 内存硬限制无法达标 | 高 | 早期原型验证，必要时放宽到 5GB |
| Apple API 变更（Metal/Hypervisor） | 低 | 紧跟 WWDC，使用稳定 API |

---

## Milestones

### Milestone 1: Alpha（Phase 1-3 完成）
- 内存 < 4.5GB，Metal 基础可用，加载时间优化
- 目标日期：+7 weeks

### Milestone 2: Beta（Phase 4-6 完成）
- 帧率稳定，Shader 缓存，自动化测试上线
- 目标日期：+12 weeks

### Milestone 3: Release（Phase 7-8 完成）
- 开发者工具齐全，UX 打磨完成
- 目标日期：+16 weeks

---
*Roadmap created: 2026-04-24*
*Last updated: 2026-04-24 after Plan 04 completion*

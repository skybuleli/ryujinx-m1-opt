# SwitchPro

## What This Is

SwitchPro 是一款专为 macOS Apple Silicon（M1/M2/M3）平台打造的 Nintendo Switch 模拟器。基于 Ryujinx 深度定制，彻底放弃跨平台冗余，专注于在 8GB 内存设备上实现极致的低内存占用和高帧率性能。通过原生 Metal 后端、.NET 现代内存原语和硬件加速，为 Mac 用户提供流畅、优雅的 Switch 游戏体验。

## Core Value

在 M1 8GB 内存设备上，以低于 4.5GB 的常驻内存实现稳定 30+ FPS 的主流游戏运行体验。

## Requirements

### Validated

- ✓ **.NET 10 运行时升级** — 利用最新 ARM64 NEON 硬件内联函数
- ✓ **ASTC 硬件纹理透传** — Apple Silicon GPU 原生支持，跳过软解压
- ✓ **ARM64 硬件加速优化** — AdvSimd 解码器、ARM64 Intrinsics 位操作
- ✓ **Weight-based 显存驱逐策略** — 智能纹理缓存管理
- ✓ **M1 Fast Math 支持** — 浮点运算性能优化
- ✓ **Metal 扩展框架** — SharpMetalExtensions 原生 Metal 接入层
- ✓ **多图形后端支持** — OpenGL / Vulkan(MoltenVK) / Metal
- ✓ **跨平台音频后端** — OpenAL / SDL2 / SoundIo
- ✓ **基准测试框架** — BenchmarkDotNet 性能基准
- ✓ **无头测试模式** — Ryujinx.Headless.SDL2 自动化测试支持

### Active

- [ ] **内存预算系统** — 实时监控和限制内存使用，确保 < 4.5GB
- [ ] **Metal 原生渲染管线** — 替换 MoltenVK 间接层，直接 Metal 驱动
- [ ] **Memoryless 深度/模板缓冲** — 利用 TBDR 架构减少显存占用
- [ ] **Shader 编译缓存优化** — 减少着色器编译时间和内存开销
- [ ] **自动化基准测试体系** — 性能回归测试、黄金镜像对比
- [ ] **开发者诊断看板** — 实时内存、帧率、ASTC 命中率监控
- [ ] **逐帧对比模式** — 基准版与优化版左右分屏对比
- [ ] **Profiled Persistent Translation Cache 增强** — 进一步减少加载时间
- [ ] **统一内存优化** — 利用 Apple Silicon 统一内存架构减少拷贝

### Out of Scope

- **Windows/Linux 支持** — 专注 macOS Apple Silicon，放弃跨平台维护成本
- **x86_64 Mac 支持** — 仅支持 Apple Silicon (ARM64)
- **在线多人联机** — 网络协议模拟非核心目标，法律风险高
- **Amiibo 硬件模拟** — 保持现有软件模拟方案
- **Joy-Con 体感直连** — 依赖第三方驱动，保持现有方案
- **Android/iOS 移植** — 完全不同的平台架构

## Context

### 技术环境

- **平台**: macOS 14+ / Apple Silicon (M1/M2/M3)
- **运行时**: .NET 10 (已升级)
- **UI 框架**: Avalonia 11.0.13 (保留现有)
- **图形 API**: Metal (原生) / Vulkan via MoltenVK / OpenGL
- **CPU 模拟**: ARMeilleure JIT (ARM64 → x86_64，计划转向虚拟化)
- **音频**: OpenAL / SDL2 / libsoundio
- **构建系统**: MSBuild / Directory.Packages.props 中央包管理

### 现有优化基础

项目基于 `ryujinx-m1-opt` 私有仓库，已实施的 M1 特定优化：

1. **ASTC 透传**: 直接将 Switch ASTC 纹理数据传给 Metal，跳过 RGBA8888 解压
2. **NEON 加速**: BCnDecoder 和位操作使用 ARM64 硬件指令
3. **内存驱逐**: 基于权重的显存纹理缓存管理
4. **Fast Math**: 重新启用 M1 特有的快速数学运算路径

### 核心矛盾

**"有限内存与膨胀纹理"** — 8GB M1 设备在运行大型游戏时，ASTC 纹理软解压会导致内存暴涨并触发 Swap，造成严重卡顿。现有 ASTC 透传已解决此问题的大部分，但仍有优化空间。

### 已知问题

- MoltenVK 作为 Vulkan-to-Metal 翻译层存在性能损耗
- .NET GC 在长时间游戏过程中可能产生卡顿
- Shader 编译缓存机制不够激进，重复编译浪费资源
- 缺乏系统性的性能回归测试防护网

## Constraints

- **内存**: 目标常驻内存 < 4.5GB（8GB 设备的可用内存上限）
- **性能**: 主流游戏稳定 30+ FPS（Switch 原生帧率）
- **兼容性**: 保持与 Ryujinx 游戏兼容性基线一致
- **法律**: 严格遵守 MIT 许可证，不包含任天堂专有代码/密钥
- **平台**: macOS 14+，仅 Apple Silicon（放弃 Intel Mac）
- **时间**: 采用迭代式优化，每个阶段必须有可量化的性能提升

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| 基于 Ryujinx 深度定制而非从零开发 | 利用成熟代码库，降低开发周期和风险 | ✓ Good — 已有大量 M1 优化基础 |
| 保留 Avalonia UI 框架 | 避免重写 UI 的工程开销，专注后端优化 | — Pending |
| 深度 .NET 内存优化 + Metal 后端 | 充分利用 .NET 现代原语和 Apple Silicon GPU 架构 | — Pending |
| 性能基准回归测试优先 | 确保每次优化都可衡量、可验证、不引入回归 | — Pending |
| 放弃跨平台支持 | 集中资源在 macOS M1 极致优化，避免平台兼容代码膨胀 | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-24 after initialization*

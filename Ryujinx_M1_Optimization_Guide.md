# Ryujinx (M1/Apple Silicon) 极致优化与质量验收工程指南

**版本**: 1.0 (2026-01-16)
**目标设备**: MacBook Air/Pro (M1/M2/M3 系列) - **重点针对 8GB 内存版本**
**核心原则**: 专注于 macOS/ARM 平台原生能力，彻底放弃跨平台冗余，数据驱动优化。

---

## 1. 核心优化战略 (Technical Pillars)

### 1.1 CPU 执行层：从 JIT 转向虚拟化 (Hypervisor)
*   **技术目标**: 消除 ARMeilleure (JIT) 的翻译开销。
*   **实施方案**: 利用 macOS 的 `Hypervisor.framework`。由于宿主与模拟对象均为 ARMv8 架构，可将 Switch 指令直接映射到 M1 的虚拟化核心运行。
*   **预期收益**: CPU 负载降低 ~50%，大幅提升能效，减少热降频。

### 1.2 图形与内存：8GB 设备的救赎 (ASTC Passthrough)
*   **技术目标**: 消除纹理软解压导致的内存暴涨。
*   **实施方案**: 
    *   **ASTC 原生透传**: Apple Silicon GPU 硬件支持 ASTC 格式。修改显存管理器，直接将 Switch 的 ASTC 纹理数据传给 Metal/Vulkan，跳过解压至 RGBA8888 的步骤。
    *   **Memoryless 资源**: 利用 TBDR 架构，将 Depth/Stencil Buffer 标记为 `Memoryless`，使其仅存在于 GPU 片上缓存，不占用物理 RAM。
*   **预期收益**: 显存/系统内存占用降低 ~60%，彻底解决 8GB 内存设备频繁触发 Swap 的卡顿。

### 1.3 运行时与工具链：.NET 9 + NativeAOT
*   **技术目标**: 消除 .NET 运行时自身的开销。
*   **实施方案**: 
    *   升级至 **.NET 9**，利用针对 ARM64 NEON 的最新硬件内联函数 (Hardware Intrinsics)。
    *   开启 **NativeAOT** 编译，将模拟器编译为纯原生二进制文件，消除 JIT 引擎内存占用。

---

## 2. 实施路线图 (Implementation Phases)

| 阶段 | 重点任务 | 复杂度 | 优先级 |
| :--- | :--- | :--- | :--- |
| **阶段 1 (止血)** | .NET 9 升级、ASTC 硬件透传、MoltenVK 性能参数调优 | 中 | P0 |
| **阶段 2 (流畅)** | 引入 Vulkan Shader Object 扩展、内存预算流式加载 | 高 | P1 |
| **阶段 3 (换心)** | 接入 Hypervisor.framework、NativeAOT 迁移 | 极高 | P2 |

---

## 3. 验收与量化体系 (QA & Metrics)

为了确保“小步快跑”且不引入回归 Bug，必须通过量化数据验收每一项改动。

### 3.1 核心量化指标 (KPI)

| 维度 | 指标 | 工具 | 目标值 |
| :--- | :--- | :--- | :--- |
| **性能** | 平均 FPS | Ryujinx OSD | 提升 >5% |
| **稳定性** | 1% Low FPS (掉帧感) | Tracy Profiler | 提升 >20% |
| **内存** | App Resident Memory | 活动监视器 / Instruments | **< 4.5 GB** |
| **交换** | Swap Used (交换内存) | 活动监视器 | **趋近于 0 GB** |
| **响应** | Shader 编译时间 | 日志 (Timestamp) | 减少 50% |

### 3.2 验收工具链
1.  **Metal Performance HUD**: 环境变量 `MTL_HUD_ENABLED=1`。实时监控显存分配和 GPU 吞吐量。
2.  **macOS Instruments**: 使用 "Game Performance" 模板分析每一帧的 CPU/GPU 时间分布。
3.  **Tracy Profiler**: 深度分析 C# 函数调用栈和线程同步阻塞。

---

## 4. 自动化回归测试 (Regression Testing)

防止优化导致图形 Bug 或模拟不准（Regression）。

### 4.1 黄金镜像对比 (Golden Image Test)
*   **流程**: 
    1. 在基准版本 (Baseline) 在特定帧 (如第 2000 帧) 自动截图并保存。
    2. 在优化版本运行同样的序列并截图。
    3. 利用像素对比工具 (ImageMagick `compare`) 计算差异。
*   **验收**: 差异分值必须为 0。

### 4.2 自动化基准测试脚本
编写测试驱动脚本 `run_benchmark.py`，实现闭环验证：

```python
import os, subprocess

def run_test(branch_name, game_id):
    # 1. 编译并运行无头模式
    # --headless: 无窗口渲染
    # --frames 3000: 运行3000帧后退出
    # --screenshot-at 3000: 自动截图
    cmd = f"./Ryujinx.Headless --frames 3000 --screenshot-at 3000 --log-csv {branch_name}.csv {game_id}"
    subprocess.run(cmd, shell=True)

def validate():
    # 2. 对比 CSV 日志中的 FPS 和 RAM 指标
    # 3. 对比黄金图片差异
    # 4. 输出: [OPTIMIZED] 或 [REGRESSED] 或 [BROKEN]
    pass
```

---

## 5. 调试与演示方案

*   **侧边栏看板**: 在模拟器中增加一个“开发者看板”，实时显示当前内存使用、ASTC 命中率、vCPU 状态。
*   **逐帧对比模式**: 提供左右分屏模式，左侧运行 Baseline，右侧运行优化版，直观演示画面一致性和帧率提升。

---

## 总结
针对 8GB M1 设备的优化，核心矛盾是 **“有限内存与膨胀纹理”** 的斗争。通过 **ASTC 透传** 解决生存问题，通过 **Hypervisor** 解决性能问题，并通过 **自动化基准测试** 确保每一步改动都稳健前进。

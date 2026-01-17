# Ryujinx 二次开发与性能优化指南

## 1. 架构概览

Ryujinx 采用高度模块化的架构，每个核心硬件组件都有独立的项目（Project）进行模拟。这种设计使得各个子系统的开发和测试可以相对独立地进行。

### 核心模块

*   **CPU (ARMeilleure & Ryujinx.Cpu)**:
    *   **ARMeilleure**: 自研的动态二进制翻译器（JIT），负责将 ARM64 指令转换为中间表示（IR），经过优化后再编译为宿主机器代码（x86_64 或 ARM64）。
    *   **Ryujinx.Cpu**: 定义了 CPU 执行引擎的抽象接口。
*   **GPU (Ryujinx.Graphics.Gpu & Ryujinx.Graphics.GAL)**:
    *   **Ryujinx.Graphics.Gpu**: 处理 GPU 的高层逻辑，包括宏代码执行（Macro execution）、状态跟踪、内存管理等。
    *   **Ryujinx.Graphics.GAL**: 图形抽象层（Graphics Abstraction Layer），定义了统一的图形接口，屏蔽了底层图形 API（OpenGL, Vulkan）的差异。
*   **HLE (Ryujinx.HLE)**:
    *   **Horizon OS**: 模拟 Switch 的操作系统内核、进程管理、线程调度。
    *   **Services**: 实现了大量的系统服务（Services），这是模拟器兼容性的关键部分。
*   **Memory (Ryujinx.Memory)**:
    *   处理复杂的内存映射，利用页表（PageTable）实现快速的虚拟地址到物理地址的转换。
*   **Audio (Ryujinx.Audio)**:
    *   实现音频渲染器（Audio Renderer）及多种后端（OpenAL, SDL2, SoundIO）。

---

## 2. 二次开发指南

### 2.1 添加或修改 HLE 服务 (Services)

Switch 的大部分功能通过 IPC（进程间通信）调用系统服务来实现。

*   **位置**: `src/Ryujinx.HLE/HOS/Services`
*   **开发流程**:
    1.  确定目标服务所在的模块（例如 `Audio`, `Nv`, `Vi` 等）。
    2.  在相应目录下找到或创建服务类。
    3.  实现服务接口。通常需要参考 Switch 的逆向工程文档（如 SwitchBrew）。
    4.  在 `src/Ryujinx.HLE/HOS/Horizon.cs` 或相应的服务工厂中注册新服务。

### 2.2 修改 CPU 指令行为

如果发现 CPU 模拟有误或需要添加新指令支持：

*   **指令解码与实现**: `src/ARMeilleure/Instructions`
*   **指令表**: `src/ARMeilleure/Decoders`
*   **开发流程**:
    1.  找到对应的指令操作码定义。
    2.  在 `Instructions` 目录下实现该指令的逻辑（生成对应的 IR 代码）。
    3.  `Translator.Translate` 是翻译过程的入口，可在此处打断点调试。

### 2.3 图形层修改 (GPU)

*   **逻辑修改**: `src/Ryujinx.Graphics.Gpu`
    *   **命令处理**: `ClassId` 对应的处理方法通常在 `Engine` 目录下。
*   **后端修改**:
    *   **Vulkan**: `src/Ryujinx.Graphics.Vulkan`
    *   **OpenGL**: `src/Ryujinx.Graphics.OpenGL`
    *   如果需要优化渲染性能或修复图形错误，通常需要深入这些后端实现。

### 2.4 用户界面 (UI)

*   **位置**: `src/Ryujinx/UI`
*   Ryujinx 目前主要使用 Avalonia（跨平台）或 GTK 作为 UI 框架。修改界面布局或添加菜单项需在此处进行。

---

## 3. 性能优化指南

### 3.1 JIT 优化 (ARMeilleure)

这是性能优化的核心区域。

*   **中间表示 (IR) 优化**: `src/ARMeilleure/Optimizations.cs`
    *   **策略**: 添加新的优化 Pass（如死代码消除、常量折叠的增强版）。
*   **寄存器分配**: 优化寄存器分配算法可以显著减少内存访问，提高生成的机器码质量。
*   **关键入口**: `Translator.Translate`

### 3.2 GPU 性能优化

*   **着色器编译**:
    *   着色器转换（SPIR-V / GLSL）是主要的卡顿来源。
    *   **优化方向**: 改进着色器缓存机制，或者优化 `Ryujinx.Graphics.Shader` 中的翻译逻辑，减少生成的代码量。
*   **后端开销**:
    *   **Vulkan**: 减少 Pipeline Barrier 的使用，优化 Descriptor Set 的更新频率。
    *   **纹理同步**: 减少 CPU-GPU 之间的纹理数据同步操作。

### 3.3 内存访问优化

*   **位置**: `src/Ryujinx.Memory`
*   **策略**:
    *   Ryujinx 使用软件页表。优化页表查找（Page Table Walk）的路径可以带来全局性能提升。
    *   JIT 代码中会内联内存访问检查，优化这部分生成的汇编代码（Fast Path）至关重要。

### 3.4 多线程与同步

*   **位置**: `src/Ryujinx.HLE/HOS/Kernel`
*   **策略**:
    *   Switch 游戏严重依赖同步原语。优化 `KEvent`, `KThread` 等内核对象的实现，减少宿主机的上下文切换开销。

---

## 4. 关键文件索引

| 模块 | 关键文件路径 | 描述 |
| :--- | :--- | :--- |
| **JIT** | `src/ARMeilleure/Translation/Translator.cs` | JIT 翻译入口 |
| **JIT** | `src/ARMeilleure/Optimizations.cs` | IR 优化 Pass 定义 |
| **CPU** | `src/Ryujinx.Cpu/ICpuEngine.cs` | CPU 引擎接口 |
| **GPU** | `src/Ryujinx.Graphics.Gpu/GpuContext.cs` | GPU 上下文及状态管理 |
| **HLE** | `src/Ryujinx.HLE/HOS/Horizon.cs` | 操作系统初始化与服务入口 |
| **Memory** | `src/Ryujinx.Memory/IVirtualMemoryManager.cs` | 虚拟内存管理接口 |
| **Audio** | `src/Ryujinx.Audio/Renderer/AudioRenderer.cs` | 音频渲染核心 |


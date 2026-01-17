# Ryujinx M1 优化行动清单 (M1 Optimization Action Plan)

基于 `Ryujinx_M1_Optimization_Guide.md` 版本 1.0 (2026-01-16)

## 📊 核心指标看板 (KPIs)
- [ ] 平均 FPS 提升 > 5%
- [ ] 1% Low FPS 提升 > 20%
- [ ] App Resident Memory < 4.5 GB (针对 8GB 设备)
- [ ] Swap Used 趋近于 0 GB
- [ ] Shader 编译时间减少 50%

---

## 🏗 Phase 1: 止血 (Stop the Bleeding) - P0
**目标**: 解决 8GB 内存设备崩溃与卡顿，升级基础架构。

### 1.1 .NET 10 升级 ✅
- [x] **环境准备**
    - [x] 修改 `global.json` 升级 SDK 版本至 .NET 10。
    - [x] 遍历所有 `.csproj` 文件，将 `<TargetFramework>` 更新为 `net10.0` (已通过 Directory.Build.props 统一处理)。
    - [x] 更新 `Directory.Packages.props` 或 `nuget.config` 确保依赖兼容。
- [x] **编译验证**
    - [x] 执行 `dotnet clean && dotnet build` 确保无错误。
    - [x] 修复因升级导致的 API 弃用或破坏性变更 (Breaking Changes)。
- [x] **硬件内联优化 (Intrinsics)**
    - [x] 扫描 `Ryujinx.Cpu` 和 `Ryujinx.Graphics` 模块，识别可使用 .NET 10 ARM64 Intrinsics 的热点代码。
    - [x] 替换旧的 Vector 运算为 .NET 10 新指令集实现 (以 `Ryujinx.Common.BitUtils.ReverseBits64` 为例完成验证与落地)。
    - [x] 优化纹理软解压 (BCnDecoder) 使用 ARM64 AdvSimd 指令集 (BC1 +22%, BC3 +4%)。

### 1.2 ASTC 硬件透传 (ASTC Passthrough)
- [ ] **功能原型**
    - [ ] 定位纹理加载与解码逻辑 (`Ryujinx.Graphics.Texture`).
    - [ ] 实现检测宿主 GPU 是否支持 ASTC 的逻辑 (在 macOS/Metal 下应为 True)。
    - [ ] 在 `Ryujinx.Graphics.Gpu` 中添加 ASTC 透传路径：跳过 CPU 软解压，直接上传压缩数据。
- [ ] **集成与测试**
    - [ ] 验证纹理颜色是否正确 (避免 R/B 通道互换问题)。
    - [ ] 使用 `Metal Performance HUD` 验证显存占用是否显著下降。

### 1.3 MoltenVK 调优
- [ ] **配置注入**
    - [ ] 在 `Ryujinx.app` 启动脚本或初始化代码中注入优选环境变量。
    - [ ] 关键参数验证: `MVK_CONFIG_USE_METAL_ARGUMENT_BUFFERS` 等。

---

## 🚀 Phase 2: 流畅 (Smoothness) - P1
**目标**: 提升帧率稳定性，减少卡顿。

### 2.1 Vulkan Shader Object
- [ ] **扩展支持**
    - [ ] 检查 MoltenVK 是否支持 `VK_EXT_shader_object` (或等待上游更新)。
    - [ ] 在 `Ryujinx.Graphics.Vulkan` 后端实现 Shader Object 路径，替代传统的 Pipeline 构建。
- [ ] **编译优化**
    - [ ] 实现并行的 Shader 编译队列。

### 2.2 内存预算流式加载 (Memory Budget Streaming)
- [ ] **资源管理**
    - [ ] 实现基于权重的纹理逐出策略 (Eviction Policy)。
    - [ ] 监控 `MTLDevice.currentAllocatedSize`，动态调整缓存池大小。

---

## 🧠 Phase 3: 换心 (Transplant) - P2
**目标**: 彻底释放 M1 性能，消除 JIT 开销。

### 3.1 Hypervisor.framework 接入
- [ ] **虚拟化层**
    - [ ] 创建新的 CPU 后端 `Ryujinx.Cpu.Hypervisor`。
    - [ ] 封装 macOS `Hypervisor.framework` API (C# Bindings)。
- [ ] **执行映射**
    - [ ] 实现 Guest (Switch ARMv8) 到 Host (M1 ARMv8) 的寄存器上下文映射。
    - [ ] 处理 MMIO 陷阱 (Trap) 和中断注入。

### 3.2 NativeAOT 迁移
- [ ] **裁剪适配**
    - [ ] 标记所有使用反射 (Reflection) 的代码，进行 AOT 兼容性改造。
    - [ ] 配置 `rd.xml` 保留必要的元数据。
- [ ] **构建发布**
    - [ ] 配置 Release 发布脚本，启用 `PublishAot=true`。
    - [ ] 验证生成的二进制文件大小与启动速度。

---

## 🧪 自动化与质量保证 (QA & Automation)
- [ ] **基准测试工具**
    - [ ] 编写 `run_benchmark.py` 脚本 (无头模式运行、自动截图、CSV日志)。
- [ ] **黄金镜像对比**
    - [ ] 建立 Baseline 截图库。
    - [ ] 集成 `ImageMagick` 对比流程。
- [ ] **开发者看板 (OSD)**
    - [ ] 在 GUI 中添加 Overlay，显示 ASTC 命中率、RAM 占用等实时数据。

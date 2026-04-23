# Phase 1: 内存止血 - Context

**Gathered:** 2026-04-24
**Status:** Ready for planning

<domain>
## Phase Boundary

建立全面的内存监控体系，消除热点路径的堆分配，确保基础内存占用达标（空闲 < 2GB，游戏运行 30 分钟 < 4.5GB，Swap = 0）。

**In scope:**
- 内存监控指标采集（RSS, GC Heap, 非托管内存, Swap）
- 内存预算管理器基础框架
- 热点路径零分配优化（Texture 解码器, Memory 核心）
- BenchmarkDotNet 微基准测试套件

**Out of scope:**
- UI 开发者看板（属于 Phase 7）
- 自动化回归测试流水线（属于 Phase 6）
- 黄金镜像对比测试（属于 Phase 6）
- Metal 渲染优化（属于 Phase 2）
- GC 调优配置（属于 Phase 3）

</domain>

<decisions>
## Implementation Decisions

### 零分配优化策略
- **D-01:** 聚焦热点路径，非全面审计。仅优化 BenchmarkDotNet 测量出的 top 分配热点。
- **D-02:** 采用数据驱动方法：先建立 BenchmarkDotNet 基准，测量各解码器分配频率和内存压力，再按数据优先级优化。
- **D-03:** 已知的高频优化目标：`AstcDecoder` 的 `new byte[]` 路径（第232行，需确认调用方是否使用 MemoryOwner 重载）、Texture 模块中 16 处 `new byte[]` 直接分配。
- **D-04:** `BCnDecoder` 和 `ETC2Decoder` 已使用 `MemoryOwner<byte>.Rent()` + `stackalloc`，基准测试验证其性能基线，暂不作为优化目标（除非数据显示有残留分配）。

### 内存监控指标
- **D-05:** 全量监控四个维度：RSS（物理内存）、GC Heap Size（托管堆）、非托管内存（Native/GPU 纹理）、Swap Used（交换内存）。
- **D-06:** 精度目标：RSS 精度 ±10MB（与 REQUIREMENTS.md 一致）。
- **D-07:** 监控频率：每秒采样一次（可配置），避免过度开销。

### 内存监控暴露方式
- **D-08:** 采用分层架构：底层为结构化日志输出（CSV/JSON）+ 内部 C# API，不直接在 Phase 1 构建 UI。
- **D-09:** 日志格式：CSV，包含时间戳、RSS(MB)、GCHeap(MB)、Unmanaged(MB)、Swap(MB)、内存压力级别。
- **D-10:** 内部 API 设计为 `IMemoryTracker` 接口，供其他模块订阅内存事件（如接近阈值时触发缓存清理）。
- **D-11:** Phase 7 的开发者看板将消费此 API，Phase 1 不实现 UI。

### 基准测试范围
- **D-12:** 覆盖 Texture 解码器（BCnDecoder 各格式、ASTCDecoder、ETC2Decoder）+ Memory 核心操作（MemoryManager 分配/映射）。
- **D-13:** 不包含 ARMeilleure PTC（Translation Cache）测试，其复杂度超出 Phase 1 范围。

### 基准测试数据
- **D-14:** 合成随机数据为主：程序生成各种尺寸和格式的随机纹理块，保证测试可重复、CI 友好。
- **D-15:** 真实纹理样本为辅：从合法拥有的游戏中提取少量代表性纹理块（如 ASTC 4x4, 8x8 等常见 block 尺寸），用于手动验证合成数据的代表性。
- **D-16:** 基准测试测量维度：执行时间、内存分配量（B/ops）、吞吐量（MB/s）。

### the agent's Discretion
- 内存预算管理器达到阈值时的具体行为（硬限制/软提示/OOM 防护策略）— 用户未指定，由实现者根据最佳实践决定。
- 日志文件轮转策略（文件大小限制、历史保留天数）。
- 合成纹理数据的具体生成算法（纯随机 vs 结构化模式）。
- `IMemoryTracker` 接口的精确形状和事件模型。

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Memory Management
- `.planning/PROJECT.md` §Context — 现有优化基础（ASTC 透传、Weight-based 驱逐、.NET 10 升级）
- `.planning/REQUIREMENTS.md` §v1 Requirements — MEM-01~03, CPU-03, QA-01 的具体验收标准
- `src/Ryujinx.Memory/` — 内存管理核心模块（MemoryBlock, NativeMemoryManager, PageTable）
- `src/Ryujinx.Memory/MemoryManagementUnix.cs` — macOS 平台内存管理实现

### Texture Decoding
- `src/Ryujinx.Graphics.Texture/BCnDecoder.cs` — BC1~BC7 解码器，已使用 MemoryOwner + stackalloc
- `src/Ryujinx.Graphics.Texture/Astc/AstcDecoder.cs` — ASTC 解码器，仍有 new byte[] 路径（行232）
- `src/Ryujinx.Graphics.Texture/ETC2Decoder.cs` — ETC2 解码器，已使用 MemoryOwner + stackalloc
- `src/Ryujinx.Graphics.Texture/Ryujinx.Graphics.Texture.csproj` — 模块依赖

### Benchmarking
- `Directory.Packages.props` — `BenchmarkDotNet` 版本 0.15.8
- `BenchmarkDotNet.Artifacts/` — 现有基准测试产物目录
- `Ryujinx_M1_Optimization_Guide.md` §验收与量化体系 — KPI 目标值和验收工具链

### Existing Patterns
- `src/ARMeilleure/Translation/PTC/Ptc.cs` — `MemoryStreamManager` + `RecyclableMemoryStream` 使用示例
- `src/Ryujinx.Common/MemoryOwner.cs` 或类似 — MemoryOwner<T> 的使用模式

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MemoryStreamManager` (ARMeilleure/Translation/PTC/) — 已封装 RecyclableMemoryStream，可作为 Phase 1 内存池化参考模式
- `MemoryOwner<byte>` (Ryujinx.Graphics.Texture/) — 已在 BCn/ETC2 解码器中广泛使用，AstcDecoder 已有 MemoryOwner 重载但可能未全覆盖
- `Ryujinx_M1_Optimization_Guide.md` — 包含完整的验收 KPI 和工具链指南

### Established Patterns
- 纹理解码器输出使用 `MemoryOwner<byte>.Rent(size)` + `stackalloc byte[] tile` 作为临时缓冲区 — 这是已验证的高效低分配模式
- `new byte[]` 直接分配在 AstcDecoder 中仍有残留（行232的 `byte[] output = new byte[QueryDecompressedSize(...)]`）
- Texture 模块中有 16 处 `new byte[]` 直接分配待审计

### Integration Points
- 内存监控 API 需要插入到 `Ryujinx.Memory` 的核心分配路径（MemoryBlock, NativeMemoryManager）
- 日志输出可复用现有的日志基础设施（Ryujinx.Common 中的日志系统）
- BenchmarkDotNet 测试项目可新建或复用现有测试结构

</code_context>

<specifics>
## Specific Ideas

- "像 Activity Monitor 一样显示内存" — 用户期望内存监控直观易懂
- 优先优化 `AstcDecoder` 的 `new byte[]` 路径（调用最频繁，且已有 ASTC 透传优化基础）
- BenchmarkDotNet 数据应能直接对比优化前后的分配量和执行时间

</specifics>

<deferred>
## Deferred Ideas

- 内存预算达到阈值时的主动缓存释放策略 — 可纳入 Phase 1 实现，但具体行为由实现者决定
- UI 内存看板（Overlay/侧边栏）— Phase 7
- 自动化 CI 基准测试流水线 — Phase 6
- 黄金镜像对比测试 — Phase 6
- GC 调优（Server GC, Latency Mode）— Phase 3
- Metal 后端 Memoryless 深度缓冲 — Phase 2

</deferred>

---

*Phase: 01-memory-stop-the-bleeding*
*Context gathered: 2026-04-24*

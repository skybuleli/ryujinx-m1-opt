# State: SwitchPro

**Updated:** 2026-04-24
**Status:** 🔄 Executing Phase 1

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-24)

**Core value:** 在 M1 8GB 内存设备上，以低于 4.5GB 的常驻内存实现稳定 30+ FPS 的主流游戏运行体验。
**Current focus:** Phase 1 — 内存止血 (Stop the Bleeding)

## Phase Status

| Phase | Status | Requirements | Completion |
|-------|--------|--------------|------------|
| 1 — 内存止血 | ✅ Complete | 5 | 100% |
| 2 — Metal 基础 | ⚪ Pending | 3 | 0% |
| 3 — CPU 与 GC 优化 | ⚪ Pending | 4 | 0% |
| 4 — Shader 优化 | ⚪ Pending | 2 | 0% |
| 5 — 帧率稳定 | ⚪ Pending | 3 | 0% |
| 6 — 基准测试体系 | ⚪ Pending | 4 | 0% |
| 7 — 开发者工具 | ⚪ Pending | 5 | 0% |
| 8 — 体验打磨 | ⚪ Pending | 4 | 0% |

## Active Work

Phase 1: All plans completed (Plans 01-04).
Next: Run `/gsd-verify-work 1` to validate Phase 1 completion.

## Blockers

- NuGet package sources `git.ryujinx.app` are defunct (404), preventing full solution build on fresh machines. Local `Ryujinx.Common` builds successfully. Does not block Phase 1 execution.

## Recent Decisions

- 保留 Avalonia UI，专注后端优化
- 放弃跨平台，专注 macOS Apple Silicon
- 采用细粒度分阶段执行，每阶段可量化验证
- Moved `IMemoryInfoProvider` to `Ryujinx.Common` to avoid circular dependency with `MemoryBudgetManager`
- Used Logger event system (`ILogTarget`) for CSV memory log dispatch instead of direct coupling
- Deleted unused `AstcDecoder.TryDecodeToRgba8(out Span<byte>)` overload (zero callers in src/)
- LayoutConverter now throws `ArgumentException` on empty output span instead of silently allocating
- Replaced all remaining `new MemoryStream()` calls in src/ with `MemoryStreamManager.Shared.GetStream()`
- Avoided circular dependency between Ryujinx.Common and Ryujinx.Memory by placing TrackNativeMemory as extension method in Ryujinx project
- Used SetPressureHandler late-binding pattern because GpuContext is created in Switch.cs, not Program.cs
- Added Clear() methods to TextureCache and ShaderCache instead of disposing and recreating

## Metrics

| Metric | Baseline | Target | Current |
|--------|----------|--------|---------|
| Resident Memory (BotW) | ~6-7 GB | < 4.5 GB | TBD |
| Average FPS (BotW) | ~25 FPS | 30+ FPS | TBD |
| 1% Low FPS | ~15 FPS | 20+ FPS | TBD |
| Swap Used | ~1-2 GB | ~0 GB | TBD |
| Load Time (Title Screen) | ~45s | < 30s | TBD |

## Next Actions

1. Run `/gsd-verify-work 1` to validate Phase 1 completion
2. Run `/gsd-discuss-phase 2` to begin Phase 2 (Metal 基础) planning

---
*State file updated: 2026-04-24 after Plan 04 completion*

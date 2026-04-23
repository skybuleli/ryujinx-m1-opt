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
| 1 — 内存止血 | 🔄 Executing | 5 | 0% |
| 2 — Metal 基础 | ⚪ Pending | 3 | 0% |
| 3 — CPU 与 GC 优化 | ⚪ Pending | 4 | 0% |
| 4 — Shader 优化 | ⚪ Pending | 2 | 0% |
| 5 — 帧率稳定 | ⚪ Pending | 3 | 0% |
| 6 — 基准测试体系 | ⚪ Pending | 4 | 0% |
| 7 — 开发者工具 | ⚪ Pending | 5 | 0% |
| 8 — 体验打磨 | ⚪ Pending | 4 | 0% |

## Active Work

Phase 1 planned: 4 plans in 2 waves

## Blockers

(None)

## Recent Decisions

- 保留 Avalonia UI，专注后端优化
- 放弃跨平台，专注 macOS Apple Silicon
- 采用细粒度分阶段执行，每阶段可量化验证

## Metrics

| Metric | Baseline | Target | Current |
|--------|----------|--------|---------|
| Resident Memory (BotW) | ~6-7 GB | < 4.5 GB | TBD |
| Average FPS (BotW) | ~25 FPS | 30+ FPS | TBD |
| 1% Low FPS | ~15 FPS | 20+ FPS | TBD |
| Swap Used | ~1-2 GB | ~0 GB | TBD |
| Load Time (Title Screen) | ~45s | < 30s | TBD |

## Next Actions

1. Run `/gsd-discuss-phase 1` to gather context and clarify approach for Phase 1
2. Run `/gsd-plan-phase 1` to create detailed execution plan
3. Run `/gsd-execute-phase 1` to begin implementation

---
*State file updated: 2026-04-24*

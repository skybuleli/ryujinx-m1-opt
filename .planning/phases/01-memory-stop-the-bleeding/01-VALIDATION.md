---
phase: 1
slug: memory-stop-the-bleeding
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-24
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x + BenchmarkDotNet 0.15.8 |
| **Config file** | `tests/Ryujinx.Benchmarks/Benchmarks.csproj` |
| **Quick run command** | `dotnet test tests/Ryujinx.Tests/` |
| **Full suite command** | `dotnet run --project tests/Ryujinx.Benchmarks/ -- --filter '*'` |
| **Estimated runtime** | ~120 seconds (benchmarks) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Ryujinx.Tests/`
- **After every plan wave:** Run `dotnet run --project tests/Ryujinx.Benchmarks/ -- --filter 'AstcDecoderBenchmarks|BCnDecoderBenchmarks|MemoryBlockBenchmarks'`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | MEM-01 | — | N/A | unit | `dotnet test --filter 'MemoryTrackerTests'` | ❌ W0 | ⬜ pending |
| 1-01-02 | 01 | 1 | MEM-01 | — | N/A | unit | `dotnet test --filter 'MacOSMemoryInfoProviderTests'` | ❌ W0 | ⬜ pending |
| 1-02-01 | 02 | 1 | CPU-03 | — | N/A | benchmark | `dotnet run --project tests/Ryujinx.Benchmarks -- --filter 'AstcDecoderBenchmarks'` | ❌ W0 | ⬜ pending |
| 1-02-02 | 02 | 1 | CPU-03 | — | N/A | benchmark | `dotnet run --project tests/Ryujinx.Benchmarks -- --filter 'BCnDecoderBenchmarks'` | ✅ | ⬜ pending |
| 1-02-03 | 02 | 1 | CPU-03 | — | N/A | benchmark | `dotnet run --project tests/Ryujinx.Benchmarks -- --filter 'ETC2DecoderBenchmarks'` | ❌ W0 | ⬜ pending |
| 1-03-01 | 03 | 1 | CPU-03 | — | N/A | static | `grep -r "new MemoryStream(" src/ || true` | — | ⬜ pending |
| 1-04-01 | 04 | 1 | MEM-02 | — | N/A | integration | Headless run + CSV parse | — | ⬜ pending |
| 1-04-02 | 04 | 1 | MEM-03 | — | N/A | integration | Headless run + Swap monitor | — | ⬜ pending |
| 1-05-01 | 05 | 1 | QA-01 | — | N/A | benchmark | `dotnet run --project tests/Ryujinx.Benchmarks` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Ryujinx.Benchmarks/AstcDecoderBenchmarks.cs` — stubs for CPU-03
- [ ] `tests/Ryujinx.Benchmarks/ETC2DecoderBenchmarks.cs` — stubs for CPU-03
- [ ] `tests/Ryujinx.Benchmarks/MemoryBlockBenchmarks.cs` — stubs for QA-01
- [ ] `tests/Ryujinx.Tests/Memory/MacOSMemoryInfoProviderTests.cs` — stubs for MEM-01
- [ ] `tests/Ryujinx.Tests/Memory/MemoryBudgetManagerTests.cs` — stubs for MEM-02

*Wave 0 installs test stubs so subsequent tasks have automated verify targets.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| BotW 30-min RSS < 4.5GB | MEM-02 | Requires full game + human gameplay | Launch BotW, play 30 min, check Activity Monitor vs CSV log |
| Idle RSS < 2GB | MEM-02 | Requires real macOS hardware | Launch emulator, idle 5 min, read Activity Monitor |
| Swap = 0 during gameplay | MEM-03 | Requires real macOS hardware | Monitor `CompressorPageCount` in CSV log during gameplay |

*All phase behaviors have automated verification EXCEPT integration targets requiring real hardware + licensed game.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

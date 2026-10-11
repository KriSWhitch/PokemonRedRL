# Roadmap: computer-vision + RL overhaul of PokemonRedRL

Status: In Progress (5/14 tasks done — Tasks 1, 2, 2b, 3, 3b complete; Task 4 is next)

> **How to read this plan.** This is an *epic-level* roadmap, not a single implementation task. Its "Steps" are an ordered list of **separate future tasks**, each of which will get its own `docs/tasks/<slug>.md` (via `/bmad-plan`) and its own BMAD cycle when it's picked up. This file's job is to sequence them correctly, record the cross-cutting decisions and risks that span multiple tasks, and give the user one place to see the whole arc. Treat each `- [ ]` below as "spin up its own plan when we get here", not as a literal file edit.
>
> **Project spec:** [`docs/PROJECT_SPEC.md`](../PROJECT_SPEC.md) — the single source of truth for overall project state. Updated automatically by BMAD prompts.
>
> **Brainstorm sources:** [`_bmad-output/brainstorm-epic-roadmap-review.md`](../../_bmad-output/brainstorm-epic-roadmap-review.md) (epic quality review) + [`_bmad-output/brainstorm-full-architecture-review.md`](../../_bmad-output/brainstorm-full-architecture-review.md) (full architecture review). Key decisions from these brainstorms are incorporated below.

## Goal

Turn PokemonRedRL from a memory-state-only DQN agent into one with (a) computer-vision-augmented perception feeding both the policy and reward shaping, (b) a deliberately chosen/validated RL algorithm, (c) durable model/knowledge persistence, (d) a Docker-first deployment where `docker compose up` starts everything, (e) a single-window ControlPanel with screenshot grid replacing 20 separate mGBA windows, and (f) a long-term path to eliminating the Lua/TCP bridge in favor of direct P/Invoke to mGBA core — while reusing the existing 5-project architecture, Redis-backed multi-agent training loop, and TorchSharp ML stack.

## Architecture Decisions (confirmed by brainstorm)

These are settled — downstream tasks treat them as constraints, not open questions:

| Decision | Verdict | Rationale |
|---|---|---|
| **Emulator** | mGBA stays | Best Game Boy emulator with scripting API. PyBoy slower, BizHawk heavier, SameBoy no API. |
| **ML framework** | TorchSharp stays | Only production-ready C# RL framework. ML.NET doesn't do RL. |
| **OpenCV** | ❌ Removed | `torchvision.transforms` (Resize→Grayscale→Normalize) covers all preprocessing. |
| **Redis vs Kafka** | Redis stays | Covers 5/5 use cases. Kafka covers 1/5. Two stores > one store at this scale. |
| **Frame resolution** | 84×84 grayscale | Atari DQN standard. Saves VRAM. Proven. |
| **Network architecture** | Hybrid CNN+MLP | Industry standard for games with hidden state. Pixels-only confirmed inadequate. |
| **Feature flag** | `VisionEnabled` config switch | Keeps pre-vision baseline runnable for A/B comparison. |
| **Long-term direction** | P/Invoke to mGBA core | Eliminates TCP + Lua + JSON. Spike first, migrate if API stable. |

## Affected files/projects

Ultimately: all 5 C# projects, `mgba_socket.lua`, `Program.cs` DI wiring, `Dockerfile`, `docker-compose.yml`, `docs/PROJECT_OVERVIEW.md`, `docs/CODEBASE_MAP.md`, and likely a new `PokemonRedRL.Vision` project. Exact file lists belong in each sub-task's own plan, not here.

## Steps (ordered list of future sub-tasks)

### ✅ Completed

- [x] **Task 1 — Spike: validate the frame-capture mechanism. RESOLVED.** ([docs/tasks/archive/frame-capture-spike.md](archive/frame-capture-spike.md)): OS-level `PrintWindow` achieves ~141–145 Hz. Decision: vision uses OS-level window capture, correlated to each agent via process handle.
- [x] **Task 2 — Pre-overhaul cleanup of known debt. RESOLVED.** ([docs/tasks/archive/pre-overhaul-cleanup.md](archive/pre-overhaul-cleanup.md)): Reward bug fixed, stale Lua removed, paths fixed, DI dedup, Parquet.Net removed.
- [x] **Task 2b — Desktop control panel (WPF). RESOLVED (MVP).** ([docs/tasks/archive/automated-mgba-launch-and-fast-forward.md](archive/automated-mgba-launch-and-fast-forward.md)): Manual attach mode shipped. Auto-launch blocked by mGBA 0.10.5 limitation.
- [x] **Task 3 — .NET & package modernization. RESOLVED.** ([docs/tasks/archive/net-package-modernization.md](archive/net-package-modernization.md)): net8.0→net10.0, TorchSharp 0.107, CUDA verified. OpenCvSharp4 added as placeholder (to be removed in Task 4a).
- [x] **Task 3b — Docker containerization. RESOLVED.** ([docs/tasks/archive/docker-containerization.md](archive/docker-containerization.md)): `docker compose up` for Redis + Agent. HARD GATE cleared.

### 🔜 Next Up

- [ ] **Task 4a — Remove OpenCV, adopt torchvision.transforms.** P0 cleanup before design work. Remove `OpenCvSharp4` + `OpenCvSharp4.runtime.win` from `PokemonRedRL.Utils.csproj`. Document the torchvision.transforms pipeline (Resize→Grayscale→Normalize) that replaces it. Verify `dotnet build` passes. **Effort: 1 hour. Blocks: nothing.**
- [ ] **Task 4 — Design the combined (vision + memory-state) representation.** Write a design doc/ADR settling: hybrid CNN+MLP architecture (confirmed by brainstorm — this task formalizes, doesn't re-litigate), 84×84 grayscale frames, frame-stack depth for motion (2-4 frames), normalization scheme, feature flag `VisionEnabled`, **A/B test protocol** (N episodes with/without vision, same seed, specific milestone, success threshold — e.g. "vision agent reaches Route 3 in ≤80% of episodes of no-vision agent"), and decision on whether positional features (X, Y, MapID) stay in the memory vector or are dropped if vision covers them. **Depends on: T1, T3b. Blocks: T6, T8.**
- [ ] **Task 5 — RL algorithm review.** Research/decision task: stay with Double-DQN + prioritized replay vs. Rainbow improvements (dueling heads, n-step, noisy nets) vs. PPO. Document tradeoffs given real-time emulator rollouts, multi-agent Redis-shared learning, GPU/VRAM budget. Produce recommendation. **This must land before T8.** Also measure per-agent GPU memory with current MLP-only network as baseline for VRAM budgeting. **Depends on: —. Blocks: T8.**

### ⬜ Planned

- [ ] **Task 6 — Vision project + frame pipeline.** New `PokemonRedRL.Vision` project (`IFrameProvider`/`IVisionPreprocessorService`), OS-level `PrintWindow` capture per Task 1 decision, consume ControlPanel run manifest for window↔agent correlation, produce `Tensor` per frame, performance instrumentation (capture latency, dropped-frame rate). DI registrations in `Program.cs` (Scoped). Tensor disposal as first-class concern with soak-test. **Depends on: T1, T2b, T4. Blocks: T9.**
- [ ] **Task 7 — Protocol verification + contract test.** Verify no protocol changes needed for vision (OS-level capture is independent of TCP bridge). Land contract test: serialize/deserialize `GameStateResponse` against `GameStateSerializer`. **Depends on: T1. Blocks: —.**
- [ ] **Task 8 — Dual-modality network + trainer.** Implement T4's design: `PokeVisionDQN` (or algorithm from T5), extend `ModelExperience`/`GameState`/`StatePreprocessorService` for dual modalities, update trainer for dual-tensor batches. **Mandatory Redis payload measurement:** measure per-experience size with 1 agent at 84×84 grayscale, project for 10 agents × BatchSize × MaxStreamLength. If >50% Redis memory, implement compression (store only CNN embeddings, not raw frames). DI registrations. Tensor disposal soak-test. **Depends on: T4, T5. Blocks: —.**
- [ ] **Task 9 — Vision-aware reward shaping.** Extend `RewardCalculatorService` with visual-novelty signals (perceptual-hash or embedding-distance bonus), visual menu/battle detection. Algorithm-agnostic — does not depend on T5/T8. **Depends on: T2, T6. Blocks: —.**
- [ ] **Task 10 — Model persistence redesign.** Versioned on-disk checkpoints (`src/data/models/`), checkpoint metadata (step count, reward, epsilon), resume-on-restart flow. **Depends on: —. Blocks: —.**
- [ ] **Task 11 — Testing infrastructure (xUnit).** Create xUnit test project (`net10.0`). Tests for: `RewardCalculatorService` (incl. regression for T2 bug), `StatePreprocessorService`, vision preprocessing math, `SumTree`/replay sampling. **Create skeleton early (parallel with T6), add tests incrementally per component.** Contract test pulled forward into T7. **Depends on: —. Blocks: —.**
- [ ] **Task 12 — Documentation catch-up.** Fold "planned" language in `docs/PROJECT_OVERVIEW.md` into present-tense. Run `/cartograph` to refresh `docs/CODEBASE_MAP.md`. Recurring checklist inside each task, not a single end-of-epic task. **Depends on: All above. Blocks: —.**

### 🟢 Future (post-epic)

- [ ] **Task 13 — Headless mGBA in Docker.** Build mGBA without Qt/SDL for Linux, package as Docker container. TCP between containers (mGBA + Agent). `docker compose up` starts everything — no manual mGBA launch. **Effort: 2-3 days. Depends on: T8 (vision working).**
- [ ] **Task 14 — P/Invoke spike: mGBA as .so.** Build mGBA as shared library, call `mCoreInitConfig` + `mCoreLoadROM` from C# via P/Invoke. If API stable → plan full migration (eliminates TCP + Lua + JSON). **Effort: 1-2 days. Depends on: T13 (headless build proven).**
- [ ] **Task 15 — ControlPanel screenshot grid.** Add `screenshot` command to Lua protocol, render all agent screenshots in single WPF grid (5 fps/agent). Replaces 20 separate mGBA windows. **Effort: 2-3 days. Depends on: T6 (frame pipeline).**
- [ ] **Task 16 — PPO + LSTM spike.** After T8 ships, port CleanRL PPO to TorchSharp, compare sample efficiency on navigation task. **Effort: 3-5 days. Depends on: T8.**

## Risks & irreversible actions

- **Task 3b (containerization) is a user-requested hard gate on Task 4 onward.** ✅ Cleared.
- **Task 5 (RL algorithm review) must precede Task 8 (trainer build).** Building trainer code before algorithm choice risks wasted work.
- **Redis payload size (Task 8)** is a HIGH risk. Raw frames in experience stream could silently blow up Redis memory. Mandatory measurement step added to T8.
- **TorchSharp tensor lifecycle risk gets materially worse with vision.** Frame tensors are orders of magnitude larger than 14-float vectors. T6/T8 must treat disposal as first-class concern with soak-tests.
- **GPU/VRAM capacity is an unresourced risk.** T5 must measure baseline per-agent GPU memory. T8 must budget for CNN branch. Fallback: shared CNN encoder, fewer agents, or smaller network.
- **mGBA C API stability (Task 14).** Not a public API — may break between versions. Mitigation: pin mGBA version, don't update without tests.
- **Protocol changes are two-sided and hard to verify automatically.** Every Lua change needs real mGBA smoke test.
- **Nothing here should be done as a single mega-PR.** Each task goes through its own BMAD cycle.

## Verification plan

- Each sub-task defines its own verification plan when planned individually.
- Roadmap-level success criteria: (1) `dotnet build src/PokemonRedRL.sln` passes after every task; (2) multi-agent training loop runs end-to-end against real mGBA; (3) reward signal not double/over-firing; (4) frame-capture throughput not bottlenecking; (5) `VisionEnabled` feature flag lets pre-vision baseline run for comparison at every stage; (6) `docker compose up` starts everything (post-T13).

## Open questions/assumptions

- **.NET 10 confirmed** — already migrated in T3.
- **OpenCV removed** — decision confirmed by brainstorm. TorchSharp-only preprocessing.
- **Hybrid CNN+MLP confirmed** — industry standard, pixels-only rejected.
- **Redis stays** — Kafka is overengineering at this scale.
- **84×84 grayscale frames** — Atari DQN standard, saves VRAM.
- **GPU/VRAM budget** — needs empirical measurement in T5 (baseline) and T8 (with CNN).
- **P/Invoke feasibility** — unknown until T14 spike. TCP+Lua is the fallback.

## Review notes (from `/bmad-plan` v3 — 2026-10-11)

🔄 **Major revision — epic restructured based on two brainstorms:**

**Changes from v2:**
- **Status fixed:** 5/14 tasks done (was incorrectly "7/12" — T1, T2, T2b, T3, T3b = 5 completed tasks, not 7).
- **New task T4a:** Remove OpenCV (P0, 1 hour) — inserted before T4 so design work starts with clean dependencies.
- **T4 enhanced:** Added A/B test protocol requirement (concrete metrics, episode count, success threshold). Hybrid confirmed as default, not open question.
- **T5 enhanced:** Added baseline GPU memory measurement for VRAM budgeting.
- **T8 enhanced:** Added mandatory Redis payload measurement step before committing to storage strategy.
- **T11 rescheduled:** Test skeleton created early (parallel with T6), tests added incrementally — not retroactively at the end.
- **New tasks T13-T16:** Headless mGBA Docker, P/Invoke spike, ControlPanel screenshot grid, PPO+LSTM spike — moved from "maybe later" to explicit future tasks with dependencies.
- **Architecture Decisions table added:** Settled decisions from brainstorms documented as constraints, not re-litigated in each task.
- **Kafka rejected:** Documented rationale (5/5 use cases vs 1/5).
- **Open questions resolved:** OpenCV, hybrid vs pixels-only, Redis vs Kafka, frame resolution — all settled.

**Carried forward from v2:**
- Task ordering (T4→T5→T6→T7→T8→T9→T10→T11→T12) preserved for core vision pipeline.
- All risks from v2 review notes still apply.
- Hard gate (T3b) still cleared.
- "No mega-PR" rule still applies.

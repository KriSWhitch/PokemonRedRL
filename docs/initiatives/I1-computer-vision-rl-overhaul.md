# I1 — Computer-Vision + RL Overhaul

**ID:** I1
**Tracking:** See [Work Items Registry](../WORK_ITEMS.md) for status, branch, progress, dependencies, and next work.

## Outcome

Turn PokemonRedRL from a memory-state-only DQN agent into a system with vision-augmented perception and reward shaping, a deliberately selected RL algorithm, durable model persistence, and a verified containerized-backend foundation, while retaining the existing .NET, TorchSharp, Redis, and mGBA architecture. The longer-term goal of `docker compose up` starting a headless emulator as well as Redis and the Agent remains S16, a post-Initiative candidate rather than a completion gate for these 15 core stories.

## Initiative Success Signals

- `Vision.Enabled=false` preserves the existing memory-only behavior; the vision path combines frames with all 14 memory features.
- The S1 capture mechanism is sustained and measured against the training cadence by S9; latency, dropped frames, and supported concurrency are reported rather than inferred from the 3-window spike.
- Vision-aware reward signals do not double-fire or reward repeated frames; the selected algorithm trains end-to-end against mGBA.
- S11 measures actual CNN training VRAM and Redis payload costs for the target workload, applies the documented storage threshold, and records residual environment limits.
- Versioned checkpoints resume with their training metadata; regression tests/manual checks cover the agreed high-risk boundaries.
- The solution builds after each story. Docker Compose continues to start Redis and the Agent; full headless-mGBA-in-Docker remains explicitly assigned to S16, not silently counted as complete here.

## Child Epics

Each Epic owns its child user stories and has a long-lived branch cut from this Initiative branch. Story branches are cut from their Epic branch. Completed historical work is recorded in its Epic but is not replayed through the new branch hierarchy.

| ID | Epic | Outcome |
|---|---|---|
| E1 | [Vision perception and pipeline](../epics/E1-vision-perception.md) | Reliable visual input and progress signals. |
| E2 | [RL training and models](../epics/E2-rl-training-and-models.md) | Dual-modality learning and recoverable checkpoints. |
| E3 | [Emulator integration and runtime](../epics/E3-emulator-integration-runtime.md) | Verified host-attached emulator contract. |
| E4 | [Reliability and validation](../epics/E4-reliability-validation.md) | Buildable, testable, documented platform. |

The Initiative is complete only after all four Epics are complete, archived, and merged into this branch. Then the Initiative itself is archived and merged into the configured base branch (`development`). Promotion from `development` to `main` is manual and outside this workflow.

## Confirmed Decisions

| Decision | Rationale |
|---|---|
| Keep mGBA and TorchSharp | Existing scripting and C# ML stack meet the project needs. |
| Use OS-level `PrintWindow` capture | S1 measured ~141–145 Hz, versus ~1 Hz for `emu:screenshot()`. |
| Use the accepted S7 representation | F=2 84×84 grayscale frames normalized to [-1,1], all M=14 memory features, 512+64→576→256→6 fusion; see the [representation ADR](../adr/ADR-0001-vision-representation.md). |
| Keep Redis | It supports the current replay and shared-parameter use cases without another datastore. |
| Use `Vision.Enabled` for baseline comparison | Vision changes must remain comparable against the memory-only agent; false is the bit-identical backward-compatible path. The accepted S7 ADR is authoritative for the configuration key; `VisionEnabled` in the archived S7 plan is shorthand. |
| Use Double DQN + Dueling Heads + Noisy Nets for the vision-enabled learner | Accepted by the [RL algorithm ADR](../adr/ADR-0002-rl-algorithm-review.md); PPO remains a separate experiment. |
| Treat P/Invoke as a later spike | Keep TCP+Lua as the working fallback until API stability is demonstrated. |

## Source Artifacts

- [Epic roadmap review brainstorm](../../_bmad-output/archive/brainstorms/BR-0001-epic-roadmap-review.md)
- [Full architecture review brainstorm](../../_bmad-output/archive/brainstorms/BR-0002-full-architecture-review.md)
- [Frame-capture evidence](../stories/archive/S1-frame-capture-spike.md)
- [Vision representation ADR](../adr/ADR-0001-vision-representation.md)
- [RL algorithm ADR](../adr/ADR-0002-rl-algorithm-review.md)

## Completed Baseline

- [S1 frame-capture spike](../stories/archive/S1-frame-capture-spike.md), [S2 pre-overhaul cleanup](../stories/archive/S2-pre-overhaul-cleanup.md), [S3 ControlPanel MVP](../stories/archive/S3-controlpanel-mvp.md), [S4 .NET modernization](../stories/archive/S4-net-package-modernization.md), [S5 Docker Compose](../stories/archive/S5-docker-containerization.md), [S6 OpenCV removal](../stories/archive/S6-remove-opencv-adopt-torchvision.md), [S7 vision representation](../stories/archive/S7-design-vision-representation.md), and [S8 RL algorithm review](../stories/archive/S8-rl-algorithm-review.md) are complete.
- The selected learner is Double DQN + Dueling Heads + Noisy Nets. PPO remains an experiment, not the current implementation.

## Future Candidates

These are not part of the 15-story Initiative completion count. Start a new Initiative or explicitly add them through a scope update before implementation:

- S16 — Headless mGBA in Docker.
- S17 — P/Invoke spike for mGBA core.
- S18 — ControlPanel screenshot grid.
- S19 — PPO + LSTM experiment.

See WORK_ITEMS for the authoritative scope and dependency edges for these candidates.

S16 evaluates a Linux headless mGBA build without Qt/SDL, container networking to the Agent, and the full-stack `docker compose up` target. S17 tests `mCoreInitConfig`/`mCoreLoadROM` through P/Invoke with TCP+Lua retained as fallback. S18 adds a Lua screenshot command and a single ControlPanel grid at the planned per-agent refresh rate. S19 ports CleanRL PPO with LSTM to TorchSharp and evaluates it against the navigation task; it is not the selected S11 algorithm.

## Cross-Epic Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Tensor leaks grow with frame tensors | High | DisposeScope review and soak tests in S9/S11. |
| Redis payload size grows with stored frames | High | Measure per-experience size and project 10 agents × batch size × max stream length in S11; if projected use exceeds 50% of the Redis memory budget, store CNN embeddings instead of raw frames unless an approved course correction chooses another bounded strategy. |
| S8 VRAM budget is theoretical rather than a measured live baseline | Medium | Measure peak CNN training memory in S11 when CUDA is available; if unavailable, record the blocker and keep the capacity assumption explicit. |
| No existing test project or CI | Medium | Add xUnit incrementally in S14; document manual mGBA checks. |
| mGBA C API changes | Medium | Pin version and retain TCP+Lua fallback. |

## Scope Changes

Material changes to this Initiative update this file and affected Epics before child stories proceed. Do not silently add scope to an active story.
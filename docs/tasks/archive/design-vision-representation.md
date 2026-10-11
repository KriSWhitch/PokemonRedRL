# Task 4 — Design combined (vision + memory-state) representation
**Status:** ✅ Done
**Depth:** Standard
**Created:** 2026-10-11

## Why (Motivation)

The agent currently sees only a hand-picked 14-float memory vector — it has no visual awareness of obstacles, NPCs, menus, or battle UI. Adding vision as a supplementary input is the core bet of this epic. But before building the frame pipeline (T6) or the dual-modality network (T8), we need a settled design for how vision and memory-state coexist: what the tensors look like, how they fuse, what the feature flag looks like, and how we'll prove vision helps. Without this design, T6 and T8 will make incompatible assumptions and require rework.

## What (Specification)

### Capabilities

| ID | Intent (WHAT) | Success Signal (how to verify) |
|---|---|---|
| CAP-1 | Define the vision tensor shape: resolution, channels, frame-stack depth, normalization | A developer reading the ADR can implement `IVisionPreprocessorService` without asking "what size tensor?" |
| CAP-2 | Define the memory-state tensor shape: which of the existing 14 features stay, which (if any) are dropped because vision covers them | A developer reading the ADR can update `StatePreprocessorService` without ambiguity |
| CAP-3 | Define the fusion architecture: where CNN and MLP branches meet, what the combined forward pass looks like | A developer reading the ADR can sketch `PokeVisionDQN.Forward(Tensor frame, Tensor state)` |
| CAP-4 | Define the `VisionEnabled` feature flag: where it lives (config/env/DI), how it gates the dual path, what the fallback (memory-only) path looks like | `ExplorationAgent` can switch between vision and no-vision mode with a single config change; no-vision mode produces identical behavior to pre-T4 |
| CAP-5 | Define the A/B test protocol: concrete episode count, starting state, seed, milestone, success threshold | A researcher can run the protocol and answer "does vision help?" with a yes/no and a confidence level |
| CAP-6 | Decide whether positional features (X, Y, MapID) stay in the memory vector or are dropped if vision covers them | The decision is documented with rationale; the memory vector size is finalized |

### Constraints

- **Hybrid CNN+MLP is settled** (brainstorm decision). This task formalizes the design, does not re-litigate pixels-only vs hybrid.
- **84×84 grayscale frames** (brainstorm decision). This task confirms and documents, does not re-open.
- **TorchSharp-only preprocessing** — `torchvision.transforms` pipeline, no OpenCV (T4a already removed it).
- **Frame capture is OS-level `PrintWindow`** (T1 decision). Vision pipeline consumes frames from the OS, not from the Lua bridge.
- **Existing 14-float vector must remain runnable** — the `VisionEnabled=false` path must produce bit-identical behavior to the current code.
- **No new NuGet packages** — TorchSharp already covers `torchvision.transforms` and CNN layers.
- **No Lua protocol changes** — vision capture is independent of the TCP bridge (T1 decision).
- **No Redis schema changes in this task** — storage strategy is T8's concern. This task only defines the in-memory tensor shapes.

### Non-goals

- **Not implementing anything.** This is a design/ADR task. No code changes, no new projects, no DI registrations.
- **Not deciding the RL algorithm.** That's T5. This design works with DQN, Double DQN, Dueling DQN, or PPO — the fusion point (before the Q-head / policy head) is algorithm-agnostic.
- **Not designing the frame capture pipeline.** That's T6. This task assumes frames arrive as tensors; how they're captured is T6's problem.
- **Not designing Redis storage for frames.** That's T8. This task only defines in-memory shapes.
- **Not designing reward shaping.** That's T9. Vision-aware rewards are out of scope here.

### Success Signal

A developer can read the ADR and implement `PokeVisionDQN` (T8) and `IVisionPreprocessorService` (T6) independently, and the two implementations will compose correctly at the fusion point without rework.

## How (Implementation Plan)

### Architecture Notes

This task produces a single artifact: an Architecture Decision Record at `docs/adr/2026-10-11-vision-representation.md`.

The ADR must settle these specific questions:

```
┌─────────────────────────────────────────────────────────┐
│                 Vision Branch (CNN)                      │
│  Input: [F×84×84] grayscale, F = frame-stack depth      │
│  Conv2d(1×F, 32, 8, 4) → ReLU                           │
│  Conv2d(32, 64, 4, 2) → ReLU                            │
│  Conv2d(64, 64, 3, 1) → ReLU                            │
│  Flatten → 3136-d → Linear(3136, 512) → ReLU            │
│  Output: 512-d embedding                                 │
├─────────────────────────────────────────────────────────┤
│               Memory Branch (MLP, existing)              │
│  Input: M-d vector (M = final memory size)              │
│  Linear(M, 128) → ReLU → Linear(128, 64) → ReLU         │
│  Output: 64-d embedding                                  │
├─────────────────────────────────────────────────────────┤
│                    Fusion Point                          │
│  Concat([512, 64]) → 576-d                               │
│  Linear(576, 256) → ReLU                                 │
│  Linear(256, 6) → Q-values                               │
└─────────────────────────────────────────────────────────┘
```

The CNN architecture above is the **Atari DQN standard** (Mnih et al. 2015), adapted for grayscale input. The exact channel counts and kernel sizes are a starting point — the ADR should confirm or adjust them, but the shape contract (input → 512-d embedding) is what T6 and T8 need to agree on.

### Steps

- [x] 1. **Research frame-stack depth**
  - **What:** Determine F (number of consecutive frames stacked). Atari DQN uses F=4. For Pokémon Red (slower-paced, turn-based elements), F=2 or F=3 may suffice. Document the tradeoff: more frames = more motion information but more VRAM and compute.
  - **Verify:** ADR states F with rationale. If F=2, input shape is `[2×84×84]`; if F=4, `[4×84×84]`.

- [x] 2. **Decide memory vector composition**
  - **Files:** Reference `StatePreprocessorService.cs` (current 14-float vector), `GameState.cs`
  - **What:** For each of the 14 features, decide: keep, drop (vision covers it), or modify. Key question: do X, Y, MapID stay? Arguments for dropping: CNN sees spatial position. Arguments for keeping: precise coordinates are cheap, reliable, and the CNN may not learn map identity from pixels alone. Document the decision.
  - **Verify:** ADR lists the final M features with rationale for each keep/drop decision. The new M is a concrete number.

- [x] 3. **Define normalization scheme for frames**
  - **What:** Confirm the `torchvision.transforms` pipeline: `Resize(84,84)` → `Grayscale()` → `Normalize(mean, std)`. Specify mean and std values. Atari DQN uses `mean=0, std=255` (scale to [0,1]). For grayscale with single channel, `mean=[0.5], std=[0.5]` (scale to [-1,1]) is also common. Pick one and document why.
  - **Verify:** ADR states the exact `Normalize` parameters. A developer can write the transform without guessing.

- [x] 4. **Design the fusion architecture**
  - **What:** Confirm the CNN→embedding→concat→Q-head architecture shown above. Specify: CNN output embedding size (512), MLP output embedding size (64), fusion layer sizes (256→6). Document why 512 and 64 were chosen (balance expressiveness vs parameter count).
  - **Verify:** ADR includes a mermaid diagram of the full forward pass. Input and output tensor shapes are specified at every layer.

- [x] 5. **Design the `VisionEnabled` feature flag**
  - **Files:** Reference `Program.cs` (DI), `ExplorationAgent.cs` (episode loop), `appsettings.json`
  - **What:** Define where the flag lives and how it gates behavior:
    - **Config:** `appsettings.json` key `"VisionEnabled": false` (default false — safe, backward-compatible)
    - **DI:** When `VisionEnabled=false`, `IVisionPreprocessorService` is either not registered or registered as a no-op stub. When `true`, the real implementation is registered.
    - **Network:** When `VisionEnabled=false`, `PokeVisionDQN` degrades to the existing `PokeDQN` (memory-only path). When `true`, both branches are active.
    - **Experience:** `ModelExperience` carries an optional `FrameBytes` field (null when vision disabled).
  - **Verify:** ADR describes the gating mechanism. A developer can implement the flag without ambiguity about which components are affected.

- [x] 6. **Define the A/B test protocol**
  - **What:** Specify a concrete, runnable comparison:
    - **Episodes:** N = 50 per condition (vision on, vision off)
    - **Starting state:** Same save state / new game, same seed for `Random`
    - **Milestone:** Reach Route 3 (Viridian Forest exit) — a navigation-heavy early-game segment where vision should help
    - **Success threshold:** Vision agent reaches milestone in ≤80% of the mean steps of no-vision agent (i.e., vision is at least 20% more sample-efficient)
    - **Metrics to record:** Steps to milestone, total reward, unique locations visited
    - **Statistical test:** Welch's t-test on steps-to-milestone (p < 0.05)
  - **Verify:** ADR includes the protocol. A researcher can execute it without asking "how many episodes?" or "what counts as success?"

- [x] 7. **Write the ADR**
  - **Files:** Create `docs/adr/2026-10-11-vision-representation.md`
  - **What:** Consolidate all decisions from steps 1–6 into a single Architecture Decision Record following the project's ADR format.
  - **Verify:** ADR is self-contained — a new contributor can read it and understand the vision representation without reading this plan.

### Dependencies
- New NuGet packages: none
- Redis schema changes: none
- Lua protocol changes: none
- Configuration changes: `appsettings.json` — new `VisionEnabled` key (documented, not implemented in this task)

## Risks & Edge Cases

| Risk | Likelihood | Mitigation |
|---|---|---|
| Frame-stack depth F=4 is too VRAM-heavy for 10 agents | Medium | Default to F=2; document that F can be tuned later without architecture changes |
| Dropping X/Y/MapID from memory vector loses precise localization the CNN can't recover | Medium | Default to keeping all 14 features; only drop if strong evidence CNN covers them |
| Fusion at 576-d is too many parameters for the Q-head | Low | 576→256→6 is ~150K params — modest. If VRAM-constrained, reduce to 256→128→6 |
| `VisionEnabled` flag creates two code paths that diverge | Medium | Design the flag as a DI-level switch, not if/else scattered through the codebase. No-vision path reuses existing `PokeDQN` directly |

## Verification Checklist
- [x] ADR exists at `docs/adr/2026-10-11-vision-representation.md`
- [x] ADR answers all 6 capability questions (tensor shapes, memory vector, fusion, feature flag, A/B protocol, positional features)
- [x] ADR includes a mermaid diagram of the full forward pass with tensor shapes
- [x] `dotnet build src/PokemonRedRL.sln` passes (no code changes, but verify nothing broke)
- [ ] ADR is linked from `docs/PROJECT_OVERVIEW.md` §7 (the vision direction section)

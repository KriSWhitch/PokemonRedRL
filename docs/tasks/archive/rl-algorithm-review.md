# Task 5 — RL Algorithm Review
**Status:** ✅ Done
**Depth:** Standard
**Created:** 2026-10-11

## Why (Motivation)

The current Double DQN + prioritized replay setup was chosen as a reasonable starting point but was never benchmarked against alternatives. Before building the dual-modality network (T8) — which locks in the algorithm for the vision-enabled agent — we need a deliberate, evidence-based decision on the RL algorithm. Changing the algorithm after T8 would mean rebuilding the trainer and possibly the network architecture. Getting it right now saves rework.

Additionally, the epic roadmap requires a **baseline GPU/VRAM measurement** of the current MLP-only network before adding the CNN branch. Without this baseline, T8 can't budget VRAM for the ~1.84M-param `PokeVisionDQN`.

## What (Specification)

### Capabilities

| ID | Intent (WHAT) | Success Signal (how to verify) |
|---|---|---|
| CAP-1 | Evaluate the current Double DQN + prioritized replay against Rainbow DQN improvements (dueling heads, n-step returns, noisy nets, distributional RL) and PPO | A recommendation document lists each candidate with tradeoffs specific to this project's constraints (real-time emulator, multi-agent Redis sharing, GPU/VRAM budget) |
| CAP-2 | Measure baseline per-agent GPU memory usage with the current MLP-only `PokeDQN` (14→128→64→6, ~10K params) | A concrete number in MB for 1 agent and projected for 10 agents, measured on the RTX 4070 SUPER |
| CAP-3 | Estimate VRAM budget for the planned `PokeVisionDQN` (~1.84M params per T4 ADR) and determine if 10 concurrent agents fit | A yes/no answer: "10 agents fit" or "must reduce to N agents / share CNN encoder" |
| CAP-4 | Produce a final algorithm recommendation with rationale, migration cost estimate, and impact on T8 implementation | T8's implementer can read the recommendation and know exactly which algorithm to build, without re-litigating the decision |

### Constraints

- **Must work with TorchSharp.** Any algorithm must be implementable with TorchSharp's current API (no JAX, no PyTorch-only features). TorchSharp covers: `Conv2d`, `Linear`, `LSTM`, distributions (`torch.distributions`), optimizers (Adam, RMSprop), gradient clipping.
- **Must work with multi-agent Redis sharing.** The current `ParameterServer` push/pull pattern must remain viable. Algorithms that require a central learner (e.g., IMPALA-style) are out of scope unless we redesign the multi-agent architecture — which is not in this epic.
- **Must fit GPU/VRAM budget.** RTX 4070 SUPER (12 GB VRAM). 10 concurrent agents + Redis + OS overhead. The algorithm must not exceed available VRAM.
- **Must support the dual-modality architecture from T4 ADR.** The algorithm recommendation must compose with the CNN+MLP fusion design. If an algorithm requires architectural changes to the fusion point, those must be documented.
- **No Lua protocol changes.** The algorithm review does not touch the emulator bridge.
- **No Redis schema changes in this task.** Storage strategy changes (if any) are T8's concern.

### Non-goals

- **Not implementing any algorithm.** This is a research/decision task. No code changes, no new projects, no DI registrations.
- **Not implementing the dual-modality network.** That's T8. This task only decides what algorithm T8 should implement.
- **Not benchmarking algorithms against each other.** We don't have the infrastructure for rigorous A/B testing of RL algorithms (no test harness, no standardized environments). The recommendation is based on literature, known characteristics, and fit to our constraints — not empirical comparison.
- **Not redesigning the multi-agent architecture.** The current Redis-based weight sharing stays. If an algorithm requires a fundamentally different multi-agent setup, it's rejected.
- **Not evaluating offline RL or model-based RL.** The scope is model-free online RL algorithms suitable for real-time emulator interaction.

### Success Signal

A developer reading the recommendation document can implement the chosen algorithm in T8 without ambiguity about: network architecture changes, loss function, exploration strategy, replay buffer requirements, or multi-agent synchronization.

## How (Implementation Plan)

### Architecture Notes

This task produces a single artifact: a recommendation document at `docs/adr/2026-10-11-rl-algorithm-review.md` (or a section appended to the existing T4 ADR — decision during build).

The document must cover these specific questions:

```
Current State                    Candidates                      Recommendation
─────────────                    ──────────                      ──────────────
Double DQN          ──→    Rainbow DQN improvements      ──→    Algorithm X
+ Prioritized replay       (dueling, n-step, noisy,            + Rationale
+ Multi-agent Redis        distributional, combined)           + Migration cost
+ Epsilon-greedy                                               + T8 impact
+ Adam optimizer         ──→    PPO
+ Gradient clipping              (actor-critic, GAE,
+ Adaptive LR                    on-policy, multi-agent?)
```

### Steps

- [x] 1. **Document current algorithm state**
  - **Files:** `DQNTrainer.cs`, `PokeDQN.cs`, `ExplorationAgent.cs`, `ParameterServer.cs`, `AdaptiveLRScheduler.cs`
  - **What:** Produce a concise summary of what we have today: Double DQN (confirmed — `_model` for action selection, `_targetModel` for value), prioritized replay (70/30 mix, Redis-backed), epsilon-greedy (1.0→0.01, decay 0.9995), multi-agent weight sharing (Redis push/pull every 500 steps), adaptive LR (smoothed reward signal), gradient clipping (1.0), Adam optimizer, StepLR scheduler, γ=0.99.
  - **Verify:** Summary is accurate against the code. Any discrepancies between what the code does and what the roadmap says are flagged.

- [x] 2. **Evaluate Rainbow DQN improvements**
  - **What:** For each Rainbow component, assess fit for our constraints:
    - **Dueling heads** (separate value/advantage streams): Low implementation cost (~20 lines in `PokeDQN`). Improves value estimation in states where actions don't matter. **Fit: HIGH.**
    - **N-step returns** (bootstrap from n steps ahead): Medium cost (need n-step buffer in `ExplorationAgent`). Reduces bias-variance tradeoff. Complicates multi-agent sharing (n-step trajectories are agent-local). **Fit: MEDIUM.**
    - **Noisy nets** (learned exploration noise): Low cost (replace ε-greedy with `NoisyLinear` layers). Removes ε hyperparameter tuning. TorchSharp supports custom `Linear` subclasses. **Fit: HIGH.**
    - **Distributional RL** (C51/QR-DQN): High cost (output is distribution over returns, not scalar Q). Changes loss function, network output, and replay sampling. **Fit: LOW** — complexity cost outweighs benefit for our scale.
    - **Combined Rainbow:** All of the above together. High implementation complexity. **Fit: LOW** — incremental adoption of individual components is safer.
  - **Verify:** Each component has a fit assessment (HIGH/MEDIUM/LOW) with rationale specific to our constraints.

- [x] 3. **Evaluate PPO**
  - **What:** Assess PPO against our constraints:
    - **On-policy:** PPO requires trajectories from the *current* policy. Our multi-agent Redis replay is off-policy (any agent can sample any experience). PPO would require either: (a) per-agent local buffers (no Redis sharing of experiences), or (b) a synchronous central learner (agents collect trajectories, central learner updates, broadcast new policy). Both are significant architectural changes.
    - **Actor-critic:** PPO needs a value network (critic) in addition to the policy network. This doubles the parameter count (~3.7M for dual-modality).
    - **Sample efficiency:** PPO is typically less sample-efficient than DQN (on-policy data is used once, not replayed). With a real-time emulator, sample efficiency is critical.
    - **Stability:** PPO is more stable than DQN (no deadly triad). But our Double DQN already mitigates the worst instability.
    - **TorchSharp support:** PPO is implementable (GAE, clipped objective, actor-critic). No blocking API gaps.
  - **Verify:** PPO assessment includes sample efficiency comparison, architectural impact, and VRAM estimate.

- [x] 4. **Measure baseline GPU memory**
  - **Files:** `PokeDQN.cs`, `DQNTrainer.cs`
  - **What:** Run the current agent (MLP-only, 14→128→64→6) and measure:
    - Per-agent GPU memory (model weights + optimizer state + activations during training)
    - Peak memory during a training step (batch of 32)
    - Idle memory (model loaded, no training)
    - Project to 10 agents: `10 × per_agent_memory + Redis_overhead`
  - **Verify:** Concrete MB numbers. If measurement is blocked (no running mGBA instance), document the theoretical calculation and flag as "measure when possible."

- [x] 5. **Estimate VRAM for PokeVisionDQN**
  - **What:** Using the T4 ADR architecture (~1.84M params), estimate:
    - Model weights: ~1.84M × 4 bytes (float32) = ~7.4 MB
    - Optimizer state (Adam): ~1.84M × 8 bytes (momentum + variance) = ~14.7 MB
    - Activations (batch 32, forward pass): estimate from layer dimensions
    - Total per agent: weights + optimizer + activations
    - 10 agents total vs 12 GB VRAM budget
  - **Verify:** Yes/no answer on whether 10 agents fit. If no, specify the maximum number of agents or mitigation (shared CNN encoder, gradient accumulation, mixed precision).

- [x] 6. **Produce final recommendation**
  - **What:** Consolidate findings into a recommendation:
    - **Recommended algorithm:** e.g., "Double DQN + dueling heads + noisy nets" (incremental improvements on current baseline)
    - **Rationale:** Why this over PPO, why these Rainbow components and not others
    - **Migration cost:** What changes in `PokeDQN`, `DQNTrainer`, `ExplorationAgent`
    - **T8 impact:** What T8 needs to build (e.g., `PokeVisionDQN` with dueling heads, `NoisyLinear` layers)
    - **Deferred improvements:** Which Rainbow components to revisit later (e.g., n-step returns after T8 ships)
  - **Verify:** Recommendation is actionable — a developer can implement it without further research.

- [x] 7. **Write the recommendation document**
  - **Files:** Create `docs/adr/2026-10-11-rl-algorithm-review.md`
  - **What:** Consolidate all findings from steps 1–6 into a single document.
  - **Verify:** Document is self-contained. A new contributor can read it and understand the algorithm choice without reading this plan.

### Dependencies
- New NuGet packages: none
- Redis schema changes: none
- Lua protocol changes: none
- Configuration changes: none

## Risks & Edge Cases

| Risk | Likelihood | Mitigation |
|---|---|---|
| GPU memory measurement blocked (no running mGBA) | Medium | Provide theoretical calculation as fallback; flag for measurement when mGBA is available |
| PPO is theoretically better but architecturally incompatible | Medium | Document the incompatibility clearly; if PPO is strongly preferred, spin up a separate epic for multi-agent architecture redesign |
| Rainbow components interact poorly (e.g., noisy nets + prioritized replay) | Low | Known interaction from literature — noisy nets reduce the benefit of prioritized replay. Document the interaction. |
| TorchSharp lacks a required op for a candidate algorithm | Low | TorchSharp 0.107 covers all standard DQN/PPO ops. Verify during build if a specific component is chosen. |
| Recommendation is "stay with current Double DQN" — feels like no progress | Low | "No change" is a valid and valuable decision. Document why the current setup is already good, and what incremental improvements are worth making. |

## Verification Checklist
- [ ] Recommendation document exists at `docs/adr/2026-10-11-rl-algorithm-review.md`
- [ ] Document covers all 4 capabilities (current state, Rainbow eval, PPO eval, final recommendation)
- [ ] GPU memory baseline is measured or theoretically calculated
- [ ] VRAM budget for `PokeVisionDQN` is estimated with yes/no on 10 agents
- [ ] Recommendation is actionable for T8 implementation
- [ ] `dotnet build src/PokemonRedRL.sln` passes (no code changes, but verify nothing broke)

# BR-0001 — Brainstorm: Epic Roadmap Quality & Feasibility Review

**ID:** BR-0001
**Status:** Archived
**Disposition:** Incorporated
**Incorporated into:** [I1 Initiative](../../../docs/initiatives/I1-computer-vision-rl-overhaul.md), [E1 Vision](../../../docs/epics/E1-vision-perception.md), [E2 RL](../../../docs/epics/E2-rl-training-and-models.md), [E4 Reliability](../../../docs/epics/E4-reliability-validation.md), [ADR-0001](../../../docs/adr/ADR-0001-vision-representation.md)
**Archived:** 2026-10-30
**Date:** 2026-10-11
**Stance:** Ideate for Me (Mary 📊 reviews existing epic independently)
**Participants:** AI (Mary — Analyst persona)

## Topic Statement

Review the active epic `roadmap-vision-and-rl-overhaul` (computer-vision + RL overhaul, 7/12 tasks done) for structural quality, completeness, feasibility, and missing pieces.

---

## Top Candidates (after pre-mortem)

### 1. Add frame compression to T8 (🟢 — Risk: LOW)

**What:** T8 (Dual-modality network + trainer) currently mentions "decide between storing compressed/downsized frames vs. only CNN embeddings" but doesn't mandate a concrete measurement step. Add an explicit step: "Before committing to a storage strategy, measure per-experience Redis payload size with 1 agent at target frame resolution. Calculate projected memory for 10 agents × BatchSize × MaxStreamLength. If > 50% of available Redis memory, implement compression (resize to 84×84 grayscale, or store only CNN embeddings)."

**Why it could fail:** If skipped, Redis OOM will manifest as silent slowdown (swap) rather than a clear error — hard to diagnose.

**Early warning signs:** T8 implementation starts without a `MEMORY USAGE` check on Redis after first experience writes.

---

### 2. Add A/B test protocol to T4 (🟢 — Risk: LOW)

**What:** T4 currently requires a feature flag for vision disable, but doesn't specify how to USE it to prove vision helps. Add to T4: "Define a concrete A/B comparison protocol: run N episodes with VisionEnabled=true, N episodes with VisionEnabled=false, same starting state, same seed. Success criterion: vision-enabled agent reaches [specific milestone] in ≤ X% of the episodes of vision-disabled agent. Document the protocol so T8/T9 can execute it."

**Why it could fail:** Feature flag exists but nobody runs the comparison. Months later, nobody knows if vision is helping or just adding cost.

**Early warning signs:** After T8 ships, no one has run a side-by-side comparison run.

---

### 3. Move T11 (testing) earlier in the sequence (🟡 — Risk: MEDIUM)

**What:** T11 (xUnit test project) is currently the second-to-last task. This means T6 (pipeline), T8 (trainer), and T9 (reward shaping) all ship without automated tests. Move T11 to run in parallel with T6: create the test project skeleton early, add tests for each new component as it's built (not retroactively). Minimum: `RewardCalculatorService` tests after T9, `StatePreprocessorService` tests after T4, vision preprocessing tests after T6.

**Why it could fail:** Resistance to changing task order — T11 is "infrastructure" and feels like it should be done once. But retroactive testing is 3x more expensive than testing during development.

**Early warning signs:** T9 ships with manual-only verification of reward shaping.

---

## Full Idea Catalog

| # | Technique | Idea | Cluster | Rating |
|---|---|---|---|---|
| 1 | Obvious | Epic structure is solid — 12 tasks, clear dependencies, gated | A | 🟢 |
| 2 | Obvious | 7/12 done proves the plan is grounded in reality | A | 🟢 |
| 3 | Obvious | T4 as next task is correct — design before build | A | 🟢 |
| 4 | Obvious | T5 before T8 is correct (fixed in review) | A | 🟢 |
| 5 | Obvious | T9 independence from T5/T8 is correctly noted | A | 🟢 |
| 6 | Analogous | No serious Pokémon RL project uses pixels-only — hybrid is industry standard | C | 🟢 |
| 7 | Analogous | Retro Gym supports multimodal observations — confirms hybrid approach | C | 🟢 |
| 8 | Analogous | CleanRL single-file PPO implementations — reference if switching algorithms | C | 🟢 |
| 9 | Wild | Full PPO + Transformer instead of DQN + MLP | C | 🔴 |
| 10 | Wild | World model (DreamerV3-style) for latent planning | C | 🔴 |
| 11 | Wild | Autoencoder for frame compression before Redis storage | B | 🟡 |
| 12 | Wild | Multi-GPU: separate inference and training GPUs | C | 🔴 |
| 13 | Wild | Self-play with population of agents | B | 🟡 |
| 14 | Constrained | OpenCV may not be needed — TorchSharp has torchvision transforms | C | 🟢 |
| 15 | Constrained | VRAM limit → need shared CNN encoder with batched inference | D | 🟡 |
| 16 | Constrained | VRAM limit → reduce NUMBER_OF_AGENTS for vision mode | D | 🟡 |
| 17 | Inverted | Storing raw frames without compression → Redis OOM | B | 🟡 |
| 18 | Inverted | Tests at the end → bugs found late | B | 🟡 |
| 19 | Inverted | No CUDA smoke test in verification plan | B | 🟡 |
| 20 | First Principles | All 6 fundamental needs are covered by the epic | A | 🟢 |
| 21 | First Principles | Missing: success criteria for vision vs no-vision comparison | B | 🟡 |

---

## Clusters

- **Cluster A: Epic structure quality** — Order, dependencies, gates, coverage of fundamentals (5 ideas). 🟢 Rating: the epic is well-structured.
- **Cluster B: Missing pieces** — Frame compression, A/B protocol, CUDA smoke test, test timing, success criteria (7 ideas). 🟡 Rating: 3 actionable additions identified.
- **Cluster C: Architectural decisions** — Hybrid vs pixels-only, OpenCV vs TorchSharp, batching, algorithm choice (7 ideas). 🟢 Rating: correctly flagged as open questions in the epic.
- **Cluster D: Risks without mitigation** — VRAM, Redis payload, baseline comparison (3 ideas). 🟡 Rating: partially covered, needs concrete mitigation steps.

---

## Pre-mortem Details

### Pre-mortem #1: "3 months wasted on non-working vision"

**Failure mode:** T4 picks pixels-only (rejecting hybrid recommendation). Pixels-only can't learn hidden state (party, inventory, moves). Months spent debugging a fundamentally broken architecture.

**Root cause:** T4 treated as research task instead of decision task. "Let's explore both options" instead of "hybrid is default, prove otherwise."

**Mitigation:** T4 must produce a decision, not a research paper. Hybrid is the default. Pixels-only needs strong evidence to override.

**Risk:** MEDIUM

---

### Pre-mortem #2: "Redis explodes from frame storage"

**Failure mode:** Raw 160×144×3 frames stored per experience. 10 agents × 1000 steps = ~690MB. Redis swap → 100x slowdown → project appears "broken" but it's just memory.

**Root cause:** No payload size measurement before committing to storage strategy.

**Mitigation:** Add explicit measurement step to T8. If >50% Redis memory, implement compression.

**Risk:** HIGH

---

### Pre-mortem #3: "Can't tell if vision helps"

**Failure mode:** Feature flag exists but no controlled comparison is run. Vision adds noise. Training appears slower. Nobody knows if it's the vision or just bad luck.

**Root cause:** Feature flag without an A/B test protocol is just a config switch, not a scientific instrument.

**Mitigation:** T4 must define concrete A/B protocol: N episodes, same seed, specific milestone, success threshold.

**Risk:** MEDIUM

---

## Recommendation

**The epic is solid and does NOT need to be re-planned from scratch.** The structure, dependencies, and risk awareness are good. Three targeted additions are recommended:

1. **T4 addition:** Define A/B test protocol for vision vs no-vision comparison (concrete metrics, episode count, success threshold).
2. **T8 addition:** Mandatory Redis payload size measurement step before committing to frame storage strategy.
3. **T11 rescheduling:** Move test project creation earlier — create skeleton in parallel with T6, add tests incrementally per component rather than retroactively at the end.

These are small, targeted fixes — not a rewrite. The epic's backbone (12 tasks, dependency chain, hard gates, risk documentation) is sound.

---

## Next Steps

- Apply the 3 recommended additions to the epic roadmap: `/bmad-plan` (light depth — update existing roadmap)
- Then proceed with T4: `/bmad-plan` Task 4 — Design combined (vision + memory-state) representation
- Or discuss which of the 3 changes to prioritize: just ask

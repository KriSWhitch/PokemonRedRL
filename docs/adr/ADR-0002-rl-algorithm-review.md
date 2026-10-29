# ADR-0002: RL Algorithm Selection — Double DQN + Dueling Heads + Noisy Nets

**ID:** ADR-0002
**Date:** 2026-10-11
**Status:** Accepted
**Supersedes:** None
**Superseded by:** None

## Context

The PokemonRedRL agent currently uses Double DQN with prioritized experience replay, epsilon-greedy exploration, and multi-agent weight sharing via Redis. This setup was chosen as a reasonable starting point but was never benchmarked against alternatives. Before building the dual-modality `PokeVisionDQN` network (S11), we need a deliberate, evidence-based decision on the RL algorithm. Changing the algorithm after S11 would mean rebuilding the trainer and possibly the network architecture.

The evaluation must respect these **spine invariants**:

| # | Invariant | Impact |
|---|---|---|
| I1 | Game state read from mGBA via Lua socket before each action | Rules out parallel/batched environment stepping |
| I2 | Tensor allocations disposed within same iteration | Any new loss/network must work with `DisposeScope` |
| I3 | Redis is single source of truth for shared experience | Rules out on-policy algorithms that can't use off-policy replay |
| I4 | Multi-agent: N independent agents sharing via Redis | Rules out central-learner architectures |
| I5 | Vision capture is OS-level, independent of Lua bridge | Algorithm doesn't affect vision pipeline |
| I6 | `Vision.Enabled=false` must remain bit-identical | Algorithm changes must not break memory-only fallback |
| I7 | No new NuGet packages | Must be implementable with TorchSharp 0.107 alone |

### Current Algorithm (confirmed from code)

The current implementation in `DQNTrainer.cs` is a correct **Double DQN**:

- **Action selection:** `_model.Forward(nextStates).argmax(1)` — uses online network
- **Value estimation:** `_targetModel.Forward(nextStates).gather(1, nextActions)` — uses target network
- **Target sync:** Every 500 training steps (`_trainStepCount % _syncInterval`)
- **Prioritized replay:** 70/30 prioritized/random mix via Redis Sorted Set
- **Exploration:** ε-greedy, 1.0→0.01, decay 0.9995 per step
- **Optimizer:** Adam (lr=0.001) + StepLR scheduler (step=100K, γ=0.1)
- **Adaptive LR:** Smoothed reward signal via `AdaptiveLRScheduler`
- **Gradient clipping:** `clip_grad_norm_` at 1.0
- **Multi-agent:** `ParameterServer` push/pull every 500 steps
- **Network:** 3-layer MLP (14→128→64→6), ~10K params

## Decision

**Adopt Double DQN + Dueling Heads + Noisy Nets as the RL algorithm for the vision-enabled agent (S11).**

This is an incremental improvement on the current baseline — keep what works, add two high-impact, low-cost Rainbow components:

### Component 1: Dueling Heads (Wang et al. 2016)

Replace the final `Linear(64, 6)` Q-value head with separate value and advantage streams:

```
V(s) = Linear(embedding, 1)           ← state value
A(s, a) = Linear(embedding, n_actions) ← action advantage
Q(s, a) = V(s) + A(s, a) - mean(A(s, a))
```

**Rationale:** In many Pokémon Red states, the action doesn't matter (walking down an empty corridor, waiting in battle). Dueling heads let the network learn that the state value is high regardless of action, without forcing all action values to be similar through noisy gradient updates. This is especially valuable for navigation — the agent should learn that moving *somewhere* is good, without needing to learn the exact Q-value for each direction.

**Cost:** ~20 lines in `PokeDQN.Forward()`. Replaces one `Linear(64,6)` with `Linear(64,1)` + `Linear(64,6)`. ~448 params vs 384 — negligible.

### Component 2: Noisy Nets (Fortunato et al. 2018)

Replace ε-greedy exploration with learned parametric noise in the network weights:

```
NoisyLinear(x) = (μ_w + σ_w ⊙ ε) × x + (μ_b + σ_b ⊙ ε)
where ε ~ N(0, 1), sampled fresh each forward pass
```

**Rationale:** Pokémon Red's exploration needs vary dramatically by context:
- **Pallet Town / Routes:** Random walking is fine — explore freely
- **Viridian Forest:** Careful navigation needed — don't walk into trees
- **Battles:** Exploit known effective moves, explore new strategies occasionally
- **Menus:** Deterministic — always select the right option

Noisy nets learn *state-dependent* exploration: the network reduces noise in familiar states and increases it in novel ones. This replaces three hyperparameters (ε_start, ε_end, ε_decay) with a single initialization parameter (σ₀ = 0.5, standard value).

**Cost:** Custom `NoisyLinear` TorchSharp module (~50 lines). Replace `Linear` layers with `NoisyLinear` in the network. Remove ε-greedy logic from `ExplorationAgent.SelectAction`. ~2× params per replaced layer (μ and σ for each weight/bias) — for MLP: ~20K→~41K, negligible.

### What We're NOT Adopting (Yet)

| Component | Verdict | Rationale |
|---|---|---|
| **N-step returns** | ⚠️ Defer | Requires agent-local trajectory buffers — conflicts with shared Redis replay. Revisit after S11. |
| **Distributional RL** | ❌ Reject | High implementation complexity (new output head, loss function, training step). Modest benefit at our scale. |
| **Combined Rainbow** | ❌ Reject | Incremental adoption is safer — measure each component's impact. |
| **PPO** | ❌ Reject | On-policy — conflicts with invariants I3 (Redis shared replay) and I4 (multi-agent sharing). Requires separate epic. |

## Alternatives Considered

### Alternative 1: Stay with current Double DQN (no changes)

**Pros:**
- Zero implementation cost
- Known behavior, no surprises
- Backward compatible by definition

**Cons:**
- ε-greedy is state-blind — explores equally in Pallet Town and Viridian Forest
- No dueling heads — network must learn that actions don't matter in many states
- Leaves known improvements on the table before locking in S11 architecture

**Why rejected:** The cost of adding dueling heads + noisy nets is low (~70 lines total). The benefit is well-established in literature. Doing it now, before S11 builds the dual-modality network, avoids rework.

### Alternative 2: Full Rainbow DQN

**Pros:**
- State-of-the-art DQN variant
- Published results show significant improvements over Double DQN

**Cons:**
- N-step returns conflict with Redis shared replay architecture
- Distributional RL adds major complexity to training loop
- Combined Rainbow is harder to debug — which component helped?
- Higher implementation risk for S11 timeline

**Why rejected:** Incremental adoption is safer. Adopt the two highest-impact, lowest-cost components now. Measure their impact. Add n-step returns and distributional RL later if needed.

### Alternative 3: PPO

**Pros:**
- More stable than DQN (no deadly triad)
- Dominant algorithm in many RL benchmarks
- Naturally supports recurrent policies (LSTM) for partial observability

**Cons:**
- **On-policy** — requires agent-local trajectory buffers, conflicts with Redis shared replay (I3)
- **Sample efficiency** — on-policy data used once vs DQN's replay (critical with real-time emulator)
- **Multi-agent** — agents can't share experiences, only weights (loses a key advantage of our architecture)
- **Network size** — actor + critic = ~2× params (~3.7M for dual-modality)
- **Architectural changes** — new training loop, new buffer system, new loss function

**Why rejected:** PPO violates invariants I3 and I4. It's not an incremental improvement — it's a different architecture. If PPO is desired, it should be a separate epic with its own multi-agent design. The S19 spike (PPO + LSTM, post-epic) is the right place to evaluate it.

## Consequences

### Positive

- **State-aware exploration.** Noisy nets learn *when* to explore — conservative in familiar areas, exploratory in new ones. This is ideal for Pokémon Red's varied environments.
- **Better value estimation.** Dueling heads separate "is this state good?" from "which action is best?" — critical for navigation-heavy gameplay.
- **Fewer hyperparameters.** ε-greedy has 3 parameters (start, end, decay). Noisy nets have 1 (σ₀). Less tuning, more robust.
- **Incremental.** Both changes are additive — the core Double DQN training loop is unchanged. If noisy nets don't work, revert to ε-greedy in one commit.
- **VRAM-friendly.** Dueling heads add ~64 params. Noisy nets double the MLP params (~20K→~41K). Total impact: ~21K additional params — negligible.
- **S11-ready.** The algorithm decision is settled. S11 can build `PokeVisionDQN` with dueling heads and `NoisyLinear` layers from day one.

### Negative

- **NoisyLinear is custom code.** TorchSharp doesn't ship a `NoisyLinear` module. We must implement and maintain it (~50 lines). Risk: subtle bugs in noise sampling or gradient flow.
- **Noisy nets + prioritized replay interaction.** Literature shows noisy nets reduce the benefit of prioritized replay (both address exploration). We may find that the 70/30 prioritized/random mix is no longer optimal. Mitigation: keep prioritized replay initially, measure if it still helps.
- **ε-greedy removal is irreversible for existing Redis data.** Experiences collected with ε-greedy are compatible with noisy-net training (both produce (s, a, r, s') tuples), but new experiences will be collected with state-dependent noise. If we revert to ε-greedy, the mixed exploration strategies in Redis may cause issues. Mitigation: if reverting, flush Redis or train only on recent experiences.

### Neutral / Notes

- **N-step returns** are deferred, not rejected. After S11 ships, we can implement a local n-step buffer per agent (separate from Redis) and measure the impact. This is a natural follow-up task.
- **Distributional RL** is rejected for now but may become more valuable as the agent tackles later-game challenges with higher variance (gym battles, legendary encounters).
- **PPO** is rejected for this epic but the S19 spike remains the right place to evaluate it. If PPO shows dramatically better sample efficiency on Pokémon Red, a future epic can redesign the multi-agent architecture around it.

## GPU/VRAM Budget

### Current MLP-only baseline (theoretical)

| Component | Per Agent | 10 Agents |
|---|---|---|
| Model weights (~10.6K params) | ~41 KB | ~410 KB |
| Optimizer state (Adam, 2×) | ~82 KB | ~820 KB |
| Activations (batch=32) | ~27 KB | ~270 KB |
| **Total** | **~150 KB** | **~1.5 MB** |

### PokeVisionDQN with dueling + noisy nets (~1.86M params)

| Component | Per Agent | 10 Agents |
|---|---|---|
| Model weights (~1.86M params) | ~7.1 MB | ~71 MB |
| Optimizer state (Adam, 2×) | ~14.2 MB | ~142 MB |
| Activations (batch=32) | ~4.8 MB | ~48 MB |
| **Total per agent** | **~26.1 MB** | **~261 MB** |

### Total VRAM Budget (RTX 4070 SUPER, 12 GB)

| Component | VRAM |
|---|---|
| 10× PokeVisionDQN | ~261 MB |
| TorchSharp/CUDA runtime | ~200-500 MB |
| Frame buffers (10 × 2 × 84×84 × 4B) | ~0.6 MB |
| Redis (in-memory) | ~100-500 MB |
| OS + other | ~500 MB |
| **Total estimated** | **~1.1-1.8 GB** |
| **Available** | **12 GB** |
| **Headroom** | **>10 GB** |

**Verdict: ✅ 10 agents fit comfortably with >10 GB headroom.** No shared CNN encoder, gradient accumulation, or mixed precision needed.

## Migration Path

### S11 Implementation Order

1. **Create `NoisyLinear` module** in `PokemonRedRL.Models.ReinforcementLearning/NoisyLinear.cs`:
   ```csharp
   public class NoisyLinear : Module {
       private Parameter mu_weight, sigma_weight, mu_bias, sigma_bias;
       private bool _noiseEnabled = true;
       // Forward: sample noise, compute y = (mu + sigma*noise)*x + bias
   }
   ```

2. **Add dueling heads to `PokeDQN`** (and `PokeVisionDQN` in S11):
   ```csharp
   // Replace: Linear(64, 6)
   // With:
   var valueHead = NoisyLinear(64, 1);      // V(s)
   var advantageHead = NoisyLinear(64, 6);  // A(s, a)
   // Q(s, a) = V(s) + A(s, a) - mean(A(s, a))
   ```

3. **Replace `Linear` with `NoisyLinear`** in the MLP branch (14→128, 128→64). Keep CNN branch with regular `Linear` initially — noisy nets in CNN may be unstable.

4. **Remove ε-greedy from `ExplorationAgent.SelectAction`**:
   ```csharp
   // Before: if (rand < epsilon) randomAction else argmax(Q)
   // After:  argmax(Q)  (noise is in the network)
   ```

5. **Remove ε parameters** from `ExplorationAgent` (`_epsilonStart`, `_epsilonEnd`, `_epsilonDecay`).

6. **Add `ResetNoise()` method** to `PokeDQN` — called at the start of each episode to resample noise.

7. **Verify backward compatibility (I6):** `Vision.Enabled=false` path uses the same dueling+noisy `PokeDQN`. Input/output contract (14→6) unchanged. Existing Redis experiences compatible.

# Epic E2: RL Training and Models

**ID:** E2
**Parent Initiative:** [Computer-Vision + RL Overhaul](../initiatives/I1-computer-vision-rl-overhaul.md)
**Tracking:** See [Work Items Registry](../WORK_ITEMS.md) for status, branch, dependencies, and next work.

## Outcome

Implement the selected learning architecture with dual-modality inputs, measurable storage costs, and recoverable model checkpoints.

## Success Signals

- Training uses the accepted Double DQN + Dueling + Noisy Nets decision and accepts the specified visual and memory inputs.
- Redis storage impact is measured against the 10-agent workload before choosing raw-frame or embedding persistence.
- Checkpoints can be saved, identified, and resumed with relevant training metadata.

## Child User Stories

| ID | User Story | Intent | Done Signal |
|---|---|---|---|
| S8 | [Select RL algorithm](../stories/archive/S8-rl-algorithm-review.md) | Compare candidate algorithms against data reuse, Redis, and compute constraints. | ADR records Double DQN + Dueling Heads + Noisy Nets, rejected/deferred alternatives, and the VRAM estimate. The estimate is theoretical; S11 owns runtime validation. |
| S11 | Dual-modality network and trainer | Implement `PokeVisionDQN` with the S8 learner; extend `ModelExperience`, `GameState`, `StatePreprocessorService`, and `DQNTrainer` for vision and memory tensors. | Implement the ADR's two modalities and selected learner; verify tensor shapes, training step, disposal/soak behavior, and measured GPU memory. Measure Redis bytes per experience, project 10 agents × batch size × max stream length against the configured Redis memory budget, and if projected use exceeds 50% of that budget, store CNN embeddings rather than raw frames unless an approved course correction chooses another bounded strategy. If no finite Redis budget is configured, resolve it before approving a storage choice. |
| S13 | Model persistence redesign | Save versioned checkpoints and resume training after restart. | Checkpoint metadata round-trips for step count, reward, and epsilon; a saved model resumes after restart. |

## Future Candidate

S19 — Port CleanRL PPO + LSTM to TorchSharp and evaluate it against the navigation task (`story/s19-ppo-lstm-spike`) is outside the current Initiative completion scope and requires a separately approved scope update or follow-on Initiative; it does not replace the S8-selected learner for S11. See WORK_ITEMS for dependency edges.

## Branch and Delivery

Create each story branch from `epic/e2-rl-training-and-models`. Use Quick Flow for a single well-defined build; use Full Flow when requirements or architecture must be settled first. The task plan is created for the story flow under `docs/stories/`.